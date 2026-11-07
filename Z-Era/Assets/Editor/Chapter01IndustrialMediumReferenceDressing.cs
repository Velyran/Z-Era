using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Chapter01 单栋测试：从 3_Detailed_Reference_Scene 的
/// Interior_Dressing_SM_Bld_Industrial_Medium_01 提取办公楼室内装饰，
/// 复制到 3_Chapter01 的目标建筑 SM_Bld_Industrial_Medium_01 @ (-38, 0, 22)。
///
/// 参考场景中，建筑本体与装饰物同属一个 Interior_Dressing 根节点：
///   建筑根 local TRS = (0,0,0) / identity / (1,1,1)
///   Interior_Dressing 根 = (200,0,50) / identity / (1,1,1)
/// 因此参考建筑本体的 local TRS 为 identity，装饰物的局部坐标可以直接
/// 作为"相对建筑本体"的局部位姿使用；不复制参考场景的世界坐标，
/// 也不叠加目标建筑自身 90° 的旋转（设置 local* 后由 Transform 自动叠加）。
///
/// 数据来源：Prefab.m_SourcePrefab 的 GUID + 序列化 Transform 的
/// localPosition / localRotation / localScale；剔除建筑本体
/// （其自带的玻璃、附件、植被随之排除）。
/// 参考 Dresser_03 有两层抽屉被拉开（Drawer_03 / Drawer_05），
/// 复制后一并恢复这两个预制体内部覆盖。
///
/// 不直接改场景 YAML、不自动保存场景、不删除已有道具。
/// 菜单：Tools > Chapter01 > 参考场景装饰 - Industrial Medium 01（单栋测试）
/// </summary>
public static class Chapter01IndustrialMediumReferenceDressing
{
    private const string ScenePath = "Assets/Scenes/3_Chapter01.unity";
    private const string BuildingName = "SM_Bld_Industrial_Medium_01";
    private const string BuildingGuid = "ea4c9df57c4270e4d998edf3e65b0738";
    private const string PropsFolder = "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/";
    private const string InstancePrefix = "Chapter01_IndustrialMedium_";
    private const string UndoLabel = "Add Chapter01 Industrial Medium reference interior";
    private static readonly Vector3 Anchor = new Vector3(-38f, 0f, 22f);

    [MenuItem("Tools/Chapter01/参考场景装饰 - Industrial Medium 01（单栋测试）")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Chapter01IndustrialMediumReferenceDressing：请退出 Play 模式后执行。");
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("Chapter01IndustrialMediumReferenceDressing：请先打开并保存 3_Chapter01 场景。");
            return;
        }

        Transform building = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                bool nameMatches = candidate.name == BuildingName || candidate.name.StartsWith(BuildingName + " (", StringComparison.Ordinal);
                if (!nameMatches || Vector3.Distance(candidate.position, Anchor) > 0.1f)
                    continue;
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(candidate.gameObject);
                if (source == null || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) != BuildingGuid)
                    continue;
                if (Vector3.Distance(candidate.lossyScale, Vector3.one) > 0.01f)
                {
                    Debug.LogError("Chapter01IndustrialMediumReferenceDressing：目标建筑缩放不是 1，局部坐标会失真，未创建装饰。", candidate);
                    return;
                }
                if (building != null)
                {
                    Debug.LogError("Chapter01IndustrialMediumReferenceDressing：坐标附近匹配到多个目标，未创建装饰。");
                    return;
                }
                building = candidate;
            }
        }
        if (building == null)
        {
            Debug.LogError("Chapter01IndustrialMediumReferenceDressing：未找到 " + BuildingName + " @ " + Anchor + "，未创建装饰。");
            return;
        }
        foreach (Transform child in building)
        {
            if (child.name.StartsWith(InstancePrefix, StringComparison.Ordinal))
            {
                Debug.LogWarning("Chapter01IndustrialMediumReferenceDressing：本栋已执行参考布置，跳过，避免重复叠放。可先 Undo 撤销上次布置。", building);
                return;
            }
        }

        // Load every asset before creating anything, so a missing asset cannot leave a partial layout.
        var prefabs = new Dictionary<string, GameObject>();
        foreach (Placement entry in Data)
        {
            if (prefabs.ContainsKey(entry.PrefabName))
                continue;
            string path = PropsFolder + entry.PrefabName + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("Chapter01IndustrialMediumReferenceDressing：缺少预制体 " + path + "，未创建装饰。", building);
                return;
            }
            prefabs.Add(entry.PrefabName, prefab);
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoLabel);
        Undo.RegisterFullObjectHierarchyUndo(building.gameObject, UndoLabel);
        int created = 0;
        try
        {
            foreach (Placement entry in Data)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[entry.PrefabName], building);
                Undo.RegisterCreatedObjectUndo(instance, UndoLabel);
                instance.name = InstancePrefix + created.ToString("D3") + "_" + entry.PrefabName;
                instance.transform.localPosition = entry.Position;
                instance.transform.localRotation = entry.Rotation;
                instance.transform.localScale = entry.Scale;
                ApplyReferenceChildOverrides(instance, entry.PrefabName);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                created++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            Debug.LogError("Chapter01IndustrialMediumReferenceDressing：创建失败，已撤销本次操作。", building);
            return;
        }

        Selection.activeGameObject = building.gameObject;
        Debug.Log("Chapter01IndustrialMediumReferenceDressing：" + building.name + " @ " + building.position +
            "，创建 " + created + " 件参考装饰（" + prefabs.Count + " 种预制体）。使用建筑局部坐标；场景尚未自动保存，可单次 Undo 撤销。", building);
    }

    // The reference Dresser_03 has two drawers pulled open; the other four
    // drawers already match the prefab and need no override.
    private static void ApplyReferenceChildOverrides(GameObject instance, string prefabName)
    {
        if (prefabName != "SM_Prop_Dresser_03")
            return;

        foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "SM_Prop_Dresser_03_Drawer_05")
            {
                child.localPosition = new Vector3(-0.467997f, 0.443f, 0.79809433f);
                child.localRotation = new Quaternion(0.046609446f, 0f, 1.3597355e-07f, 0.99891317f);
            }
            else if (child.name == "SM_Prop_Dresser_03_Drawer_03")
            {
                child.localPosition = new Vector3(0.468f, 0.24005157f, 0.598f);
            }
            else
            {
                continue;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
        }
    }

    private readonly struct Placement
    {
        public readonly string PrefabName;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        public Placement(string prefabName, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            PrefabName = prefabName;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }

    // Extracted from Prefab.m_SourcePrefab GUIDs and serialized Transform values.
    // Source group: Interior_Dressing_SM_Bld_Industrial_Medium_01 (Transform 503476249).
    // 80 prefab roots: 79 props included; the main building (with its own glass /
    // attachments / vegetation) is excluded. Reference building local TRS is
    // identity; the group offset (200, 0, 50) is not copied.
    private static readonly Placement[] Data =
    {
        // Reference Transform 1436158574; m_SourcePrefab GUID 3942d04f71c806e47869062034bb6e78; SM_Prop_BarrelStack_02 (5)
        new Placement("SM_Prop_BarrelStack_02", new Vector3(-2.5269928f, 6.2339997f, 1.875f), new Quaternion(-0.0f, -0.664571f, -0.0f, 0.74722517f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1692536593; m_SourcePrefab GUID 193ece0069db35d4299e1a7c7ceb0b49; SM_Prop_Bookshelf_01 (16)
        new Placement("SM_Prop_Bookshelf_01", new Vector3(-3.371994f, 6.2279997f, -0.09500122f), new Quaternion(-0.0f, 0.7686409f, -0.0f, 0.6396805f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 899620074; m_SourcePrefab GUID 193ece0069db35d4299e1a7c7ceb0b49; SM_Prop_Bookshelf_01 (17)
        new Placement("SM_Prop_Bookshelf_01", new Vector3(-3.1069946f, 3.226f, -2.6660004f), new Quaternion(-0.0f, 0.6893977f, -0.0f, 0.72438306f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1349975336; m_SourcePrefab GUID 193ece0069db35d4299e1a7c7ceb0b49; SM_Prop_Bookshelf_01 (18)
        new Placement("SM_Prop_Bookshelf_01", new Vector3(-3.4309998f, 9.217446f, -0.29800034f), new Quaternion(0.0f, 0.6893977f, 0.0f, 0.72438306f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1073198505; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e; SM_Prop_CardboardBox_02 (31)
        new Placement("SM_Prop_CardboardBox_02", new Vector3(3.2570038f, 0.48f, -0.9560013f), new Quaternion(0.0f, 0.3093569f, 0.0f, 0.95094603f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 136236310; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e; SM_Prop_CardboardBox_02 (32)
        new Placement("SM_Prop_CardboardBox_02", new Vector3(0.91400146f, 0.48f, -3.9199982f), new Quaternion(0.0f, 0.7659029f, 0.0f, 0.6429563f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1637733032; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e; SM_Prop_CardboardBox_02 (33)
        new Placement("SM_Prop_CardboardBox_02", new Vector3(1.7749939f, 0.48f, 2.308998f), new Quaternion(0.0f, 0.6568426f, 0.0f, 0.7540277f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1250856104; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e; SM_Prop_CardboardBox_02 (34)
        new Placement("SM_Prop_CardboardBox_02", new Vector3(-5.009018f, 0.48f, 6.0289993f), new Quaternion(0.0f, 0.79937035f, 0.0f, 0.6008386f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 223508250; m_SourcePrefab GUID 9b2427eedaee08f4e912e8c38b0ed785; SM_Prop_CardboardBox_Wall_01 (23)
        new Placement("SM_Prop_CardboardBox_Wall_01", new Vector3(1.818985f, 0.23300001f, 4.0650024f), new Quaternion(0.0f, -0.70710576f, 0.0f, 0.70710784f), new Vector3(0.8775736f, 0.8775736f, 0.8775736f)),
        // Reference Transform 1507982938; m_SourcePrefab GUID 9b2427eedaee08f4e912e8c38b0ed785; SM_Prop_CardboardBox_Wall_01 (24)
        new Placement("SM_Prop_CardboardBox_Wall_01", new Vector3(5.1607513f, 6.224f, -2.8048553f), new Quaternion(-0.0f, -0.64041525f, -0.0f, 0.76802886f), new Vector3(0.87757397f, 0.8775736f, 0.87757397f)),
        // Reference Transform 222686615; m_SourcePrefab GUID 6a33dabe374b6a4438b519d0735016eb; SM_Prop_Chair_Soft_01 (4)
        new Placement("SM_Prop_Chair_Soft_01", new Vector3(-3.2460022f, 0.23017456f, 1.8790016f), new Quaternion(0.0f, 0.6936619f, 0.0f, 0.72030073f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1487045473; m_SourcePrefab GUID 6a33dabe374b6a4438b519d0735016eb; SM_Prop_Chair_Soft_01 (5)
        new Placement("SM_Prop_Chair_Soft_01", new Vector3(-2.8629913f, 0.23017454f, -4.235935f), new Quaternion(0.0f, -0.000920424f, 0.0f, 0.9999996f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1771801251; m_SourcePrefab GUID cd8236f65d8b07f48be1f52b3bd834ad; SM_Prop_Chair_Soft_Damaged_01 (2)
        new Placement("SM_Prop_Chair_Soft_Damaged_01", new Vector3(-3.2644196f, 0.247f, 3.0331001f), new Quaternion(0.0f, 0.6936619f, 0.0f, 0.72030073f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 24054370; m_SourcePrefab GUID cd8236f65d8b07f48be1f52b3bd834ad; SM_Prop_Chair_Soft_Damaged_01 (3)
        new Placement("SM_Prop_Chair_Soft_Damaged_01", new Vector3(-4.7446136f, 0.718f, -4.389f), new Quaternion(-0.23916656f, 0.25078395f, 0.68359965f, -0.64233816f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1845502629; m_SourcePrefab GUID 9311c2216a86fc049b817877941d991a; SM_Prop_CoffeeTable_02 (10)
        new Placement("SM_Prop_CoffeeTable_02", new Vector3(4.404007f, 3.19f, 4.977001f), new Quaternion(0.0f, -0.030465845f, 0.0f, 0.9995358f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1442910830; m_SourcePrefab GUID 9311c2216a86fc049b817877941d991a; SM_Prop_CoffeeTable_02 (11)
        new Placement("SM_Prop_CoffeeTable_02", new Vector3(4.404007f, 9.181446f, 4.977001f), new Quaternion(0.0f, -0.030465845f, 0.0f, 0.9995358f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 412323960; m_SourcePrefab GUID dbcab12f42b76414db49660110c4bca6; SM_Prop_Crate_Wall_01 (14)
        new Placement("SM_Prop_Crate_Wall_01", new Vector3(3.5299988f, 3.21f, 5.626999f), new Quaternion(0.0f, 1.0f, 0.0f, 2.9504295e-06f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1742980087; m_SourcePrefab GUID dbcab12f42b76414db49660110c4bca6; SM_Prop_Crate_Wall_01 (15)
        new Placement("SM_Prop_Crate_Wall_01", new Vector3(-0.71699524f, 0.22100002f, -3.4049988f), new Quaternion(0.0f, 0.75806546f, -0.0f, -0.65217847f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1900559792; m_SourcePrefab GUID dbcab12f42b76414db49660110c4bca6; SM_Prop_Crate_Wall_01 (16)
        new Placement("SM_Prop_Crate_Wall_01", new Vector3(3.5299988f, 9.201446f, 5.626999f), new Quaternion(0.0f, 1.0f, 0.0f, 2.9504295e-06f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1756509628; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (91)
        new Placement("SM_Prop_Desk_02", new Vector3(3.1690063f, 3.23f, -1.6459999f), new Quaternion(0.0f, -0.70710677f, 0.0f, 0.7071068f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 134694747; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (92)
        new Placement("SM_Prop_Desk_02", new Vector3(5.401993f, 0.23099992f, 5.323017f), new Quaternion(0.0f, -0.70710677f, 0.0f, 0.7071068f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1341794526; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (93)
        new Placement("SM_Prop_Desk_02", new Vector3(-0.53700256f, 3.23f, 5.318001f), new Quaternion(0.0f, -0.6194709f, 0.0f, 0.78501964f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 333983769; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (94)
        new Placement("SM_Prop_Desk_02", new Vector3(4.136978f, 0.23099992f, 5.4630165f), new Quaternion(0.0f, 0.6350427f, 0.0f, 0.77247703f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1600712284; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (95)
        new Placement("SM_Prop_Desk_02", new Vector3(2.1699982f, 3.23f, -1.6459999f), new Quaternion(-0.0f, 0.7071089f, -0.0f, 0.7071047f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 914334104; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (96)
        new Placement("SM_Prop_Desk_02", new Vector3(4.257004f, 3.23f, -4.4140015f), new Quaternion(-0.0f, 1.0f, -0.0f, -1.5199182e-06f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 722725624; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (97)
        new Placement("SM_Prop_Desk_02", new Vector3(4.727005f, 0.6643771f, -1.5340004f), new Quaternion(0.7070846f, -0.005426511f, 0.0054307478f, 0.70708734f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 677928462; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (98)
        new Placement("SM_Prop_Desk_02", new Vector3(-3.116516f, 3.23f, 1.5818901f), new Quaternion(0.0f, -0.6893792f, 0.0f, 0.72440064f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1407295843; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (99)
        new Placement("SM_Prop_Desk_02", new Vector3(1.6650085f, 6.2219996f, 5.498001f), new Quaternion(0.0f, -0.70710677f, 0.0f, 0.7071068f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1790276160; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (100)
        new Placement("SM_Prop_Desk_02", new Vector3(0.66600037f, 6.2219996f, 5.498001f), new Quaternion(-0.0f, 0.7071089f, -0.0f, 0.7071047f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 999959873; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (101)
        new Placement("SM_Prop_Desk_02", new Vector3(4.8139954f, 6.2219996f, 3.1990013f), new Quaternion(0.0f, 0.75986105f, 0.0f, 0.6500856f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1200992452; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (102)
        new Placement("SM_Prop_Desk_02", new Vector3(-2.5169983f, 4.09f, -0.40399933f), new Quaternion(0.019404586f, 0.0f, 0.9998117f, 0.0f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 771155291; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (103)
        new Placement("SM_Prop_Desk_02", new Vector3(3.1690063f, 9.221446f, -1.6459999f), new Quaternion(0.0f, -0.70710677f, 0.0f, 0.7071068f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 701883551; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (104)
        new Placement("SM_Prop_Desk_02", new Vector3(2.1699982f, 9.221446f, -1.6459999f), new Quaternion(-0.0f, 0.7071089f, -0.0f, 0.7071047f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 471301941; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (105)
        new Placement("SM_Prop_Desk_02", new Vector3(4.257004f, 9.221446f, -4.4140015f), new Quaternion(-0.0f, 1.0f, -0.0f, -1.5199182e-06f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 345575269; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (106)
        new Placement("SM_Prop_Desk_02", new Vector3(-1.4389954f, 9.221446f, 6.0060005f), new Quaternion(0.0f, -0.015617233f, 0.0f, 0.99987805f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1862093662; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (107)
        new Placement("SM_Prop_Desk_02", new Vector3(-2.5059967f, 10.081447f, 1.8909988f), new Quaternion(0.29558006f, -0.0f, 0.955318f, 0.0f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1598904136; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac; SM_Prop_Desk_02 (108)
        new Placement("SM_Prop_Desk_02", new Vector3(-3.0630035f, 9.221446f, -2.3860016f), new Quaternion(0.0f, 0.742758f, 0.0f, -0.66956f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1603769437; m_SourcePrefab GUID d6112e16f7e54344099937776b3d7f38; SM_Prop_Dresser_03 (5)
        new Placement("SM_Prop_Dresser_03", new Vector3(-0.5764618f, 6.266f, -4.388653f), new Quaternion(-0.0f, -0.007250842f, -0.0f, 0.9999737f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 669645269; m_SourcePrefab GUID 43fc67994ff37d049ba7df9cf4715237; SM_Prop_Medical_Shelf_02 (7)
        new Placement("SM_Prop_Medical_Shelf_02", new Vector3(-6.0420074f, 6.2149997f, -3.375f), new Quaternion(-0.0f, 0.636328f, -0.0f, 0.77141863f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 747874840; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f; SM_Prop_Papers_01 (43)
        new Placement("SM_Prop_Papers_01", new Vector3(-2.5080109f, 6.2385535f, -3.512001f), new Quaternion(-0.0f, 0.94450176f, -0.0f, -0.32850644f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1734287700; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f; SM_Prop_Papers_01 (44)
        new Placement("SM_Prop_Papers_01", new Vector3(2.0070038f, 3.246554f, 0.15499878f), new Quaternion(0.0f, 0.99505377f, 0.0f, 0.09933783f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 50652624; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f; SM_Prop_Papers_01 (45)
        new Placement("SM_Prop_Papers_01", new Vector3(1.3109894f, 0.2475538f, 5.591999f), new Quaternion(0.0f, 0.7518401f, 0.0f, 0.65934545f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1031671538; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f; SM_Prop_Papers_01 (46)
        new Placement("SM_Prop_Papers_01", new Vector3(-5.3580017f, 6.2385535f, -2.665001f), new Quaternion(0.0f, -0.13784644f, 0.0f, 0.9904536f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 632833515; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f; SM_Prop_Papers_01 (47)
        new Placement("SM_Prop_Papers_01", new Vector3(0.008010864f, 3.246554f, 4.8010025f), new Quaternion(0.0f, -0.478805f, 0.0f, 0.8779213f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 97482017; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f; SM_Prop_Papers_01 (48)
        new Placement("SM_Prop_Papers_01", new Vector3(3.800003f, 3.246554f, -3.8079987f), new Quaternion(-0.0f, -0.4666183f, 0.0f, 0.8844588f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 962041050; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f; SM_Prop_Papers_01 (49)
        new Placement("SM_Prop_Papers_01", new Vector3(3.9120026f, 6.2385535f, 3.3360023f), new Quaternion(-0.0f, -0.4666183f, 0.0f, 0.8844588f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 316890280; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f; SM_Prop_Papers_01 (50)
        new Placement("SM_Prop_Papers_01", new Vector3(2.0070038f, 9.238f, 0.15499878f), new Quaternion(0.0f, 0.99505377f, 0.0f, 0.09933783f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1080516402; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f; SM_Prop_Papers_01 (51)
        new Placement("SM_Prop_Papers_01", new Vector3(0.008010864f, 9.238f, 4.8010025f), new Quaternion(0.0f, -0.478805f, 0.0f, 0.8779213f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 611499622; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f; SM_Prop_Papers_01 (52)
        new Placement("SM_Prop_Papers_01", new Vector3(3.800003f, 9.238f, -3.8079987f), new Quaternion(-0.0f, -0.4666183f, 0.0f, 0.8844588f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1175535617; m_SourcePrefab GUID d1ca87288ad45a5479c469f358ed6781; SM_Prop_Papers_02 (21)
        new Placement("SM_Prop_Papers_02", new Vector3(0.7320099f, 3.246f, 2.6699982f), new Quaternion(0.0f, 0.0f, 0.0f, 1.0f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 553699721; m_SourcePrefab GUID d1ca87288ad45a5479c469f358ed6781; SM_Prop_Papers_02 (22)
        new Placement("SM_Prop_Papers_02", new Vector3(1.1849823f, 0.24699995f, 2.8709984f), new Quaternion(0.0f, 0.09831792f, 0.0f, 0.99515504f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 505156378; m_SourcePrefab GUID d1ca87288ad45a5479c469f358ed6781; SM_Prop_Papers_02 (23)
        new Placement("SM_Prop_Papers_02", new Vector3(4.4299927f, 0.24699995f, -0.035999298f), new Quaternion(0.0f, 0.8888809f, 0.0f, 0.4581383f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1731545799; m_SourcePrefab GUID d1ca87288ad45a5479c469f358ed6781; SM_Prop_Papers_02 (24)
        new Placement("SM_Prop_Papers_02", new Vector3(0.7320099f, 9.237446f, 2.6699982f), new Quaternion(0.0f, 0.0f, 0.0f, 1.0f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 2143872012; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f; SM_Prop_Papers_03 (21)
        new Placement("SM_Prop_Papers_03", new Vector3(-3.8069916f, 3.244524f, 4.318012f), new Quaternion(0.0f, 0.0f, 0.0f, 1.0f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 2049567936; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f; SM_Prop_Papers_03 (22)
        new Placement("SM_Prop_Papers_03", new Vector3(1.9009857f, 0.2455239f, -0.1969986f), new Quaternion(0.0f, -0.20990084f, 0.0f, 0.9777227f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 517244836; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f; SM_Prop_Papers_03 (23)
        new Placement("SM_Prop_Papers_03", new Vector3(1.8880157f, 3.244524f, 5.326004f), new Quaternion(0.0f, 0.3841484f, 0.0f, 0.92327136f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 830240088; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f; SM_Prop_Papers_03 (24)
        new Placement("SM_Prop_Papers_03", new Vector3(-3.8069916f, 9.2359705f, 4.318012f), new Quaternion(0.0f, 0.0f, 0.0f, 1.0f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 2144564319; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f; SM_Prop_Papers_03 (25)
        new Placement("SM_Prop_Papers_03", new Vector3(1.8880157f, 9.2359705f, 5.326004f), new Quaternion(0.0f, 0.3841484f, 0.0f, 0.92327136f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 928571684; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5; SM_Prop_Seat_01 (44)
        new Placement("SM_Prop_Seat_01", new Vector3(3.8379974f, 3.242f, -1.8409996f), new Quaternion(-0.06355117f, -0.42273173f, -0.091293946f, 0.8994023f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1072214956; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5; SM_Prop_Seat_01 (45)
        new Placement("SM_Prop_Seat_01", new Vector3(6.070984f, 0.243f, 5.1280174f), new Quaternion(-0.06355117f, -0.42273173f, -0.091293946f, 0.8994023f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1452414147; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5; SM_Prop_Seat_01 (46)
        new Placement("SM_Prop_Seat_01", new Vector3(-2.4579926f, 3.242f, 1.3540001f), new Quaternion(-0.06579236f, -0.40032956f, -0.08969201f, 0.90959495f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1742790422; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5; SM_Prop_Seat_01 (47)
        new Placement("SM_Prop_Seat_01", new Vector3(1.5800018f, 3.242f, -1.7579994f), new Quaternion(-0.10486552f, 0.7553223f, 0.037102412f, 0.64584434f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 231694062; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5; SM_Prop_Seat_01 (48)
        new Placement("SM_Prop_Seat_01", new Vector3(2.3339996f, 6.2339997f, 5.3030014f), new Quaternion(-0.06355117f, -0.42273173f, -0.091293946f, 0.8994023f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 432488174; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5; SM_Prop_Seat_01 (49)
        new Placement("SM_Prop_Seat_01", new Vector3(0.07600403f, 6.2339997f, 5.3860016f), new Quaternion(-0.10486552f, 0.7553223f, 0.037102412f, 0.64584434f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1847964730; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5; SM_Prop_Seat_01 (50)
        new Placement("SM_Prop_Seat_01", new Vector3(3.8379974f, 9.233446f, -1.8409996f), new Quaternion(-0.06355117f, -0.42273173f, -0.091293946f, 0.8994023f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 691009033; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5; SM_Prop_Seat_01 (51)
        new Placement("SM_Prop_Seat_01", new Vector3(-1.6130066f, 9.233446f, 5.3312263f), new Quaternion(-0.10917191f, 0.32241535f, -0.021324594f, 0.94003993f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 35233549; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5; SM_Prop_Seat_01 (52)
        new Placement("SM_Prop_Seat_01", new Vector3(1.5800018f, 9.233446f, -1.7579994f), new Quaternion(-0.10486552f, 0.7553223f, 0.037102412f, 0.64584434f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1922129160; m_SourcePrefab GUID b4fc790295bd2ba47b7b9820b821c2a3; SM_Prop_ShopShelf_Wall_01 (27)
        new Placement("SM_Prop_ShopShelf_Wall_01", new Vector3(4.442993f, 3.223f, 2.2109985f), new Quaternion(0.0f, 0.0f, 0.0f, 1.0f), new Vector3(2.5572104f, 2.557211f, 2.557211f)),
        // Reference Transform 242479770; m_SourcePrefab GUID b4fc790295bd2ba47b7b9820b821c2a3; SM_Prop_ShopShelf_Wall_01 (28)
        new Placement("SM_Prop_ShopShelf_Wall_01", new Vector3(4.442993f, 3.223f, 1.5890007f), new Quaternion(0.0f, 1.0f, 0.0f, -2.9504295e-06f), new Vector3(2.5572104f, 2.557211f, 2.557211f)),
        // Reference Transform 740125199; m_SourcePrefab GUID b4fc790295bd2ba47b7b9820b821c2a3; SM_Prop_ShopShelf_Wall_01 (29)
        new Placement("SM_Prop_ShopShelf_Wall_01", new Vector3(4.442993f, 9.214446f, 2.2109985f), new Quaternion(0.0f, 0.0f, 0.0f, 1.0f), new Vector3(2.5572104f, 2.557211f, 2.557211f)),
        // Reference Transform 1224506404; m_SourcePrefab GUID b4fc790295bd2ba47b7b9820b821c2a3; SM_Prop_ShopShelf_Wall_01 (30)
        new Placement("SM_Prop_ShopShelf_Wall_01", new Vector3(4.442993f, 9.214446f, 1.5890007f), new Quaternion(-0.0f, 1.0f, -0.0f, -2.9504295e-06f), new Vector3(2.5572104f, 2.557211f, 2.557211f)),
        // Reference Transform 387215657; m_SourcePrefab GUID a151f4c9163cc504e960dddfef069aac; SM_Prop_Study_Chair_01 (1)
        new Placement("SM_Prop_Study_Chair_01", new Vector3(-4.1016846f, 0.226f, 6.1241302f), new Quaternion(0.0f, 0.0f, 0.0f, 1.0f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 1225788610; m_SourcePrefab GUID f5b9d044c96a4b5428c97342203b91a1; SM_Prop_WaterCooler_01 (5)
        new Placement("SM_Prop_WaterCooler_01", new Vector3(5.5169983f, 0.21f, 1.0530014f), new Quaternion(0.0f, -0.70710576f, 0.0f, 0.70710784f), new Vector3(1.0f, 1.0f, 1.0f)),
        // Reference Transform 754192962; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892; SM_Prop_Television_Flat_01
        new Placement("SM_Prop_Television_Flat_01", new Vector3(-1.442276f, 10.073446f, 6.1109505f), new Quaternion(0.0f, 0.9979435f, 0.0f, 0.06409985f), new Vector3(0.54462695f, 0.54462683f, 0.54462695f)),
        // Reference Transform 822241750; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892; SM_Prop_Television_Flat_01
        new Placement("SM_Prop_Television_Flat_01", new Vector3(3.0640106f, 10.073446f, -1.6459999f), new Quaternion(-0.0f, 0.6719749f, -0.0f, 0.74057394f), new Vector3(0.54462695f, 0.54462683f, 0.54462695f)),
        // Reference Transform 715430486; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892; SM_Prop_Television_Flat_01
        new Placement("SM_Prop_Television_Flat_01", new Vector3(1.5600128f, 7.0739994f, 5.498001f), new Quaternion(-0.0f, 0.6719749f, -0.0f, 0.74057394f), new Vector3(0.54462695f, 0.54462683f, 0.54462695f)),
        // Reference Transform 191129088; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892; SM_Prop_Television_Flat_01
        new Placement("SM_Prop_Television_Flat_01", new Vector3(3.0640106f, 4.082f, -1.6459999f), new Quaternion(-0.0f, 0.6719749f, -0.0f, 0.74057394f), new Vector3(0.54462695f, 0.54462683f, 0.54462695f)),
        // Reference Transform 1641488627; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892; SM_Prop_Television_Flat_01
        new Placement("SM_Prop_Television_Flat_01", new Vector3(-3.2213898f, 4.082f, 1.5870895f), new Quaternion(0.0f, 0.69010836f, 0.0f, 0.72370607f), new Vector3(0.54462713f, 0.54462683f, 0.54462713f)),
        // Reference Transform 1148351800; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892; SM_Prop_Television_Flat_01
        new Placement("SM_Prop_Television_Flat_01", new Vector3(5.296997f, 1.083f, 5.323017f), new Quaternion(-0.0f, 0.6719749f, -0.0f, 0.74057394f), new Vector3(0.54462695f, 0.54462683f, 0.54462695f)),
    };
}
