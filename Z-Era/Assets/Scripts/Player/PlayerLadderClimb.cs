using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家爬梯：挂在玩家物体上（与 PlayerController 同级）。
///
/// 触发逻辑（与需求一致）：
///   玩家前进碰到梯子后
///     · 继续按 W（前进）  → 进入攀爬，向上爬；
///     · 按 S（后退）      → 下梯子（脱离攀爬）。
///   攀爬中按 W 向上爬、按 S 向下爬，松键停在原地；
///   攀爬中只检测 W / S，按 A 或 D 不会移动也不会脱离；
///   爬到上端不会自动翻上，只是停在最高点；
///   爬到下端（落地）后按 A / S / D 或松开都会脱离，回到地面行走。
///
/// 进入攀爬不改变玩家朝向与视角：不强制转身、不锁相机，
/// 玩家保持撞上梯子那一刻的朝向。
/// PlayerController 的移动与重力通过 movementExternal 挂起，
/// 相机与射击不受影响。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerLadderClimb : MonoBehaviour
{
    [Header("按键")]
    [Tooltip("进入 / 继续攀爬（向上）的按键")]
    [SerializeField]
    private KeyCode climbKey = KeyCode.W;

    [Tooltip("下梯子（向下爬）的按键")]
    [SerializeField]
    private KeyCode descendKey = KeyCode.S;

    [Header("参数")]
    [Tooltip("碰到梯子后按键的宽容时间：在此期间按 W 都算攀爬意图")]
    [SerializeField, Min(0f)]
    private float inputGraceTime = 0.15f;

    [Header("调试")]
    [Tooltip("Scene 视图里显示攀爬状态")]
    [SerializeField]
    private bool drawDebug = true;

    private CharacterController characterController;
    private PlayerController playerController;

    // 当前攀爬的梯子，null 表示未攀爬。
    private Ladder activeLadder;

    // 最近接触到的梯子（用于 W 键判定），离开触发区后清空。
    private Ladder touchedLadder;
    private float lastTouchTime = float.NegativeInfinity;

    // 攀爬期间需要忽略碰撞的梯子实体碰撞体，退出时恢复。
    private readonly List<Collider> ignoredColliders =
        new List<Collider>();

    /// <summary>是否正在攀爬。</summary>
    public bool IsClimbing => activeLadder != null;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerController = GetComponent<PlayerController>();
    }

    private void OnDisable()
    {
        // 组件被停用（剧情锁定、对话等）时强制脱离攀爬，
        // 否则会残留 movementExternal 与忽略碰撞状态。
        if (activeLadder != null)
        {
            ExitClimb(transform.position);
        }
    }

    private void Update()
    {
        if (activeLadder == null)
        {
            UpdateEnter();
            return;
        }

        UpdateClimbing();
    }

    // 未攀爬：判定是否要进入。
    private void UpdateEnter()
    {
        if (!Input.GetKey(climbKey))
        {
            return;
        }

        if (Time.time - lastTouchTime > inputGraceTime)
        {
            return;
        }

        if (touchedLadder == null)
        {
            return;
        }

        EnterClimb(touchedLadder);
    }

    private void UpdateClimbing()
    {
        Ladder ladder = activeLadder;

        float feetHeight = ladder.HeightAlong(FeetPosition());

        // 已爬到抓握区域顶端之外（爬到上方平台）：脱离。
        // 放在最前面且不看按键：哪怕一直按着 W 也会脱离，
        // 不会继续往上升；脱离后 W 交还给 PlayerController，
        // 恢复成正常的前进。
        //
        // 用脚底高度判定：玩家是胶囊体，上升时头顶会被平台底面挡住，
        // 全身包围盒永远差一个 capsule height 才能越过区域顶端，
        // 所以这里比的是脚底，不是包围盒。
        if (ladder.HasPlayerExited(feetHeight))
        {
            // 配置了顶部锚点就传送到锚点（平台表面），
            // 否则原地脱离。传送是必须的：脚底最高只能升到
            // 平台表面下方一个 capsule height 处，不传送就上不去。
            Vector3 exit = ladder.TopExitPosition;

            ExitClimb(
                float.IsPositiveInfinity(exit.x)
                    ? transform.position
                    : exit
            );

            return;
        }

        // 梯子上只检测 W / S：A 和 D 在攀爬状态下被忽略。
        bool climbing = Input.GetKey(climbKey);
        bool descending = Input.GetKey(descendKey);

        if (climbing)
        {
            // 这里不判落地：玩家在梯底按 W 必须能重新往上爬，
            // 否则会因为贴着地面被卡在原地动不了。
            // 上升幅度按极限高度收口，到顶就停住不动，
            // 由上面的 HasPlayerExited 负责判定脱离。
            float step = Mathf.Min(
                ladder.ClimbSpeed * Time.deltaTime,
                Mathf.Max(0f, ladder.CeilingHeight - feetHeight)
            );

            if (step > 0f)
            {
                characterController.Move(
                    ladder.UpDirection * step
                );
            }

            return;
        }

        // 已落到地面：按 A / S / D 或松开都脱离，回到地面行走。
        if (IsAtGround())
        {
            ExitClimb(transform.position);
            return;
        }

        // S：还没落地就继续往下爬。
        if (descending)
        {
            characterController.Move(
                -ladder.UpDirection *
                (ladder.ClimbSpeed * Time.deltaTime)
            );
        }

        // 其余情况（松键、按 A / D）：悬在梯上不动，不脱离。
    }

    // 玩家脚底的世界坐标。
    // CharacterController 的胶囊中心就是 transform.position，
    // 脚底在中心下方半个身高处。
    private Vector3 FeetPosition() =>
        transform.position +
        Vector3.down * (characterController.height * 0.5f -
            characterController.skinWidth);

    // 是否已经落到梯子下端的地面。
    // isGrounded 是主判据：玩家胶囊中心永远到不了触发区域下沿
    // （两者差一个胶囊半高），只比较高度会永远判不到落地。
    // height <= 0 作为兜底，兼容下端锚点高于地面的配置。
    private bool IsAtGround()
    {
        if (activeLadder == null)
        {
            return false;
        }

        return characterController.isGrounded ||
            activeLadder.HeightAlong(transform.position) <= 0f;
    }

    private void EnterClimb(Ladder ladder)
    {
        activeLadder = ladder;

        // 挂起 PlayerController 的移动与重力，避免与攀爬抢位置。
        if (playerController != null)
        {
            playerController.movementExternal = true;
        }

        // 攀爬中忽略梯子实体碰撞，否则沿梯轴上升会被自己挡住。
        ignoredColliders.Clear();

        foreach (Collider collider in
            ladder.GetComponentsInChildren<Collider>(true))
        {
            if (collider.isTrigger ||
                collider.enabled == false)
            {
                continue;
            }

            Physics.IgnoreCollision(
                characterController,
                collider,
                true
            );

            ignoredColliders.Add(collider);
        }

        // 进入攀爬不改变玩家朝向：不强制转身，
        // 玩家保持撞上梯子那一刻的朝向，避免视角突然被拽走。

        ClampPosition();
    }

    private void ExitClimb(Vector3 exitPosition)
    {
        Ladder ladder = activeLadder;

        activeLadder = null;

        // 恢复与梯子的碰撞。
        foreach (Collider collider in ignoredColliders)
        {
            if (collider != null)
            {
                Physics.IgnoreCollision(
                    characterController,
                    collider,
                    false
                );
            }
        }

        ignoredColliders.Clear();

        // 解除 PlayerController 的挂起。
        if (playerController != null)
        {
            playerController.movementExternal = false;
        }

        transform.position = exitPosition;

        // 退出时清掉接触状态，避免退出瞬间又满足 W 判定被拉回攀爬。
        touchedLadder = null;
        lastTouchTime = float.NegativeInfinity;
    }

    // 把玩家限制在梯子的攀爬范围内（只限制沿梯轴的高度）。
    private void ClampPosition()
    {
        Ladder ladder = activeLadder;

        if (ladder == null)
        {
            return;
        }

        float height = ladder.HeightAlong(transform.position);

        // TopHeight <= 0 说明锚点没摆好，此时不钳制，
        // 避免把玩家瞬移到梯底。
        if (ladder.TopHeight <= 0f)
        {
            return;
        }

        float clamped =
            Mathf.Clamp(height, 0f, ladder.TopHeight);

        if (!Mathf.Approximately(clamped, height))
        {
            transform.position +=
                ladder.UpDirection * (clamped - height);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Ladder ladder = other.GetComponentInParent<Ladder>();

        if (ladder == null)
        {
            return;
        }

        touchedLadder = ladder;
        lastTouchTime = Time.time;
    }

    private void OnTriggerExit(Collider other)
    {
        Ladder ladder = other.GetComponentInParent<Ladder>();

        if (ladder == null)
        {
            return;
        }

        // 离开当前接触的梯子才清；仍在攀爬的不受影响。
        if (touchedLadder == ladder && activeLadder == null)
        {
            touchedLadder = null;
            lastTouchTime = float.NegativeInfinity;
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawDebug || activeLadder == null)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
}
