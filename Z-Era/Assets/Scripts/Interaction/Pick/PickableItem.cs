using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 可拾取物品的分类：分类决定拾取行为与描边颜色，
// 在检查器中按物品类型选择。
public enum PickupCategory
{
    // 可拾取道具：钥匙等，拾取后进入背包（InventoryContainer），
    // 可打开库存查看，描边绿色。
    Prop,

    // 武器：AK47 等，拾取后进入背包，可打开库存查看，描边蓝色。
    Weapon,

    // 弹药箱：拾取（交互）后不进背包也不消失，补满玩家身上
    // 所有武器的弹药，描边红色。分可交互指定次数与无限交互两种。
    AmmoBox,

    // 弹匣：拾取后不进背包但本体消失，按枪支名称索引对应武器，
    // 把弹药数加进备弹（不补满），描边红色。
    Magazine,

    // 特殊收集品：后续规划，拾取后进入背包可查看，描边金色。
    SpecialCollectible
}

public class PickableItem : MonoBehaviour
{
    private static readonly List<PickableItem>
        registeredItems = new List<PickableItem>();

    // 当前场景中所有已激活的可拾取道具，供控制器遍历，避免全场扫描。
    public static IReadOnlyList<PickableItem> RegisteredItems
    {
        get { return registeredItems; }
    }

    [Header("分类")]

    [Tooltip("物品分类：决定拾取行为与描边颜色（道具绿/武器蓝/弹药箱与弹匣红/特殊收集品金）")]
    public PickupCategory category = PickupCategory.Prop;

    [Tooltip("覆盖全局的单件交互距离（米），0 表示使用 PickupController 的默认交互距离")]
    public float interactionDistanceOverride;

    [Header("显示")]

    [Tooltip("瞄准该物体时在交互提示里显示的物品名；留空则回退到 InventoryItemInfo 的显示名，再回退到物体名")]
    [SerializeField]
    private string displayName;

    // 瞄准该物体时显示的物品名，供 PickupController 读取；
    // 未配置时为空串，由调用方决定回退。
    public string DisplayName
    {
        get { return displayName; }
    }

    [Header("库存设置（分类为进背包的类型时生效：道具/武器/特殊收集品）")]

    [Tooltip("进库存时写入的初始旋转（欧拉角，相对背包容器）。默认值即原全局写死的 (0, -90, -90)，不改则表现与从前一致")]
    public Vector3 inventoryInitialRotation = new Vector3(0f, -90f, -90f);

    [Header("弹药箱设置（分类为 AmmoBox 时生效）")]

    [Tooltip("勾选后可无限次交互（新手教程无限补弹用）；取消勾选则按下面的可交互次数限制")]
    public bool unlimitedInteractions;

    [Tooltip("可交互次数（关闭无限交互时生效），每次交互补满全部武器弹药，耗尽后该物品不再可交互")]
    [Min(1)]
    public int maxInteractions = 1;

    [Header("弹匣设置（分类为 Magazine 时生效）")]

    [Tooltip("补充哪把武器的弹药，按玩家身上武器物体名称索引（如 Vector）；留空则从弹匣物体名称中推断（名称含枪支名即可，如 Vector_Magazine）")]
    public string weaponName;

    [Tooltip("弹药数（仅分类为 Magazine 时生效）：拾取后把该数量加到对应武器的备弹（不进背包、不直接补满；超出备弹上限的部分由武器每帧钳掉）")]
    [Min(0)]
    [SerializeField]
    private int magazineAmmo;

    [Header("拾取效果")]

    [Tooltip("拾取成功时自动设置的对话标记（写入 DialogueFlags），供对话/任务条件判定，如 Task01_Done")]
    [SerializeField]
    private string[] flagsToSetOnPickup;

    [Tooltip("拾取成功时上报进度的任务 Id（QuestManager），留空则不上报")]
    [SerializeField]
    private string questId;

    [Tooltip("拾取成功时上报进度的目标 Objective Id，需与任务资产中配置一致；questId 为空时忽略")]
    [SerializeField]
    private string objectiveId;

    [Tooltip("拾取成功时为该目标增加的进度数量")]
    [Min(1)]
    [SerializeField]
    private int progressAmount = 1;

    [Tooltip("拾取成功时触发，例如接 QuestManager.AddProgress 上报任务进度（参数在检查器里填）")]
    [SerializeField]
    private UnityEvent onPickedUp;

    // 弹药箱剩余可交互次数，OnEnable 时重置为 maxInteractions。
    public int RemainingInteractions
    {
        get { return remainingInteractions; }
    }

    // 弹匣拾取时加进备弹的弹药数（仅 Magazine 分类生效）。
    public int MagazineAmmo
    {
        get { return magazineAmmo; }
    }

    private int remainingInteractions;

    // 各分类的描边颜色（Alpha 为呼吸最满时的不透明度）。
    public Color OutlineColor
    {
        get
        {
            switch (category)
            {
                case PickupCategory.Weapon:
                    return new Color(0.25f, 0.6f, 1f, 1f);
                case PickupCategory.AmmoBox:
                case PickupCategory.Magazine:
                    return new Color(1f, 0.25f, 0.2f, 1f);
                case PickupCategory.SpecialCollectible:
                    return new Color(1f, 0.8f, 0f, 1f);
                default:
                    return new Color(0f, 1f, 0.4f, 1f);
            }
        }
    }

    // 分类为 Magazine 但弹药数为 0：拾取将不增加任何备弹，
    // 在检查器里直接告警提示补填，不等拾取时才发现
    private void OnValidate()
    {
        if (category == PickupCategory.Magazine &&
            magazineAmmo <= 0)
        {
            Debug.LogWarning(
                "PickableItem：分类为 Magazine 但 Magazine Ammo 为 0，" +
                    "拾取后不会增加任何备弹。",
                this
            );
        }
    }

    private void Awake()
    {
        ResetInteractions();
    }

    private void OnEnable()
    {
        if (!registeredItems.Contains(this))
        {
            registeredItems.Add(this);
        }

        // 弹药箱次数在每次启用时重置，支持剧情阶段重复启用。
        ResetInteractions();
    }

    private void OnDisable()
    {
        registeredItems.Remove(this);
    }

    // 弹药箱消耗一次交互次数，耗尽后禁用自身（退出注册表，
    // 不再高亮也不可交互）。无限交互时无副作用。
    public void ConsumeInteraction()
    {
        if (unlimitedInteractions)
        {
            return;
        }

        remainingInteractions--;

        if (remainingInteractions <= 0)
        {
            enabled = false;
        }
    }

    // 解析弹匣对应的武器名称：优先取检查器里填的 weaponName，
    // 否则从弹匣物体名称中推断（包含枪支枚举名即可）。
    public string ResolveMagazineWeaponName()
    {
        if (!string.IsNullOrWhiteSpace(weaponName))
        {
            return weaponName.Trim();
        }

        string selfName = gameObject.name;

        foreach (
            GunType gunType in
                System.Enum.GetValues(typeof(GunType))
        )
        {
            string gunName = gunType.ToString();

            if (selfName.Contains(gunName))
            {
                return gunName;
            }
        }

        return selfName;
    }

    // 当前是否可被交互（高亮与拾取共用的门槛）：
    // 非 Magazine 分类恒可用；Magazine 只有库存容器
    // （InventoryContainer）下已存在与本弹匣对应（名称匹配规则与
    // weaponName 字段一致：解析出的武器名与子物体名精确相等，或
    // 子物体名包含该武器名）的武器时才可用——容器缺失（场景没有
    // 库存系统）或武器尚未入包时一律不可用。
    public bool IsAvailable(Transform inventoryContainer)
    {
        if (category != PickupCategory.Magazine)
        {
            return true;
        }

        if (inventoryContainer == null)
        {
            return false;
        }

        string weaponName = ResolveMagazineWeaponName();

        foreach (Transform child in inventoryContainer)
        {
            if (child != null &&
                (child.name == weaponName ||
                 child.name.Contains(weaponName)))
            {
                return true;
            }
        }

        return false;
    }

    // 由 PickupController 在拾取成功时调用。
    // 在道具入包/销毁/禁用前触发，监听方仍可访问道具本体。
    public void NotifyPickedUp()
    {
        if (flagsToSetOnPickup != null)
        {
            foreach (string flagName in flagsToSetOnPickup)
            {
                DialogueFlags.SetFlag(flagName, true);
            }
        }

        // 任务进度上报：questId/objectiveId 任一为空则跳过
        // （不是所有物品都挂任务），其余由 QuestManager 校验告警
        if (!string.IsNullOrWhiteSpace(questId) &&
            !string.IsNullOrWhiteSpace(objectiveId))
        {
            QuestManager.AddProgress(
                questId,
                objectiveId,
                progressAmount
            );
        }

        onPickedUp?.Invoke();
    }

    private void ResetInteractions()
    {
        remainingInteractions = maxInteractions;
    }

    // 查询道具当前是否处于注册状态（已激活且可拾取）。
    public static bool IsRegistered(PickableItem item)
    {
        return item != null && registeredItems.Contains(item);
    }

    // 从射线命中的碰撞体向上查找所属的可拾取道具。
    public static PickableItem FromCollider(Collider collider)
    {
        return collider != null
            ? collider.GetComponentInParent<PickableItem>()
            : null;
    }
}
