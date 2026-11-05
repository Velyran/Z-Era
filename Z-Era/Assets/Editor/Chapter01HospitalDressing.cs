using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 第一章医院室内装饰工具（手工排布，不参考任何场景）。
///
/// 目标建筑：3_Chapter01 里的 SM_Bld_Commercial_Medium_01，
/// 世界坐标 (119.8, 0, 46.8)（剧情中是医院）。场景里另有 3 栋同名
/// 建筑，按名称 + 世界坐标锁定。
///
/// 风格：病毒爆发后的医院——大件还在原位（床、桌、柜大体整齐），
/// 小件凌乱：位置/朝向带随机抖动，部分椅子、轮椅、输液架翻倒，
/// 地面散落纸箱、开口木箱、破损医疗容器、帘子、拉警戒的障碍。
/// 随机器用固定种子，同一次菜单重复执行结果一致。
///
/// 分层用途：
///   1 层 急诊 / 入口：分诊台、候诊椅、担架、轮椅、输液架、药架、指示牌
///   2 层 病房：双侧病床 + 输液架 + 医疗架 + 护士台
///   3 层 病房（隔离）：同 2 层，另加担架、破损容器、铁丝网、隔离告示
///   4 层 办公：成排办公桌 + 椅子 + 书柜 + 会客沙发
///
/// 装饰物是目标建筑的子物体、使用局部坐标，跟随建筑的位移与旋转。
/// 楼层高度与层高集中在 GroundFloorY / FloorPitch 两个常量里，
/// 若某层整体悬空或陷入地面，改这两个值即可（注明哪层偏移多少）。
///
/// 菜单：Tools > Chapter01 > 医院装饰 - ...
/// </summary>
public static class Chapter01HospitalDressing
{
    private const string PropsFolder =
        "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/";

    private const string TargetBuildingName =
        "SM_Bld_Commercial_Medium_01";

    private static readonly Vector3 TargetAnchor =
        new Vector3(119.8f, 0f, 46.8f);

    private const float AnchorTolerance = 0.5f;

    // 一层地面高度与层高（先按量测值，偏移由人工反馈后修正）
    private const float GroundFloorY = 0f;
    private const float FloorPitch = 3.05f;

    // 混乱度的固定随机种子：同一次菜单执行结果可复现
    private const uint RandomSeed = 20261105u;

    private static Transform targetBuilding;
    private static int createdCount;
    private static uint randomState;

    // 摆放期间临时补在建筑上的 MeshCollider，摆完就删
    private static readonly List<Collider> tempColliders =
        new List<Collider>();

    [MenuItem("Tools/Chapter01/医院装饰 - 第 1 层（急诊）")]
    private static void BuildEmergencyFloor()
    {
        Run("第 1 层 急诊", FillEmergency);
    }

    [MenuItem("Tools/Chapter01/医院装饰 - 第 2 层（病房）")]
    private static void BuildWardFloor2()
    {
        Run("第 2 层 病房", () => FillWard(2));
    }

    [MenuItem("Tools/Chapter01/医院装饰 - 第 3 层（病房）")]
    private static void BuildWardFloor3()
    {
        Run("第 3 层 病房", () => FillWard(3));
    }

    [MenuItem("Tools/Chapter01/医院装饰 - 第 4 层（办公）")]
    private static void BuildOfficeFloor()
    {
        Run("第 4 层 办公", FillOffice);
    }

    [MenuItem("Tools/Chapter01/医院装饰 - 全部四层")]
    private static void BuildAllFloors()
    {
        Run("全部四层", () =>
        {
            FillEmergency();
            FillWard(2);
            FillWard(3);
            FillOffice();
        });
    }

    [MenuItem("Tools/Chapter01/医院装饰 - 清除本建筑装饰")]
    private static void ClearDressing()
    {
        Transform building = FindTargetBuilding();

        if (building == null)
        {
            Debug.LogError(
                "Chapter01HospitalDressing：场景里找不到目标建筑 " +
                    TargetBuildingName + " @ " + TargetAnchor + "。"
            );

            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(
            building.gameObject,
            "Clear Hospital Dressing"
        );

        int removed = 0;

        // 只删本工具摆放的道具（SM_Prop_ 前缀），
        // 建筑自带的孩子（Glass / Attachments / Overgrowth）不动。
        for (int i = building.childCount - 1; i >= 0; i--)
        {
            Transform child = building.GetChild(i);

            if (!child.name.StartsWith("SM_Prop_"))
            {
                continue;
            }

            Undo.DestroyObjectImmediate(child.gameObject);
            removed++;
        }

        Debug.Log(
            "Chapter01HospitalDressing：清除 " + removed + " 件装饰。",
            building
        );
    }

    // 公共执行流程：找建筑 → 注册撤销 → 逐条摆放 → 汇总日志
    private static void Run(string label, System.Action fill)
    {
        targetBuilding = FindTargetBuilding();

        if (targetBuilding == null)
        {
            Debug.LogError(
                "Chapter01HospitalDressing：场景里找不到目标建筑 " +
                TargetBuildingName + " @ " + TargetAnchor + "。"
            );

            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(
            targetBuilding.gameObject,
            "Place Hospital Dressing"
        );

        createdCount = 0;
        randomState = RandomSeed;

        // 逐件把道具吸附到真实楼板高度，需要建筑有碰撞体；
        // 没有就先临时补一层，摆完在 finally 里删掉。
        PrepareTempColliders();

        try
        {
            fill();
        }
        finally
        {
            RemoveTempColliders();
        }

        Debug.Log(
            "Chapter01HospitalDressing：" + label + " 完成，创建 " +
                createdCount + " 件装饰。",
            targetBuilding
        );
    }

    // 线性同余随机，避免动到 Unity 全局随机状态
    private static float Range(float min, float max)
    {
        randomState = randomState * 1664525u + 1013904223u;
        float t = (randomState >> 8) / 16777216f;

        return min + (max - min) * t;
    }

    /// <summary>
    /// 单件摆放。
    /// jitter：位置抖动幅度（米）与偏航抖动（约为它的 20 倍角度）；
    /// tilt：俯仰/翻滚的最大角度，>0 时随机倾斜（用于翻倒的家具）。
    /// 倾斜件会按角度略微抬高，减少一角插进地面。
    /// </summary>
    private static void Place(
        string prefabName,
        float x,
        float floorY,
        float z,
        float yaw,
        float jitter = 0.3f,
        float tilt = 0f
    )
    {
        string path = PropsFolder + prefabName + ".prefab";
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogWarning(
                "Chapter01HospitalDressing：找不到预制体 " + path
            );

            return;
        }

        GameObject instance =
            PrefabUtility.InstantiatePrefab(prefab, targetBuilding)
                as GameObject;

        if (instance == null)
        {
            Debug.LogWarning(
                "Chapter01HospitalDressing：实例化失败 " + path
            );

            return;
        }

        float pitch = tilt > 0f ? Range(-tilt, tilt) : 0f;
        float roll = tilt > 0f ? Range(-tilt, tilt) : 0f;
        float yawError = Range(-jitter * 20f, jitter * 20f);

        // 倾斜件抬高一点：避免倾斜后一角沉进地面
        float lift =
            (Mathf.Abs(pitch) + Mathf.Abs(roll)) * 0.006f;

        float jitterX = x + Range(-jitter, jitter);
        float jitterZ = z + Range(-jitter, jitter);

        // 用射线把 y 吸附到真实楼板面：楼层高度差一点点都不会再陷进去
        Vector3 worldPoint = targetBuilding.TransformPoint(
            new Vector3(jitterX, floorY, jitterZ)
        );

        float snappedY = SnapToFloor(worldPoint, floorY);

        // 世界坐标赋值后由 Unity 换算成本地坐标
        instance.transform.position = new Vector3(
            worldPoint.x,
            snappedY + lift,
            worldPoint.z
        );

        instance.transform.localRotation =
            Quaternion.Euler(pitch, yaw + yawError, roll);

        instance.transform.localScale = Vector3.one;

        createdCount++;
    }

    // 从预期楼层高度上方 0.7m 往下打射线，命中面即该层地板；
    // 打不到就退回预期高度本身
    private static float SnapToFloor(
        Vector3 worldPoint,
        float expectedFloorY
    )
    {
        float startY = targetBuilding.TransformPoint(
            new Vector3(0f, expectedFloorY + 0.7f, 0f)
        ).y;

        RaycastHit hit;

        if (
            Physics.Raycast(
                new Vector3(worldPoint.x, startY, worldPoint.z),
                Vector3.down,
                out hit,
                2.4f,
                ~0,
                QueryTriggerInteraction.Ignore
            ) &&
            IsBuildingCollider(hit.collider)
        )
        {
            return hit.point.y + 0.01f;
        }

        return targetBuilding.TransformPoint(
            new Vector3(0f, expectedFloorY, 0f)
        ).y;
    }

    // 建筑自身碰撞体才算数，本工具摆的道具（SM_Prop_）不参与
    private static bool IsBuildingCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Transform owner = collider.transform;

        if (!owner.IsChildOf(targetBuilding))
        {
            return false;
        }

        return !owner.name.StartsWith("SM_Prop_");
    }

    private static void PrepareTempColliders()
    {
        tempColliders.Clear();

        foreach (Renderer renderer in
            targetBuilding.GetComponentsInChildren<Renderer>())
        {
            if (renderer.gameObject.name.Contains("Overgrowth"))
            {
                continue;
            }

            if (renderer.GetComponent<Collider>() != null)
            {
                continue;
            }

            MeshCollider collider =
                Undo.AddComponent<MeshCollider>(renderer.gameObject);

            if (collider != null)
            {
                tempColliders.Add(collider);
            }
        }
    }

    private static void RemoveTempColliders()
    {
        foreach (Collider collider in tempColliders)
        {
            if (collider != null)
            {
                Undo.DestroyObjectImmediate(collider);
            }
        }

        tempColliders.Clear();
    }

    // 第 n 层的地面高度
    private static float FloorY(int floor)
    {
        return GroundFloorY + (floor - 1) * FloorPitch;
    }

    // 在一段矩形区域里撒落同一种小件杂物（位置/朝向随机、轻微倾斜）
    private static void Scatter(
        string prefabName,
        int count,
        float minX,
        float maxX,
        float minZ,
        float maxZ,
        float floorY,
        float tilt = 12f
    )
    {
        for (int i = 0; i < count; i++)
        {
            float x = Range(minX, maxX);
            float z = Range(minZ, maxZ);

            Place(prefabName, x, floorY, z, Range(0f, 359f), 0.05f, tilt);
        }
    }

    // 第 1 层：急诊 / 入口
    private static void FillEmergency()
    {
        float y = FloorY(1);

        // 分诊台：一张还正，一张被撞歪
        Place("SM_Prop_Desk_02", -3f, y, 2.4f, 180f, 0.2f);
        Place("SM_Prop_Desk_02", -1f, y, 2.2f, 168f, 0.25f, 8f);

        // 候诊区：西侧三把、东侧三把金属椅，其中两把翻倒
        Place("SM_Prop_Chair_Metal_01", -8.6f, y, -3.4f, 90f, 0.35f);
        Place("SM_Prop_Chair_Metal_01", -8.6f, y, -1.6f, 90f, 0.35f);
        Place("SM_Prop_Chair_Metal_01", -8.6f, y, 0.2f, 120f, 0.4f, 75f);
        Place("SM_Prop_Chair_Metal_01", 6.6f, y, -3.4f, 270f, 0.35f);
        Place("SM_Prop_Chair_Metal_01", 6.6f, y, -1.6f, 250f, 0.4f, 70f);
        Place("SM_Prop_Chair_Metal_01", 6.6f, y, 0.2f, 270f, 0.35f);

        // 担架两副（一副斜停）、轮椅两辆（一辆翻倒）
        Place("SM_Prop_Bed_Gurney_01", -4.8f, y, -2.2f, 90f, 0.25f);
        Place("SM_Prop_Bed_Gurney_01", -2.6f, y, -2.6f, 115f, 0.3f, 4f);
        Place("SM_Prop_WheelChair_01", 3.4f, y, 1.2f, 180f, 0.3f);
        Place("SM_Prop_WheelChair_01", 4.8f, y, 0.8f, 205f, 0.4f, 85f);

        // 输液架三支（一支倒地）
        Place("SM_Prop_Medical_IVStand_01", -4f, y, -1f, 0f, 0.3f);
        Place("SM_Prop_Medical_IVStand_01", -2f, y, -0.6f, 0f, 0.35f, 80f);
        Place("SM_Prop_Medical_IVStand_01", 5f, y, 0.4f, 0f, 0.3f);

        // 药架 / 医疗货架
        Place("SM_Prop_Medical_Shelf_01", 7.6f, y, -2.6f, 270f, 0.15f);
        Place("SM_Prop_Medical_Shelf_02", 7.6f, y, -0.4f, 275f, 0.25f, 4f);

        // 医疗箱与容器
        Place("SM_Prop_MedicalBox_01", 5.4f, y, 2.8f, 180f, 0.4f);
        Place("SM_Prop_Medical_Container_01", 2.2f, y, 2.6f, 165f, 0.4f);

        // 指示牌
        Place("SM_Prop_Sign_Hospital_01", 0.4f, y, 3.3f, 180f, 0.1f);
        Place("SM_Prop_Sign_Emergency_01", 5.6f, y, 3.3f, 172f, 0.2f);
        Place(
            "SM_Prop_Sign_Quarantine_01",
            -7.4f, y, 3.3f, 190f, 0.2f, 10f
        );

        // 家属等候座（一张还坐着，一张翻倒）
        Place("SM_Prop_Chair_Soft_01", -7f, y, 2.6f, 180f, 0.3f);
        Place("SM_Prop_Chair_Soft_01", -5.6f, y, 2.9f, 160f, 0.4f, 60f);

        // 凌乱：散落纸箱、开口木箱、破损容器、地毯、警戒障碍
        Place("SM_Prop_CardboardBox_01", 1.4f, y, -0.4f, 30f, 0.3f);
        Place("SM_Prop_CardboardBox_01", -9.6f, y, 1.6f, 75f, 0.3f);
        Place("SM_Prop_CardboardBox_03", 4.2f, y, -2.2f, 15f, 0.35f);
        Place("SM_Prop_CardboardBox_03", -6.6f, y, -4f, 55f, 0.35f);
        Place("SM_Prop_Crate_Open_02", -10f, y, -4.2f, 20f, 0.2f);
        Place(
            "SM_Prop_Medical_Container_Broken_01",
            3f, y, -4.1f, 210f, 0.3f, 25f
        );
        Place(
            "SM_Prop_Medical_Container_Broken_01",
            7f, y, 0.8f, 40f, 0.3f, 15f
        );
        Place("SM_Prop_Rug_02", -4.6f, y, 0.2f, 12f, 0.1f);
        Place("SM_Prop_Barricade_Corrugated_01", 0f, y, 3.4f, 5f, 0.15f);
        Place("SM_Prop_Curtain_01", -8.2f, y, 3.6f, 180f, 0.1f);
        Place("SM_Prop_Sign_NotSafe_01", -4.2f, y, 3.3f, 185f, 0.3f, 20f);

        // 密一点：通道与诊区之间的零散杂物
        Scatter("SM_Prop_CardboardBox_02", 4, -3.4f, 3.4f, -2.6f, 1.2f, y);
        Scatter("SM_Prop_Crate_01", 3, -3f, 3f, -1.4f, 1.6f, y);
        Scatter("SM_Prop_MedicalBox_01", 3, -2.5f, 3.5f, -3.6f, -2.2f, y);
        Scatter("SM_Prop_Chair_Metal_01", 2, -2f, 2f, 0.6f, 2.2f, y, 40f);
    }

    // 第 2 / 3 层：病房（双侧各三床，床旁输液架）
    private static void FillWard(int floor)
    {
        float y = FloorY(floor);

        // 第 3 层床位排布略微内收，避免与 2 层看起来完全一样
        float startZ = floor == 3 ? -3.4f : -3.2f;

        for (int i = 0; i < 3; i++)
        {
            float z = startZ + i * 2.8f;

            // 西侧床面朝东（通道），床头靠西墙；中间那张被挪歪
            float westYaw = i == 1 ? 70f : 90f;
            float westJitter = i == 1 ? 0.55f : 0.25f;

            Place(
                "SM_Prop_Bed_Hospital_01",
                -8.9f, y, z, westYaw, westJitter
            );

            Place(
                "SM_Prop_Medical_IVStand_01",
                -7.4f, y, z, 0f, 0.35f, i == 0 ? 70f : 0f
            );

            // 东侧床面朝西
            Place(
                "SM_Prop_Bed_Hospital_01",
                6.9f, y, z, 270f, 0.25f
            );

            Place(
                "SM_Prop_Medical_IVStand_01",
                5.4f, y, z, 0f, 0.35f, i == 2 ? 78f : 0f
            );
        }

        // 护士台（被拖偏一点）
        Place("SM_Prop_Desk_01", 0f, y, 3f, 178f, 0.45f);

        // 医疗架靠南墙
        Place("SM_Prop_Medical_Shelf_01", -2f, y, -3.9f, 0f, 0.2f);
        Place("SM_Prop_Medical_Shelf_02", 2f, y, -3.9f, 5f, 0.2f, 6f);

        // 病房区椅子（一张翻倒）
        Place("SM_Prop_Chair_Soft_01", -2.2f, y, 1.4f, 200f, 0.4f);
        Place(
            "SM_Prop_Chair_Soft_01",
            2.4f, y, 1.1f, 150f, 0.45f, 65f
        );

        // 隔离告示
        Place(
            "SM_Prop_Sign_Quarantine_01",
            -6f, y, 3.4f, 182f, 0.2f, 12f
        );

        // 凌乱：纸箱、开口木箱、破损容器、帘子、地毯
        Place("SM_Prop_CardboardBox_05", 4.4f, y, 2.4f, 25f, 0.35f);
        Place("SM_Prop_CardboardBox_05", 3.6f, y, -3.6f, 70f, 0.35f);
        Place("SM_Prop_Crate_Open_01", 8.4f, y, 1.8f, 10f, 0.2f);
        Place(
            "SM_Prop_Medical_Container_Broken_01",
            -6f, y, -4f, 195f, 0.3f, 20f
        );
        Place("SM_Prop_Curtain_01", -9.9f, y, 3.5f, 90f, 0.1f);
        Place("SM_Prop_Rug_03", 0.8f, y, -1.4f, 20f, 0.15f);

        if (floor == 3)
        {
            // 第 3 层：隔离区，担架、破损容器、铁丝网、轮椅
            Place("SM_Prop_Bed_Gurney_01", 0.6f, y, 1.6f, 10f, 0.35f);
            Place("SM_Prop_Bed_Gurney_01", 1.8f, y, 1.4f, 25f, 0.4f, 5f);
            Place(
                "SM_Prop_Medical_Container_01",
                -1.4f, y, -4f, 0f, 0.25f
            );
            Place(
                "SM_Prop_Medical_Container_Broken_01",
                -0.4f, y, -4f, 30f, 0.3f, 18f
            );
            Place("SM_Prop_WheelChair_01", 4.2f, y, 2.6f, 180f, 0.3f);
            Place(
                "SM_Prop_WheelChair_01",
                5.4f, y, 2.4f, 195f, 0.35f, 80f
            );
            Place(
                "SM_Prop_Wall_Quarantine_BarbedWire_01",
                -4.6f, y, 3f, 0f, 0.1f
            );
            Place(
                "SM_Prop_Wall_Quarantine_BarbedWire_01",
                -4.6f, y, -2.6f, 0f, 0.1f
            );
            Place(
                "SM_Prop_Sign_Quarantine_01",
                -4.6f, y, 3.4f, 180f, 0.2f, 8f
            );
            Place("SM_Prop_CardboardBox_03", 7f, y, -2.8f, 45f, 0.35f);
            Place("SM_Prop_Crate_Open_03", -8.4f, y, 2.2f, 15f, 0.2f);
        }

        // 密一点：床头柜区之外的零散杂物
        Scatter("SM_Prop_CardboardBox_02", 4, -3.2f, 3.2f, -3.4f, -1.2f, y);
        Scatter("SM_Prop_Crate_01", 3, -2.6f, 2.6f, -0.8f, 1.4f, y);
        Scatter("SM_Prop_MedicalBox_01", 3, -3.4f, 2.6f, 0.2f, 2.2f, y);
        Scatter("SM_Prop_Chair_Folding_01", 2, -1.6f, 2.4f, -1f, 1.6f, y, 35f);
    }

    // 第 4 层：办公
    private static void FillOffice()
    {
        float y = FloorY(4);

        // 四张办公桌成排，桌后各一把椅子；两把椅子翻倒、两张桌歪斜
        float[] deskX = { -6.4f, -3f, 1f, 4.4f };
        float[] deskYaw = { 0f, 14f, 0f, 22f };

        for (int i = 0; i < deskX.Length; i++)
        {
            Place(
                "SM_Prop_Study_Desk_01",
                deskX[i], y, 0.4f, deskYaw[i], 0.3f
            );

            float chairTilt = i == 0 ? 70f : (i == 3 ? 55f : 0f);

            Place(
                "SM_Prop_Study_Chair_01",
                deskX[i], y, -0.4f, deskYaw[i], 0.4f, chairTilt
            );
        }

        // 书柜靠两侧长墙
        Place("SM_Prop_Bookshelf_01", -10f, y, -2.4f, 90f, 0.2f);
        Place(
            "SM_Prop_Bookshelf_01",
            8f, y, -2.4f, 275f, 0.25f, 5f
        );

        // 主管桌
        Place("SM_Prop_Desk_02", -1f, y, 3f, 180f, 0.3f);
        Place("SM_Prop_Chair_Soft_01", -1f, y, 2.2f, 0f, 0.4f, 30f);

        // 会客角（沙发被撞偏、茶几歪）
        Place("SM_Prop_Couch_01", -7.6f, y, 2.6f, 88f, 0.35f);
        Place("SM_Prop_Couch_02", 2.6f, y, 2.8f, 200f, 0.3f, 12f);
        Place("SM_Prop_CoffeeTable_02", -6.2f, y, 2.6f, 35f, 0.3f);

        // 工作台
        Place("SM_Prop_Workbench_01", 6f, y, 2.4f, 268f, 0.3f);

        // 凌乱：纸箱、开口木箱、地毯、翻倒的金属椅
        Place("SM_Prop_CardboardBox_04", -9.4f, y, 0.6f, 60f, 0.35f);
        Place("SM_Prop_CardboardBox_04", 7.2f, y, 3f, 20f, 0.35f);
        Place("SM_Prop_Crate_Open_04", -4.6f, y, -3.6f, 5f, 0.2f);
        Place("SM_Prop_Rug_05", 3.4f, y, 2.6f, 15f, 0.15f);
        Place(
            "SM_Prop_Chair_Metal_01",
            -8.6f, y, -4.2f, 30f, 0.4f, 85f
        );
        Place("SM_Prop_Curtain_01", 9.2f, y, 3.5f, 270f, 0.1f);

        // 密一点：办公区过道散落的纸箱与椅子
        Scatter("SM_Prop_CardboardBox_02", 4, -4.4f, 4.4f, -2.6f, -0.6f, y);
        Scatter("SM_Prop_Crate_01", 3, -3.6f, 3.6f, -1.6f, 0.8f, y);
        Scatter("SM_Prop_Chair_Folding_01", 3, -4f, 4f, -2.4f, 1.4f, y, 45f);
        Scatter("SM_Prop_ToolBox_01", 2, -3f, 3f, -1f, 1.4f, y);
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
