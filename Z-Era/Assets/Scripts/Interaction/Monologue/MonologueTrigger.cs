using UnityEngine;

/// <summary>
/// 自白触发器：两种激活方式——
/// - FlagCondition：每帧检查自白资产的对话标记条件
///   （requiredFlags 全部已设置且 forbiddenFlags 均未设置），
///   条件通过即播放，与玩家位置无关（挂常驻物体上）；
/// - OnTriggerEnter：玩家进入本物体的触发器碰撞体（Is Trigger）
///   时才检查条件并播放。
/// 播放交给 HUD 上的 MonologueUIController。
/// </summary>
public class MonologueTrigger : MonoBehaviour
{
    public enum Activation
    {
        // 标记条件满足即播放（原行为，与位置无关）。
        FlagCondition,

        // 玩家进入触发器范围时才播放。
        OnTriggerEnter
    }

    [Tooltip("要显示的自白资产")]
    [SerializeField]
    private MonologueAsset monologue;

    [Tooltip("触发方式：条件满足即播放（与位置无关）/ 进入触发器时才播放")]
    [SerializeField]
    private Activation activation = Activation.FlagCondition;

    [Tooltip("触发方式为\"进入触发器\"时，只有该 Tag 的碰撞体会触发；留空则不筛选")]
    [SerializeField]
    private string triggerTag = "Player";

    [Tooltip("触发一次后是否不再生效")]
    [SerializeField]
    private bool triggerOnce = true;

    // 标记为已触发要放在条件通过之后：
    // 门槛不满足时保持可重试（与 QuestTrigger 一致）。
    private bool fired;

    private void Awake()
    {
        if (activation != Activation.OnTriggerEnter)
        {
            return;
        }

        if (!HasTriggerCollider())
        {
            Debug.LogWarning(
                "MonologueTrigger：触发方式为\"进入触发器\"，但本物体及其" +
                    "子物体没有勾选 Is Trigger 的碰撞体，" +
                    "玩家走过去不会触发。",
                this
            );
        }
    }

    private void Update()
    {
        // 进入触发模式由 OnTriggerEnter 驱动，不做每帧轮询。
        if (activation != Activation.FlagCondition)
        {
            return;
        }

        TryShow();
    }

    // 进入触发模式：玩家进入范围时判定一次条件。
    // 门槛不满足时保持可重试，但需重新进一次范围才会再次判定
    // （与 QuestTrigger 的 OnTriggerEnter 行为一致）。
    private void OnTriggerEnter(Collider other)
    {
        if (activation != Activation.OnTriggerEnter)
        {
            return;
        }

        if (!string.IsNullOrEmpty(triggerTag) &&
            !other.CompareTag(triggerTag))
        {
            return;
        }

        TryShow();
    }

    // 条件通过且尚未触发时播放一次。
    private void TryShow()
    {
        if (monologue == null)
        {
            return;
        }

        if (triggerOnce && fired)
        {
            return;
        }

        if (!PassesFlagConditions(monologue))
        {
            return;
        }

        fired = true;

        MonologueUIController.Show(monologue);
    }

    // 本物体及其子物体上是否存在勾选 Is Trigger 的碰撞体。
    private bool HasTriggerCollider()
    {
        foreach (
            Collider childCollider in
            GetComponentsInChildren<Collider>(true)
        )
        {
            if (childCollider.isTrigger)
            {
                return true;
            }
        }

        return false;
    }

    // 条件判定（资产内 Required/Forbidden 组合）：本组件与
    // MonologueFlagRelay 共用这一份实现，保证两种挂载方式
    // 的判定语义完全一致。
    public static bool PassesFlagConditions(MonologueAsset asset)
    {
        if (asset.RequiredFlags != null)
        {
            foreach (string flag in asset.RequiredFlags)
            {
                if (!DialogueFlags.HasFlag(flag))
                {
                    return false;
                }
            }
        }

        if (asset.ForbiddenFlags != null)
        {
            foreach (string flag in asset.ForbiddenFlags)
            {
                if (DialogueFlags.HasFlag(flag))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
