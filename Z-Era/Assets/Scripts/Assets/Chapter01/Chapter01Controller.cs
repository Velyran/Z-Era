using UnityEngine;

/// <summary>
/// 第一章线性任务控制器：按对话标记推进场景中的解锁，
/// 例如对讲机对话结束后允许拾取，任务推进到捡枪时放出 Glock；
/// 并在玩家拾取场景 Glock 模型时，把玩家手持 Glock 的弹药
/// 设为检查器里的拾取初始值（默认 20/0）。
/// </summary>
public class Chapter01Controller : MonoBehaviour
{
    // 对讲机对话结束（最后一句播完）时置位的标记，
    // 对应 ch01_dia_william_01_radio.asset 的 End Flag。
    private const string WilliamDialogueEndFlag =
        "ch01_dia_william_01_end";

    // 任务推进到"捡起旁边的枪"时置位的标记，
    // 用于放出 Player 下的 Glock 物体。
    private const string GlockRevealFlag =
        "ch01_tk_01_pickupglock";

    // 听完对讲机旁的自白（mono_04）后置位的标记，
    // 用于解锁通往下一区域的梯子。
    private const string LadderUnlockFlag = "ch01_mono_04";

    [Tooltip("对讲机对话结束后才启用的可拾取组件" +
        "（拖入 Glock 上的 PickableItem）")]
    [SerializeField]
    private PickableItem glockPickable;

    [Tooltip("该标记置位后才启用的场景对象" +
        "（拖入 Player 下的 Glock 物体）")]
    [SerializeField]
    private GameObject glockObject;

    [Tooltip("该标记置位后才启用的梯子组件" +
        "（拖入目标梯子上的 Ladder）")]
    [SerializeField]
    private Ladder ladder;

    [Header("拾取初始弹药（拾取场景模型时写入手持 Glock）")]

    [Tooltip("拾取 Glock 时写入手持枪弹匣的子弹数（Glock 弹匣容量 20）")]
    [SerializeField, Min(0)]
    private int pickupMagazineAmmo = 20;

    [Tooltip("拾取 Glock 时写入手持枪的备弹数")]
    [SerializeField, Min(0)]
    private int pickupReserveAmmo;

    // 手持 Glock 上的 WeaponController，按 glockObject 自动解析并缓存
    private WeaponController cachedGlockWeapon;

    // 拾取发生在手持枪放出之前时的补写标记：
    // 直接写弹药会被随后的 WeaponInitialization（Awake 满弹）覆盖，
    // 记账等放出后补写
    private bool pickupAmmoPending;

    private void Awake()
    {
        // 场景重进时标记可能早已置位，这里直接按当前状态对齐：
        // 对话结束前枪不可交互，任务推进前枪不出现。
        ApplyPickableUnlock(
            DialogueFlags.HasFlag(WilliamDialogueEndFlag)
        );

        ApplyObjectUnlock(
            DialogueFlags.HasFlag(GlockRevealFlag)
        );

        ApplyLadderUnlock(
            DialogueFlags.HasFlag(LadderUnlockFlag)
        );
    }

    private void OnEnable()
    {
        DialogueFlags.FlagSet += OnFlagSet;
    }

    private void OnDisable()
    {
        DialogueFlags.FlagSet -= OnFlagSet;
    }

    private void OnFlagSet(string flagName)
    {
        if (flagName == WilliamDialogueEndFlag)
        {
            ApplyPickableUnlock(true);
            return;
        }

        if (flagName == GlockRevealFlag)
        {
            ApplyObjectUnlock(true);
            return;
        }

        if (flagName == LadderUnlockFlag)
        {
            ApplyLadderUnlock(true);
        }
    }

    // 启用/禁用目标可拾取组件；未赋值时跳过。
    private void ApplyPickableUnlock(bool unlocked)
    {
        if (glockPickable == null)
        {
            return;
        }

        glockPickable.enabled = unlocked;
    }

    /// <summary>
    /// 拾取场景 Glock 模型时调用（挂在该模型 PickableItem 的
    /// On Picked Up / onPickedUp 上，检查器接线）：
    /// 把玩家手持 Glock 的弹药设为上方的拾取初始值（默认 20/0）。
    /// 拾取的场景模型与手持模型是两个物体，弹药写在手持那份上。
    /// </summary>
    public void SetGlockPickupAmmo()
    {
        WeaponController weapon = ResolveGlockWeapon();

        if (weapon == null)
        {
            Debug.LogWarning(
                "Chapter01Controller：找不到手持 Glock 的 WeaponController" +
                    "（Glock Object 未赋值或其上没有该组件），拾取弹药未设置。",
                this
            );
            return;
        }

        // 手持枪还没放出：记账，等 ApplyObjectUnlock 放出时补写
        if (!weapon.gameObject.activeInHierarchy)
        {
            pickupAmmoPending = true;
            return;
        }

        ApplyPickupAmmo(weapon);
    }

    // 按 glockObject 解析手持枪的 WeaponController（含子物体），缓存
    private WeaponController ResolveGlockWeapon()
    {
        if (cachedGlockWeapon == null && glockObject != null)
        {
            cachedGlockWeapon =
                glockObject.GetComponent<WeaponController>();

            if (cachedGlockWeapon == null)
            {
                cachedGlockWeapon =
                    glockObject.GetComponentInChildren<
                        WeaponController
                    >(true);
            }
        }

        return cachedGlockWeapon;
    }

    // 写入拾取初始弹药。此前 WeaponInitialization 已在 Awake 里
    // 按满弹初始化完毕（每实例只跑一次），这里的赋值不会被覆盖；
    // 满匣 + 零备弹 与 HandleAmmo 的换弹守门自洽，每帧也不会被改。
    private void ApplyPickupAmmo(WeaponController weapon)
    {
        if (weapon == null)
        {
            return;
        }

        weapon.currentMagazineAmmo = pickupMagazineAmmo;
        weapon.currentCarriedAmmo = pickupReserveAmmo;
    }

    // 显示/隐藏目标物体；未赋值时跳过。
    private void ApplyObjectUnlock(bool unlocked)
    {
        if (glockObject == null)
        {
            return;
        }

        glockObject.SetActive(unlocked);

        // 拾取先于手持枪放出时记的账：放出瞬间 WeaponController
        // 的 Awake（满弹初始化）已同步执行完，此刻补写才不会被覆盖
        if (unlocked && pickupAmmoPending)
        {
            pickupAmmoPending = false;
            ApplyPickupAmmo(ResolveGlockWeapon());
        }
    }

    // 启用/禁用梯子组件；未赋值时跳过。
    // 注意：禁用的是组件而不是物体，梯子的碰撞体仍在，
    // 由 PlayerLadderClimb 检查 ladder.enabled 拦截攀爬。
    private void ApplyLadderUnlock(bool unlocked)
    {
        if (ladder == null)
        {
            return;
        }

        ladder.enabled = unlocked;
    }
}
