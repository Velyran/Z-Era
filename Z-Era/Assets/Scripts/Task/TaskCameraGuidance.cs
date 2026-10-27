using System.Collections;
using UnityEngine;

/// <summary>
/// 任务运镜与指引：Required Flag 置位时——
/// - 相机与目标位姿都配好了：禁用玩家控制器、锁操作并隐藏 HUD，
///   相机缓入缓出移到目标位姿，停留指定时长后按同样方式返回，
///   回到初始位姿后再在 Marker Anchor 处显示指引标志；
/// - 两者没有配齐：直接在 Marker Anchor 处显示指引标志。
/// 标志出现后可选地置位一个标记，供后续流程判定。
/// 挂在常驻空物体上统一管理；多段演出给同一个空物体
/// 添加多个本组件即可，各自独立配置锚点与图标。
/// </summary>
public class TaskCameraGuidance : MonoBehaviour
{
    [Header("触发")]

    [Tooltip("必填：该标记为 true 时开始演出，留空则本组件永不触发")]
    [SerializeField]
    private string requiredFlag;

    [Header("相机（两项都拖入才会运镜）")]

    [Tooltip("要移动的相机，一般拖主相机")]
    [SerializeField]
    private Camera targetCamera;

    [Tooltip("目标位姿锚点：相机移动到该物体的位置与旋转")]
    [SerializeField]
    private Transform cameraTarget;

    [Header("运镜节奏")]

    [Tooltip("移动到目标位姿的时长（秒）")]
    [Min(0f)]
    [SerializeField]
    private float moveDuration = 1.5f;

    [Tooltip("在目标位姿停留的时长（秒）")]
    [Min(0f)]
    [SerializeField]
    private float holdDuration = 2f;

    [Tooltip("返回初始位姿的时长（秒）")]
    [Min(0f)]
    [SerializeField]
    private float returnDuration = 1.5f;

    [Tooltip("缓入缓出曲线：横轴 0~1 为线性进度，纵轴为实际应用进度；默认两端平缓")]
    [SerializeField]
    private AnimationCurve easing =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("任务指引")]

    [Tooltip("演出结束时显示的指引标志，留空则不显示")]
    [SerializeField]
    private TaskMarker marker;

    [Tooltip("指引标志的世界锚点：标志出现在该点上方，留空则无法显示")]
    [SerializeField]
    private Transform markerAnchor;

    [Tooltip("出现指引标志后置为 true 的标记，留空则不设置")]
    [SerializeField]
    private string markerFlag;

    [Header("锁定")]

    [Tooltip("运镜期间整体禁用的玩家对象（拖 Player 物体）；留空则不额外禁用")]
    [SerializeField]
    private GameObject playerObject;

    // 已触发过：标记置位后只演一次。
    private bool fired;

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(requiredFlag))
        {
            Debug.LogWarning(
                "TaskCameraGuidance：Required Flag 为空，本组件不会触发。",
                this
            );
        }

        if (marker != null)
        {
            marker.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        DialogueFlags.FlagSet += OnFlagSet;

        // 场景重进时标记可能早已置位，补一次。
        if (!string.IsNullOrWhiteSpace(requiredFlag) &&
            DialogueFlags.HasFlag(requiredFlag))
        {
            Begin();
        }
    }

    private void OnDisable()
    {
        DialogueFlags.FlagSet -= OnFlagSet;
    }

    private void OnFlagSet(string flagName)
    {
        if (flagName != requiredFlag)
        {
            return;
        }

        Begin();
    }

    private void Begin()
    {
        if (fired)
        {
            return;
        }

        fired = true;

        bool hasCamera = targetCamera != null && cameraTarget != null;

        if (!hasCamera)
        {
            // 只填了一个属于配置漏项，直接显示标志并告警。
            if (targetCamera != null || cameraTarget != null)
            {
                Debug.LogWarning(
                    "TaskCameraGuidance：Camera 与 Camera Target " +
                        "需要同时配置才会运镜，将直接显示指引标志。",
                    this
                );
            }

            ShowMarker();
            return;
        }

        // 相机若挂在玩家层级下，禁用玩家会连相机一起停掉，
        // 运镜期间就看不到画面了。
        if (playerObject != null &&
            targetCamera.transform.IsChildOf(playerObject.transform))
        {
            Debug.LogWarning(
                "TaskCameraGuidance：要运镜的相机在 Player 层级下，" +
                    "禁用 Player 后运镜期间将看不到画面。",
                this
            );
        }

        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        Transform cameraTransform = targetCamera.transform;

        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;

        // 复用对话的"锁操作 + 隐藏 HUD"逻辑，保证运镜期间
        // 玩家输入与相机后坐力不会覆盖演出。
        bool locked = DialogueRunner.TryLockGameplay();

        if (!locked)
        {
            Debug.LogWarning(
                "TaskCameraGuidance：没有可用的 DialogueRunner " +
                    "或对话正忙，运镜期间玩家操作不会被锁定。",
                this
            );
        }

        // 上锁之后再整体禁用玩家对象：相机若在玩家层级下，
        // 需要靠上面的锁定与下面的恢复把状态对齐。
        bool playerWasActive =
            playerObject != null && playerObject.activeSelf;

        if (playerObject != null)
        {
            playerObject.SetActive(false);
        }

        yield return TweenPose(
            cameraTransform,
            startPosition,
            startRotation,
            cameraTarget.position,
            cameraTarget.rotation,
            moveDuration
        );

        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        yield return TweenPose(
            cameraTransform,
            cameraTarget.position,
            cameraTarget.rotation,
            startPosition,
            startRotation,
            returnDuration
        );

        if (playerObject != null)
        {
            playerObject.SetActive(playerWasActive);
        }

        // 先恢复操作与 HUD 再显示标志：标志挂在 HUD Canvas 下，
        // 画布仍隐藏时显示会被一起藏起来。
        if (locked)
        {
            DialogueRunner.UnlockGameplay();
        }

        ShowMarker();
    }

    // 按时长把相机从 from 缓动到 to，进度经 easing 曲线映射。
    private IEnumerator TweenPose(
        Transform cameraTransform,
        Vector3 fromPosition,
        Quaternion fromRotation,
        Vector3 toPosition,
        Quaternion toRotation,
        float duration
    )
    {
        if (duration <= 0f)
        {
            cameraTransform.SetPositionAndRotation(
                toPosition,
                toRotation
            );

            yield break;
        }

        for (float elapsed = 0f;
             elapsed < duration;
             elapsed += Time.deltaTime)
        {
            float progress = easing.Evaluate(elapsed / duration);

            cameraTransform.SetPositionAndRotation(
                Vector3.LerpUnclamped(
                    fromPosition,
                    toPosition,
                    progress
                ),
                Quaternion.SlerpUnclamped(
                    fromRotation,
                    toRotation,
                    progress
                )
            );

            yield return null;
        }

        cameraTransform.SetPositionAndRotation(toPosition, toRotation);
    }

    private void ShowMarker()
    {
        if (marker == null)
        {
            return;
        }

        if (markerAnchor == null)
        {
            Debug.LogWarning(
                "TaskCameraGuidance：没有配置 Marker Anchor，" +
                    "指引标志无法定位，本次不显示。",
                this
            );

            return;
        }

        // 锚点由控制器注入，同一个标志可被不同演出复用到不同位置。
        marker.SetAnchor(markerAnchor);
        marker.gameObject.SetActive(true);

        // 标志出现之后才置位，供后续任务/对话条件判定。
        if (!string.IsNullOrWhiteSpace(markerFlag))
        {
            DialogueFlags.SetFlag(markerFlag, true);
        }
    }
}
