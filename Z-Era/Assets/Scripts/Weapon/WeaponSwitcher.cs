using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家换枪：按检查器配置的键位，切换 WeaponHolder 下已解锁的
/// 手持武器（Player 下每把枪一个独立物体，各带 WeaponController，
/// 多把同时激活会互相抢输入，同一时间只能有一把激活）。
/// "使用哪把枪，哪把枪才启用"由本组件保证：切换时停旧启新。
/// - 解锁判定：按条目里的解锁标记（DialogueFlags）——拥有但
///   未在用的枪平时处于禁用状态，activeSelf 不能表达"已拥有"；
///   标记留空则回退为按武器物体激活状态判定（测试用）；
/// - 切换流程：当前武器播放 HolsterWeapon 收枪动画，播完整体禁用，
///   再启用目标武器（Animator 重置，WeaponController.OnEnable 里
///   已写好层权重与首帧姿势处理，自动从 TakeOutWeapon 掏枪开始）；
/// - 弹药各自保留：WeaponController 实例停用时字段值不清空，
///   不调用 RefillAmmo（训练场的回满是教学语义，章节里不适用）；
/// - 对话/演出进行中、玩家移动被剧情锁定、鼠标未锁定（背包/设置
///   打开）、切换中重入时不触发。
/// HUD 由 WeaponHUD 每帧重读当前激活武器自动同步；切枪音效由
/// WeaponEffects 监听动画状态自动播放，均无需额外接线。
/// </summary>
public class WeaponSwitcher : MonoBehaviour
{
    /// <summary>一个按键 → 武器的映射条目。</summary>
    [Serializable]
    public class WeaponSlot
    {
        [Tooltip("触发切换到该武器的按键")]
        public KeyCode key = KeyCode.Alpha1;

        [Tooltip("WeaponHolder 下手持武器的物体名（须与 GunType " +
            "枚举成员同名，如 Vector / Glock）")]
        public string weaponName;

        [Tooltip("该武器的解锁标记：标记已置位才允许切到这把枪" +
            "（拾取/剧情流程置位）。留空则回退为按武器物体是否激活" +
            "判定（适合临时测试场景手动激活武器）")]
        public string unlockFlag;
    }

    [Header("键位映射")]

    [Tooltip("按键 → 武器的映射列表：按对应键切换到该武器；" +
        "习惯 1 主武器 2 副武器时按喜好填写即可")]
    [SerializeField]
    private List<WeaponSlot> slots = new List<WeaponSlot>();

    [Header("引用")]

    [Tooltip("玩家手持武器模型容器（Player 下各武器模型的父物体）；" +
        "留空则运行时从场景中任意 WeaponController 的父级自动解析")]
    [SerializeField]
    private Transform weaponHolder;

    // 收枪切枪进行中：期间忽略新的切换请求，防重入。
    private bool switching;

    // 场景启动时处于激活状态的武器名集合：unlockFlag 留空的回退
    // 判定用。切换会停用旧枪，若直接按"当前是否激活"判定，
    // 切走一次后就永远"未解锁"、切不回来；改为以开局激活状态
    // 为准，整个会话内保持可切换。
    private readonly HashSet<string> initiallyActiveWeapons =
        new HashSet<string>();

    // 玩家控制组件：判断"玩家移动是否被剧情锁定"用
    private PlayerController cachedPlayerController;

    // 背包容器 InventoryContainer：拾取的世界武器模型会存到这里
    //（场景内常驻），作为"已拥有"的判定来源
    private Transform cachedInventoryContainer;

    private void Start()
    {
        // 一次性状态打印：确认容器解析与各武器的激活状态。
        Transform holder = ResolveHolder();

        if (holder == null)
        {
            Debug.LogWarning(
                "WeaponSwitcher：未解析到 WeaponHolder（检查器未赋值" +
                    "且场景里找不到 WeaponController）。",
                this
            );

            return;
        }

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        builder.Append("WeaponSwitcher：WeaponHolder='")
            .Append(holder.name)
            .Append("'，子物体：");

        foreach (Transform child in holder)
        {
            builder.Append(child.name)
                .Append(child.gameObject.activeSelf ? "(激活) " : "(禁用) ");

            if (child.gameObject.activeSelf)
            {
                initiallyActiveWeapons.Add(child.name);
            }
        }

        Debug.Log(builder.ToString(), this);
    }

    private void Update()
    {
        if (slots == null ||
            slots.Count == 0 ||
            switching)
        {
            return;
        }

        foreach (WeaponSlot slot in slots)
        {
            if (slot == null ||
                string.IsNullOrWhiteSpace(slot.weaponName) ||
                !Input.GetKeyDown(slot.key))
            {
                continue;
            }

            Transform holder = ResolveHolder();
            Transform target = ResolveWeapon(slot.weaponName);
            Transform current = FindCurrentWeapon();
            Transform container = ResolveInventoryContainer();

            // 按键瞬间的完整链路状态，便于排查按键无反应。
            Debug.Log(
                "WeaponSwitcher：按键 " + slot.key +
                    " → '" + slot.weaponName + "'" +
                    " | Holder=" +
                    (holder == null ? "未解析" : holder.name) +
                    " | Target=" +
                    (target == null
                        ? "未找到"
                        : target.gameObject.activeSelf
                            ? "激活"
                            : "禁用") +
                    " | UnlockFlag='" + slot.unlockFlag + "'" +
                    (string.IsNullOrWhiteSpace(slot.unlockFlag)
                        ? ""
                        : " | Flag已置位=" +
                            DialogueFlags.HasFlag(slot.unlockFlag)) +
                    " | 库存中有=" +
                    (container == null
                        ? "无容器"
                        : container.Find(slot.weaponName) != null
                            ? "是"
                            : "否") +
                    " | Current=" +
                    (current == null ? "无" : current.name) +
                    " | 操作锁定=" + IsGameplayLocked(),
                this
            );

            // 锁定期间不切枪：对话/演出进行中、玩家移动被剧情
            // 锁定、鼠标未锁定（背包/设置打开）。
            if (IsGameplayLocked())
            {
                return;
            }

            if (target == null || !IsSlotUnlocked(slot, target))
            {
                // 武器不存在或未解锁：按键无效
                return;
            }

            // 按的就是当前手里的枪：无事发生
            if (current == target)
            {
                return;
            }

            // 匕首攻击动画播放期间不允许切换。
            if (IsKnifeAttacking(current))
            {
                Debug.Log(
                    "WeaponSwitcher：匕首攻击动画播放中，" +
                        "忽略切枪请求。",
                    this
                );

                return;
            }

            // 换弹动画播放期间不允许切换。
            if (IsReloadingMidway(current))
            {
                Debug.Log(
                    "WeaponSwitcher：换弹动画播放中，" +
                        "忽略切枪请求。",
                    this
                );

                return;
            }

            StartCoroutine(SwitchWeapon(current, target));

            return;
        }
    }

    // 玩家操作是否被外部锁定：DialogueRunner 收尾瞬间状态可能
    // 已回 Idle 但锁还没解，movementLocked 与鼠标状态兜住这些间隙。
    private bool IsGameplayLocked()
    {
        if (DialogueRunner.Instance != null &&
            DialogueRunner.Instance.CurrentState !=
                DialogueRunner.State.Idle)
        {
            return true;
        }

        if (cachedPlayerController == null)
        {
            cachedPlayerController =
                GetComponentInParent<PlayerController>();
        }

        if (cachedPlayerController != null &&
            cachedPlayerController.movementLocked)
        {
            return true;
        }

        return Cursor.lockState != CursorLockMode.Locked;
    }

    // 解析武器容器：优先用检查器赋值，否则从场景中任意
    // WeaponController 的父级解析（每把枪的父物体就是容器）。
    private Transform ResolveHolder()
    {
        if (weaponHolder != null)
        {
            return weaponHolder;
        }

        WeaponController anyWeapon =
            FindObjectOfType<WeaponController>(true);

        if (anyWeapon != null)
        {
            weaponHolder = anyWeapon.transform.parent;
        }

        return weaponHolder;
    }

    // 按名字查找武器：Transform.Find 能找到未激活的子物体。
    private Transform ResolveWeapon(string weaponName)
    {
        Transform holder = ResolveHolder();

        if (holder == null)
        {
            return null;
        }

        return holder.Find(weaponName);
    }

    // 条目是否已解锁，按优先级：
    // 1. 配置了解锁标记 → 按 DialogueFlags 判定（剧情显式控制）；
    // 2. 背包容器里有同名物品 → 已拥有（拾取的武器模型会存进
    //    InventoryContainer，场景内常驻；Transform.Find 能找到
    //    入包后被停用的物品）；
    // 3. 兜底：武器物体激活中，或开局时激活过（纯测试场景用手）。
    private bool IsSlotUnlocked(WeaponSlot slot, Transform target)
    {
        if (!string.IsNullOrWhiteSpace(slot.unlockFlag))
        {
            return DialogueFlags.HasFlag(slot.unlockFlag);
        }

        Transform container = ResolveInventoryContainer();

        if (container != null &&
            container.Find(slot.weaponName) != null)
        {
            return true;
        }

        return target.gameObject.activeSelf ||
               initiallyActiveWeapons.Contains(slot.weaponName);
    }

    // 解析背包容器：按名称查找（含未激活物体，
    // InventoryContainer 初始就是禁用状态），结果缓存。
    private Transform ResolveInventoryContainer()
    {
        if (cachedInventoryContainer != null)
        {
            return cachedInventoryContainer;
        }

        foreach (
            GameObject go in
            UnityEngine.Object.FindObjectsOfType<GameObject>(true)
        )
        {
            if (go != null && go.name == "InventoryContainer")
            {
                cachedInventoryContainer = go.transform;
                break;
            }
        }

        return cachedInventoryContainer;
    }

    // 当前手里的枪：WeaponHolder 下已激活且带 WeaponController
    // 的子物体（同一时间只激活一把）。
    private Transform FindCurrentWeapon()
    {
        Transform holder = ResolveHolder();

        if (holder == null)
        {
            return null;
        }

        foreach (Transform child in holder)
        {
            if (!child.gameObject.activeSelf)
            {
                continue;
            }

            if (child.GetComponentInChildren<WeaponController>(true)
                    != null)
            {
                return child;
            }
        }

        return null;
    }

    // 切换流程：当前武器播收枪动画 → 播完整体禁用 → 启用目标武器。
    // 当前枪为空（理论不出现）或没有动画机时直接切换，不播收枪。
    private IEnumerator SwitchWeapon(
        Transform current,
        Transform target
    )
    {
        switching = true;

        Debug.Log(
            "WeaponSwitcher：开始切换 " +
                (current == null ? "无" : current.name) +
                " → " + target.name,
            this
        );

        if (current != null)
        {
            yield return PlayHolster(current);
        }

        // 旧枪整体停用、新枪启用：WeaponController 的 OnEnable
        // 会写好层权重并推进动画机，从掏枪动画开始。
        if (current != null)
        {
            current.gameObject.SetActive(false);
        }

        target.gameObject.SetActive(true);

        Debug.Log(
            "WeaponSwitcher：切换完成，当前武器=" + target.name,
            this
        );

        switching = false;
    }

    // 在当前武器上播放 HolsterWeapon 并等它播完。
    // 状态机没有任何 transition 入口，外部 Play 是唯一进入方式；
    // WeaponController.Update 检测到该状态时会屏蔽开火等输入，
    // 动画不会被自己打断。 WeaponEffects 会在进入状态的瞬间
    // 自动播放收枪音效。
    private IEnumerator PlayHolster(Transform weapon)
    {
        WeaponController controller =
            weapon.GetComponentInChildren<WeaponController>(true);

        if (controller == null)
        {
            yield break;
        }

        Animator animator = controller.GetComponent<Animator>();

        if (animator == null)
        {
            yield break;
        }

        // 收枪状态与武器动画在同一层：层号 = 枪型序号 + 1
        //（0 层为基础层），与 WeaponController.AnimatorController
        // 里的取法保持一致。
        int layer = (int)controller.CurrentGunData.gunType + 1;

        animator.Play("HolsterWeapon", layer, 0f);

        // Play 到动画机下一次评估才生效：第一帧查状态可能还是
        // 旧状态，会被误判成"已播完"导致瞬切。先等状态真正进入
        // HolsterWeapon（超时兜底，状态机异常时不至于卡死切枪）。
        float enterDeadline = Time.time + 1f;

        while (weapon != null && weapon.gameObject.activeSelf)
        {
            if (animator.GetCurrentAnimatorStateInfo(layer)
                    .IsName("HolsterWeapon"))
            {
                break;
            }

            if (Time.time >= enterDeadline)
            {
                yield break;
            }

            yield return null;
        }

        // 已进入收枪状态：等 normalizedTime 到 1 即播完；
        // 武器中途被禁用/销毁则直接结束等待。
        while (weapon != null && weapon.gameObject.activeSelf)
        {
            AnimatorStateInfo state =
                animator.GetCurrentAnimatorStateInfo(layer);

            if (!state.IsName("HolsterWeapon") ||
                state.normalizedTime >= 1f)
            {
                yield break;
            }

            yield return null;
        }
    }

    // 当前武器是否正在播放匕首攻击动画（含正在切入该状态的
    // 过渡）：匕首攻击期间不允许切枪。
    private bool IsKnifeAttacking(Transform weapon)
    {
        WeaponController controller =
            weapon.GetComponentInChildren<WeaponController>(true);

        if (controller == null)
        {
            return false;
        }

        Animator animator = controller.GetComponent<Animator>();

        if (animator == null)
        {
            return false;
        }

        // 武器动画与枪型在同一层：层号 = 枪型序号 + 1（0 层为基础层）
        int layer = (int)controller.CurrentGunData.gunType + 1;

        AnimatorStateInfo state =
            animator.GetCurrentAnimatorStateInfo(layer);

        if (state.IsName("KnifeAttack") &&
            state.normalizedTime < 1f)
        {
            return true;
        }

        return animator.IsInTransition(layer) &&
               animator.GetNextAnimatorStateInfo(layer)
                   .IsName("KnifeAttack");
    }

    // 武器是否还在换弹动画中途（normalizedTime 未到 1）：
    // 换弹期间不允许切枪，弹药状态天然保持一致。
    private bool IsReloadingMidway(Transform weapon)
    {
        WeaponController controller =
            weapon.GetComponentInChildren<WeaponController>(true);

        if (controller == null)
        {
            return false;
        }

        Animator animator = controller.GetComponent<Animator>();

        if (animator == null)
        {
            return false;
        }

        int layer = (int)controller.CurrentGunData.gunType + 1;

        AnimatorStateInfo state =
            animator.GetCurrentAnimatorStateInfo(layer);

        return state.normalizedTime < 1f &&
               (state.IsName("ReloadLeftAmmo") ||
                state.IsName("ReloadOutOfAmmo"));
    }
}
