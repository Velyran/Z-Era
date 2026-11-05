using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 玩家自白 HUD：挂在 HUD Canvas 下的自白物体上（建议直接挂在
/// 一个 TMP_Text 物体上，文本留空则自动取同物体/子物体上的组件）。
/// 由 MonologueTrigger 通过 Show(asset) 调用：以打字机形式逐段
/// 打印自白（段数自定、按序播放，段间不淡入淡出、上一段立即
/// 消失下一段立即出现，段间间隔可配），最后一段停留指定时长后
/// 整体淡出。文本位置在场景中预先摆好，本脚本不再覆盖。
/// 可见性只靠 CanvasGroup 透明度控制（缺省运行时自动添加），
/// 物体常驻启用，保证 Instance 在场景加载后即可用。
/// 打字、停留、淡出均使用未缩放时间，暂停面板打开时照常走完。
/// </summary>
public class MonologueUIController : MonoBehaviour
{
    public static MonologueUIController Instance { get; private set; }

    [Tooltip("自白正文文本，留空则自动取同物体或子物体上的 TMP_Text")]
    [SerializeField]
    private TMP_Text monologueText;

    [Tooltip("整体淡出的 CanvasGroup，留空则运行时自动添加")]
    [SerializeField]
    private CanvasGroup canvasGroup;

    private Coroutine displayRoutine;

    // HUD 被关闭（对话、睁眼演出、背包等）期间请求的自白：
    // 此时本组件不在激活状态，协程起不来（Unity 会报
    // "Coroutine couldn't be started because the game object is inactive"），
    // 先存下来，等 OnEnable 再播。后到的自白顶掉先前的。
    private MonologueAsset pendingAsset;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (monologueText == null)
        {
            monologueText = GetComponent<TMP_Text>();

            if (monologueText == null)
            {
                monologueText =
                    GetComponentInChildren<TMP_Text>(true);
            }
        }

        if (monologueText == null)
        {
            Debug.LogError(
                "MonologueUIController：找不到 TMP_Text，" +
                    "自白无法显示。",
                this
            );
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        ResetDisplay();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        // 挂起期间 HUD 被重新启用：把当时没播成的自白补播出来。
        if (pendingAsset == null)
        {
            return;
        }

        MonologueAsset asset = pendingAsset;
        pendingAsset = null;

        BeginShow(asset);
    }

    private void OnDisable()
    {
        // 画布整体被隐藏（如对话开始时关闭 HUD）时收尾，
        // 避免恢复后残留半透明的旧文本。
        // 注意：pendingAsset 不在这里清空，那是"等 HUD 回来再播"的排队项。
        ResetDisplay();
    }

    /// <summary>
    /// 显示一段自白。正在显示时新自白会立即顶掉旧的。
    /// </summary>
    public static void Show(MonologueAsset asset)
    {
        if (asset == null)
        {
            return;
        }

        if (Instance == null)
        {
            Debug.LogWarning(
                "MonologueUIController：场景中没有自白 UI" +
                    "（HUD Canvas 下缺 MonologueUIController），" +
                    "无法显示自白。"
            );
            return;
        }

        Instance.BeginShow(asset);
    }

    private void BeginShow(MonologueAsset asset)
    {
        if (monologueText == null)
        {
            return;
        }

        // 物体不在层级激活状态时 StartCoroutine 会失败并报错，
        // 协程根本不会跑——先挂起，等 OnEnable 补播。
        if (!isActiveAndEnabled)
        {
            pendingAsset = asset;
            return;
        }

        pendingAsset = null;

        if (displayRoutine != null)
        {
            StopCoroutine(displayRoutine);
            displayRoutine = null;
        }

        // 屏幕位置不在这里设置（文本位置已在场景里固定），
        // 文本内容与可见字符数由逐段播放循环接管
        monologueText.text = string.Empty;
        monologueText.maxVisibleCharacters = 0;
        canvasGroup.alpha = 1f;

        // 自白开始显示即写入桥接标记，供后续自白/对话联动。
        if (asset.FlagsToSetOnShown != null)
        {
            foreach (string flag in asset.FlagsToSetOnShown)
            {
                DialogueFlags.SetFlag(flag, true);
            }
        }

        displayRoutine = StartCoroutine(DisplayRoutine(asset));
    }

    private IEnumerator DisplayRoutine(MonologueAsset asset)
    {
        string[] segments = asset.Segments;
        int segmentCount =
            segments != null ? segments.Length : 0;

        for (int index = 0; index < segmentCount; index++)
        {
            string fullText = segments[index] ?? string.Empty;

            // 打字机：maxVisibleCharacters 只统计可见字符，
            // 富文本标签不会被当作字符打印出来
            //（与 DialogueUIController 相同的数法）。
            int total = fullText.Length;
            int revealed = 0;
            float revealProgress = 0f;

            monologueText.text = fullText;
            monologueText.maxVisibleCharacters = 0;

            while (revealed < total)
            {
                revealProgress +=
                    asset.CharactersPerSecond * Time.unscaledDeltaTime;

                int target = Mathf.Min(
                    total,
                    Mathf.FloorToInt(revealProgress)
                );

                if (target > revealed)
                {
                    monologueText.maxVisibleCharacters = target;
                    revealed = target;
                }

                yield return null;
            }

            // 停留：本段打印完毕后保持完整显示。
            float holdEnd = Time.unscaledTime + asset.HoldDuration;

            while (Time.unscaledTime < holdEnd)
            {
                yield return null;
            }

            // 段间切换（非末段）：不淡入淡出——文字立即消失，
            // 间隔结束后下一段立即出现（回到循环顶部重新打字）
            if (index < segmentCount - 1)
            {
                monologueText.text = string.Empty;
                monologueText.maxVisibleCharacters = 0;

                if (asset.SegmentInterval > 0f)
                {
                    yield return new WaitForSecondsRealtime(
                        asset.SegmentInterval
                    );
                }
            }
        }

        // 全部段播完后整体淡出：透明度渐变到 0 后清空文本。
        float fadeDuration = asset.FadeDuration;

        if (fadeDuration > 0f)
        {
            for (float elapsed = 0f;
                 elapsed < fadeDuration;
                 elapsed += Time.unscaledDeltaTime)
            {
                canvasGroup.alpha =
                    1f - elapsed / fadeDuration;

                yield return null;
            }
        }

        ResetDisplay();
    }

    // 清空文本与透明度，回到待机状态。
    private void ResetDisplay()
    {
        if (displayRoutine != null)
        {
            StopCoroutine(displayRoutine);
            displayRoutine = null;
        }

        if (monologueText != null)
        {
            monologueText.text = string.Empty;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }
}
