using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 僵尸分区激活触发器：挂在楼门口的触发器碰撞体上
/// （BoxCollider 勾选 Is Trigger）。匹配 Tag 的碰撞体进入时，
/// 把检查器里配置的僵尸组（整楼僵尸挂在初始禁用的空物体下）
/// 逐组整体激活——僵尸的 AI、动画、碰撞全部随之启动，
/// 玩家从此可以攻击它们；未激活时它们完全不存在于物理世界。
/// 激活是幂等的：玩家身上有多个碰撞体（CharacterController、
/// 武器触发器等），OnTriggerEnter 会进入多次，重复触发无副作用。
/// 全部组激活完后禁用自身，整个区域只触发一次。
/// 可选地在触发时置位一个 DialogueFlags 标记，供剧情/任务判定。
/// </summary>
public class ZombieZoneActivator : MonoBehaviour
{
    [Header("激活目标")]

    [Tooltip("进入触发器时要整体激活的僵尸组（每组是该楼所有" +
        "僵尸实例的父空物体，初始保持禁用状态）")]
    [SerializeField]
    private List<GameObject> groupsToActivate =
        new List<GameObject>();

    [Header("触发条件")]

    [Tooltip("进入触发器的物体必须具有的 Tag，玩家根物体通常为 " +
        "Player")]
    [SerializeField]
    private string triggerTag = "Player";

    [Header("剧情联动")]

    [Tooltip("触发激活时置位的标记（供任务/对话条件判定），" +
        "留空则不设置")]
    [SerializeField]
    private string activateFlag;

    // 已激活的组：防止同一次停留内 OnTriggerEnter 多次进入
    //（玩家身上多个碰撞体）重复执行。
    private readonly HashSet<GameObject> activatedGroups =
        new HashSet<GameObject>();

    private void Awake()
    {
        Collider zoneCollider = GetComponent<Collider>();

        if (zoneCollider == null || !zoneCollider.isTrigger)
        {
            Debug.LogWarning(
                "ZombieZoneActivator：本物体没有勾选 Is Trigger 的" +
                    "碰撞体，玩家无法进入触发范围。",
                this
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(triggerTag) &&
            !other.CompareTag(triggerTag))
        {
            return;
        }

        if (groupsToActivate == null)
        {
            return;
        }

        foreach (GameObject group in groupsToActivate)
        {
            if (group == null || activatedGroups.Contains(group))
            {
                continue;
            }

            activatedGroups.Add(group);

            group.SetActive(true);

            Debug.Log(
                "ZombieZoneActivator：已激活僵尸组 '" +
                    group.name + "'。",
                this
            );
        }

        // 触发激活时置位剧情标记；留空则不设置，
        // 重复置位由 DialogueFlags 自身去重。
        if (!string.IsNullOrWhiteSpace(activateFlag))
        {
            DialogueFlags.SetFlag(activateFlag, true);
        }

        // 全部组都激活完：本区域只触发一次，禁用自身不再响应。
        if (groupsToActivate.Count > 0 &&
            activatedGroups.Count >= groupsToActivate.Count)
        {
            enabled = false;
        }
    }
}
