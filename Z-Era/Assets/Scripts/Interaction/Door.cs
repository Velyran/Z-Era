using TMPro;
using UnityEngine;

/// <summary>
/// 门：挂在门的根物体上（旋转轴即本物体的本地 Y 轴，枢轴摆在哪，
/// 门就绕哪里转）。
/// - 玩家在交互距离内、准星对准门时，在 HUD 提示文本上显示
///   "按 E 开门"（与 NPCDialogue 共用同一个 DialoguePromptText）；
/// - 按交互键后门绕本地竖直轴旋转到检查器配置的角度，
///   旋转方向（顺/逆时针）与最终角度都在检查器里设置；
/// - 门完全打开后可自动设置一个对话标记（DialogueFlags），留空则跳过，
///   供后续对话/任务条件判断；该标记已设置时重新载入场景，
///   门直接保持打开状态。
/// </summary>
public class Door : MonoBehaviour
{
    /// <summary>开门旋转方向（俯视门所在竖直轴看）。</summary>
    public enum OpenDirection
    {
        // 顺时针：绕本地 +Y 轴正角度旋转。
        Clockwise = 0,

        // 逆时针：绕本地 +Y 轴负角度旋转。
        CounterClockwise = 1,
    }

    [Header("交互")]

    [Tooltip("开门按键")]
    [SerializeField]
    private KeyCode interactKey = KeyCode.E;

    [Tooltip("准星对准门且距离小于该值时才能开门（米）")]
    [Min(0.1f)]
    [SerializeField]
    private float interactDistance = 2.5f;

    [Tooltip("交互提示文本（HUD 上的 DialoguePromptText），留空则不显示提示")]
    [SerializeField]
    private TMP_Text promptText;

    [Tooltip("提示文本格式，{0} 会被替换为按键名")]
    [SerializeField]
    private string promptFormat = "按 {0} 开门";

    [Tooltip("开门条件标记：只有该标记已设置时才能靠近按 E 开门；留空则无限制")]
    [SerializeField]
    private string requiredFlag;

    [Header("旋转")]

    [Tooltip("开门后相对初始朝向转过的角度（度）")]
    [Range(0f, 180f)]
    [SerializeField]
    private float openAngle = 90f;

    [Tooltip("旋转方向：Clockwise 顺时针 / CounterClockwise 逆时针（俯视门的竖直轴看）")]
    [SerializeField]
    private OpenDirection openDirection = OpenDirection.Clockwise;

    [Tooltip("从关到开所需时间（秒），越小开得越快")]
    [Min(0.05f)]
    [SerializeField]
    private float openDuration = 1f;

    [Header("标记")]

    [Tooltip("门完全打开后自动设置的标记（写入 DialogueFlags），留空则不设置")]
    [SerializeField]
    private string openFlag;

    private Camera cachedCamera;
    private PlayerController cachedPlayerController;

    // 本次开门动画的起点旋转（关门状态即初始朝向）。
    private Quaternion openStartRotation;

    // 开门动画已进行的时间。
    private float openElapsed;

    // 正在播放开门动画。
    private bool opening;

    // 已经完全打开：不再显示提示、不再响应按键。
    private bool opened;

    private void Awake()
    {
        // 开门动画以关门时的朝向为基准。
        openStartRotation = transform.localRotation;

        // 标记已设置（如重新载入场景）：直接保持打开状态。
        if (!string.IsNullOrWhiteSpace(openFlag) &&
            DialogueFlags.HasFlag(openFlag))
        {
            opened = true;
            ApplyRotation(GetSignedOpenAngle());
        }
    }

    private void OnDisable()
    {
        // 归还提示文本，避免禁用后仍占着文本、别人写不进去。
        ReleasePrompt();
    }

    private void Update()
    {
        UpdateOpening();
        UpdateInteraction();
    }

    // 开门动画：从起点旋转平滑转到目标角度。
    private void UpdateOpening()
    {
        if (!opening)
        {
            return;
        }

        openElapsed += Time.deltaTime;

        float progress = Mathf.Clamp01(openElapsed / openDuration);

        ApplyRotation(
            GetSignedOpenAngle() * Mathf.SmoothStep(0f, 1f, progress)
        );

        if (progress < 1f)
        {
            return;
        }

        // 动画结束：定格在最终角度并设置标记。
        opening = false;
        opened = true;

        ApplyRotation(GetSignedOpenAngle());

        if (!string.IsNullOrWhiteSpace(openFlag))
        {
            DialogueFlags.SetFlag(openFlag, true);
        }
    }

    private void UpdateInteraction()
    {
        if (opened || opening)
        {
            ReleasePrompt();
            return;
        }

        // 开门条件标记未设置：不显示提示、不响应按键。
        if (!IsOpenAllowed())
        {
            ReleasePrompt();
            return;
        }

        // 对话进行中、鼠标未锁定（背包打开等）、设置面板打开、
        // 玩家控制被外部锁定时不检测。
        if (IsInteractionBlocked())
        {
            ReleasePrompt();
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

        if (!CanInteract())
        {
            ReleasePrompt();
            return;
        }

        ShowPrompt();

        if (Input.GetKeyDown(interactKey))
        {
            BeginOpen();
        }
    }

    // 开门条件：requiredFlag 留空则无限制；填写时必须已设置才能开门。
    private bool IsOpenAllowed()
    {
        return string.IsNullOrWhiteSpace(requiredFlag) ||
               DialogueFlags.HasFlag(requiredFlag);
    }

    private bool IsInteractionBlocked()    {
        return
            (DialogueRunner.Instance != null &&
             DialogueRunner.Instance.CurrentState !=
                 DialogueRunner.State.Idle) ||
            Cursor.lockState != CursorLockMode.Locked ||
            SettingsUIRoot.IsOpen ||
            !IsPlayerControlEnabled();
    }

    // 准星射线命中的碰撞体必须属于本门（允许命中子物体的碰撞体），
    // 且在交互距离内。
    private bool CanInteract()
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

        Door owner = hit.collider.GetComponentInParent<Door>();

        return owner == this;
    }

    private void BeginOpen()
    {
        opening = true;
        openElapsed = 0f;
        openStartRotation = transform.localRotation;

        ReleasePrompt();
    }

    // 相对初始朝向的有符号旋转角度：顺时针为正，逆时针为负。
    private float GetSignedOpenAngle()
    {
        return openDirection == OpenDirection.Clockwise
            ? openAngle
            : -openAngle;
    }

    // 绕本物体本地竖直轴旋转指定角度（相对本次开门起点）。
    private void ApplyRotation(float signedAngle)
    {
        transform.localRotation =
            openStartRotation *
            Quaternion.AngleAxis(signedAngle, Vector3.up);
    }

    // 玩家控制是否可用：PlayerController 被外部系统禁用时不响应交互。
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

    private void ShowPrompt()
    {
        if (promptText == null)
        {
            return;
        }

        InteractionPromptRegistry.Acquire(promptText, this);

        promptText.text = string.Format(promptFormat, interactKey);
    }

    private void ReleasePrompt()
    {
        InteractionPromptRegistry.Release(promptText, this);
    }
}
