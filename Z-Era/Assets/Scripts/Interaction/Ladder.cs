using UnityEngine;

/// <summary>
/// 梯子：定义一个可攀爬区域与攀爬轴向。
///
/// 场景搭建：
///   1) 本组件挂在梯子根物体上；
///   2) 梯子需要真实碰撞体（MeshCollider / BoxCollider），
///      玩家前进才会被挡住，产生"碰到梯子"的手感；
///   3) 需要一个 IsTrigger 的碰撞体作为抓握区域
///      （建议新建子物体，挂 BoxCollider 并勾 IsTrigger，
///        大小覆盖整个可攀爬体积），拖到 Trigger Zone；
///      留空则自动取本物体或子物体上第一个 Trigger；
///   4) 梯子连着上方平台时，把 Trigger Zone 顶端调到平台表面高度，
///      并配置 Top Exit Anchor（玩家上平台后应站的脚底位置）。
///
/// 攀爬范围：
///   Bottom / Top Anchor 都留空时，自动用抓握区域包围盒的
///   上下端作为攀爬范围（零配置即可用）；
///   指定锚点则以锚点为准（锚点用世界坐标，脚底/手部位置）。
///
/// 顶部脱离：
///   玩家是胶囊体，上升时头顶会被平台底面挡住，脚底最高只能到
///   平台表面下方一个 capsule height 处，靠物理上升到不了平台，
///   因此爬到顶端是脱离 + 传送到 Top Exit Anchor。
///   脱离判据用脚底高度而非全身包围盒，否则永远差一个
///   capsule height 判不出来。
/// </summary>
public class Ladder : MonoBehaviour
{
    [Header("抓握区域")]
    [Tooltip("判定玩家是否碰到梯子的触发区域；留空自动获取本物体/子物体上第一个 Trigger")]
    [SerializeField]
    private Collider triggerZone;

    [Header("攀爬范围")]
    [Tooltip("攀爬方向参考轴；留空使用世界正上方（垂直梯）")]
    [SerializeField]
    private Transform axisTransform;

    [Tooltip("攀爬范围下端（脚底位置）；与 Top Anchor 同时留空时自动取抓握区域下端")]
    [SerializeField]
    private Transform bottomAnchor;

    [Tooltip("攀爬范围上端（手到达位置）；与 Bottom Anchor 同时留空时自动取抓握区域上端")]
    [SerializeField]
    private Transform topAnchor;

    [Tooltip("未指定锚点且没有抓握区域时，沿攀爬轴的长度（米）")]
    [SerializeField, Min(0.1f)]
    private float climbLength = 4f;

    [Header("顶部脱离")]
    [Tooltip("爬到顶后玩家应站的位置（填脚底位置）。" +
        "强烈建议配置：玩家是胶囊体，上升时头顶会被平台底面挡住，" +
        "脚底永远差一个 capsule height 才到平台表面，此时只能靠" +
        "脱离时传送才能上到平台。留空则原地脱离（可能上不去）")]
    [SerializeField]
    private Transform topExitAnchor;

    [Header("参数")]
    [Tooltip("攀爬速度（米/秒）")]
    [SerializeField, Min(0.1f)]
    private float climbSpeed = 2.5f;

    /// <summary>攀爬方向（已归一化）。</summary>
    public Vector3 UpDirection =>
        axisTransform != null
            ? axisTransform.up.normalized
            : Vector3.up;

    /// <summary>攀爬范围下端点的世界坐标。</summary>
    public Vector3 BottomPoint
    {
        get
        {
            if (bottomAnchor != null)
            {
                return bottomAnchor.position;
            }

            if (triggerZone != null)
            {
                return AxisExtremePoint(
                    triggerZone.bounds,
                    UpDirection,
                    false
                );
            }

            return transform.position;
        }
    }

    /// <summary>攀爬范围上端点的世界坐标。</summary>
    public Vector3 TopPoint
    {
        get
        {
            if (topAnchor != null)
            {
                return topAnchor.position;
            }

            if (triggerZone != null)
            {
                return AxisExtremePoint(
                    triggerZone.bounds,
                    UpDirection,
                    true
                );
            }

            return BottomPoint + UpDirection * climbLength;
        }
    }

    /// <summary>攀爬速度（米/秒）。</summary>
    public float ClimbSpeed => climbSpeed;

    /// <summary>
    /// 玩家是否已爬到抓握区域顶端之外（爬到上方平台时）。
    /// 判定不看按键，所以一直按着 W 也会脱离，
    /// 脱离后 W 交还给 PlayerController 恢复成正常前进。
    ///
    /// 用"脚底高度"而不是全身包围盒：玩家是胶囊体，上升时头顶
    /// 会被平台底面挡住，全身包围盒永远差一个 capsule height
    /// 才能越过抓握区域顶端，用包围盒判定会导致永远脱离不了。
    /// 脚底越过顶端即视为已爬上平台。
    /// </summary>
    public bool HasPlayerExited(float feetHeight)
    {
        // 没有抓握区域时不判定脱离，避免误伤。
        if (triggerZone == null)
        {
            return false;
        }

        return feetHeight >= CeilingHeight;
    }

    /// <summary>
    /// 攀爬的极限高度（相对下端）：脚底到此高度即脱离。
    /// 取抓握区域顶端，因此把触发器顶端调到平台表面高度时，
    /// 玩家脚底踏上平台的那一刻脱离，位置正好衔接。
    /// </summary>
    public float CeilingHeight
    {
        get
        {
            if (triggerZone != null)
            {
                return HeightAlong(
                    AxisExtremePoint(
                        triggerZone.bounds,
                        UpDirection,
                        true
                    )
                );
            }

            return TopHeight;
        }
    }

    /// <summary>
    /// 爬到顶后玩家应站的位置（脚底位置）。
    /// 配置了 Top Exit Anchor 就传送到锚点，
    /// 否则原地脱离。
    /// </summary>
    public Vector3 TopExitPosition =>
        topExitAnchor != null
            ? topExitAnchor.position
            : Vector3.positiveInfinity;

    /// <summary>顶端在攀爬轴上的高度（相对于下端）。</summary>
    public float TopHeight =>
        Vector3.Dot(TopPoint - BottomPoint, UpDirection);

    /// <summary>把世界坐标投影到攀爬轴上，得到相对下端的高度。</summary>
    public float HeightAlong(Vector3 worldPosition) =>
        Vector3.Dot(worldPosition - BottomPoint, UpDirection);

    // 沿攀爬轴取包围盒的上端/下端点。
    // up 为单位向量，extents 沿世界轴，所以要按分量绝对值累加。
    private static Vector3 AxisExtremePoint(
        Bounds bounds,
        Vector3 up,
        bool upper
    )
    {
        float halfExtent =
            Mathf.Abs(up.x) * bounds.extents.x +
            Mathf.Abs(up.y) * bounds.extents.y +
            Mathf.Abs(up.z) * bounds.extents.z;

        return upper
            ? bounds.center + up * halfExtent
            : bounds.center - up * halfExtent;
    }

    // 在 Inspector 里改动后自动补全抓握区域引用，省一次手动拖拽。
    private void OnValidate()
    {
        if (triggerZone != null)
        {
            return;
        }

        Collider[] colliders =
            GetComponentsInChildren<Collider>(true);

        foreach (Collider collider in colliders)
        {
            if (collider.isTrigger)
            {
                triggerZone = collider;
                return;
            }
        }
    }

    // 场景里画出攀爬轴、极限高度与顶部脱离点，方便确认位置摆对没有。
    private void OnDrawGizmosSelected()
    {
        Vector3 bottom = BottomPoint;
        Vector3 top = TopPoint;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(bottom, top);
        Gizmos.DrawWireSphere(bottom, 0.08f);
        Gizmos.DrawWireSphere(top, 0.08f);

        // 极限高度（脚底到此即脱离）：用绿色标出。
        Vector3 ceiling = BottomPoint + UpDirection * CeilingHeight;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ceiling, 0.12f);

        // 顶部脱离锚点：用青色标出。
        if (topExitAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(topExitAnchor.position, 0.15f);
            Gizmos.DrawLine(ceiling, topExitAnchor.position);
        }
    }
}
