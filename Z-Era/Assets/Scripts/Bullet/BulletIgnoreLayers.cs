using UnityEngine;

/// <summary>
/// 子弹免碰撞图层：列在 IgnoredLayerNames 里的图层上的碰撞体，
/// 子弹既不会与其发生物理碰撞，也不会被子弹的命中射线打到。
/// 无需挂载：Player 启动时自动执行一次，把结果缓存到 Mask，
/// 由 BulletHandle 在 Awake 中从命中掩码里剔除。
/// 只影响子弹，不影响玩家交互射线、相机遮挡等其它物理查询。
/// </summary>
public static class BulletIgnoreLayers
{
    // 子弹免碰撞的图层名（定死在代码里，需要新增时在这里加一项）。
    private static readonly string[] IgnoredLayerNames =
    {
        "AirWall"
    };

    // 子弹自身所在图层的层名，用于关闭物理层间碰撞。
    private const string BulletLayerName = "Bullet";

    // 上述图层在射线掩码里的位，未配置/图层不存在时为 0。
    public static int Mask { get; private set; }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad
    )]
    private static void Initialize()
    {
        int bulletLayer = LayerMask.NameToLayer(BulletLayerName);
        int mask = 0;

        foreach (string layerName in IgnoredLayerNames)
        {
            int layer = LayerMask.NameToLayer(layerName);

            if (layer < 0)
            {
                Debug.LogWarning(
                    $"BulletIgnoreLayers：图层 '{layerName}' 不存在，" +
                        "已跳过。"
                );

                continue;
            }

            mask |= 1 << layer;

            // 物理碰撞兜底：子弹与该层的 OnCollisionEnter /
            // OnTriggerEnter 也不会触发。
            if (bulletLayer >= 0)
            {
                Physics.IgnoreLayerCollision(
                    bulletLayer,
                    layer,
                    true
                );
            }
        }

        Mask = mask;
    }
}
