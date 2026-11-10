using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ZombieController : MonoBehaviour,
    IDamageable
{
    #region 属性

    [Tooltip("生命值")]
    private float health = 100f;

    #endregion

    #region 参数

    [Tooltip("是否已经死亡")]
    private bool isDead = false;

    [Tooltip("防止死亡清理逻辑重复执行")]
    private bool deathCleanupStarted = false;

    #endregion

    #region 组件

    [Tooltip("动画器")]
    private Animator animator;

    [Tooltip("角色控制器")]
    private CharacterController characterController;

    #endregion

    #region 引用

    private ZombieEffects zombieEffects;

    #endregion

    public bool CanTakeDamage =>
        !isDead &&
        health > 0f;

    private void Start()
    {
        animator = GetComponent<Animator>();

        characterController =
            GetComponent<CharacterController>();

        zombieEffects =
            GetComponent<ZombieEffects>();
    }

    private void Update()
    {
        if (health <= 0f && !isDead)
        {
            health = 0f;
            isDead = true;

            // 死亡后不再需要移动和碰撞计算。
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            // 死亡瞬间就移除伤害判定盒（子物体上的 Trigger 胶囊），
            // 不等 Die 动画播完：尸体不再参与瞄准与命中判断，
            // 否则贴脸时准星射线会先命中尸体的判定盒（0.x 米处），
            // 子弹会朝这个极近的点飞，弹道明显偏离准星。
            DestroyHitboxTriggers();
        }

        ZombieAnimation();

        if (!isDead)
        {
            return;
        }

        ClearComponents();
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        // 死亡或已经归零的 Zombie 不再接受伤害。
        if (!CanTakeDamage ||
            damageInfo.damage <= 0f)
        {
            return;
        }

        float damageMultiplier =
            GetDamageMultiplier(
                damageInfo.hitPart
            );

        float finalDamage =
            damageInfo.damage *
            damageMultiplier;

        health = Mathf.Max(
            0f,
            health - finalDamage
        );
    }

    private float GetDamageMultiplier(
        HitPart hitPart
    )
    {
        switch (hitPart)
        {
            case HitPart.Head:
                return 2f;

            case HitPart.Body:
                return 1f;

            case HitPart.Arm_L:
            case HitPart.Arm_R:
            case HitPart.Knee_L:
            case HitPart.Knee_R:
            case HitPart.Leg:
                return 0.8f;

            case HitPart.Feet:
                return 0.5f;

            default:
                return 1f;
        }
    }

    private void ZombieAnimation()
    {
        if (health <= 0f)
        {
            animator.SetBool("die", isDead);
        }
    }

    // 销毁子物体上的伤害判定盒（Trigger 胶囊），保留根物体与
    // ZombieHearing 层（听觉感知）的碰撞体。可重复调用：已销毁的
    // 不会再被收集到。
    private void DestroyHitboxTriggers()
    {
        int hearingLayer =
            LayerMask.NameToLayer(
                "ZombieHearing"
            );

        Collider[] triggers =
            GetComponentsInChildren<Collider>(
                includeInactive: true
            )
            .Where(c =>
                c != null &&
                c.isTrigger &&
                c.transform != transform &&
                (
                    hearingLayer < 0 ||
                    c.gameObject.layer !=
                        hearingLayer
                )
            )
            .ToArray();

        foreach (Collider trigger in triggers)
        {
            if (trigger != null)
            {
                Destroy(trigger);
            }
        }
    }

    private void ClearComponents()
    {
        AnimatorStateInfo info =
            animator.GetCurrentAnimatorStateInfo(0);

        if (!info.IsName("Die"))
        {
            return;
        }

        if (info.normalizedTime < 1f ||
            animator.IsInTransition(0))
        {
            return;
        }

        if (!deathCleanupStarted)
        {
            deathCleanupStarted = true;

            // 通常死亡瞬间已经清过了，这里兜底再清一次（幂等）。
            DestroyHitboxTriggers();
        }

        if (zombieEffects != null)
        {
            zombieEffects.ZombieDissolve();
        }

        if (zombieEffects != null &&
            zombieEffects.isFullyDissolved)
        {
            Destroy(gameObject);
        }
    }
}