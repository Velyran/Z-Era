using UnityEngine;

/// <summary>
/// 任务指引标志：把世界空间锚点投影成屏幕坐标，
/// 驱动一个 Screen Space - Overlay Canvas 下的 UI 元素跟随。
/// 因此标志像素大小恒定（不随距离变化）、永远正对主相机，
/// 且不会被场景物体遮挡。
/// 挂在标志的根物体上，由任务脚本 SetActive 控制显隐。
/// </summary>
public class TaskMarker : MonoBehaviour
{
    [Tooltip("跟随锚点移动的 UI 元素（Overlay Canvas 下的 Image）")]
    [SerializeField]
    private RectTransform markerRect;

    [Tooltip("锚点在世界空间的高度偏移（米）")]
    [SerializeField]
    private float worldHeightOffset = 1f;

    [Tooltip("投影用相机，留空则用 Camera.main")]
    [SerializeField]
    private Camera projectionCamera;

    // 世界空间锚点：由 TaskCameraGuidance 在显示前注入，
    // 同一套标志可被不同演出复用到不同位置。
    private Transform anchor;

    /// <summary>
    /// 设置标志跟随的世界锚点。必须在激活标志前调用。
    /// </summary>
    public void SetAnchor(Transform value)
    {
        anchor = value;
    }

    private void Awake()
    {
        if (markerRect == null)
        {
            Debug.LogWarning(
                "TaskMarker：没有指定 Marker Rect，标志不会显示。",
                this
            );

            return;
        }

        Canvas canvas = markerRect.GetComponentInParent<Canvas>();

        if (canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            Debug.LogWarning(
                "TaskMarker：Marker Rect 所在的 Canvas 不是 " +
                    "Screen Space - Overlay，按屏幕坐标定位会偏移。",
                this
            );
        }
    }

    private void LateUpdate()
    {
        if (markerRect == null)
        {
            return;
        }

        if (anchor == null)
        {
            // 未注入锚点：保持隐藏，避免停在上一处位置。
            markerRect.localScale = Vector3.zero;

            return;
        }

        if (projectionCamera == null)
        {
            projectionCamera = Camera.main;

            if (projectionCamera == null)
            {
                return;
            }
        }

        Vector3 worldPoint =
            anchor.position + Vector3.up * worldHeightOffset;

        Vector3 screenPoint =
            projectionCamera.WorldToScreenPoint(worldPoint);

        // 锚点跑到相机背后时隐藏，否则标志会镜像出现在屏幕上。
        // 用缩放而非 SetActive：本组件与 Marker Rect 经常是同一物体，
        // 停用会把组件一起停掉、再也显示不出来。
        bool visible = screenPoint.z > 0f;

        Vector3 scale = visible ? Vector3.one : Vector3.zero;

        if (markerRect.localScale != scale)
        {
            markerRect.localScale = scale;
        }

        if (visible)
        {
            markerRect.position = screenPoint;
        }
    }
}
