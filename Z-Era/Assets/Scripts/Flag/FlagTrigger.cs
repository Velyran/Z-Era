using UnityEngine;

/// <summary>
/// 标记触发器：玩家进入本物体或其子物体上勾选了
/// Is Trigger 的碰撞体时，把指定标记置为 true，
/// 供对话/任务/演出等系统按标记条件判定。
/// </summary>
public class FlagTrigger : MonoBehaviour
{
    [Tooltip("进入范围时置为 true 的标记名，必填")]
    [SerializeField]
    private string flagName;

    [Tooltip("只有该 Tag 的碰撞体会触发；留空则不筛选")]
    [SerializeField]
    private string triggerTag = "Player";

    [Tooltip("触发一次后是否不再生效")]
    [SerializeField]
    private bool triggerOnce = true;

    // 已触发过；triggerOnce 关闭时无意义。
    private bool fired;

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            Debug.LogWarning(
                "FlagTrigger：Flag Name 为空，本组件不会设置任何标记。",
                this
            );
        }

        if (!HasTriggerCollider())
        {
            Debug.LogWarning(
                "FlagTrigger：本物体及其子物体没有勾选 Is Trigger " +
                    "的碰撞体，玩家走过去不会触发。",
                this
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && fired)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(flagName))
        {
            return;
        }

        if (!string.IsNullOrEmpty(triggerTag) &&
            !other.CompareTag(triggerTag))
        {
            return;
        }

        fired = true;

        DialogueFlags.SetFlag(flagName, true);
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
}
