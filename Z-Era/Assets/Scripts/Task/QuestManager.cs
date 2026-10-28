using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 任务管理器：任务状态机 + 目标进度 + 跨场景持久化 + 对话标记桥接。
/// 首次访问时自动创建常驻物体（同 DialogueFlags），无需手动摆进场景。
///
/// 状态流转：NotStarted --StartQuest--> Active --全部目标达标--> Completed。
/// 接取校验：前置任务全部 Completed + requiredFlags 全部已设置 +
/// forbiddenFlags 均未设置。
/// Flag 桥接：接取/完成时按任务资产配置自动写入 DialogueFlags，
/// 对话系统无需感知任务系统的存在。
///
/// 任意系统上报进度：
/// QuestManager.AddProgress(questId, objectiveId, amount);
/// </summary>
public class QuestManager : MonoBehaviour
{
    public enum QuestState
    {
        // 未接取（或前置条件未满足）。
        NotStarted,
        // 进行中。
        Active,
        // 已完成。
        Completed
    }

    private static QuestManager instance;

    // 首次访问自动创建常驻物体（同 DialogueFlags 模式）。
    public static QuestManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new GameObject("QuestManager")
                    .AddComponent<QuestManager>();
            }

            return instance;
        }
    }

    // 任务状态与资产引用（资产引用用于完成后触发事件与 UnityEvent）。
    private readonly Dictionary<string, QuestState> questStates =
        new Dictionary<string, QuestState>();

    private readonly Dictionary<string, QuestAsset> questAssets =
        new Dictionary<string, QuestAsset>();

    // 进行中任务按接取顺序排列，供 HUD 展示时取"最新任务"。
    private readonly List<QuestAsset> activeQuestOrder =
        new List<QuestAsset>();

    // 已完成任务按完成顺序排列，供任务面板展示。
    private readonly List<QuestAsset> completedQuestOrder =
        new List<QuestAsset>();

    // 目标进度，键为 "questId:objectiveId"。
    private readonly Dictionary<string, int> objectiveProgress =
        new Dictionary<string, int>();

    public event Action<QuestAsset> QuestAccepted;
    public event Action<QuestAsset, QuestAsset.QuestObjective, int, int>
        ObjectiveUpdated;
    public event Action<QuestAsset> QuestCompleted;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnEnable()
    {
        DialogueFlags.FlagSet += OnDialogueFlagSet;
    }

    private void OnDisable()
    {
        DialogueFlags.FlagSet -= OnDialogueFlagSet;
    }

    // 目标的 completeFlag 被设置时，直接把该目标置为完成。
    private void OnDialogueFlagSet(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName) ||
            activeQuestOrder.Count == 0)
        {
            return;
        }

        // 先取快照：置为完成可能立即结束任务并改动 activeQuestOrder。
        List<QuestAsset> activeSnapshot =
            new List<QuestAsset>(activeQuestOrder);

        foreach (QuestAsset asset in activeSnapshot)
        {
            if (asset == null)
            {
                continue;
            }

            foreach (
                QuestAsset.QuestObjective objective in asset.Objectives
            )
            {
                if (objective == null ||
                    string.IsNullOrWhiteSpace(objective.completeFlag) ||
                    objective.completeFlag != flagName)
                {
                    continue;
                }

                SetObjectiveComplete(
                    asset.QuestId,
                    objective.objectiveId
                );
            }
        }
    }

    /// <summary>
    /// 清空全部任务状态与目标进度：回到主菜单、准备重新游玩关卡时调用。
    /// 只重置任务系统自身的数据，不触碰对话标记（由 DialogueFlags 负责）。
    /// 已完成任务列表一并清空，重新游玩时任务面板不会残留上一轮记录。
    /// </summary>
    public static void ResetAll()
    {
        if (instance == null)
        {
            return;
        }

        instance.questStates.Clear();
        instance.questAssets.Clear();
        instance.activeQuestOrder.Clear();
        instance.completedQuestOrder.Clear();
        instance.objectiveProgress.Clear();
    }

    private static string ProgressKey(string questId, string objectiveId)
    {
        return questId + ":" + objectiveId;
    }

    // ============ 接取 ============

    /// <summary>
    /// 尝试接取任务。前置任务、对话标记条件不满足时返回 false 并告警。
    /// </summary>
    public static bool StartQuest(QuestAsset asset)
    {
        if (asset == null)
        {
            Debug.LogWarning("QuestManager：任务资产为空，无法接取。");
            return false;
        }

        if (!CheckCanStart(asset, out string reason))
        {
            Debug.LogWarning(
                $"QuestManager：任务 '{asset.QuestId}' 无法接取：{reason}",
                asset
            );

            return false;
        }

        Instance.BeginQuest(asset);
        return true;
    }

    private void BeginQuest(QuestAsset asset)
    {
        questStates[asset.QuestId] = QuestState.Active;
        questAssets[asset.QuestId] = asset;
        activeQuestOrder.Add(asset);

        foreach (
            QuestAsset.QuestObjective objective in asset.Objectives
        )
        {
            if (objective == null)
            {
                continue;
            }

            objectiveProgress[ProgressKey(
                asset.QuestId,
                objective.objectiveId
            )] = 0;
        }

        // 接取即写入对话标记，NPC/对话可立刻读到。
        SetFlags(asset.FlagsToSetOnAccept);

        asset.OnAcceptEvent?.Invoke();
        QuestAccepted?.Invoke(asset);
    }

    // 接取条件校验：状态 + 前置任务链 + 对话标记。
    private static bool CheckCanStart(
        QuestAsset asset,
        out string reason
    )
    {
        if (string.IsNullOrWhiteSpace(asset.QuestId))
        {
            reason = "questId 为空";
            return false;
        }

        QuestState state = GetQuestState(asset.QuestId);

        if (state != QuestState.NotStarted)
        {
            reason = state == QuestState.Active
                ? "任务已在进行中"
                : "任务已完成";
            return false;
        }

        if (asset.PrerequisiteQuestIds != null)
        {
            foreach (string prerequisiteId in asset.PrerequisiteQuestIds)
            {
                if (GetQuestState(prerequisiteId) !=
                    QuestState.Completed)
                {
                    reason = $"前置任务 '{prerequisiteId}' 未完成";
                    return false;
                }
            }
        }

        if (asset.RequiredFlags != null)
        {
            foreach (string flag in asset.RequiredFlags)
            {
                if (!DialogueFlags.HasFlag(flag))
                {
                    reason = $"缺少对话标记 '{flag}'";
                    return false;
                }
            }
        }

        if (asset.ForbiddenFlags != null)
        {
            foreach (string flag in asset.ForbiddenFlags)
            {
                if (DialogueFlags.HasFlag(flag))
                {
                    reason = $"存在禁止标记 '{flag}'";
                    return false;
                }
            }
        }

        reason = null;
        return true;
    }

    // ============ 进度上报 ============

    /// <summary>
    /// 为指定任务目标累计进度，达到需求量时自动完成整个任务。
    /// 任务未在进行中时忽略并告警。
    /// </summary>
    public static void AddProgress(
        string questId,
        string objectiveId,
        int amount = 1
    )
    {
        if (string.IsNullOrWhiteSpace(questId) ||
            string.IsNullOrWhiteSpace(objectiveId))
        {
            Debug.LogWarning(
                "QuestManager：questId 或 objectiveId 为空，忽略进度上报。"
            );

            return;
        }

        QuestManager manager = Instance;

        if (GetQuestState(questId) != QuestState.Active)
        {
            Debug.LogWarning(
                $"QuestManager：任务 '{questId}' 不在进行中，忽略进度上报。",
                manager
            );

            return;
        }

        QuestAsset asset = manager.questAssets[questId];
        QuestAsset.QuestObjective objective =
            asset.GetObjective(objectiveId);

        if (objective == null)
        {
            Debug.LogError(
                $"QuestManager：任务 '{questId}' 不存在目标 " +
                    $"'{objectiveId}'，忽略进度上报。",
                asset
            );

            return;
        }

        // 目标按列表顺序解锁：前面的目标没达标时，后面的进度忽略。
        if (!IsObjectiveUnlocked(asset, objective))
        {
            return;
        }

        string key = ProgressKey(questId, objectiveId);

        int previous = manager.objectiveProgress.TryGetValue(
            key,
            out int stored
        )
            ? stored
            : 0;

        // 进度夹在 [0, requiredAmount]，超量上报不会溢出。
        int current = Mathf.Clamp(
            previous + amount,
            0,
            objective.requiredAmount
        );

        if (current == previous)
        {
            return;
        }

        manager.objectiveProgress[key] = current;

        manager.ObjectiveUpdated?.Invoke(
            asset,
            objective,
            current,
            objective.requiredAmount
        );

        if (current >= objective.requiredAmount)
        {
            CheckQuestComplete(asset);
        }
    }

    /// <summary>
    /// 直接把指定目标的进度设置为指定值（可清零/回退），
    /// 达到需求量时自动完成整个任务。用于"可失败"的任务：
    /// 失败时把 HUD 与任务系统同步归零。
    /// </summary>
    public static void SetProgress(
        string questId,
        string objectiveId,
        int value
    )
    {
        if (string.IsNullOrWhiteSpace(questId) ||
            string.IsNullOrWhiteSpace(objectiveId))
        {
            Debug.LogWarning(
                "QuestManager：questId 或 objectiveId 为空，忽略进度设置。"
            );

            return;
        }

        QuestManager manager = Instance;

        if (GetQuestState(questId) != QuestState.Active)
        {
            Debug.LogWarning(
                $"QuestManager：任务 '{questId}' 不在进行中，忽略进度设置。",
                manager
            );

            return;
        }

        QuestAsset asset = manager.questAssets[questId];
        QuestAsset.QuestObjective objective =
            asset.GetObjective(objectiveId);

        if (objective == null)
        {
            Debug.LogError(
                $"QuestManager：任务 '{questId}' 不存在目标 " +
                    $"'{objectiveId}'，忽略进度设置。",
                asset
            );

            return;
        }

        // 目标按列表顺序解锁：前面的目标没达标时，后面的进度忽略。
        if (!IsObjectiveUnlocked(asset, objective))
        {
            return;
        }

        string key = ProgressKey(questId, objectiveId);

        int previous = manager.objectiveProgress.TryGetValue(
            key,
            out int stored
        )
            ? stored
            : 0;

        int current = Mathf.Clamp(
            value,
            0,
            objective.requiredAmount
        );

        if (current == previous)
        {
            return;
        }

        manager.objectiveProgress[key] = current;

        manager.ObjectiveUpdated?.Invoke(
            asset,
            objective,
            current,
            objective.requiredAmount
        );

        if (current >= objective.requiredAmount)
        {
            CheckQuestComplete(asset);
        }
    }

    /// <summary>
    /// 直接把指定目标置为完成（内部换算为一次足量进度上报）。
    /// </summary>
    public static void SetObjectiveComplete(
        string questId,
        string objectiveId
    )
    {
        QuestAsset asset = Instance.questAssets.TryGetValue(
            questId,
            out QuestAsset stored
        )
            ? stored
            : null;

        QuestAsset.QuestObjective objective =
            asset != null ? asset.GetObjective(objectiveId) : null;

        if (objective == null)
        {
            Debug.LogWarning(
                $"QuestManager：找不到任务 '{questId}' 的目标 " +
                    $"'{objectiveId}'，忽略完成请求。"
            );

            return;
        }

        int current = GetObjectiveCurrent(questId, objectiveId);

        AddProgress(
            questId,
            objectiveId,
            objective.requiredAmount - current
        );
    }

    // 目标是否已轮到：列表中排在它之前的目标全部达标才算解锁。
    // 任务按单线顺序推进，跳过前面的目标不应计入进度，
    // 否则会提前播放目标完成/任务完成的表现。
    private static bool IsObjectiveUnlocked(
        QuestAsset asset,
        QuestAsset.QuestObjective objective
    )
    {
        foreach (
            QuestAsset.QuestObjective other in asset.Objectives
        )
        {
            if (other == null || other == objective)
            {
                return true;
            }

            int current = GetObjectiveCurrent(
                asset.QuestId,
                other.objectiveId
            );

            if (current < other.requiredAmount)
            {
                return false;
            }
        }

        return true;
    }

    // 全部目标达标时完成任务：写入完成标记并广播。
    private static void CheckQuestComplete(QuestAsset asset)
    {
        foreach (
            QuestAsset.QuestObjective objective in asset.Objectives
        )
        {
            if (objective == null)
            {
                continue;
            }

            int current = instance.objectiveProgress.TryGetValue(
                ProgressKey(asset.QuestId, objective.objectiveId),
                out int stored
            )
                ? stored
                : 0;

            if (current < objective.requiredAmount)
            {
                return;
            }
        }

        instance.questStates[asset.QuestId] = QuestState.Completed;
        instance.activeQuestOrder.Remove(asset);
        instance.completedQuestOrder.Add(asset);

        // 完成即写入对话标记（如 TargetDone），驱动对话/后续任务。
        SetFlags(asset.FlagsToSetOnComplete);

        asset.OnCompleteEvent?.Invoke();
        instance.QuestCompleted?.Invoke(asset);
    }

    private static void SetFlags(string[] flagNames)
    {
        if (flagNames == null)
        {
            return;
        }

        foreach (string flagName in flagNames)
        {
            DialogueFlags.SetFlag(flagName, true);
        }
    }

    // ============ 查询 ============

    public static QuestState GetQuestState(string questId)
    {
        if (instance == null ||
            string.IsNullOrWhiteSpace(questId) ||
            !instance.questStates.TryGetValue(
                questId,
                out QuestState state
            ))
        {
            return QuestState.NotStarted;
        }

        return state;
    }

    public static bool IsQuestActive(string questId)
    {
        return GetQuestState(questId) == QuestState.Active;
    }

    public static bool IsQuestCompleted(string questId)
    {
        return GetQuestState(questId) == QuestState.Completed;
    }

    public static int GetObjectiveCurrent(
        string questId,
        string objectiveId
    )
    {
        if (instance == null ||
            !instance.objectiveProgress.TryGetValue(
                ProgressKey(questId, objectiveId),
                out int current
            ))
        {
            return 0;
        }

        return current;
    }

    /// <summary>
    /// 当前所有进行中的任务，按接取顺序排列
    /// （HUD 等 UI 从这里取展示对象）。
    /// </summary>
    public static IReadOnlyList<QuestAsset> ActiveQuests
    {
        get
        {
            if (instance == null)
            {
                return Array.Empty<QuestAsset>();
            }

            // 顺便清理已完成的资产引用，保证列表里全是进行中的任务。
            instance.activeQuestOrder.RemoveAll(
                quest => quest == null ||
                    instance.questStates.TryGetValue(
                        quest.QuestId,
                        out QuestState state
                    ) && state != QuestState.Active
            );

            return instance.activeQuestOrder;
        }
    }

    /// <summary>
    /// 所有已完成的任务，按完成顺序排列
    /// （任务面板等 UI 从这里取展示对象）。
    /// </summary>
    public static IReadOnlyList<QuestAsset> CompletedQuests
    {
        get
        {
            if (instance == null)
            {
                return Array.Empty<QuestAsset>();
            }

            // 顺便清理失效引用，保证列表里全是已完成的任务。
            instance.completedQuestOrder.RemoveAll(
                quest => quest == null ||
                    instance.questStates.TryGetValue(
                        quest.QuestId,
                        out QuestState state
                    ) && state != QuestState.Completed
            );

            return instance.completedQuestOrder;
        }
    }
}
