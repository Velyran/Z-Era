using UnityEngine;

/// <summary>
/// 玩家自白的数据资产，在 Project 窗口右键 Create > Dialogue >
/// Monologue Asset 创建。可配置自定义段数的文本，按数组顺序逐段
/// 播放（段间不淡入淡出，直接切换）；由 MonologueTrigger 监听
/// 对话标记（DialogueFlags）触发，条件通过时交给 HUD 上的
/// MonologueUIController 以打字机形式显示。
/// 文本的屏幕位置不在资产里配置（场景中已单独摆好）。
/// </summary>
[CreateAssetMenu(
    menuName = "Dialogue/Monologue Asset",
    fileName = "Monologue_"
)]
public class MonologueAsset : ScriptableObject
{
    [Header("自白内容")]

    [TextArea(3, 10)]
    [Tooltip("自白段落（段数自定）：按数组顺序逐段播放，支持 TMP 富文本。" +
        "段与段之间不淡入淡出：上一段立即消失，间隔结束后下一段立即出现")]
    [SerializeField]
    private string[] segments = new string[1];

    [Tooltip("打字速度（每秒字符数）")]
    [Min(1f)]
    [SerializeField]
    private float charactersPerSecond = 20f;

    [Tooltip("每段打印完毕后的停留时长（秒）：非末段随后进入段间间隔，最后一段随后整体淡出")]
    [Min(0f)]
    [SerializeField]
    private float holdDuration = 2f;

    [Tooltip("淡出时长（秒），0 表示最后一段停留结束后立即消失")]
    [Min(0f)]
    [SerializeField]
    private float fadeDuration = 1f;

    [Tooltip("段与段之间的播放间隔（秒）：上一段停留结束后文字立即消失，间隔结束后下一段立即出现")]
    [Min(0f)]
    [SerializeField]
    private float segmentInterval = 0.5f;

    [Header("触发条件（全部满足才触发）")]

    [Tooltip("必须已设置的全部对话标记（DialogueFlags），留空则不限制")]
    [SerializeField]
    private string[] requiredFlags;

    [Tooltip("只要设置了其中任一标记就不触发")]
    [SerializeField]
    private string[] forbiddenFlags;

    [Header("Flag 桥接（与对话/任务系统联动）")]

    [Tooltip("自白开始显示时自动设置的对话标记，例如用于连锁下一段自白或对话")]
    [SerializeField]
    private string[] flagsToSetOnShown;

    public string[] Segments => segments;
    public float CharactersPerSecond => charactersPerSecond;
    public float HoldDuration => holdDuration;
    public float FadeDuration => fadeDuration;
    public float SegmentInterval => segmentInterval;
    public string[] RequiredFlags => requiredFlags;
    public string[] ForbiddenFlags => forbiddenFlags;
    public string[] FlagsToSetOnShown => flagsToSetOnShown;
}
