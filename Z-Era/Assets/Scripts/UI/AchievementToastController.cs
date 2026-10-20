using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 成就弹窗：玩家达成成就时，在画面右下方从下往上滑出一条提示，
/// 停留若干秒后再滑出。表现参考 Steam 的成就通知。
///
/// 全局生效：本组件挂在 "SwitchScene Canvas" 下的弹窗根物体上
/// （该 Canvas 由 SceneTransitionController 通过 DontDestroyOnLoad
/// 跨场景复用，Sorting Order = 100，层级高于所有面板）。
///
/// 触发方式：订阅 DialogueFlags.FlagSet 广播。任何一个全局标记
/// 从"未设置"变为"已设置"时，按配置里的成就标记匹配到对应条目
/// 并入队播放。任务资产、对话选项、UnityEvent、代码调用设置标记
/// 都能触发，不需要额外接线。
///
/// 每条成就在检查器里填：解锁标记 + 图标 + 名称，不需要描述。
///
/// 只弹一次：每条成就弹过之后会在 PlayerPrefs 里留下记录，
/// 重进关卡、重启游戏（乃至更新版本后）都不会再弹第二次。
/// 想重置请用本组件右键菜单里的"清除全部弹窗记录"。
/// </summary>
public class AchievementToastController : MonoBehaviour
{
    /// <summary>一条成就的展示信息，在检查器里逐条填写。</summary>
    [System.Serializable]
    public class AchievementEntry
    {
        [Tooltip("成就解锁标记（DialogueFlags 里的全局标记名），" +
            "该标记被置位时弹出本条；与成就面板的解锁标记保持一致")]
        public string unlockFlag;

        [Tooltip("成就图标（弹窗方框里显示的图）")]
        public Sprite icon;

        [Tooltip("成就名称文本，例如“是的，长官！”")]
        public string achievementName;
    }

    [Header("引用")]

    [Tooltip("弹窗根物体上的 CanvasGroup：整体透明度与射线拦截由它控制，" +
        "留空则运行时自动添加")]
    [SerializeField]
    private CanvasGroup canvasGroup;

    [Tooltip("会整体滑动的那层 RectTransform（通常是弹窗根物体或它的" +
        "唯一子物体），位置动画以它为准；留空则用本物体的 RectTransform")]
    [SerializeField]
    private RectTransform slideRoot;

    [Tooltip("成就图标 Image")]
    [SerializeField]
    private Image iconImage;

    [Tooltip("成就名称文本")]
    [SerializeField]
    private TMP_Text nameText;

    [Header("成就列表")]

    [Tooltip("逐条填写成就：解锁标记 + 图标 + 名称。" +
        "标记被置位时按这里的顺序匹配")]
    [SerializeField]
    private List<AchievementEntry> entries =
        new List<AchievementEntry>();

    [Header("表现")]

    [Tooltip("滑入时长（秒）")]
    [SerializeField, Min(0.01f)]
    private float slideInDuration = 0.35f;

    [Tooltip("停留时长（秒），从滑入完成开始计时")]
    [SerializeField, Min(0f)]
    private float holdDuration = 3f;

    [Tooltip("滑出时长（秒）")]
    [SerializeField, Min(0.01f)]
    private float slideOutDuration = 0.3f;

    [Tooltip("相邻两条成就之间的间隔（秒）")]
    [SerializeField, Min(0f)]
    private float queueGap = 0.15f;

    [Tooltip("滑动距离（像素）：从目标位置下方这么多像素处滑入，" +
        "滑出时反向回到这里")]
    [SerializeField, Min(1f)]
    private float slideDistance = 160f;

    [Tooltip("滑入/滑出的缓动曲线：横轴为归一化时间，纵轴为进度")]
    [SerializeField]
    private AnimationCurve slideCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("音效")]

    [Tooltip("成就弹出时播放的音效，留空则不播放")]
    [SerializeField]
    private AudioClip showClip;

    [Tooltip("音效音量（0~1）")]
    [SerializeField, Range(0f, 1f)]
    private float showVolume = 1f;

    // 正在排队的成就（先进先出）
    private readonly Queue<AchievementEntry> pending =
        new Queue<AchievementEntry>();

    private Coroutine playCoroutine;

    // 播放音效的专属音源：弹窗物体可能随画布一起被禁用，
    // 用独立音源并把音效放完，避免滑出瞬间被截断
    private AudioSource audioSource;

    // 弹窗的基准位置（滑入的目标位置），Awake 时记录
    private Vector2 shownPosition;

    private bool ready;

    // ============ 弹窗记录（跨启动只弹一次） ============

    private const string ToastedPrefsPrefix = "AchievementToast.Toasted.";

    private static string ToastedKey(string flagName)
    {
        return ToastedPrefsPrefix + flagName;
    }

    private static bool HasToasted(string flagName)
    {
        return PlayerPrefs.GetInt(ToastedKey(flagName), 0) == 1;
    }

    private static void MarkToasted(string flagName)
    {
        PlayerPrefs.SetInt(ToastedKey(flagName), 1);
        PlayerPrefs.Save();
    }

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (slideRoot == null)
        {
            slideRoot = transform as RectTransform;
        }

        if (slideRoot != null)
        {
            shownPosition = slideRoot.anchoredPosition;
        }

        // 弹窗不拦截点击：它只是提示，不该挡住按钮
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0f;

        // 引用不全时只告警一次，之后不再尝试播放，避免每帧刷日志
        ready = slideRoot != null &&
            iconImage != null &&
            nameText != null;

        if (!ready)
        {
            Debug.LogWarning(
                "AchievementToastController：引用未配齐（需要 Slide Root、" +
                    "Icon Image、Name Text），成就弹窗不会显示。",
                this
            );
        }

        // 音效音源：复用物体上已有的，没有则自动补一个；
        // playOnAwake 关掉避免物体激活时误播
        if (!TryGetComponent(out audioSource))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        // 成就常在场景过渡（Time.timeScale = 0 + AudioListener.pause）
        // 或暂停面板打开期间达成，音效必须忽略全局暂停才听得见
        audioSource.ignoreListenerPause = true;
    }

    private void OnEnable()
    {
        // 订阅全局标记置位广播：任务/对话/UnityEvent 设标记都会经过这里
        DialogueFlags.FlagSet += OnFlagSet;
    }

    private void OnDisable()
    {
        DialogueFlags.FlagSet -= OnFlagSet;

        // 弹窗随画布禁用时收尾，避免半透明状态带进下一轮
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        pending.Clear();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        if (slideRoot != null)
        {
            slideRoot.anchoredPosition = shownPosition;
        }
    }

    // 标记被置位：匹配到成就条目就入队
    private void OnFlagSet(string flagName)
    {
        if (!ready || string.IsNullOrWhiteSpace(flagName))
        {
            return;
        }

        AchievementEntry matched = FindEntry(flagName);

        if (matched == null)
        {
            return;
        }

        // 只弹一次：弹过的成就写入 PlayerPrefs，跨启动不再重复
        if (HasToasted(flagName))
        {
            return;
        }

        MarkToasted(flagName);
        pending.Enqueue(matched);

        // 已在播放则不打断，队列里的等当前这条播完自动接上
        if (playCoroutine == null)
        {
            playCoroutine = StartCoroutine(PlayQueue());
        }
    }

    private AchievementEntry FindEntry(string flagName)
    {
        foreach (AchievementEntry entry in entries)
        {
            if (entry != null &&
                !string.IsNullOrWhiteSpace(entry.unlockFlag) &&
                entry.unlockFlag == flagName)
            {
                return entry;
            }
        }

        return null;
    }

    // 依次播放队列里的每条成就，播完一条再播下一条
    private IEnumerator PlayQueue()
    {
        while (pending.Count > 0)
        {
            AchievementEntry entry = pending.Dequeue();

            ApplyEntry(entry);

            // 滑入的同一帧播放音效：与弹窗出现的时机对齐
            PlayShowSound();

            yield return Slide(0f, 1f, slideInDuration);

            // 停留：用未缩放时间，暂停面板打开期间也照常计时
            if (holdDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(holdDuration);
            }

            yield return Slide(1f, 0f, slideOutDuration);

            if (queueGap > 0f && pending.Count > 0)
            {
                yield return new WaitForSecondsRealtime(queueGap);
            }
        }

        playCoroutine = null;
    }

    // 把条目内容写入 UI；图标为空时保留原有贴图，不清空
    private void ApplyEntry(AchievementEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        if (iconImage != null && entry.icon != null)
        {
            iconImage.sprite = entry.icon;
        }

        if (nameText != null)
        {
            nameText.text = entry.achievementName;
        }
    }

    // 播放成就弹出音效；音量取检查器里的 Show Volume
    private void PlayShowSound()
    {
        if (showClip == null || audioSource == null)
        {
            return;
        }

        // 音量在播放时读取，Play 模式里调滑块即时生效
        audioSource.PlayOneShot(showClip, Mathf.Clamp01(showVolume));
    }

    // 透明度与位置一起过渡：alpha 0→1 的同时从下方 slideDistance
    // 处滑到目标位置；滑出时反向。全部使用未缩放时间，
    // Time.timeScale = 0（暂停面板、场景过渡）时也能正常播放
    private IEnumerator Slide(float alphaFrom, float alphaTo, float duration)
    {
        Vector2 hiddenPosition =
            shownPosition + Vector2.down * slideDistance;

        // 滑入从下方开始，滑出从目标位置开始
        Vector2 positionFrom = alphaTo > alphaFrom
            ? hiddenPosition
            : shownPosition;

        Vector2 positionTo = alphaTo > alphaFrom
            ? shownPosition
            : hiddenPosition;

        canvasGroup.alpha = alphaFrom;
        slideRoot.anchoredPosition = positionFrom;

        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;

            float progress = slideCurve.Evaluate(
                Mathf.Clamp01(time / duration)
            );

            canvasGroup.alpha = Mathf.Lerp(
                alphaFrom,
                alphaTo,
                progress
            );

            slideRoot.anchoredPosition = Vector2.LerpUnclamped(
                positionFrom,
                positionTo,
                progress
            );

            yield return null;
        }

        canvasGroup.alpha = alphaTo;
        slideRoot.anchoredPosition = positionTo;
    }

    /// <summary>
    /// 清除全部"已弹过"记录，让所有成就都能重新弹一次。
    /// 仅用于测试，正常玩法不需要调用。
    /// </summary>
    [ContextMenu("清除全部弹窗记录（测试）")]
    public void ClearAllToastRecords()
    {
        foreach (AchievementEntry entry in entries)
        {
            if (entry != null &&
                !string.IsNullOrWhiteSpace(entry.unlockFlag))
            {
                PlayerPrefs.DeleteKey(ToastedKey(entry.unlockFlag));
            }
        }

        PlayerPrefs.Save();
    }
}
