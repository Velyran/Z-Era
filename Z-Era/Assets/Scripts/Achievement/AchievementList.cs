using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 成就列表面板控制器：统一刷新成就面板下所有 AchievementEntry。
/// 挂在存储成就 UI 组件的空物体上（成就面板的直接子物体）。
///
/// 用法：打开成就面板时调用 RefreshAll()（可接到打开按钮的
/// onClick 上）；也可勾选 Refresh On Enable 让面板每次显示时自动刷新。
///
/// 后续新增成就：在面板下再建一个空物体作为成就根，
/// 按 Achievement01 的结构摆好子物体并挂 AchievementEntry，
/// 本组件会在刷新时自动收进列表，无需额外登记。
/// </summary>
public class AchievementList : MonoBehaviour
{
    [Tooltip("每次面板显示时自动刷新全部成就")]
    [SerializeField]
    private bool refreshOnEnable = true;

    private readonly List<AchievementEntry> entries =
        new List<AchievementEntry>();

    private void Awake()
    {
        CollectEntries();
    }

    private void OnEnable()
    {
        if (refreshOnEnable)
        {
            RefreshAll();
        }
    }

    /// <summary>
    /// 收集本物体（含子物体）上全部 AchievementEntry。
    /// 新建成就后会自动被收进来，不需要手动维护列表。
    /// </summary>
    public void CollectEntries()
    {
        entries.Clear();

        // true 参数把未激活的成就根物体也收进来，
        // 便于先摆好结构、之后再逐个开放。
        AchievementEntry[] found =
            GetComponentsInChildren<AchievementEntry>(true);

        foreach (AchievementEntry entry in found)
        {
            if (entry != null)
            {
                entries.Add(entry);
            }
        }
    }

    /// <summary>刷新全部成就（打开面板时调用）。</summary>
    public void RefreshAll()
    {
        if (entries.Count == 0)
        {
            CollectEntries();
        }

        foreach (AchievementEntry entry in entries)
        {
            if (entry != null)
            {
                entry.Refresh();
            }
        }
    }

    // ============ 以下实例方法供 UnityEvent 在检查器里接线 ============

    /// <summary>已解锁成就数量（可用于面板标题统计）。</summary>
    public int UnlockedCount
    {
        get
        {
            int count = 0;

            foreach (AchievementEntry entry in entries)
            {
                if (entry != null && entry.IsUnlocked)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
