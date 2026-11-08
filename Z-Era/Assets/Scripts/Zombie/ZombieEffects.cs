using INab.Dissolve;
using UnityEngine;

public class ZombieEffects : MonoBehaviour
{
    [Header("受击特效")]

    [Tooltip("子弹命中的溅血粒子预制体")]
    [SerializeField]
    private ParticleSystem bloodEffectPrefab;

    [Tooltip("匕首命中的溅血粒子预制体，留空时使用子弹溅血粒子")]
    [SerializeField]
    private ParticleSystem knifeHitBloodEffectPrefab;

    [Tooltip("渲染器")]
    [SerializeField]
    private Renderer rend;

    [Tooltip("材质")]
    [SerializeField]
    private Material mat;

    [Header("参数")]

    [Tooltip("子弹溅血特效存活时间")]
    [SerializeField, Min(0.01f)]
    private float bloodEffectLifeTime = 0.5f;

    [Tooltip("匕首溅血特效存活时间")]
    [SerializeField, Min(0.01f)]
    private float knifeHitBloodEffectLifetime = 0.5f;

    [Tooltip("匕首溅血进入敌人身体内部的距离")]
    [SerializeField, Min(0f)]
    private float knifeBloodSurfaceOffset = 0.02f;

    [Tooltip("溶解参数名")]
    private string dissolvePropertyName =
        "_DissolveAmount";

    [Tooltip("溶解总时长")]
    [SerializeField]
    private float dissolveDuration = 4f;

    [Tooltip("溶解已经过去的时间")]
    private float elapsed = 0.5f;

    [Tooltip("是否完全溶解")]
    public bool isFullyDissolved = false;

    // 溶解材质是否已准备（实例化并换到 renderer 上）
    private bool dissolveMaterialPrepared;

    private void Start()
    {
        if (rend == null)
        {
            rend = GetComponentInChildren<Renderer>();
        }

        PrepareDissolveMaterial();
    }

    /// <summary>
    /// 子弹命中时播放溅血效果。
    /// </summary>
    public void PlayHitEffect(
        Vector3 hitPoint,
        Vector3 hitNormal
    )
    {
        if (bloodEffectPrefab == null)
        {
            return;
        }

        Vector3 effectDirection =
            hitNormal.sqrMagnitude > 0.0001f
                ? hitNormal.normalized
                : Vector3.up;

        PlayBloodEffect(
            bloodEffectPrefab,
            hitPoint,
            effectDirection,
            bloodEffectLifeTime
        );
    }

    /// <summary>
    /// 匕首命中时播放溅血效果。
    /// attackDirection 应该是从玩家指向敌人命中点的方向。
    /// </summary>
    public void PlayKnifeHitEffect(
        Vector3 hitPoint,
        Vector3 attackDirection
    )
    {
        ParticleSystem effectPrefab =
            knifeHitBloodEffectPrefab != null
                ? knifeHitBloodEffectPrefab
                : bloodEffectPrefab;

        if (effectPrefab == null)
        {
            return;
        }

        Vector3 direction =
            attackDirection.sqrMagnitude > 0.0001f
                ? attackDirection.normalized
                : Vector3.forward;

        // 向敌人内部偏移，避免粒子生成在模型表面外侧。
        Vector3 spawnPoint =
            hitPoint +
            direction *
            Mathf.Max(0f, knifeBloodSurfaceOffset);

        // 血液朝攻击方向的反方向喷出，也就是朝玩家一侧喷出。
        Vector3 effectDirection = -direction;

        PlayBloodEffect(
            effectPrefab,
            spawnPoint,
            effectDirection,
            knifeHitBloodEffectLifetime
        );
    }

    private void PlayBloodEffect(
        ParticleSystem effectPrefab,
        Vector3 spawnPoint,
        Vector3 effectDirection,
        float lifeTime
    )
    {
        ParticleSystem effect = Instantiate(
            effectPrefab,
            spawnPoint,
            Quaternion.LookRotation(effectDirection)
        );

        if (!effect.main.playOnAwake)
        {
            effect.Play();
        }

        Destroy(
            effect.gameObject,
            Mathf.Max(0.01f, lifeTime)
        );
    }

    // 溶解材质准备（幂等，只做一次），按优先级：
    // 1. 检查器配置了 Mat（对应皮肤的溶解材质）→ 用它；
    // 2. 自动匹配：外壳当前材质名 + "_Dissolve" 到
    //    Resources/ZombieDissolve/ 下查（生成工具的输出命名约定），
    //    查到即用——各皮肤无需任何手动赋值；
    // 3. 兜底：外壳材质本身是溶解材质（如 Father_Male_01 的
    //    历史配置）→ 直接接管。
    // 命中的溶解材质都会实例化一份换到 renderer 上（每僵尸
    // 独立实例，SetFloat 不会改到材质资产影响其它同皮肤僵尸）。
    private void PrepareDissolveMaterial()
    {
        if (dissolveMaterialPrepared)
        {
            return;
        }

        dissolveMaterialPrepared = true;

        if (rend == null)
        {
            rend = GetComponentInChildren<Renderer>();
        }

        if (rend == null)
        {
            return;
        }

        if (mat == null)
        {
            mat = ResolveDissolveMaterialByConvention();
        }

        if (mat != null)
        {
            mat = Instantiate(mat);
            rend.material = mat;
        }
        else
        {
            mat = rend.material;
        }
    }

    // 命名约定自动匹配：外壳当前主材质名 + "_Dissolve"，
    // 到 Resources/ZombieDissolve/ 下加载（生成工具的输出
    // 目录与命名约定）。查不到返回 null，走兜底逻辑。
    private Material ResolveDissolveMaterialByConvention()
    {
        Material skinMaterial = rend.sharedMaterial;

        if (skinMaterial == null)
        {
            return null;
        }

        return Resources.Load<Material>(
            "ZombieDissolve/" +
            skinMaterial.name +
            "_Dissolve"
        );
    }

    /// <summary>
    /// Zombie 死亡后开始溶解。
    /// </summary>
    public void ZombieDissolve()
    {
        // Start 之前就可能被外部调用（组件运行时动态添加等），
        // 这里兜底再准备一次。
        PrepareDissolveMaterial();

        if (mat == null)
        {
            return;
        }

        elapsed += Time.deltaTime;

        float t = Mathf.Clamp01(
            elapsed / Mathf.Max(
                0.01f,
                dissolveDuration
            )
        );

        float dissolveValue =
            Mathf.Lerp(0.5f, 1f, t);

        mat.SetFloat(
            dissolvePropertyName,
            dissolveValue
        );

        if (dissolveValue >= 1f)
        {
            dissolveValue = 1f;
            isFullyDissolved = true;
        }
    }
}