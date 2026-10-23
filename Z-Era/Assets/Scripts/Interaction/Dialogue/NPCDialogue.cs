using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// NPC 对话触发器，挂在人物根物体上。触发方式在检查器二选一：
/// - 对准目标按 E：准星指向本 NPC 且在交互距离内、站在朝向
///   扇形内，按交互键触发（原有行为）；
/// - 位于触发器内自动触发：进入本物体上勾选 is Trigger 的碰撞体
///   即直接开始对话，无需按键、无需对准、不受扇形限制，
///   进入物体按 Tag 过滤，同一次停留只触发一次；
///   按键提示文本仅对准模式显示。
/// 支持后续对话样式：
/// - 默认：每次对话使用默认资产，内容相同；
/// - playOnce：只能完整对话一次；
/// - talkedFlag：对话结束后自动设置标记，
///   配合条件对话列表可在下次交互时切换为新内容；
/// - 条件对话：按标记决定本次使用哪个对话资产，
///   标记不满足且无默认资产时无法对话。
/// </summary>
public class NPCDialogue : MonoBehaviour
{
    [Serializable]
    public class ConditionalDialogue
    {
        [Tooltip("这里列出的标记全部已设置时，才使用该对话资产")]
        public string[] requiredFlags;

        [Tooltip("条件满足时使用的对话资产")]
        public DialogueAsset dialogue;
    }

    [Header("对话")]

    [Tooltip("默认对话资产：条件对话都不满足时使用，留空则条件不满足时无法对话")]
    [SerializeField]
    private DialogueAsset dialogue;

    [Tooltip("按顺序检查的条件对话：第一个标记全部满足的条目优先生效")]
    [SerializeField]
    private List<ConditionalDialogue> conditionalDialogues =
        new List<ConditionalDialogue>();

    [Tooltip("对话完整结束后自动设置的标记，留空则不设置（配合条件对话实现后续不同内容）")]
    [SerializeField]
    private string talkedFlag;

    [Tooltip("勾选后该 NPC 只能完整对话一次，之后不再提供交互")]
    [SerializeField]
    private bool playOnce;

    [Tooltip("开始对话的交互按键（仅\"对准目标按 E\"模式使用；触发器模式自动触发，不用按键）")]
    [SerializeField]
    private KeyCode interactKey = KeyCode.E;

    [Tooltip("准星指向本 NPC 且距离小于该值时才能开始对话（米），仅对准模式使用")]
    [Min(0.1f)]
    [SerializeField]
    private float interactDistance = 2.5f;

    /// <summary>对话触发方式。</summary>
    public enum TriggerMode
    {
        // 准星指向本 NPC 且在交互距离内按 E（原有行为）。
        AimAtNpc = 0,

        // 进入本物体的触发器即自动开始对话，无需按键与对准。
        InsideTrigger = 1,
    }

    [Header("触发方式")]

    [Tooltip("对准目标按 E：准星指向本 NPC、在交互距离内、站在朝向" +
        "扇形内，按交互键触发（原行为，需要碰撞体）；位于触发器内自动" +
        "触发：进入勾选 is Trigger 的碰撞体即直接开始对话，无需按键与" +
        "对准、不受扇形限制，每次进入触发一次")]
    [SerializeField]
    private TriggerMode triggerMode = TriggerMode.AimAtNpc;

    [Tooltip("进入触发器的物体必须具有的 Tag，留空则不限（仅\"位于触发器内\"模式使用，玩家根物体通常为 Player）")]
    [SerializeField]
    private string triggerTag = "Player";

    [Header("提示")]

    [Tooltip("交互提示文本，留空则不显示提示")]
    [SerializeField]
    private TMP_Text promptText;

    [Tooltip("提示文本格式，{0} 会被替换为按键名（仅对准模式显示；触发器模式自动触发、不出按键提示）")]
    [SerializeField]
    private string promptFormat = "按 {0} 对话";

    [Tooltip("NPC 尚未介绍自己时显示的提示文本")]
    [SerializeField]
    private string unknownPrompt = "？？？";

    [Tooltip("播放到该对话节点 ID 时，读取节点 Speaker Name 并更新提示")]
    [SerializeField]
    private string nameRevealNodeId;

    [Header("朝向限制")]

    [Tooltip("是否要求玩家站在 NPC 朝向的指定角度范围内才能对话（防止绕到 NPC 背后对话）；仅\"对准目标按 E\"模式生效")]
    [SerializeField]
    private bool requireForwardZone = true;

    [Tooltip("允许对话的半角（度）：玩家相对 NPC 正前方偏离不超过该角度时可对话，180 等于不限制（仅对准模式生效）")]
    [Range(0f, 180f)]
    [SerializeField]
    private float interactHalfAngle = 90f;

    private Camera cachedCamera;
    private DialogueRunner dialogueRunner;

    // 玩家控制组件：判断"玩家是否被外部系统锁定"用
    // （如第一章睁眼开场的全锁定期间不允许开对话）。
    private PlayerController cachedPlayerController;

    // 已发起对话、等待其结束（结束后设置标记）。
    private bool awaitingEnd;

    // 当前等待结束的对话资产，用于对话结束后读取 speakerName。
    private DialogueAsset awaitingDialogueAsset;

    // 是否已经完整对话过一次。
    private bool hasTalked;

    // 是否已经从对话资产中取得 NPC 名字。
    private bool hasIntroduced;

    // 触发器模式：当前位于触发器内、且匹配 Tag 的碰撞体。
    // 用集合而非计数，玩家身上多个碰撞体进出也不会错乱。
    private readonly HashSet<Collider> insideColliders =
        new HashSet<Collider>();

    // 触发器模式：本次进入是否已自动触发过对话。同一次停留只
    // 触发一次（否则对话一结束立刻无限重开），玩家整体离开
    // 范围后复位，再次进入会重新触发。
    private bool firedThisVisit;

    private void Awake()
    {
        // 重新加载场景后，根据 talkedFlag 恢复已介绍状态。
        if (!string.IsNullOrWhiteSpace(talkedFlag) &&
            DialogueFlags.HasFlag(talkedFlag))
        {
            hasIntroduced = true;
        }

        // 触发器模式却没有触发器碰撞体：永远进不了范围，
        // 这里明确告警而不是无声失效
        if (triggerMode == TriggerMode.InsideTrigger &&
            !HasTriggerCollider())
        {
            Debug.LogWarning(
                "NPCDialogue：触发方式为\"位于触发器内\"，但本物体及其" +
                    "子物体没有勾选 is Trigger 的碰撞体，无法进入触发范围。",
                this
            );
        }
    }

    // 本物体及其子物体上是否存在勾选 is Trigger 的碰撞体。
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

    private void OnEnable()
    {
        EnsureDialogueRunnerSubscription();
    }

    private void OnDisable()
    {
        if (dialogueRunner != null)
        {
            dialogueRunner.DialogueNodeShown -= OnDialogueNodeShown;
            dialogueRunner = null;
        }

        // 离开场景/禁用后重新启用：进出事件不会再补发，
        // 这里清空，避免残留"在范围内"的假状态
        insideColliders.Clear();
    }

    // 触发器模式：记录进入本物体触发器（is Trigger）的匹配 Tag 碰撞体。
    // 无论当前模式如何都记录，检查器里运行时切换模式立即生效。
    private void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(triggerTag) &&
            !other.CompareTag(triggerTag))
        {
            return;
        }

        insideColliders.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        insideColliders.Remove(other);

        // 玩家整体离开范围：复位"本次进入已触发"，
        // 下次进入会重新自动触发
        if (insideColliders.Count == 0)
        {
            firedThisVisit = false;
        }
    }

    private void Update()
    {
        EnsureDialogueRunnerSubscription();

        // 已发起的对话结束后：记录"聊过"并设置 talkedFlag。
        // 结束序列期间状态已是 Idle，标记会立即生效。
        if (awaitingEnd &&
            (DialogueRunner.Instance == null ||
             DialogueRunner.Instance.CurrentState ==
                 DialogueRunner.State.Idle))
        {
            awaitingEnd = false;
            hasTalked = true;

            if (!string.IsNullOrWhiteSpace(talkedFlag))
            {
                DialogueFlags.SetFlag(talkedFlag, true);
            }

            awaitingDialogueAsset = null;
        }

        bool talkAvailable = IsTalkAvailable();

        // 无法对话、对话进行中、鼠标未锁定（背包打开等）、
        // 玩家控制被外部锁定（如睁眼开场）时不检测。
        if (!talkAvailable ||
            (DialogueRunner.Instance != null &&
             DialogueRunner.Instance.CurrentState !=
                 DialogueRunner.State.Idle) ||
            Cursor.lockState != CursorLockMode.Locked ||
            !IsPlayerControlEnabled())
        {
            UpdatePrompt(false);
            return;
        }

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;

            if (cachedCamera == null)
            {
                return;
            }
        }

        bool canInteract = CanInteract();

        // 设置面板打开期间禁止触发对话（避免界面叠加）。
        // 对准模式：扇形内按交互键触发（原有行为）；
        // 触发器模式：进入范围即自动触发，同一次停留仅一次
        bool shouldStart;

        if (triggerMode == TriggerMode.InsideTrigger)
        {
            shouldStart =
                canInteract &&
                !firedThisVisit &&
                !SettingsUIRoot.IsOpen;
        }
        else
        {
            shouldStart =
                canInteract &&
                !SettingsUIRoot.IsOpen &&
                Input.GetKeyDown(interactKey);
        }

        if (shouldStart)
        {
            DialogueAsset asset = SelectDialogue();

            if (asset != null)
            {
                awaitingEnd = true;
                awaitingDialogueAsset = asset;
                DialogueRunner.StartDialogue(asset);

                // 触发器模式的"本次进入已触发"标记，
                // 玩家整体离开范围时由 OnTriggerExit 复位
                if (triggerMode == TriggerMode.InsideTrigger)
                {
                    firedThisVisit = true;
                }
            }

            UpdatePrompt(false);
            return;
        }

        // 按键提示仅对准模式显示（触发器模式自动触发，无需按键提示）
        UpdatePrompt(
            triggerMode == TriggerMode.AimAtNpc && canInteract
        );
    }

    // 当前是否可对话：没有 playOnce 限制，且有可用资产。
    private bool IsTalkAvailable()
    {
        if (playOnce && hasTalked)
        {
            return false;
        }

        return SelectDialogue() != null;
    }

    // 选择本次使用的对话资产：条件列表从上到下，
    // 第一个标记全部满足的条目优先生效，否则用默认资产。
    private DialogueAsset SelectDialogue()
    {
        foreach (
            ConditionalDialogue conditional in
                conditionalDialogues
        )
        {
            if (conditional == null || conditional.dialogue == null)
            {
                continue;
            }

            bool allSet = true;

            if (conditional.requiredFlags != null)
            {
                foreach (string flag in conditional.requiredFlags)
                {
                    if (!DialogueFlags.HasFlag(flag))
                    {
                        allSet = false;
                        break;
                    }
                }
            }

            if (allSet)
            {
                return conditional.dialogue;
            }
        }

        return dialogue;
    }

    private void EnsureDialogueRunnerSubscription()
    {
        if (dialogueRunner != null ||
            DialogueRunner.Instance == null)
        {
            return;
        }

        dialogueRunner = DialogueRunner.Instance;
        dialogueRunner.DialogueNodeShown += OnDialogueNodeShown;
    }

    private void OnDialogueNodeShown(
        DialogueAsset sourceDialogue,
        DialogueAsset.DialogueNode node
    )
    {
        if (hasIntroduced ||
            sourceDialogue != awaitingDialogueAsset ||
            node == null ||
            string.IsNullOrWhiteSpace(nameRevealNodeId) ||
            node.nodeId != nameRevealNodeId ||
            string.IsNullOrWhiteSpace(node.speakerName))
        {
            return;
        }

        promptFormat = node.speakerName;
        hasIntroduced = true;
    }

    // 玩家控制是否可用：PlayerController 被外部系统禁用时
    //（如第一章睁眼开场的全锁定）不触发对话——否则对话开在
    // 锁定期间，随后那个系统的解锁会把对话自己禁用的组件一并
    // 解掉，表现为"对话进行中玩家还能动"。
    // 场景里没有玩家控制器时不限制（纯 UI 场景）。
    private bool IsPlayerControlEnabled()
    {
        if (cachedPlayerController == null)
        {
            cachedPlayerController =
                FindObjectOfType<PlayerController>();
        }

        return cachedPlayerController == null ||
               cachedPlayerController.enabled;
    }

    // 当前是否可交互：对准模式 = 准星射线 + 朝向扇形；
    // 触发器模式 = 位于触发器范围内即可（不受扇形限制）。
    private bool CanInteract()
    {
        if (triggerMode == TriggerMode.InsideTrigger)
        {
            // 触发器模式：匹配 Tag 的玩家碰撞体在触发器内即可，
            // 不做准星射线；顺带清理已销毁碰撞体的残留。
            insideColliders.RemoveWhere(collider => collider == null);

            // 清理后范围已空（如玩家碰撞体被销毁、没有补发 Exit）：
            // 同步复位触发标记，保持"集合为空 ⇔ 可重新触发"一致
            if (insideColliders.Count == 0)
            {
                firedThisVisit = false;
                return false;
            }

            return true;
        }

        // 对准模式（Aim At Npc）：准星射线命中碰撞体，
        // 再过朝向扇形限制
        return CanInteractByAim() && IsInForwardZone();
    }

    // 原有行为：准星射线命中的碰撞体必须属于本 NPC
    // （允许命中子物体的碰撞体），且在交互距离内。
    private bool CanInteractByAim()
    {
        Ray ray = cachedCamera.ScreenPointToRay(
            new Vector3(
                Screen.width * 0.5f,
                Screen.height * 0.5f,
                0f
            )
        );

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                interactDistance
            ))
        {
            return false;
        }

        NPCDialogue owner =
            hit.collider.GetComponentInParent<NPCDialogue>();

        return owner == this;
    }

    // 朝向限制（仅对准模式）：玩家必须站在 NPC 正前方
    // interactHalfAngle 度的扇形范围内。水平面计算，忽略高度差，
    // 避免玩家站得稍高/稍低时被误判到区域外。
    private bool IsInForwardZone()
    {
        if (!requireForwardZone || interactHalfAngle >= 180f)
        {
            return true;
        }

        Vector3 npcForward = transform.forward;
        Vector3 toPlayer =
            cachedCamera.transform.position - transform.position;

        npcForward.y = 0f;
        toPlayer.y = 0f;

        // 水平投影接近零（玩家在正上/正下方）时方向未定义，
        // 按不通过处理。
        if (toPlayer.sqrMagnitude < 0.0001f ||
            Vector3.Angle(npcForward, toPlayer) >
                interactHalfAngle)
        {
            return false;
        }

        return true;
    }

    // 在 Scene 视图可视化对话朝向区域：半透明扇面 + 边界线 +
    // 正方向指示线，半径取对话交互距离，仅选中本物体时绘制。
    // 扇形仅对准模式有意义，触发器模式不绘制。编辑器专用。
    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!requireForwardZone ||
            triggerMode == TriggerMode.InsideTrigger)
        {
            return;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;

        // 根节点朝向垂直于水平面时退化，兜底用世界前方
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();

        float radius = Mathf.Max(0.5f, interactDistance);
        Vector3 center = transform.position;

        Quaternion leftEdge =
            Quaternion.AngleAxis(-interactHalfAngle, Vector3.up);

        Handles.color = new Color(1f, 0.92f, 0.35f, 0.15f);
        Handles.DrawSolidArc(
            center,
            Vector3.up,
            leftEdge * forward,
            interactHalfAngle * 2f,
            radius
        );

        Handles.color = new Color(1f, 0.92f, 0.35f, 0.9f);
        Handles.DrawWireArc(
            center,
            Vector3.up,
            leftEdge * forward,
            interactHalfAngle * 2f,
            radius
        );

        Gizmos.color = new Color(1f, 0.92f, 0.35f, 0.9f);
        Gizmos.DrawLine(
            center,
            center + leftEdge * forward * radius
        );
        Gizmos.DrawLine(
            center,
            center + Quaternion.AngleAxis(
                interactHalfAngle, Vector3.up) * forward * radius
        );

        // 正方向指示线略长一截，便于辨认扇形朝向
        Gizmos.color = Color.white;
        Gizmos.DrawLine(
            center,
            center + forward * radius * 1.15f
        );
#endif
    }

    private void UpdatePrompt(
        bool canInteract
    )
    {
        if (promptText == null)
        {
            return;
        }

        if (!canInteract)
        {
            promptText.text = string.Empty;
            return;
        }

        string activePrompt = hasIntroduced
            ? promptFormat
            : unknownPrompt;

        promptText.text = string.Format(
            activePrompt,
            interactKey
        );
    }
}
