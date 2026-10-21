using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单换景循环：每隔 Interval 秒，全屏黑屏从左往右溶解铺满；
/// 铺满后的那一帧把主菜单相机瞬移到下一个轮候点（位置 + 朝向），
/// 黑屏紧接着再从左往右溶解消退，回到计时，如此往复。
///
/// 视觉实现（依赖 Assets/Shaders/UI Wipe Dissolve.shader）：
///   黑屏是 MainMenu Canvas 最下层的一张全屏 Image，判据为
///   "图像 UV.x + 噪声扰动 vs 移动阈值"，所以边缘是溶解状而不是
///   矩形直边；噪声 UV 随未缩放时间平移，边缘持续变化。
///   本脚本持有材质实例，_WipeT（扫描进度）/ _WipeFlip（覆盖或消退）
///   / _NoiseTime / _Aspect 只写实例，不污染材质资产。
///
/// 相机切换（关键）：
///   铺满与消退之间没有停留，全黑只有大概一帧，就在那一帧调用
///   MainMenuCameraDrift.Reanchor 切换位置与朝向——不重锚的话，
///   漫游组件会按 Start 时锁定的旧中心把相机拉回原处。
///
/// 其他要点：
///   · Raycast Target 强制关闭：黑屏只盖场景画面，不挡菜单 UI 点击；
///   · 计时与扫描全部走未缩放时间，Time.timeScale = 0 也照常循环；
///   · 轮候点下标 0 = 相机初始机位（照进场景时的相机位姿填写）：
///     首次换景直接切到下标 1，之后按列表顺序轮换，到末尾回到 0；
///     Inspector 里可直接增删改，Scene 视图会画出各点的位置与朝向。
/// </summary>
public class MainMenuCameraWipe : MonoBehaviour
{
    // Shader 属性 ID（属性见 Assets/Shaders/UI Wipe Dissolve.shader）
    private static readonly int WipeTId = Shader.PropertyToID("_WipeT");
    private static readonly int WipeFlipId = Shader.PropertyToID("_WipeFlip");
    private static readonly int NoiseTimeId = Shader.PropertyToID("_NoiseTime");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");

    // 黑屏材质必须使用的 Shader 名（配错时给出明确报错而不是悄悄不生效）
    private const string ExpectedShaderName = "UI Wipe Dissolve";

    // 单个轮候机位：欧拉角比 Quaternion 更便于在 Inspector 里直接编辑
    [System.Serializable]
    private struct Viewpoint
    {
        [Tooltip("机位位置（世界坐标）")]
        public Vector3 position;

        [Tooltip("机位朝向（欧拉角）")]
        public Vector3 eulerAngles;
    }

    [Header("引用")]

    [Tooltip("铺满全屏的黑色 Image：MainMenu Canvas 最下层、Type = Simple、" +
        "无 Sprite、材质指定 UI Wipe Dissolve 材质。" +
        "Raycast Target 由本脚本强制关闭")]
    [SerializeField]
    private Image wipeImage;

    [Tooltip("主菜单相机上的漫游组件；留空则取本物体上的 MainMenuCameraDrift")]
    [SerializeField]
    private MainMenuCameraDrift cameraDrift;

    [Header("轮候点")]

    [Tooltip("换景目标机位：下标 0 = 相机初始机位（照相机进场景时的" +
        "位姿填写），首次换景直接切到下标 1，之后按顺序轮换、" +
        "到末尾回到下标 0")]
    [SerializeField]
    private List<Viewpoint> viewpoints = new List<Viewpoint>();

    [Header("节奏（秒，未缩放时间）")]

    [Tooltip("两次换景之间的待命时长（秒）")]
    [SerializeField, Min(0.1f)]
    private float idleInterval = 8f;

    [Tooltip("黑屏从左往右铺满全屏的时长（秒）")]
    [SerializeField, Min(0.05f)]
    private float coverDuration = 1.2f;

    [Tooltip("黑屏从左往右溶解消退的时长（秒）")]
    [SerializeField, Min(0.05f)]
    private float revealDuration = 1.2f;

    // 由共享材质复制出的运行时实例：写入进度的都是它
    private Material wipeMaterial;

    // 实例复制前的原材质（资产），销毁实例时还回去，避免 Image 悬空
    private Material sourceMaterial;

    // 下一个要切换到的轮候点索引
    private int nextViewpointIndex;

    private void Awake()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        // 黑屏只盖场景画面：强制不参与射线检测，任何时候都不挡菜单点击
        wipeImage.raycastTarget = false;

        sourceMaterial = wipeImage.material;
        wipeMaterial = new Material(sourceMaterial);
        wipeImage.material = wipeMaterial;

        // 起始状态：完全透明、覆盖方向
        wipeMaterial.SetFloat(WipeTId, 0f);
        wipeMaterial.SetFloat(WipeFlipId, 0f);
    }

    private void Start()
    {
        StartCoroutine(CycleRoutine());
    }

    private void Update()
    {
        // 噪声随时间滚动（未缩放时间，timeScale = 0 也照走）
        wipeMaterial.SetFloat(NoiseTimeId, Time.unscaledTime);

        // 宽高比每帧同步：窗口尺寸变化后噪声不再被拉扁
        Rect rect = wipeImage.rectTransform.rect;

        if (rect.height > 0f)
        {
            wipeMaterial.SetFloat(AspectId, rect.width / rect.height);
        }
    }

    // 换景主循环：待命 → 铺满 → 全黑帧切机位 → 消退 → 待命……
    private IEnumerator CycleRoutine()
    {
        // 下标 0 是相机的初始机位：首次换景直接切到下标 1
        // （轮候点只有 1 个时取模后仍为 0，即回到初始机位）
        nextViewpointIndex = 1 % viewpoints.Count;

        while (true)
        {
            yield return new WaitForSecondsRealtime(idleInterval);

            // 黑屏从左往右铺满
            yield return StartCoroutine(SweepRoutine(0f, coverDuration));

            // 铺满后画面全黑的那一帧切换机位（下一帧才开始消退，
            // 且消退起点仍是全黑，切换过程不可见）
            Viewpoint point = viewpoints[nextViewpointIndex];

            cameraDrift.Reanchor(
                point.position,
                Quaternion.Euler(point.eulerAngles)
            );

            nextViewpointIndex =
                (nextViewpointIndex + 1) % viewpoints.Count;

            // 黑屏紧接着从左往右消退
            yield return StartCoroutine(SweepRoutine(1f, revealDuration));
        }
    }

    // 单次扫描：进度 0 → 1。flip = 0 覆盖（黑在阈值左侧），
    // flip = 1 消退（黑在阈值右侧），两次扫描都从左往右推进。
    // 进度按未缩放时间推进，节奏不受 timeScale 影响
    private IEnumerator SweepRoutine(float flip, float duration)
    {
        wipeMaterial.SetFloat(WipeFlipId, flip);
        wipeMaterial.SetFloat(WipeTId, 0f);

        float progress = 0f;

        while (progress < 1f)
        {
            progress = Mathf.MoveTowards(
                progress,
                1f,
                Time.unscaledDeltaTime / duration
            );

            wipeMaterial.SetFloat(WipeTId, progress);

            yield return null;
        }
    }

    // 引用与配置校验：缺项时给出明确报错并返回 false（由 Awake 关闭本组件）
    private bool ValidateReferences()
    {
        if (wipeImage == null)
        {
            Debug.LogError(
                "MainMenuCameraWipe：未指定黑屏 Image（Wipe Image）。",
                this
            );
            return false;
        }

        if (cameraDrift == null)
        {
            cameraDrift = GetComponent<MainMenuCameraDrift>();
        }

        if (cameraDrift == null)
        {
            Debug.LogError(
                "MainMenuCameraWipe：找不到 MainMenuCameraDrift，" +
                "请把本组件挂在主菜单相机上，或在 Inspector 上指定。",
                this
            );
            return false;
        }

        if (viewpoints == null || viewpoints.Count == 0)
        {
            Debug.LogError(
                "MainMenuCameraWipe：轮候点为空，无法换景。" +
                "请在 Inspector 的 Viewpoints 里添加至少一个机位。",
                this
            );
            return false;
        }

        Material material = wipeImage.material;

        if (material == null || material.shader == null)
        {
            Debug.LogError(
                "MainMenuCameraWipe：黑屏 Image 未指定材质（或材质丢失），" +
                "无法驱动溶解进度。" ,
                this
            );
            return false;
        }

        if (material.shader.name != ExpectedShaderName)
        {
            Debug.LogError(
                "MainMenuCameraWipe：黑屏材质的 Shader 是 '" +
                material.shader.name + "'，应为 '" + ExpectedShaderName +
                "'（Assets/Shaders/UI Wipe Dissolve.shader）。",
                this
            );
            return false;
        }

        return true;
    }

    private void OnDestroy()
    {
        if (wipeImage != null && sourceMaterial != null)
        {
            wipeImage.material = sourceMaterial;
        }

        if (wipeMaterial != null)
        {
            Destroy(wipeMaterial);
        }
    }

    // Scene 视图画出各轮候点：黄球 = 位置，青线 = 朝向
    private void OnDrawGizmosSelected()
    {
        if (viewpoints == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;

        foreach (Viewpoint point in viewpoints)
        {
            Quaternion rotation = Quaternion.Euler(point.eulerAngles);

            Gizmos.DrawWireSphere(point.position, 0.15f);
            Gizmos.DrawLine(
                point.position,
                point.position + rotation * Vector3.forward * 1.5f
            );
        }
    }
}
