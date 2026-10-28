using UnityEngine;

/// <summary>
/// 标记触发器：玩家进入本物体或其子物体上勾选了
/// Is Trigger 的碰撞体时，把指定标记置为 true，
/// 供对话/任务/演出等系统按标记条件判定。
/// 可选前置条件（对话标记 / 任务目标完成），
/// 已填入的条件必须同时满足才会触发。
/// </summary>
public class FlagTrigger : MonoBehaviour
{
    [Header("触发")]

    [Tooltip("进入范围时置为 true 的标记名，必填")]
    [SerializeField]
    private string flagName;

    [Tooltip("只有该 Tag 的碰撞体会触发；留空则不筛选")]
    [SerializeField]
    private string triggerTag = "Player";

    [Tooltip("触发一次后是否不再生效")]
    [SerializeField]
    private bool triggerOnce = true;

    [Header("前置条件（都可选，未填的条件不参与判定）")]

    [Tooltip("选填：该标记已置位时才允许触发")]
    [SerializeField]
    private string requiredFlag;

    [Tooltip("选填：该任务资产的指定目标完成后才允许触发")]
    [SerializeField]
    private QuestAsset requiredQuest;

    [Tooltip("选填：要检查的目标 id，需与 Required Quest 配合使用")]
    [SerializeField]
    private string requiredObjectiveId;

    // 已触发过；triggerOnce 关闭时无意义。
    private bool fired;

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            Debug.LogWarning(
                "FlagTrigger：Flag Name 为空，本组件不会设置任何标记。",
                this
            );
        }

        if (!HasTriggerCollider())
        {
            Debug.LogWarning(
                "FlagTrigger：本物体及其子物体没有勾选 Is Trigger " +
                    "的碰撞体，玩家走过去不会触发。",
                this
            );
        }

        if (requiredQuest != null &&
            string.IsNullOrWhiteSpace(requiredObjectiveId))
        {
            Debug.LogWarning(
                "FlagTrigger：填了 Required Quest 但 Objective Id 为空，" +
                    "该条件永远不成立。",
                this
            );
        }

        if (requiredQuest == null &&
            !string.IsNullOrWhiteSpace(requiredObjectiveId))
        {
            Debug.LogWarning(
                "FlagTrigger：填了 Objective Id 但没有 Required Quest，" +
                    "该条件不参与判定。",
                this
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && fired)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(flagName))
        {
            return;
        }

        if (!string.IsNullOrEmpty(triggerTag) &&
            !other.CompareTag(triggerTag))
        {
            return;
        }

        // 前置条件不满足：本次进入无效，也不消耗 triggerOnce。
        if (!ConditionsMet())
        {
            return;
        }

        fired = true;

        DialogueFlags.SetFlag(flagName, true);
    }

    // 已配置的前置条件是否全部满足；未填的条件不参与判定。
    private bool ConditionsMet()
    {
        if (!string.IsNullOrWhiteSpace(requiredFlag) &&
            !DialogueFlags.HasFlag(requiredFlag))
        {
            return false;
        }

        if (requiredQuest == null ||
            string.IsNullOrWhiteSpace(requiredObjectiveId))
        {
            return true;
        }

        QuestAsset.QuestObjective objective =
            requiredQuest.GetObjective(requiredObjectiveId);

        if (objective == null)
        {
            return false;
        }

        return QuestManager.GetObjectiveCurrent(
            requiredQuest.QuestId,
            requiredObjectiveId
        ) >= objective.requiredAmount;
    }

    // 本物体及其子物体上是否存在勾选 Is Trigger 的碰撞体。
    private bool HasTriggerCollider()
    {
        foreach (
            Collider childCollider in
            GetComponentsInChildren<Collider>(true)
        )
        {
            if (childCollider.isTrigger)
            {
                return true;
            }
        }

        return false;
    }
}
