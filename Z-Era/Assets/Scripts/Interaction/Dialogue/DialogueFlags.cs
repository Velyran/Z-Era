using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对话标记存储：记录剧情进度，驱动选项与节点的条件显隐。
/// 静态方法供代码调用（HasFlag / SetFlag）；
/// 实例方法供选项的 UnityEvent 在检查器里接线（SetFlagTrue 等）。
/// 首次访问时自动创建常驻物体，无需手动摆进场景。
///
/// 标记置位广播（FlagSet）：某个当前标记从"未设置"变为"已设置"时
/// 触发一次，成就弹窗等全局系统订阅它即可感知解锁瞬间。
/// 三种来源（任务资产的 flagsToSetOnComplete、对话选项的
/// SetFlagTrue、代码调用）都经过 SetFlag，因此都会被覆盖。
///
/// 两层标记：
///   · 当前标记（HasFlag）：本轮游玩的剧情/任务进度，
///     回到主菜单时由 ResetProgress 清空，关卡因此可以重新游玩；
///   · 历史标记（HasEverSetFlag）：只要设置过一次为 true 就永久保留
///     （PlayerPrefs 跨启动保存），供成就、关卡解锁等一次性进度使用，
///     不受 ResetProgress 影响。
/// </summary>
public class DialogueFlags : MonoBehaviour
{
    // 历史标记在 PlayerPrefs 中的存储键（换行分隔的标记名清单）
    private const string HistoryPrefsKey = "DialogueFlags.History";

    private static DialogueFlags instance;

    // 当前标记：本轮游玩的剧情进度，ResetProgress 会清空
    private readonly HashSet<string> flags = new HashSet<string>();

    // 历史标记：设置过一次即永久保留，ResetProgress 不清空
    private readonly HashSet<string> historyFlags =
        new HashSet<string>();

    /// <summary>
    /// 某个当前标记从"未设置"变为"已设置"时触发，参数为标记名。
    /// 只在状态真正发生变化时触发：重复设置同一个已存在的标记
    /// 不会再广播（成就弹窗因此天然只弹一次）。
    /// </summary>
    public static event Action<string> FlagSet;

    public static DialogueFlags Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new GameObject("DialogueFlags")
                    .AddComponent<DialogueFlags>();
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        LoadHistory();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // 当前标记是否已设置（不含历史标记）。
    public static bool HasFlag(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return false;
        }

        return Instance.flags.Contains(flagName);
    }

    // 该标记是否曾经被设置为 true：跨场景、跨启动永久保留，
    // 供成就、关卡解锁等一次性进度判断
    public static bool HasEverSetFlag(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return false;
        }

        return Instance.historyFlags.Contains(flagName);
    }

    // 设置或清除当前标记；置位时同时写入历史标记
    public static void SetFlag(string flagName, bool value)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return;
        }

        if (value)
        {
            // 只有从"未设置"变为"已设置"才算新解锁，
            // 重复设置同一个标记不再广播（成就不会重复弹）
            bool newlySet = Instance.flags.Add(flagName);

            // 首次置位即写入历史并落盘，此后永久保留
            if (Instance.historyFlags.Add(flagName))
            {
                SaveHistory();
            }

            if (newlySet)
            {
                FlagSet?.Invoke(flagName);
            }
        }
        else
        {
            Instance.flags.Remove(flagName);
        }
    }

    // 清除历史标记（成就/解锁记录），用于测试或清档
    public static void ClearEverSetFlag(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return;
        }

        if (Instance.historyFlags.Remove(flagName))
        {
            SaveHistory();
        }
    }

    // 清空当前标记、保留历史标记：回到主菜单重开一轮时调用
    public static void ResetProgress()
    {
        // 存储尚未创建时没有需要清空的内容，不为此额外创建常驻物体
        if (instance == null)
        {
            return;
        }

        instance.flags.Clear();
    }

    // 清空全部标记（当前 + 历史），例如清档重开
    public static void ClearAll()
    {
        Instance.flags.Clear();
        Instance.historyFlags.Clear();

        PlayerPrefs.DeleteKey(HistoryPrefsKey);
        PlayerPrefs.Save();
    }

    // 从 PlayerPrefs 还原历史标记（跨启动保留）
    private void LoadHistory()
    {
        historyFlags.Clear();

        string stored = PlayerPrefs.GetString(
            HistoryPrefsKey,
            string.Empty
        );

        if (string.IsNullOrEmpty(stored))
        {
            return;
        }

        foreach (string flagName in stored.Split('\n'))
        {
            string trimmed = flagName.Trim();

            if (trimmed.Length > 0)
            {
                historyFlags.Add(trimmed);
            }
        }
    }

    private static void SaveHistory()
    {
        Instance.SaveHistoryInternal();
    }

    private void SaveHistoryInternal()
    {
        PlayerPrefs.SetString(
            HistoryPrefsKey,
            string.Join("\n", historyFlags)
        );

        // 立即落盘：成就/解锁记录不能因强退丢失
        PlayerPrefs.Save();
    }

    // 以下实例方法供 UnityEvent 在检查器中动态接线。

    public void SetFlagTrue(string flagName)
    {
        SetFlag(flagName, true);
    }

    public void SetFlagFalse(string flagName)
    {
        SetFlag(flagName, false);
    }

    public void ClearAllFlags()
    {
        ClearAll();
    }
}
