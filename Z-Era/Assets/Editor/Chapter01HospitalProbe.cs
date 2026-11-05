using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 医院装饰的几何探测工具：用射线量出目标建筑每一层的可用空间，
/// 输出平面 ASCII 图，供后续按真实房间布局摆放家具（避免穿模）。
///
/// 做法：临时给建筑的孩子补 MeshCollider（结束后撤销），
///   · 竖直向下打射线 → 统计各层楼板高度；
///   · 每个格子再向上打射线 → 判断层高/天花板；
///   · 水平四向打射线 → 判断墙体和靠墙带。
///
/// 产出：项目根目录 Temp/hospital_plan.txt
/// 图例：' ' = 该处没有楼板，'#' = 贴着墙（<0.3m），
///       '+' = 靠墙（<0.8m），'.' = 可用空地
///
/// 菜单：Tools > Chapter01 > 医院 - 探测内部可用空间
/// </summary>
public static class Chapter01HospitalProbe
{
    private const string TargetBuildingName =
        "SM_Bld_Commercial_Medium_01";

    private static readonly Vector3 TargetAnchor =
        new Vector3(119.8f, 0f, 46.8f);

    private const float AnchorTolerance = 0.5f;
    private const float CellSize = 0.5f;

    // 植被装饰会挡住射线，探测时跳过
    private const string ExcludedNamePart = "Overgrowth";

    [MenuItem("Tools/Chapter01/医院 - 探测内部可用空间")]
    private static void Probe()
    {
        Transform building = FindTargetBuilding();

        if (building == null)
        {
            Debug.LogError(
                "Chapter01HospitalProbe：场景里找不到目标建筑 " +
                    TargetBuildingName + " @ " + TargetAnchor + "。"
            );

            return;
        }

        List<Collider> addedColliders = new List<Collider>();
        List<Renderer> renderers = new List<Renderer>();

        Undo.RegisterFullObjectHierarchyUndo(
            building.gameObject,
            "Probe Hospital Interior"
        );

        foreach (Renderer renderer in
            building.GetComponentsInChildren<Renderer>())
        {
            if (renderer.gameObject.name.Contains(ExcludedNamePart))
            {
                continue;
            }

            renderers.Add(renderer);

            if (renderer.GetComponent<Collider>() != null)
            {
                continue;
            }

            MeshCollider collider =
                Undo.AddComponent<MeshCollider>(renderer.gameObject);

            if (collider != null)
            {
                addedColliders.Add(collider);
            }
        }

        try
        {
            Measure(building, renderers);
        }
        finally
        {
            foreach (Collider collider in addedColliders)
            {
                Undo.DestroyObjectImmediate(collider);
            }
        }
    }

    private static void Measure(
        Transform building,
        List<Renderer> renderers
    )
    {
        if (renderers.Count == 0)
        {
            Debug.LogError(
                "Chapter01HospitalProbe：建筑下没有可用的 Renderer。"
            );

            return;
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Count; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        int nx = Mathf.Max(
            1,
            Mathf.FloorToInt((bounds.size.x - 0.6f) / CellSize)
        );

        int nz = Mathf.Max(
            1,
            Mathf.FloorToInt((bounds.size.z - 0.6f) / CellSize)
        );

        float x0 = bounds.min.x + 0.3f + CellSize * 0.5f;
        float z0 = bounds.min.z + 0.3f + CellSize * 0.5f;
        float topY = bounds.max.y + 0.6f;
        float castLength = bounds.size.y + 2f;

        // 每个格子的向下命中点（从高到低）
        List<float>[] cellHits = new List<float>[nx * nz];

        for (int ix = 0; ix < nx; ix++)
        {
            for (int iz = 0; iz < nz; iz++)
            {
                Vector3 origin = new Vector3(
                    x0 + ix * CellSize,
                    topY,
                    z0 + iz * CellSize
                );

                List<float> hits = new List<float>();
                Vector3 rayOrigin = origin;
                float remaining = castLength;

                // 同一次 Raycast 只能拿到最近的一个交点，
                // 这里命中后把起点挪到该点下方继续打，
                // 逐层穿过楼板拿到每一层的命中高度。
                while (remaining > 0.05f)
                {
                    RaycastHit hit;

                    bool gotHit = Physics.Raycast(
                        rayOrigin,
                        Vector3.down,
                        out hit,
                        remaining,
                        ~0,
                        QueryTriggerInteraction.Ignore
                    );

                    if (
                        !gotHit ||
                        !IsMeasuredCollider(hit.collider, building)
                    )
                    {
                        break;
                    }

                    hits.Add(hit.point.y);

                    float nextOriginY = hit.point.y - 0.03f;
                    remaining -= rayOrigin.y - nextOriginY;
                    rayOrigin.y = nextOriginY;
                }

                hits.Sort();
                hits.Reverse();
                cellHits[ix * nz + iz] = hits;
            }
        }

        // 把所有命中高度聚类成若干层楼板
        List<float> allHits = new List<float>();

        foreach (List<float> hits in cellHits)
        {
            allHits.AddRange(hits);
        }

        allHits.Sort();

        List<float> slabs = new List<float>();
        int index = 0;

        while (index < allHits.Count)
        {
            float start = allHits[index];
            float end = start;
            int count = 0;

            while (
                index < allHits.Count &&
                allHits[index] - end <= 0.12f
            )
            {
                end = allHits[index];
                count++;
                index++;
            }

            if (count >= nx * nz / 12)
            {
                slabs.Add((start + end) * 0.5f);
            }
        }

        slabs.Sort();
        slabs.Reverse();

        System.Text.StringBuilder report = new System.Text.StringBuilder();

        report.AppendLine("目标建筑: " + building.name);
        report.AppendLine(
            "世界坐标: " + building.position.ToString("F2")
        );
        report.AppendLine(
            "包围盒 min=" + bounds.min.ToString("F2") +
                " max=" + bounds.max.ToString("F2") +
                " size=" + bounds.size.ToString("F2")
        );
        report.AppendLine(
            "网格: " + nx + " x " + nz +
                " 格，格宽 " + CellSize + "m"
        );
        report.AppendLine(
            "楼板数: " + slabs.Count + " —— " +
                string.Join(", ", FormatY(slabs))
        );
        report.AppendLine();

        string logSummary =
            "Chapter01HospitalProbe：楼板 " + slabs.Count + " 层（" +
            string.Join(", ", FormatY(slabs)) + "），报告 " +
            "Temp/hospital_plan.txt";

        foreach (float slabY in slabs)
        {
            string map = BuildFloorMap(
                building,
                bounds,
                cellHits,
                nx,
                nz,
                x0,
                z0,
                slabY
            );

            report.AppendLine(
                "===== 楼板 y=" + slabY.ToString("F2") + " ====="
            );
            report.AppendLine(map);
            report.AppendLine();
        }

        string tempDir = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Temp"
        );

        Directory.CreateDirectory(tempDir);

        string path = Path.Combine(tempDir, "hospital_plan.txt");
        File.WriteAllText(path, report.ToString());

        Debug.Log(logSummary + "（" + path + "）", building);
    }

    private static string[] FormatY(List<float> slabs)
    {
        string[] text = new string[slabs.Count];

        for (int i = 0; i < slabs.Count; i++)
        {
            text[i] = slabs[i].ToString("F2");
        }

        return text;
    }

    // 生成一层的平面图：'#' 贴墙 / '+' 靠墙 / '.' 空地 / ' ' 无楼板
    private static string BuildFloorMap(
        Transform building,
        Bounds bounds,
        List<float>[] cellHits,
        int nx,
        int nz,
        float x0,
        float z0,
        float slabY
    )
    {
        System.Text.StringBuilder map = new System.Text.StringBuilder();

        map.AppendLine("      x=" + bounds.min.x.ToString("F1") +
            " → " + bounds.max.x.ToString("F1"));

        for (int iz = nz - 1; iz >= 0; iz--)
        {
            System.Text.StringBuilder row = new System.Text.StringBuilder();

            for (int ix = 0; ix < nx; ix++)
            {
                List<float> hits = cellHits[ix * nz + iz];

                bool hasFloor = false;
                float ceilingY = float.PositiveInfinity;

                foreach (float y in hits)
                {
                    if (Mathf.Abs(y - slabY) <= 0.15f)
                    {
                        hasFloor = true;
                    }

                    if (y > slabY + 0.2f && y < ceilingY)
                    {
                        ceilingY = y;
                    }
                }

                if (!hasFloor)
                {
                    row.Append(' ');
                    continue;
                }

                if (ceilingY - slabY < 1.7f)
                {
                    row.Append('C');
                    continue;
                }

                Vector3 origin = new Vector3(
                    x0 + ix * CellSize,
                    slabY + 0.9f,
                    z0 + iz * CellSize
                );

                float nearestWall = float.PositiveInfinity;

                Vector3[] dirs =
                {
                    Vector3.right,
                    Vector3.left,
                    Vector3.forward,
                    Vector3.back,
                };

                foreach (Vector3 dir in dirs)
                {
                    foreach (
                        RaycastHit hit in Physics.RaycastAll(
                            origin,
                            dir,
                            1.2f,
                            ~0,
                            QueryTriggerInteraction.Ignore
                        )
                    )
                    {
                        if (!IsMeasuredCollider(hit.collider, building))
                        {
                            continue;
                        }

                        nearestWall = Mathf.Min(
                            nearestWall,
                            hit.distance
                        );
                    }
                }

                if (nearestWall < 0.3f)
                {
                    row.Append('#');
                }
                else if (nearestWall < 0.8f)
                {
                    row.Append('+');
                }
                else
                {
                    row.Append('.');
                }
            }

            map.AppendLine("z=" + (z0 + iz * CellSize).ToString("F1") +
                " " + row.ToString());
        }

        return map.ToString();
    }

    // 只统计建筑自身的碰撞体：本工具摆放的装饰（SM_Prop_ 前缀）
    // 会被跳过，避免装饰挡住真实的楼板与墙体
    private static bool IsMeasuredCollider(
        Collider collider,
        Transform building
    )
    {
        if (collider == null)
        {
            return false;
        }

        Transform owner = collider.transform;

        if (!owner.IsChildOf(building))
        {
            return false;
        }

        return !owner.name.StartsWith("SM_Prop_");
    }

    // 按名称 + 世界坐标锁定目标建筑（场景里有 4 栋同名建筑）
    private static Transform FindTargetBuilding()
    {
        Transform best = null;
        float bestDistance = float.PositiveInfinity;

        foreach (
            Transform candidate in
            Object.FindObjectsOfType<Transform>()
        )
        {
            if (candidate.name != TargetBuildingName)
            {
                continue;
            }

            float distance =
                Vector3.Distance(candidate.position, TargetAnchor);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return bestDistance <= AnchorTolerance ? best : null;
    }
}
