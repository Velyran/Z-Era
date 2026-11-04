using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 第一章线性任务控制器：按对话标记推进场景中的解锁，
/// 例如对讲机对话结束后允许拾取，任务推进到捡枪时放出 Glock；
/// 并在玩家拾取场景 Glock 模型时，把玩家手持 Glock 的弹药
/// 设为检查器里的拾取初始值（默认 20/0）。
/// 另按对话资产为 Daniel/William/Sophia/Teresa 四个 NPC 各计一次
/// 完成对话，累计 ch01_tk_01_talktopeople 的目标进度。
/// </summary>
public class Chapter01Controller : MonoBehaviour
{
    /// <summary>一个 NPC 的对话计数条目。</summary>
    [System.Serializable]
    public class TalkTarget
    {
        [Tooltip("NPC 识别名（仅用于检查器辨认，如 Daniel）")]
        public string npcName;

        [Tooltip("该 NPC 名下的对话资产：任一资产的对话完整结束后" +
            "视为\"与该 NPC 对话一次\"")]
        public List<DialogueAsset> dialogues =
            new List<DialogueAsset>();

        // 运行时锁存：该 NPC 已计过数，重复对话不再计数
        //（内存态，场景重载后重置）
        [System.NonSerialized]
        public bool counted;
    }

    // 对讲机对话结束（最后一句播完）时置位的标记，
    // 对应 ch01_dia_william_01_radio.asset 的 End Flag。
    private const string WilliamDialogueEndFlag =
        "ch01_dia_william_01_end";

    // 任务推进到"捡起旁边的枪"时置位的标记，
    // 用于放出 Player 下的 Glock 物体。
    private const string GlockRevealFlag =
        "ch01_tk_01_pickupglock";

    // 听完对讲机旁的自白（mono_04）后置位的标记，
    // 用于解锁通往下一区域的梯子。
    private const string LadderUnlockFlag = "ch01_mono_04";

    // 黑屏切场演出的触发标记：取药对话结束后置位，
    // 播放黑屏淡入淡出，全黑瞬间禁用 William 并切换 Sophia 动画。
    private const string BlackScreenCutFlag =
        "ch01_tk_03_talk_to_william_finished";

    [Tooltip("对讲机对话结束后才启用的可拾取组件" +
        "（拖入 Glock 上的 PickableItem）")]
    [SerializeField]
    private PickableItem glockPickable;

    [Tooltip("该标记置位后才启用的场景对象" +
        "（拖入 Player 下的 Glock 物体）")]
    [SerializeField]
    private GameObject glockObject;

    [Tooltip("该标记置位后才启用的梯子组件" +
        "（拖入目标梯子上的 Ladder）")]
    [SerializeField]
    private Ladder ladder;

    [Header("拾取初始弹药（拾取场景模型时写入手持 Glock）")]

    [Tooltip("拾取 Glock 时写入手持枪弹匣的子弹数（Glock 弹匣容量 20）")]
    [SerializeField, Min(0)]
    private int pickupMagazineAmmo = 20;

    [Tooltip("拾取 Glock 时写入手持枪的备弹数")]
    [SerializeField, Min(0)]
    private int pickupReserveAmmo;

    [Header("对话人数统计（ch01_tk_01_talktopeople）")]

    [Tooltip("任务 Id（QuestManager），与 ch01_tk_01 任务资产一致")]
    [SerializeField]
    private string talkQuestId = "ch01_tk_01";

    [Tooltip("目标 Id：任务资产里的 ch01_tk_01_talktopeople")]
    [SerializeField]
    private string talkObjectiveId = "ch01_tk_01_talktopeople";

    [Tooltip("每个 NPC 首次完成对话时为该目标增加的进度")]
    [Min(1)]
    [SerializeField]
    private int talkProgressAmount = 1;

    [Tooltip("四个 NPC（Daniel/William/Sophia/Teresa）各一条：" +
        "条目里拖入该 NPC 名下的对话资产，完整对话一次计数一次，" +
        "重复对话不重复计数")]
    [SerializeField]
    private List<TalkTarget> talkTargets = new List<TalkTarget>();

    [Header("黑屏切场演出（取药后）")]

    [Tooltip("触发黑屏演出的标记，留空则使用默认 ch01_tk_03_talk_to_william_finished")]
    [SerializeField]
    private string blackScreenCutFlag;

    [Tooltip("黑屏物体：一个带 CanvasGroup 的全屏黑色 Image。" +
        "初始应 alpha=0（透明），演出时由本组件驱动淡入淡出")]
    [SerializeField]
    private CanvasGroup blackScreen;

    [Tooltip("淡入到全黑的时长（秒）")]
    [SerializeField, Min(0f)]
    private float fadeInDuration = 1f;

    [Tooltip("全黑停留时长（秒），期间执行禁用 William 与切换 Sophia 动画")]
    [SerializeField, Min(0f)]
    private float holdDuration = 0.5f;

    [Tooltip("淡出到透明的时长（秒）")]
    [SerializeField, Min(0f)]
    private float fadeOutDuration = 1f;

    [Tooltip("全黑瞬间要禁用的 NPC（William）；留空则不禁用")]
    [SerializeField]
    private GameObject williamObject;

    [Tooltip("Sophia 的 Animator，用于在全黑时切换动画；留空则不切换")]
    [SerializeField]
    private Animator sophiaAnimator;

    [Tooltip("全黑时切换到的 Sophia 动画片段；留空则不切换")]
    [SerializeField]
    private AnimationClip sophiaAnimation;

    // 手持 Glock 上的 WeaponController，按 glockObject 自动解析并缓存
    private WeaponController cachedGlockWeapon;

    // 拾取发生在手持枪放出之前时的补写标记：
    // 直接写弹药会被随后的 WeaponInitialization（Awake 满弹）覆盖，
    // 记账等放出后补写
    private bool pickupAmmoPending;

    // 对话计数：DialogueRunner 可能晚于本组件就绪（对话画布的
    // 激活时机不定），每帧补订阅直到拿到；记录最近一次开始的
    // 对话资产，结束时按资产归属到对应 NPC 条目
    private DialogueRunner dialogueRunner;
    private DialogueAsset pendingTalkAsset;

    private void Awake()
    {
        // 场景重进时标记可能早已置位，这里直接按当前状态对齐：
        // 对话结束前枪不可交互，任务推进前枪不出现。
        ApplyPickableUnlock(
            DialogueFlags.HasFlag(WilliamDialogueEndFlag)
        );

        ApplyObjectUnlock(
            DialogueFlags.HasFlag(GlockRevealFlag)
        );

        ApplyLadderUnlock(
            DialogueFlags.HasFlag(LadderUnlockFlag)
        );
    }

    private void Start()
    {
        EnsureRunnerSubscription();
    }

    private void OnEnable()
    {
        DialogueFlags.FlagSet += OnFlagSet;
    }

    private void OnDisable()
    {
        DialogueFlags.FlagSet -= OnFlagSet;
        UnsubscribeRunner();
    }

    // 对话计数依赖 DialogueRunner 的实例事件；它的画布可能
    // 晚于本组件就绪，Start 与每帧都尝试补订阅，拿到即挂上
    private void Update()
    {
        EnsureRunnerSubscription();
    }

    private void EnsureRunnerSubscription()
    {
        if (dialogueRunner != null ||
            DialogueRunner.Instance == null)
        {
            return;
        }

        dialogueRunner = DialogueRunner.Instance;
        dialogueRunner.DialogueStarted += OnDialogueStarted;
        dialogueRunner.DialogueEnded += OnDialogueEnded;
    }

    private void UnsubscribeRunner()
    {
        if (dialogueRunner == null)
        {
            return;
        }

        dialogueRunner.DialogueStarted -= OnDialogueStarted;
        dialogueRunner.DialogueEnded -= OnDialogueEnded;
        dialogueRunner = null;
    }

    // 记录本次对话的资产，结束时据此归属到 NPC 条目
    private void OnDialogueStarted(DialogueAsset asset)
    {
        pendingTalkAsset = asset;
    }

    // 对话完整结束：资产命中某个 NPC 条目、且该 NPC 未计过数时，
    // 为 ch01_tk_01_talktopeople 增加进度；重复对话不重复计数。
    // 中途被打断的对话不经过 DialogueEnded，不会计数
    private void OnDialogueEnded()
    {
        DialogueAsset finished = pendingTalkAsset;
        pendingTalkAsset = null;

        if (finished == null || talkTargets == null)
        {
            return;
        }

        foreach (TalkTarget target in talkTargets)
        {
            if (target == null ||
                target.counted ||
                target.dialogues == null ||
                !target.dialogues.Contains(finished))
            {
                continue;
            }

            target.counted = true;

            QuestManager.AddProgress(
                talkQuestId,
                talkObjectiveId,
                talkProgressAmount
            );

            // 一次结束只归属一个条目
            return;
        }
    }

    private void OnFlagSet(string flagName)
    {
        if (flagName == WilliamDialogueEndFlag)
        {
            ApplyPickableUnlock(true);
            return;
        }

        if (flagName == GlockRevealFlag)
        {
            ApplyObjectUnlock(true);
            return;
        }

        if (flagName == LadderUnlockFlag)
        {
            ApplyLadderUnlock(true);
            return;
        }

        string cutFlag = ResolveBlackScreenCutFlag();

        if (!string.IsNullOrWhiteSpace(cutFlag) &&
            flagName == cutFlag)
        {
            StartCoroutine(PlayBlackScreenCut());
        }
    }

    // 解析黑屏演出触发标记：检查器留空则用常量默认值。
    private string ResolveBlackScreenCutFlag()
    {
        return string.IsNullOrWhiteSpace(blackScreenCutFlag)
            ? BlackScreenCutFlag
            : blackScreenCutFlag;
    }

    // 黑屏淡入淡出演出：
    // 淡入 → 全黑瞬间禁用 William、切换 Sophia 动画 → 淡出。
    private IEnumerator PlayBlackScreenCut()
    {
        if (blackScreen == null)
        {
            blackScreen = CreateRuntimeBlackScreen();
        }

        // 锁操作，避免黑屏期间玩家转动视角。
        // 对话结束标记置位时 DialogueRunner 可能还在收尾（状态尚未
        // 回到 Idle），这里等待一帧让收尾完成再尝试上锁。
        yield return null;

        bool locked = DialogueRunner.TryLockGameplay();

        if (!locked)
        {
            // 拿不到锁（对话仍在进行或场景里没有 DialogueRunner）
            // 也继续执行演出，只是玩家操作不会被锁定。
            Debug.LogWarning(
                "Chapter01Controller：黑屏演出期间未能锁定玩家操作。",
                this
            );
        }

        // 淡入到全黑。
        yield return FadeBlackScreen(0f, 1f, fadeInDuration);

        // 全黑瞬间执行场景切换。
        if (williamObject != null)
        {
            williamObject.SetActive(false);
        }

        if (sophiaAnimator != null && sophiaAnimation != null)
        {
            sophiaAnimator.Play(sophiaAnimation.name, 0, 0f);
        }

        // 全黑停留，给场景状态切换留一帧稳定时间。
        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        // 淡出到透明。
        yield return FadeBlackScreen(1f, 0f, fadeOutDuration);

        if (locked)
        {
            DialogueRunner.UnlockGameplay();
        }
    }

    // 把 blackScreen.alpha 从 from 插值到 to，duration<=0 时立即完成。
    private IEnumerator FadeBlackScreen(
        float from,
        float to,
        float duration
    )
    {
        if (duration <= 0f)
        {
            blackScreen.alpha = to;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            blackScreen.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        blackScreen.alpha = to;
    }

    // 运行时创建一个全屏黑屏（Screen Space - Overlay Canvas +
    // 黑色 Image + CanvasGroup），供未在检查器配置 Black Screen 时使用。
    private CanvasGroup CreateRuntimeBlackScreen()
    {
        GameObject canvasObj = new GameObject(
            "RuntimeBlackScreen",
            typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster)
        );

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        UnityEngine.UI.CanvasScaler scaler =
            canvasObj.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode =
            UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution =
            new Vector2(Screen.width, Screen.height);

        GameObject imageObj = new GameObject("BlackImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        UnityEngine.UI.Image image =
            imageObj.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.black;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        CanvasGroup group = imageObj.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        return group;
    }

    // 启用/禁用目标可拾取组件；未赋值时跳过。
    private void ApplyPickableUnlock(bool unlocked)
    {
        if (glockPickable == null)
        {
            return;
        }

        glockPickable.enabled = unlocked;
    }

    /// <summary>
    /// 拾取场景 Glock 模型时调用（挂在该模型 PickableItem 的
    /// On Picked Up / onPickedUp 上，检查器接线）：
    /// 把玩家手持 Glock 的弹药设为上方的拾取初始值（默认 20/0）。
    /// 拾取的场景模型与手持模型是两个物体，弹药写在手持那份上。
    /// </summary>
    public void SetGlockPickupAmmo()
    {
        WeaponController weapon = ResolveGlockWeapon();

        if (weapon == null)
        {
            Debug.LogWarning(
                "Chapter01Controller：找不到手持 Glock 的 WeaponController" +
                    "（Glock Object 未赋值或其上没有该组件），拾取弹药未设置。",
                this
            );
            return;
        }

        // 手持枪还没放出：记账，等 ApplyObjectUnlock 放出时补写
        if (!weapon.gameObject.activeInHierarchy)
        {
            pickupAmmoPending = true;
            return;
        }

        ApplyPickupAmmo(weapon);
    }

    // 按 glockObject 解析手持枪的 WeaponController（含子物体），缓存
    private WeaponController ResolveGlockWeapon()
    {
        if (cachedGlockWeapon == null && glockObject != null)
        {
            cachedGlockWeapon =
                glockObject.GetComponent<WeaponController>();

            if (cachedGlockWeapon == null)
            {
                cachedGlockWeapon =
                    glockObject.GetComponentInChildren<
                        WeaponController
                    >(true);
            }
        }

        return cachedGlockWeapon;
    }

    // 写入拾取初始弹药。此前 WeaponInitialization 已在 Awake 里
    // 按满弹初始化完毕（每实例只跑一次），这里的赋值不会被覆盖；
    // 满匣 + 零备弹 与 HandleAmmo 的换弹守门自洽，每帧也不会被改。
    private void ApplyPickupAmmo(WeaponController weapon)
    {
        if (weapon == null)
        {
            return;
        }

        weapon.currentMagazineAmmo = pickupMagazineAmmo;
        weapon.currentCarriedAmmo = pickupReserveAmmo;
    }

    // 显示/隐藏目标物体；未赋值时跳过。
    private void ApplyObjectUnlock(bool unlocked)
    {
        if (glockObject == null)
        {
            return;
        }

        glockObject.SetActive(unlocked);

        // 拾取先于手持枪放出时记的账：放出瞬间 WeaponController
        // 的 Awake（满弹初始化）已同步执行完，此刻补写才不会被覆盖
        if (unlocked && pickupAmmoPending)
        {
            pickupAmmoPending = false;
            ApplyPickupAmmo(ResolveGlockWeapon());
        }
    }

    // 启用/禁用梯子组件；未赋值时跳过。
    // 注意：禁用的是组件而不是物体，梯子的碰撞体仍在，
    // 由 PlayerLadderClimb 检查 ladder.enabled 拦截攀爬。
    private void ApplyLadderUnlock(bool unlocked)
    {
        if (ladder == null)
        {
            return;
        }

        ladder.enabled = unlocked;
    }
}
