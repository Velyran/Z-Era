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

    // 任务 ch01_tk_02 开始时置位的标记，
    // 用于给玩家当前手持的 Glock 补发备弹。
    private const string GlockRefillFlag = "ch01_tk_02_start";

    // 抵达超市（ch01_tk_03 相应目标完成）后置位的标记，
    // 用于启用超市物资上的 PickableItem。
    private const string SupermarketFlag =
        "ch01_tk_03_go_to_the_supermarket_finished";

    // 物资搜集目标：ch01_tk_03 的这个目标达标时，
    // 置位检查器里配置的标记。
    private const string GatherQuestId = "ch01_tk_03";
    private const string GatherObjectiveId =
        "ch01_tk_03_gather_resources";

    // 接受 Teresa 的取回十字架委托（对话选项置位）后，
    // 启用十字架上的 PickableItem。
    private const string CrossQuestAcceptedFlag = "agree_teresa";

    // 取药对话结束（药瓶已交给 Sophia）时置位：
    // 此时药瓶应离开背包，销毁 InventoryContainer 下的 Pills。
    private const string MedicineDeliveredFlag =
        "ch01_tk_02_get_medicine_finished";

    // 拿到武器箱钥匙时置位：往背包里加入武器箱钥匙。
    private const string WeaponCrateKeyFlag =
        "ch01_get_weapon_crate_key";

    // 交出手机时置位：手机离开背包，同时加入临时营地钥匙。
    private const string PhoneGivenFlag = "ch01_give_the_phone";

    // 黑屏演出等锁的最长等待时间（秒）：
    // 对话收尾要等溶解播完，超过这个时间就放弃上锁继续演出。
    private const float LockWaitTimeout = 3f;

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

    [Header("Glock 备弹补给（ch01_tk_02 开始）")]

    [Tooltip("标记 ch01_tk_02_start 置位时，" +
        "给玩家当前手持 Glock 增加的备弹数")]
    [SerializeField, Min(0)]
    private int glockRefillAmount = 20;

    [Header("黑屏切场演出（取药后）")]

    [Tooltip("触发黑屏演出的标记，留空则使用默认 ch01_tk_03_talk_to_william_finished")]
    [SerializeField]
    private string blackScreenCutFlag;

    [Tooltip("黑屏开始淡出（全黑停留结束后）时要置位的标记；" +
        "留空则不置位")]
    [SerializeField]
    private string blackScreenFadeOutFlag;

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

    [Header("超市物资拾取（取药后）")]

    [Tooltip("标记 ch01_tk_03_go_to_the_supermarket_finished " +
        "置位后才启用的拾取组件（拖入超市物资上的 PickableItem）")]
    [SerializeField]
    private List<PickableItem> supermarketPickables =
        new List<PickableItem>();

    [Header("物资搜集完成标记（ch01_tk_03_gather_resources）")]

    [Tooltip("目标 ch01_tk_03_gather_resources 达标（搜集满）时" +
        "要置位的标记；留空则只完成目标、不置位")]
    [SerializeField]
    private string gatherResourcesCompleteFlag;

    [Header("物资搜集完成演出（Teresa）")]

    [Tooltip("物资搜集完成时要摆位的 Teresa（拖入 Teresa 的 Transform）")]
    [SerializeField]
    private Transform teresaTransform;

    [Tooltip("物资搜集完成时 Teresa 的世界坐标；" +
        "未赋值 Teresa Transform 时不生效")]
    [SerializeField]
    private Vector3 teresaPosition;

    [Tooltip("物资搜集完成时 Teresa 的欧拉角（角度制）")]
    [SerializeField]
    private Vector3 teresaEulerAngles;

    [Tooltip("Teresa 的 Animator，用于替换 Base 状态里的动画片段")]
    [SerializeField]
    private Animator teresaAnimator;

    [Tooltip("要替换动画片段的状态名（Teresa 的 Animator 里为 Base）")]
    [SerializeField]
    private string teresaBaseStateName = "Base";

    [Tooltip("物资搜集完成时 Teresa 在 Base 状态播放的动画片段")]
    [SerializeField]
    private AnimationClip teresaBaseClip;

    [Tooltip("Teresa 的 NPC Dialogue 组件：物资搜集完成时把它的 " +
        "Interact Half Angle 改为下面配置的角度；" +
        "留空则从 Teresa Transform 上自动查找")]
    [SerializeField]
    private NPCDialogue teresaNpcDialogue;

    [Tooltip("物资搜集完成后 Teresa 的 Interact Half Angle（度）")]
    [Range(0f, 180f)]
    [SerializeField]
    private float teresaInteractHalfAngle = 15f;

    [Header("十字架拾取（接受 Teresa 委托后）")]

    [Tooltip("标记 agree_teresa 置位后才启用的拾取组件" +
        "（拖入十字架上的 PickableItem）")]
    [SerializeField]
    private List<PickableItem> crossPickables =
        new List<PickableItem>();

    [Header("背包物品（药瓶 / 钥匙）")]

    [Tooltip("背包容器 InventoryContainer 的 Transform；" +
        "留空则运行时按名称自动查找")]
    [SerializeField]
    private Transform inventoryContainer;

    [Tooltip("背包中药瓶的物体名（固定名称，必定存在）；" +
        "交出药瓶时按此名销毁")]
    [SerializeField]
    private string pillsObjectName = "Pills";

    [Tooltip("背包中手机的物体名（固定名称，必定存在）；" +
        "交出手机时按此名销毁")]
    [SerializeField]
    private string phoneObjectName = "Phone";

    [Tooltip("背包中十字架的物体名（固定名称，必定存在）；" +
        "拿到武器箱钥匙、十字架已交还 Teresa 时按此名销毁")]
    [SerializeField]
    private string priestCrossObjectName = "PriestCross";

    [Tooltip("标记 ch01_get_weapon_crate_key 置位时，" +
        "加入背包的武器箱钥匙预制体")]
    [SerializeField]
    private GameObject weaponCrateKeyPrefab;

    [Tooltip("武器箱钥匙加入背包时的初始旋转（欧拉角，" +
        "相对背包容器）")]
    [SerializeField]
    private Vector3 weaponCrateKeyRotation =
        new Vector3(0f, -90f, -90f);

    [Tooltip("标记 ch01_give_the_phone 置位时，" +
        "加入背包的联邦军队临时营地钥匙预制体")]
    [SerializeField]
    private GameObject armyCampKeyPrefab;

    [Tooltip("联邦军队临时营地钥匙加入背包时的初始旋转（欧拉角，" +
        "相对背包容器）")]
    [SerializeField]
    private Vector3 armyCampKeyRotation =
        new Vector3(0f, -90f, -90f);

    // 手持 Glock 上的 WeaponController，按 glockObject 自动解析并缓存
    private WeaponController cachedGlockWeapon;

    // Teresa 摆位与动画切换是否已执行（物资搜集完成只做一次）
    private bool teresaGatherApplied;

    // 拾取发生在手持枪放出之前时的补写标记：
    // 直接写弹药会被随后的 WeaponInitialization（Awake 满弹）覆盖，
    // 记账等放出后补写
    private bool pickupAmmoPending;

    // 对话计数：DialogueRunner 可能晚于本组件就绪（对话画布的
    // 激活时机不定），每帧补订阅直到拿到；记录最近一次开始的
    // 对话资产，结束时按资产归属到对应 NPC 条目
    private DialogueRunner dialogueRunner;
    private DialogueAsset pendingTalkAsset;

    // 任务进度订阅：QuestManager 同样可能晚于本组件就绪，
    // 与 DialogueRunner 一样每帧补订阅，用于监听物资搜集目标达标
    private QuestManager questManager;

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

        // 超市物资的拾取组件：抵达超市前一律禁用。
        ApplySupermarketPickables(
            DialogueFlags.HasFlag(SupermarketFlag)
        );

        // 十字架的拾取组件：接受 Teresa 委托前一律禁用。
        ApplyCrossPickables(
            DialogueFlags.HasFlag(CrossQuestAcceptedFlag)
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
        UnsubscribeQuestEvents();
    }

    // 对话计数依赖 DialogueRunner 的实例事件；它的画布可能
    // 晚于本组件就绪，Start 与每帧都尝试补订阅，拿到即挂上
    private void Update()
    {
        EnsureRunnerSubscription();
        EnsureQuestSubscription();
    }

    private void EnsureQuestSubscription()
    {
        if (questManager != null)
        {
            return;
        }

        questManager = QuestManager.Instance;
        questManager.ObjectiveUpdated += OnObjectiveUpdated;
    }

    private void UnsubscribeQuestEvents()
    {
        if (questManager == null)
        {
            return;
        }

        questManager.ObjectiveUpdated -= OnObjectiveUpdated;
        questManager = null;
    }

    // 物资搜集目标达标（搜集满）时，置位检查器配置的标记。
    private void OnObjectiveUpdated(
        QuestAsset asset,
        QuestAsset.QuestObjective objective,
        int current,
        int required
    )
    {
        if (asset == null ||
            objective == null ||
            current < required ||
            asset.QuestId != GatherQuestId ||
            objective.objectiveId != GatherObjectiveId)
        {
            return;
        }

        SetGatherResourcesFlag();
        ApplyTeresaGatherState();
    }

    // 标记留空则不置位；重复置位由 DialogueFlags 自身去重。
    private void SetGatherResourcesFlag()
    {
        if (string.IsNullOrWhiteSpace(gatherResourcesCompleteFlag))
        {
            return;
        }

        DialogueFlags.SetFlag(gatherResourcesCompleteFlag, true);
    }

    // 物资搜集完成时 Teresa 的摆位与动画切换：
    // 设置位置、朝向，并把 Base 状态里的动画片段换成检查器里配置的片段。
    // 只执行一次，避免 ObjectiveUpdated 多次回调时重复 Rebind。
    private void ApplyTeresaGatherState()
    {
        if (teresaGatherApplied)
        {
            return;
        }

        teresaGatherApplied = true;

        if (teresaTransform != null)
        {
            teresaTransform.position = teresaPosition;
            teresaTransform.rotation =
                Quaternion.Euler(teresaEulerAngles);
        }

        // 收紧 Teresa 的可对话朝向范围（默认 15 度）。
        ApplyTeresaInteractHalfAngle();

        if (teresaAnimator == null || teresaBaseClip == null)
        {
            return;
        }

        // 把 Animator 里的片段换成目标片段，再从 Base 状态第 0 帧播
        string stateName = string.IsNullOrWhiteSpace(
            teresaBaseStateName)
                ? "Base"
                : teresaBaseStateName;

        int stateHash = Animator.StringToHash(stateName);

        if (!teresaAnimator.HasState(0, stateHash))
        {
            Debug.LogWarning(
                $"Chapter01Controller：Teresa 的 Animator 里没有状态 " +
                    $"'{stateName}'，未切换动画片段。",
                this
            );

            return;
        }

        ReplaceAnimatorClip(teresaAnimator, teresaBaseClip);

        teresaAnimator.Play(stateHash, 0, 0f);
    }

    // 把 Teresa 的 NPC Dialogue 的 Interact Half Angle 设为检查器配置值。
    // 未赋值时从 Teresa Transform（含子物体）上自动查找。
    private void ApplyTeresaInteractHalfAngle()
    {
        if (teresaNpcDialogue == null && teresaTransform != null)
        {
            teresaNpcDialogue =
                teresaTransform.GetComponent<NPCDialogue>();

            if (teresaNpcDialogue == null)
            {
                teresaNpcDialogue =
                    teresaTransform.GetComponentInChildren<
                        NPCDialogue
                    >(true);
            }
        }

        if (teresaNpcDialogue == null)
        {
            Debug.LogWarning(
                "Chapter01Controller：找不到 Teresa 的 NPCDialogue" +
                    "（NPC Dialogue 未赋值，且 Teresa Transform 上" +
                    "也没有该组件），Interact Half Angle 未修改。",
                this
            );

            return;
        }

        teresaNpcDialogue.SetInteractHalfAngle(
            teresaInteractHalfAngle
        );
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

        if (flagName == GlockRefillFlag)
        {
            AddGlockReserveAmmo();
            return;
        }

        if (flagName == SupermarketFlag)
        {
            ApplySupermarketPickables(true);
            return;
        }

        if (flagName == CrossQuestAcceptedFlag)
        {
            ApplyCrossPickables(true);
            return;
        }

        // 药瓶交给 Sophia：从背包里移除 Pills。
        if (flagName == MedicineDeliveredFlag)
        {
            RemoveInventoryItem(pillsObjectName);
            return;
        }

        // 拿到武器箱钥匙：加入背包；十字架已交还，从背包移除。
        if (flagName == WeaponCrateKeyFlag)
        {
            RemoveInventoryItem(priestCrossObjectName);
            AddInventoryItem(
                weaponCrateKeyPrefab,
                weaponCrateKeyRotation
            );

            return;
        }

        // 交出手机：手机离开背包，并换上临时营地钥匙。
        if (flagName == PhoneGivenFlag)
        {
            RemoveInventoryItem(phoneObjectName);
            AddInventoryItem(
                armyCampKeyPrefab,
                armyCampKeyRotation
            );

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
        // 标记来自对话的 End Flag，此刻 DialogueRunner 仍在收尾
        // （endingDialogue 为 true 会拒绝上锁），而且收尾要等溶解
        // 播完，只等一帧不够。这里轮询到收尾结束再锁，超时才放弃。
        float lockDeadline = Time.realtimeSinceStartup + LockWaitTimeout;

        bool locked = false;

        while (locked == false &&
               Time.realtimeSinceStartup < lockDeadline)
        {
            locked = DialogueRunner.TryLockGameplay();

            if (locked == false)
            {
                yield return null;
            }
        }

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

        ReplaceAnimatorClip(sophiaAnimator, sophiaAnimation);

        // 全黑停留，给场景状态切换留一帧稳定时间。
        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        // 淡出到透明：开始淡出这一刻置位标记（留空则跳过）。
        SetBlackScreenFadeOutFlag();

        yield return FadeBlackScreen(1f, 0f, fadeOutDuration);

        if (locked)
        {
            DialogueRunner.UnlockGameplay();
        }
    }

    // 置位"黑屏开始淡出"标记；留空不置位，重复置位由 DialogueFlags 去重。
    private void SetBlackScreenFadeOutFlag()
    {
        if (string.IsNullOrWhiteSpace(blackScreenFadeOutFlag))
        {
            return;
        }

        DialogueFlags.SetFlag(blackScreenFadeOutFlag, true);
    }

    // 把 Sophia Animation 换进 Animator：Animator 里只有一个状态
    //（Base），直接 Play(clip.name) 无效——那需要状态名。
    // 这里用 AnimatorOverrideController 把原 Controller 的全部片段
    // 映射到新片段，再 Rebind 让唯一状态从第 0 帧重播新片段。
    private void ReplaceAnimatorClip(
        Animator animator,
        AnimationClip clip
    )
    {
        if (animator == null || clip == null)
        {
            return;
        }

        AnimatorOverrideController overrideController =
            animator.runtimeAnimatorController
                as AnimatorOverrideController;

        if (overrideController == null)
        {
            RuntimeAnimatorController baseController =
                animator.runtimeAnimatorController;

            if (baseController == null)
            {
                Debug.LogWarning(
                    "Chapter01Controller：Sophia 的 Animator 没有 " +
                        "AnimatorController，无法替换动画片段。",
                    this
                );

                return;
            }

            overrideController =
                new AnimatorOverrideController(baseController);

            animator.runtimeAnimatorController = overrideController;
        }

        RuntimeAnimatorController sourceController =
            overrideController.runtimeAnimatorController;

        if (sourceController == null)
        {
            return;
        }

        // 只有一个状态，把 Controller 里所有片段都换成目标片段。
        foreach (AnimationClip baseClip in
            sourceController.animationClips)
        {
            overrideController[baseClip] = clip;
        }

        animator.Rebind();
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

    // 给玩家当前手持的 Glock 补发备弹；找不到武器时告警跳过。
    private void AddGlockReserveAmmo()
    {
        WeaponController weapon = ResolveGlockWeapon();

        if (weapon == null)
        {
            Debug.LogWarning(
                "Chapter01Controller：找不到手持 Glock 的 " +
                    "WeaponController（Glock Object 未赋值或其上没有" +
                    "该组件），备弹补给未生效。",
                this
            );

            return;
        }

        // 上限由 WeaponController.HandleAmmo 每帧截断，这里直接累加
        weapon.currentCarriedAmmo += glockRefillAmount;
    }

    // 启用/禁用超市物资的拾取组件；列表为空时跳过。
    private void ApplySupermarketPickables(bool unlocked)
    {
        if (supermarketPickables == null)
        {
            return;
        }

        foreach (PickableItem pickable in supermarketPickables)
        {
            if (pickable == null)
            {
                continue;
            }

            pickable.enabled = unlocked;
        }
    }

    // 启用/禁用十字架的拾取组件；列表为空时跳过。
    private void ApplyCrossPickables(bool unlocked)
    {
        if (crossPickables == null)
        {
            return;
        }

        foreach (PickableItem pickable in crossPickables)
        {
            if (pickable == null)
            {
                continue;
            }

            pickable.enabled = unlocked;
        }
    }

    // 解析背包容器：优先用检查器赋值，否则按名称在场景里查找
    //（含未激活物体——InventoryContainer 初始就是禁用状态）。
    private Transform ResolveInventoryContainer()
    {
        if (inventoryContainer != null)
        {
            return inventoryContainer;
        }

        foreach (
            GameObject go in
            Object.FindObjectsOfType<GameObject>(true)
        )
        {
            if (go != null && go.name == "InventoryContainer")
            {
                inventoryContainer = go.transform;
                return inventoryContainer;
            }
        }

        Debug.LogError(
            "Chapter01Controller：场景里找不到 InventoryContainer，" +
                "背包物品的增减未生效。请检查 Inventory System 是否已实例化。",
            this
        );

        return null;
    }

    // 按名称销毁背包容器下的某个物品（药瓶交出、手机交出）。
    // 找不到时告警跳过，不影响其它逻辑。
    private void RemoveInventoryItem(string itemName)
    {
        Transform container = ResolveInventoryContainer();

        if (container == null || string.IsNullOrWhiteSpace(itemName))
        {
            return;
        }

        Transform target = container.Find(itemName);

        if (target == null)
        {
            Debug.LogWarning(
                $"Chapter01Controller：背包里找不到 '{itemName}'，" +
                    "无需移除（可能已被移除过）。",
                this
            );

            return;
        }

        // 先解除父子关系再销毁：销毁的唯一对象只有这一个，
        // 不依赖 Destroy 的延迟销毁时机。
        target.SetParent(null, false);
        Destroy(target.gameObject);
    }

    // 往背包容器里加入一个物品：与拾取的入包流程保持一致——
    // 换父物体、位置归零、按传入的初始旋转摆放、停用并关掉物理与碰撞，
    // 由 InventoryRadialView 打开背包时统一接管显示。
    // 初始旋转由调用方逐件传入：不同道具的模型主轴不同，
    // 展示朝向需要分别调整。
    private void AddInventoryItem(
        GameObject prefab,
        Vector3 initialRotation
    )
    {
        if (prefab == null)
        {
            Debug.LogWarning(
                "Chapter01Controller：背包物品的预制体未赋值，跳过添加。",
                this
            );

            return;
        }

        Transform container = ResolveInventoryContainer();

        if (container == null)
        {
            return;
        }

        GameObject instance = Instantiate(prefab, container);

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation =
            Quaternion.Euler(initialRotation);

        // 不覆写 localScale：与拾取入包一致，保留预制体自身的缩放。

        // 与 MoveItemToInventory 一致：入包后停用，
        // 由圆盘视图在展示时再统一激活。
        instance.SetActive(false);

        foreach (
            Rigidbody body in
            instance.GetComponentsInChildren<Rigidbody>(true)
        )
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        foreach (
            Collider collider in
            instance.GetComponentsInChildren<Collider>(true)
        )
        {
            collider.enabled = false;
        }
    }
}
