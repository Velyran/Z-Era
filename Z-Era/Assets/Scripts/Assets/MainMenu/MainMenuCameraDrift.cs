using UnityEngine;

/// <summary>
/// 主菜单相机漫游：挂在主菜单相机上，相机会在自己当前朝向所在的
/// 竖直平面上随机移动，靠近边界时逐渐减速，触边后按镜面反射改变
/// 方向并重新加速，全程不停顿。
///
/// 竖直平面的定义（关键）：
///   平面法线 = 相机视线方向投影到水平面（去掉 Y 分量）。
///   即这张平面"竖直站立"、正对着相机的水平朝向，
///   由相机 Start 那一刻的水平朝向确定，此后固定不变。
///   平面内的两个基向量：
///     · planeRight = 世界上方向 × 平面法线（水平方向）
///     · planeUp    = 世界上方向（竖直方向）
///   因此相机只在左右、上下两个方向漂移，不会前后穿插。
///
/// 转向规则（只有一种）：
///   方向只在真正触边时改变，且按反射定律镜面反弹
///   （入射角 = 反射角）。不设定时随机换向——那样的换向会在
///   离边界很远的地方凭空改变方向，看起来就像"提前撞墙弹回"。
///   为避免每次都沿同一条折线来回，反射时会按 Reflection Jitter
///   给角度加一点随机偏移，轨迹不呆板但转向仍然只发生在边界上。
///
/// 速度模型：
///   匀速区：离边界远时按 Base Speed 匀速前进；
///   减速区：按各轴向到边界的剩余距离，越近速度越小；
///           距离小于 Decel Distance 时线性衰减到 0；
///   触边后立即反射并重新加速，形成"减速—转向—加速"的连续过程。
///   速度倍率取各轴向的最小值：只要有一个方向快到头就减速，
///   避免斜着走时某一轴已越界却仍全速前进。
///
/// 其他要点：
///   · 平面基准在 Start 时锁定，运行时即使相机被 MouseLook
///     转动朝向，移动基准也不变，避免位置抖动；
///   · 用 unscaledDeltaTime，Time.timeScale = 0 时依然漂移。
/// </summary>
public class MainMenuCameraDrift : MonoBehaviour
{
    [Header("移动范围（竖直平面上的矩形）")]

    [Tooltip("矩形左边界：相对中心的横向偏移最小值（米），负值向左")]
    [SerializeField]
    private float leftEdge = -3f;

    [Tooltip("矩形右边界：相对中心的横向偏移最大值（米），正值向右")]
    [SerializeField]
    private float rightEdge = 3f;

    [Tooltip("矩形下边界：相对中心的纵向偏移最小值（米），负值向下")]
    [SerializeField]
    private float bottomEdge = -1f;

    [Tooltip("矩形上边界：相对中心的纵向偏移最大值（米），正值向上")]
    [SerializeField]
    private float topEdge = 1f;

    [Header("移动参数")]

    [Tooltip("基础速度（米/秒）：远离边界时的匀速速度")]
    [SerializeField, Min(0f)]
    private float baseSpeed = 0.6f;

    [Tooltip("减速距离（米）：距边界多近开始减速，到边界时速度衰减为 0")]
    [SerializeField, Min(0.01f)]
    private float decelDistance = 0.8f;

    [Tooltip("速度下限倍率：贴近边界时的最小速度比例，0 为完全停住")]
    [SerializeField, Range(0f, 1f)]
    private float minSpeedFactor = 0.15f;

    [Header("转向参数")]

    [Tooltip("反射角随机偏移（度）：触边反弹时给方向加一个不超过该值的随机角度，" +
        "避免每次沿同一条折线来回；0 表示严格镜面反射")]
    [SerializeField, Min(0f)]
    private float reflectionJitter = 8f;

    [Tooltip("是否在 Scene 视图绘制移动范围矩形")]
    [SerializeField]
    private bool drawGizmo = true;

    // 矩形中心：Start 时相机所在的世界位置。
    private Vector3 center;

    // 竖直平面的两个基向量（Start 时锁定）。
    private Vector3 planeRight;   // 平面内的水平方向
    private Vector3 planeUp;      // 平面内的竖直方向（世界上方向）

    // 当前在平面上的偏移：x 沿 planeRight，y 沿 planeUp。
    private Vector2 offset;

    // 当前移动方向（平面内的单位向量，已表示为 (x, y) 二维）。
    private Vector2 direction2D;

    private void Start()
    {
        // 以相机当前位置为矩形中心。
        center = transform.position;

        BuildPlane();

        PickDirection();
    }

    // 根据 Start 时的相机水平朝向构建竖直平面。
    private void BuildPlane()
    {
        // 平面法线：相机视线去掉竖直分量后的水平方向。
        Vector3 normal = transform.forward;
        normal.y = 0f;

        if (normal.sqrMagnitude < 0.0001f)
        {
            // 相机正朝上/正朝下时水平朝向退化，
            // 退化为用相机的右方向（同样去 Y）兜底。
            normal = transform.right;
            normal.y = 0f;

            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.forward;
            }
        }

        normal.Normalize();

        // 平面内的水平基向量：世界上方向 × 法线。
        planeRight = Vector3.Cross(Vector3.up, normal).normalized;

        // 平面内的竖直基向量：直接用世界上方向，
        // 保证平面是"竖直站立"的，不会随相机 roll 倾斜。
        planeUp = Vector3.up;
    }

    private void Update()
    {
        // 先按当前方向与速度推进，再判定触边。
        // 顺序很重要：先移动才能在"刚好压线"那一帧立刻转向，
        // 而不是被夹住后停住。
        float speed = baseSpeed * ComputeSpeedFactor();

        offset += direction2D * (speed * Time.unscaledDeltaTime);

        // 只有真正压到边界才改变方向：
        // 不再做定时随机换向，否则会在离边界还有距离时凭空转向，
        // 看起来就像"没碰到边界就反弹"。
        if (IsAtEdge())
        {
            // 夹回范围内，避免因单帧位移过大而越界。
            ClampIntoBounds();

            // 镜面反射：按反射定律翻向量的对应分量，
            // 反射角 = 入射角。贴哪条边翻哪个轴，角落同时翻两轴。
            ReflectOffEdge();
        }

        // 由中心 + 平面偏移合成世界位置。
        transform.position =
            center +
            planeRight * offset.x +
            planeUp * offset.y;
    }

    // 计算当前速度倍率：取各轴向的最小值。
    // 某个方向的剩余距离小于 decelDistance 时，该轴倍率线性下降；
    // 用 min 而非平均，保证斜向移动时只要有轴快到头就减速。
    private float ComputeSpeedFactor()
    {
        float factor = 1f;

        factor = Mathf.Min(factor, AxisFactor(
            direction2D.x,
            offset.x,
            leftEdge,
            rightEdge
        ));

        factor = Mathf.Min(factor, AxisFactor(
            direction2D.y,
            offset.y,
            bottomEdge,
            topEdge
        ));

        // 钳到 [minSpeedFactor, 1]：贴近边界也不完全停住，
        // 保留一点蠕动向边界靠拢，触边后才转向。
        return Mathf.Clamp(factor, minSpeedFactor, 1f);
    }

    // 单个轴向的速度倍率。
    //   dir   ：该轴上的移动方向（正向右/上，负向左/下）
    //   value ：该轴当前偏移
    //   min/max：该轴边界
    // 只考虑"正在靠近的那一侧边界"：反向边界的距离不影响减速，
    // 否则远离边界时也会被误减速。
    private float AxisFactor(
        float dir,
        float value,
        float min,
        float max
    )
    {
        if (Mathf.Approximately(dir, 0f))
        {
            // 该轴不动，不受边界影响。
            return 1f;
        }

        // 取实际大小边界，兼容 min > max 的误配置。
        float lower = Mathf.Min(min, max);
        float upper = Mathf.Max(min, max);

        float remaining;

        if (dir > 0f)
        {
            // 正在靠近 upper。
            remaining = upper - value;
        }
        else
        {
            // 正在靠近 lower。
            remaining = value - lower;
        }

        if (remaining <= 0f)
        {
            // 已在边界外（理论上被 ClampIntoBounds 挡住，兜底）。
            return 0f;
        }

        if (remaining >= decelDistance)
        {
            // 距边界还远：全速。
            return 1f;
        }

        // 减速区：线性衰减。
        return remaining / decelDistance;
    }

    // 在平面内随机选一个二维单位方向。
    private void PickDirection()
    {
        float angle = Random.Range(0f, 360f);

        direction2D = new Vector2(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad)
        );
    }

    // 触边反射：按反射定律翻方向向量的对应分量，再按
    // Reflection Jitter 加一点随机角度，让每次反弹的折线不完全相同。
    //
    // 矩形边界就是两条法线轴（X 轴与 Y 轴）：
    //   · 撞左右边（法线沿 X）→ 翻 x 分量，y 不变
    //   · 撞上下边（法线沿 Y）→ 翻 y 分量，x 不变
    //   · 撞角落（同时压两轴）→ 两轴都翻
    //
    // 反射角等于入射角：以 45° 撞墙就以 45° 离开，
    // 不像随机换向那样每次轨迹都不一样，是可预测的镜面反弹。
    private void ReflectOffEdge()
    {
        float lowerX = Mathf.Min(leftEdge, rightEdge);
        float upperX = Mathf.Max(leftEdge, rightEdge);
        float lowerY = Mathf.Min(bottomEdge, topEdge);
        float upperY = Mathf.Max(bottomEdge, topEdge);

        // 压到左右边界：x 分量取反（沿法线反射）。
        if (offset.x <= lowerX + 0.0001f)
        {
            direction2D.x = -direction2D.x;
        }
        else if (offset.x >= upperX - 0.0001f)
        {
            direction2D.x = -direction2D.x;
        }

        // 压到上下边界：y 分量取反。
        if (offset.y <= lowerY + 0.0001f)
        {
            direction2D.y = -direction2D.y;
        }
        else if (offset.y >= upperY - 0.0001f)
        {
            direction2D.y = -direction2D.y;
        }

        // 反射后方向理论上一定朝内，这里兜底防零向量：
        // 万一入射方向完全平行于边界（法向分量为 0），
        // 反射后仍是 0，给一个指向矩形中心的方向。
        if (direction2D.sqrMagnitude < 0.0001f)
        {
            Vector2 toCenter =
                -new Vector2(offset.x, offset.y).normalized;

            direction2D = toCenter.sqrMagnitude > 0.0001f
                ? toCenter
                : Vector2.right;
        }

        // 反射角随机偏移：不改变"只在边界转向"的规则，
        // 只是让多次往返的折线不重合
        ApplyReflectionJitter();
    }

    // 给反射后的方向加一个不超过 reflectionJitter 度的随机偏转，
    // 并保证偏转后仍然朝矩形内部（法向分量不得反向），
    // 否则会贴着边界来回抖动或穿出边界。
    private void ApplyReflectionJitter()
    {
        if (reflectionJitter <= 0f)
        {
            return;
        }

        float jitter = Random.Range(-reflectionJitter, reflectionJitter);

        float sin = Mathf.Sin(jitter * Mathf.Deg2Rad);
        float cos = Mathf.Cos(jitter * Mathf.Deg2Rad);

        Vector2 rotated = new Vector2(
            direction2D.x * cos - direction2D.y * sin,
            direction2D.x * sin + direction2D.y * cos
        );

        // 只有偏转后仍朝向矩形内部才采用：贴左右边时 x 分量必须
        // 指向内侧，贴上下边时 y 分量必须指向内侧
        if (!PointsInward(rotated))
        {
            return;
        }

        direction2D = rotated;
    }

    // 偏转后的方向是否朝矩形内部：贴着的那些边界要求对应分量朝内，
    // 没贴的轴不限制
    private bool PointsInward(Vector2 dir)
    {
        const float tolerance = 0.0001f;

        float lowerX = Mathf.Min(leftEdge, rightEdge);
        float upperX = Mathf.Max(leftEdge, rightEdge);
        float lowerY = Mathf.Min(bottomEdge, topEdge);
        float upperY = Mathf.Max(bottomEdge, topEdge);

        if (offset.x <= lowerX + tolerance && dir.x < 0f)
        {
            return false;
        }

        if (offset.x >= upperX - tolerance && dir.x > 0f)
        {
            return false;
        }

        if (offset.y <= lowerY + tolerance && dir.y < 0f)
        {
            return false;
        }

        if (offset.y >= upperY - tolerance && dir.y > 0f)
        {
            return false;
        }

        return true;
    }

    // 是否已到达（或越过）矩形边界：任一轴向贴近边界即算触边。
    // 用极小容差而非较大 margin：触边本意是"已经压线"，
    // 提前量交给减速逻辑处理，否则会提前截停表现为"没碰到就反弹"。
    private bool IsAtEdge()
    {
        float lowerX = Mathf.Min(leftEdge, rightEdge);
        float upperX = Mathf.Max(leftEdge, rightEdge);
        float lowerY = Mathf.Min(bottomEdge, topEdge);
        float upperY = Mathf.Max(bottomEdge, topEdge);

        bool nearLeft = offset.x <= lowerX + 0.0001f;
        bool nearRight = offset.x >= upperX - 0.0001f;
        bool nearBottom = offset.y <= lowerY + 0.0001f;
        bool nearTop = offset.y >= upperY - 0.0001f;

        return nearLeft || nearRight || nearBottom || nearTop;
    }

    // 把平面偏移夹回矩形范围内。
    // 注意边界可能是 min > max 的误配置，按小大值钳制保证不出错。
    private void ClampIntoBounds()
    {
        offset.x = Mathf.Clamp(
            offset.x,
            Mathf.Min(leftEdge, rightEdge),
            Mathf.Max(leftEdge, rightEdge)
        );

        offset.y = Mathf.Clamp(
            offset.y,
            Mathf.Min(bottomEdge, topEdge),
            Mathf.Max(bottomEdge, topEdge)
        );
    }

    // Scene 视图画出竖直平面上的矩形范围。
    // 绿线为矩形四边，黄点为矩形中心（相机初始位置）。
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmo)
        {
            return;
        }

        // 编辑器未运行时没有 center/plane 数据，用当前 transform 现算，
        // 保证摆放阶段就能预览范围。
        Vector3 gizmoCenter = Application.isPlaying
            ? center
            : transform.position;

        Vector3 gizmoRight;
        Vector3 gizmoUp;

        if (Application.isPlaying)
        {
            gizmoRight = planeRight;
            gizmoUp = planeUp;
        }
        else
        {
            Vector3 normal = transform.forward;
            normal.y = 0f;

            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.forward;
            }

            normal.Normalize();
            gizmoRight = Vector3.Cross(Vector3.up, normal).normalized;
            gizmoUp = Vector3.up;
        }

        float left = Mathf.Min(leftEdge, rightEdge);
        float right = Mathf.Max(leftEdge, rightEdge);
        float bottom = Mathf.Min(bottomEdge, topEdge);
        float top = Mathf.Max(bottomEdge, topEdge);

        Vector3 cornerLB =
            gizmoCenter + gizmoRight * left + gizmoUp * bottom;

        Vector3 cornerRB =
            gizmoCenter + gizmoRight * right + gizmoUp * bottom;

        Vector3 cornerRT =
            gizmoCenter + gizmoRight * right + gizmoUp * top;

        Vector3 cornerLT =
            gizmoCenter + gizmoRight * left + gizmoUp * top;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(cornerLB, cornerRB);
        Gizmos.DrawLine(cornerRB, cornerRT);
        Gizmos.DrawLine(cornerRT, cornerLT);
        Gizmos.DrawLine(cornerLT, cornerLB);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(gizmoCenter, 0.1f);
    }
}
