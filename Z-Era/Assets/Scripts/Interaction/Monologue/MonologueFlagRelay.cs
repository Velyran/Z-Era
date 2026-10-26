using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 自白批量触发器：一个组件集中挂载多段自白，每帧轮询各资产的
/// 触发条件——条件一律读资产内部的 Required/Forbidden Flags 组合
/// （判定复用 MonologueTrigger.PassesFlagConditions，语义与逐个
/// 挂 MonologueTrigger 完全一致），本组件只是把"一段一个组件"
/// 收敛成一张清单，条件仍全部在资产里改、组件不用动。
///
/// 要点：
/// · 挂场景常驻物体（如 HUD Canvas），表里按需增删资产；
/// · triggerOnce 按条目锁存（内存态，场景重载后重置）；
/// · 与 MonologueTrigger 相同：场景载入时条件若已满足会立即触发，
///   没有"只响应新置位"的基线过滤；
/// · 同一资产只用本组件或 MonologueTrigger 其中之一，两边都挂会
///   因"新自白顶掉旧自白"而重启打字机；
/// · 同屏只显示一条：新自白顶掉旧的（MonologueUIController 行为）。
/// </summary>
public class MonologueFlagRelay : MonoBehaviour
{
    /// <summary>一条挂载：一份自白资产 + 一次性锁存。</summary>
    [Serializable]
    public class MonologueEntry
    {
        [Tooltip("自白资产：触发条件读它内部的 Required/Forbidden Flags")]
        public MonologueAsset monologue;

        [Tooltip("触发一次后本条目不再生效")]
        public bool triggerOnce = true;

        // 运行时锁存，不写入场景
        [NonSerialized]
        public bool fired;
    }

    [Header("自白清单")]

    [Tooltip("集中挂载的自白：触发条件一律读各资产内部的 " +
        "Required/Forbidden Flags（与逐个挂 MonologueTrigger 等价），" +
        "这里只管挂哪些、弹没弹过")]
    [SerializeField]
    private List<MonologueEntry> entries = new List<MonologueEntry>();

    private void Update()
    {
        if (entries == null)
        {
            return;
        }

        foreach (MonologueEntry entry in entries)
        {
            if (entry == null ||
                entry.monologue == null ||
                (entry.triggerOnce && entry.fired))
            {
                continue;
            }

            if (!MonologueTrigger.PassesFlagConditions(entry.monologue))
            {
                continue;
            }

            entry.fired = true;

            MonologueUIController.Show(entry.monologue);
        }
    }

    private void OnEnable()
    {
        ValidateEntries();
    }

    // 启用时一次性校验：清单残缺时明确告警，
    // 不等"静默不弹"再回头排查
    private void ValidateEntries()
    {
        if (entries == null || entries.Count == 0)
        {
            Debug.LogWarning(
                "MonologueFlagRelay：清单为空，不会触发任何自白。",
                this
            );
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            MonologueEntry entry = entries[i];

            if (entry == null || entry.monologue == null)
            {
                Debug.LogWarning(
                    "MonologueFlagRelay：第 " + i +
                        " 条没有指定自白资产，已跳过。",
                    this
                );
            }
        }
    }
}
