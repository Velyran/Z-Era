using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单条成就的展示组件：挂在成就面板下每个成就根物体上
/// （例如 Achievement01Root）。
///
/// 未解锁 / 已解锁的表现由对应的对话标记（DialogueFlags）驱动：
///   · 未解锁：图标为 Locked Icon（问号图），名称为 Locked Name（"未解锁"）；
///   · 已解锁：图标为 Unlocked Icon，名称为 Unlocked Name。
/// 描述文本不参与切换：内容始终是 Inspector 里填好的原文，
/// 需要隐藏未解锁描述请自己在场景里处理。
///
/// 解锁判定读的是"历史标记"（DialogueFlags.HasEverSetFlag）：
/// 标记只要设置过一次为 true 就永久解锁，回到主菜单重置本轮剧情
/// 进度（DialogueFlags.ResetProgress）不会让已解锁的成就重新变灰。
///
/// 刷新时机：OnEnable / Start 各刷一次，另提供公开 Refresh()
/// 供面板在打开时主动调用。标记在游戏中途被置位时，
/// 下一次打开成就面板即可看到最新状态。
/// </summary>
public class AchievementEntry : MonoBehaviour
{
    [Header("解锁条件")]
    [Tooltip("对应的对话标记名；为 true 表示该成就已解锁")]
    [SerializeField]
    private string unlockFlag;

    [Header("UI 引用")]
    [Tooltip("成就图标（未锁定/已锁定两种贴图）")]
    [SerializeField]
    private Image achievementImage;

    [Tooltip("成就名称文本")]
    [SerializeField]
    private TMP_Text achievementName;

    [Tooltip("成就描述文本：只做引用，脚本不修改其内容")]
    [SerializeField]
    private TMP_Text achievementDescription;

    [Header("未解锁表现")]
    [Tooltip("未解锁时显示的图标（问号图）")]
    [SerializeField]
    private Sprite lockedIcon;

    [Tooltip("未解锁时显示的文本，默认“未解锁”")]
    [SerializeField]
    private string lockedName = "未解锁";

    [Header("已解锁表现")]
    [Tooltip("解锁后显示的成就图标")]
    [SerializeField]
    private Sprite unlockedIcon;

    [Tooltip("解锁后显示的成就名称")]
    [SerializeField]
    private string unlockedName;

    /// <summary>该成就当前是否已解锁（标记设置过一次即永久解锁）。</summary>
    public bool IsUnlocked =>
        !string.IsNullOrWhiteSpace(unlockFlag) &&
        DialogueFlags.HasEverSetFlag(unlockFlag);

    private void OnEnable()
    {
        Refresh();
    }

    private void Start()
    {
        // OnEnable 可能早于 DialogueFlags 常驻物体就绪，
        // Start 再刷一次兜底，保证进入面板时状态正确。
        Refresh();
    }

    /// <summary>
    /// 按当前标记状态刷新图标与名称。
    /// 面板每次打开时调用即可。
    /// </summary>
    public void Refresh()
    {
        bool unlocked = IsUnlocked;

        if (achievementImage != null)
        {
            Sprite target =
                unlocked ? unlockedIcon : lockedIcon;

            // 贴图为空时不改原样，避免把 Inspector 里摆好的图抹掉。
            if (target != null)
            {
                achievementImage.sprite = target;
            }
        }

        if (achievementName != null)
        {
            achievementName.text =
                unlocked ? unlockedName : lockedName;
        }

        // 描述文本不切换：内容始终保留 Inspector 里填好的原文。
    }

    // ============ 以下实例方法供 UnityEvent 在检查器里接线 ============

    /// <summary>置位解锁标记（用于测试或剧情触发）。</summary>
    public void Unlock()
    {
        if (!string.IsNullOrWhiteSpace(unlockFlag))
        {
            DialogueFlags.SetFlag(unlockFlag, true);
        }

        Refresh();
    }

    /// <summary>清除解锁标记与永久解锁记录（用于测试或清档）。</summary>
    public void Lock()
    {
        if (!string.IsNullOrWhiteSpace(unlockFlag))
        {
            DialogueFlags.SetFlag(unlockFlag, false);
            DialogueFlags.ClearEverSetFlag(unlockFlag);
        }

        Refresh();
    }
}
