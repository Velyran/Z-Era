using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 任务指引标志：把世界空间锚点投影成屏幕坐标，
/// 驱动一个 Screen Space - Overlay Canvas 下的 UI 元素跟随。
/// 因此标志像素大小恒定（不随距离变化）、永远正对主相机，
/// 且不会被场景物体遮挡。
/// 当锚点在玩家视角外（含相机背后）时，标志会切换为箭头图标，
/// 放置在屏幕边缘并旋转指向目标方向，提示玩家该往哪里看。
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

    [Header("屏幕外方向指示")]

    [Tooltip("开启后，锚点在屏幕外时把标志放到屏幕边缘并旋转指向目标")]
    [SerializeField]
    private bool showOffScreenIndicator = true;

    [Tooltip("边缘指示器距屏幕边缘的内缩距离（像素）")]
    [SerializeField]
    [Min(0f)]
    private float edgePadding = 40f;

    [Tooltip("屏幕外时显示的箭头图标；留空则不切换图标")]
    [SerializeField]
    private Sprite offScreenArrowSprite;

    [Tooltip("箭头图标的旋转角度偏移（度），默认 0 表示箭头朝上时指向目标上方")]
    [SerializeField]
    private float indicatorRotationOffset = 0f;

    // 世界空间锚点：由 TaskCameraGuidance 在显示前注入，
    // 同一套标志可被不同演出复用到不同位置。
    private Transform anchor;

    // Marker 上的 Image 组件，用于在屏幕内/外切换图标。
    private Image markerImage;

    // 屏幕内显示的原始图标，屏幕外切箭头后需要还原。
    private Sprite originalSprite;

    // 记录当前是否处于屏幕外箭头状态，避免每帧重复设置 Sprite。
    private bool showingArrow;

    /// <summary>
    /// 设置标志跟随的世界锚点。必须在激活标志前调用。
    /// </summary>
    public void SetAnchor(Transform value)
    {
        anchor = value;
    }

    /// <summary>
    /// 把标志显示到指定锚点上方，并记录当前锚点，
    /// 供 <see cref="Hide"/> 判定是否由调用者持有。
    /// </summary>
    public void Show(Transform anchor)
    {
        SetAnchor(anchor);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 隐藏标志，但仅当当前显示锚点与传入锚点一致时才生效。
    /// 多个 TaskCameraGuidance 共享同一个标志时，已被其他组件
    /// 接管显示的标志不会被本调用误关。
    /// </summary>
    public void Hide(Transform anchor)
    {
        if (this.anchor != anchor)
        {
            return;
        }

        gameObject.SetActive(false);
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

        markerImage = markerRect.GetComponent<Image>();

        if (markerImage != null)
        {
            originalSprite = markerImage.sprite;
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

        bool behindCamera = screenPoint.z <= 0f;

        // 屏幕内判定：在相机前方且屏幕坐标落在屏幕范围内。
        bool onScreen =
            !behindCamera &&
            screenPoint.x >= 0f &&
            screenPoint.x <= Screen.width &&
            screenPoint.y >= 0f &&
            screenPoint.y <= Screen.height;

        if (onScreen || !showOffScreenIndicator)
        {
            // 屏幕内（或关闭了边缘指示）：正常投影，不旋转，用原图标。
            SetArrowMode(false);
            markerRect.localScale = Vector3.one;
            markerRect.position = screenPoint;
            markerRect.localRotation = Quaternion.identity;
            return;
        }

        // 屏幕外（含相机背后）：计算从屏幕中心指向目标的方向。
        Transform cam = projectionCamera.transform;
        Vector3 toTarget = worldPoint - cam.position;

        // 用相机 yaw 构造水平基，完全不受俯仰影响。
        // 避免相机朝上/下看时 cam.forward 的水平投影归零导致方向错乱。
        float yawRad = cam.eulerAngles.y * Mathf.Deg2Rad;
        Vector3 forwardFlat =
            new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
        Vector3 rightFlat =
            new Vector3(Mathf.Cos(yawRad), 0f, -Mathf.Sin(yawRad));

        float horizontal = Vector3.Dot(toTarget, rightFlat);
        float forward = Vector3.Dot(toTarget, forwardFlat);
        // 相机空间垂直分量：目标在屏幕上的上下位置，受俯仰影响（前方需要）。
        float verticalCam = Vector3.Dot(toTarget, cam.up);

        Vector2 direction;

        if (forward > 0f)
        {
            // 前方屏幕外：水平方向不受俯仰，垂直方向用相机空间高度。
            // 目标接近正侧面时，垂直分量平滑淡出，
            // 使前方→背后在 forward=0 处连续，箭头不瞬移。
            float horizLen =
                Mathf.Sqrt(horizontal * horizontal + forward * forward);
            float cosAngle =
                horizLen > 0.001f ? forward / horizLen : 0f;
            float fade = Mathf.SmoothStep(0f, 0.5f, cosAngle);
            direction = new Vector2(horizontal, verticalCam * fade);
        }
        else
        {
            // 背后：水平方向不受俯仰，垂直方向用 forward（负值指向下）。
            // 正后方→正下方，左/右后方→左/右下，俯仰变化时不动。
            direction = new Vector2(horizontal, forward);
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            markerRect.localScale = Vector3.zero;
            return;
        }

        Vector2 center =
            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Vector2 edgePoint =
            ComputeEdgeIntersection(
                center,
                direction,
                edgePadding
            );

        SetArrowMode(true);
        markerRect.localScale = Vector3.one;
        markerRect.position = edgePoint;

        // 旋转：屏幕坐标 Y 向上，UI 的 localRotation.z 逆时针为正。
        // atan2(y, x) 给出与 +X 轴的夹角，减去 90° 让箭头朝上（+Y）
        // 时指向目标方向；再加检查器配置的偏移。
        float angle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f +
            indicatorRotationOffset;

        markerRect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    // 切换屏幕内/外的图标显示。
    private void SetArrowMode(bool arrow)
    {
        if (showingArrow == arrow)
        {
            return;
        }

        showingArrow = arrow;

        if (markerImage == null || offScreenArrowSprite == null)
        {
            return;
        }

        markerImage.sprite = arrow ? offScreenArrowSprite : originalSprite;
    }

    // 计算从屏幕中心出发、沿 direction 的射线与屏幕内缩矩形的交点。
    private Vector2 ComputeEdgeIntersection(
        Vector2 center,
        Vector2 direction,
        float padding
    )
    {
        float halfW = Screen.width * 0.5f - padding;
        float halfH = Screen.height * 0.5f - padding;

        float absX = Mathf.Abs(direction.x);
        float absY = Mathf.Abs(direction.y);

        // 避免除以零：某个分量为 0 时给一个极大值，
        // 让该方向不会选中作为约束边界。
        float tX = absX > 0.0001f ? halfW / absX : float.MaxValue;
        float tY = absY > 0.0001f ? halfH / absY : float.MaxValue;

        float t = Mathf.Min(tX, tY);

        return center + direction * t;
    }
}
