using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Single-building trial copied from 3_Detailed_Reference_Scene.
/// Data is relative to the reference building, not the dressing group's world origin.
/// No scene YAML is written; run the menu in Unity and inspect before saving.
/// </summary>
public static class Chapter01CommercialReferenceDressing
{
    private const string ScenePath = "Assets/Scenes/3_Chapter01.unity";
    private const string BuildingName = "SM_Bld_Commercial_Medium_01";
    private const string BuildingGuid = "5aed343e83179d249af6934614d6f0ea";
    private const string PropsFolder = "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/";
    private const string InstancePrefix = "Chapter01_Reference_";
    private const string UndoLabel = "Add Chapter01 reference interior";
    private static readonly Vector3 Anchor = new Vector3(119.8f, 0f, 46.8f);

    [MenuItem("Tools/Chapter01/参考场景装饰 - Commercial Medium 01（单栋测试）")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Chapter01ReferenceDressing：请退出 Play 模式后执行。");
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("Chapter01ReferenceDressing：请先打开并保存 3_Chapter01 场景。");
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
                if (building != null)
                {
                    Debug.LogError("Chapter01ReferenceDressing：坐标附近匹配到多个目标，未创建装饰。");
                    return;
                }
                building = candidate;
            }
        }
        if (building == null)
        {
            Debug.LogError("Chapter01ReferenceDressing：未找到 " + BuildingName + " @ " + Anchor + "，未创建装饰。");
            return;
        }
        foreach (Transform child in building)
        {
            if (child.name.StartsWith(InstancePrefix, StringComparison.Ordinal))
            {
                Debug.LogWarning("Chapter01ReferenceDressing：本栋已执行参考布置，跳过，避免重复叠放。可先 Undo 撤销上次布置。", building);
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
                Debug.LogError("Chapter01ReferenceDressing：缺少预制体 " + path + "，未创建装饰。", building);
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
            Debug.LogError("Chapter01ReferenceDressing：创建失败，已撤销本次操作。", building);
            return;
        }

        Selection.activeGameObject = building.gameObject;
        Debug.Log("Chapter01ReferenceDressing：" + building.name + " @ " + building.position +
            "，创建 " + created + " 件参考装饰（" + prefabs.Count + " 种预制体）。使用建筑局部坐标；场景尚未自动保存，可单次 Undo 撤销。", building);
    }

    // The reference car hides its vegetation, and two dresser drawers are open.
    // Keep these verified child overrides without adding separate prefab instances.
    private static void ApplyReferenceChildOverrides(GameObject instance, string prefabName)
    {
        if (prefabName == "SM_Prop_Car_Wrecked_01")
        {
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.IndexOf("Overgrowth", StringComparison.OrdinalIgnoreCase) < 0) continue;
                child.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            }
        }
        if (prefabName == "SM_Prop_Dresser_03")
        {
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "SM_Prop_Dresser_03_Drawer_05")
                {
                    child.localPosition = new Vector3(-0.467997f, 0.443f, 0.79809433f);
                    child.localRotation = new Quaternion(0.046609446f, 0f, 1.36e-7f, 0.99891317f);
                }
                else if (child.name == "SM_Prop_Dresser_03_Drawer_03")
                    child.localPosition = new Vector3(0.468f, 0.24005157f, 0.598f);
                else continue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(child);
            }
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
    // Source group: Interior_Dressing_SM_Bld_Commercial_Medium_01 (Transform 1736055158).
    // 141 prefab roots: 132 props included; main building and 8 structural building parts excluded.
    // Reference building local TRS is identity; group offset (150, 0, 100) is not copied.
    private static readonly Placement[] Data =
    {
        // Reference Transform 18923367; m_SourcePrefab GUID 596a9558c4b57ad47a381634c2962ce5
        new Placement("SM_Prop_PowerBoxes_02", new Vector3(6.723999f, 12.167f, -1.2040024f), new Quaternion(-0f, -0.70710576f, -0f, 0.70710784f), new Vector3(1.02f, 1.02f, 1.02f)),
        // Reference Transform 20877826; m_SourcePrefab GUID d4287851fb9b66e42b7d76bffc435029
        new Placement("SM_Prop_CarBattery_Table", new Vector3(-4.0460052f, 0.17699f, -3.5940018f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1.06f, 1.06f, 1.06f)),
        // Reference Transform 23167633; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(-6.1780243f, 6.25303f, 1.6580124f), new Quaternion(0f, -0.09934069f, 0f, 0.99505347f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 67897223; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-9.630997f, 6.248f, -2.473999f), new Quaternion(0f, -0.054746278f, 0f, 0.9985003f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 72294316; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892
        new Placement("SM_Prop_Television_Flat_01", new Vector3(1.2990112f, 4.093f, -4.3690033f), new Quaternion(-0f, 0.6719749f, -0f, 0.74057394f), new Vector3(0.5446271f, 0.54462683f, 0.5446271f)),
        // Reference Transform 79018129; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(5.098999f, 3.2575538f, -2.1279984f), new Quaternion(-0f, -0.13784644f, -0f, 0.9904536f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 90473379; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(-5.383026f, 6.26f, 4.259987f), new Quaternion(-0.037102103f, -0.64584655f, -0.10486557f, 0.75532037f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 102171718; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e
        new Placement("SM_Prop_CardboardBox_02", new Vector3(5.9859924f, 6.497f, 1.5520554f), new Quaternion(-0f, 0.7540296f, -0f, -0.65684044f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 116482020; m_SourcePrefab GUID 9b2427eedaee08f4e912e8c38b0ed785
        new Placement("SM_Prop_CardboardBox_Wall_01", new Vector3(0.44400024f, 9.247001f, 2.0930023f), new Quaternion(-0f, 1.4901161e-08f, -0f, 1f), new Vector3(0.8775736f, 0.8775736f, 0.8775736f)),
        // Reference Transform 129486669; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f
        new Placement("SM_Prop_Papers_03", new Vector3(-0.701004f, 6.251f, -3.6419678f), new Quaternion(-0f, 1f, -0f, 2.9504295e-06f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 132100551; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(-10.980011f, 6.25303f, 1.7669678f), new Quaternion(-0f, 0.99045324f, -0f, 0.13784924f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 133687837; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(-11.3450165f, 9.257001f, -3.578003f), new Quaternion(-0.109491855f, 0.3370557f, -0.019617205f, 0.93489045f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 206984780; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-3.858017f, 6.248f, -3.513031f), new Quaternion(-0f, 0.70711064f, -0f, -0.7071029f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 230875105; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f
        new Placement("SM_Prop_Papers_03", new Vector3(-10.556f, 3.255524f, -4.322998f), new Quaternion(0f, 0.8694701f, 0f, -0.49398553f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 268618980; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(2.072998f, 3.253f, -4.564003f), new Quaternion(-0.06355117f, -0.42273173f, -0.091293946f, 0.8994023f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 272773287; m_SourcePrefab GUID 9b2427eedaee08f4e912e8c38b0ed785
        new Placement("SM_Prop_CardboardBox_Wall_01", new Vector3(6.142029f, 6.2500005f, -0.2039566f), new Quaternion(-0f, 0.7071059f, -0f, 0.70710766f), new Vector3(0.8775741f, 0.8775736f, 0.8775741f)),
        // Reference Transform 283065483; m_SourcePrefab GUID d1ca87288ad45a5479c469f358ed6781
        new Placement("SM_Prop_Papers_02", new Vector3(4.063965f, 6.2524757f, 3.1660385f), new Quaternion(0f, -0.4581408f, 0f, 0.88887966f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 327692359; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(0.40499878f, 3.241f, -4.3690033f), new Quaternion(-0f, 0.7071089f, -0f, 0.7071047f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 334708256; m_SourcePrefab GUID 460021f9062a51b4c8ce90b5741f5775
        new Placement("SM_Prop_WorkShelf_01", new Vector3(9.675003f, 0.244f, 3.1439972f), new Quaternion(-0f, -0.70710576f, -0f, 0.70710784f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 361748472; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892
        new Placement("SM_Prop_Television_Flat_01", new Vector3(5.208023f, 10.097001f, -0.86699677f), new Quaternion(-0f, 0.9988228f, -0f, 0.048508413f), new Vector3(0.54462725f, 0.54462683f, 0.54462725f)),
        // Reference Transform 366702091; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-8.0890045f, 10.1050005f, 2.3619995f), new Quaternion(0.94793195f, -0f, -0.31847295f, 0f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 371861127; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e
        new Placement("SM_Prop_CardboardBox_02", new Vector3(-10.307999f, 3.49f, -5.0169983f), new Quaternion(-0f, 0.7659029f, -0f, 0.6429563f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 372155851; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892
        new Placement("SM_Prop_Television_Flat_01", new Vector3(3.182022f, 10.097001f, -0.13201904f), new Quaternion(0f, -0.048511107f, 0f, 0.9988227f), new Vector3(0.5446269f, 0.54462683f, 0.5446269f)),
        // Reference Transform 375953737; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e
        new Placement("SM_Prop_CardboardBox_02", new Vector3(5.8919983f, 6.497f, 4.681053f), new Quaternion(-0f, 0.95094687f, -0f, -0.3093542f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 437261222; m_SourcePrefab GUID cac907b686889dc48adf35617ebcdf43
        new Placement("SM_Prop_Luggage_01", new Vector3(1.5420074f, 9.228f, -0.8659973f), new Quaternion(0f, 0.7298292f, 0f, -0.6836296f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 449448641; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(5.208023f, 9.245001f, -0.9720001f), new Quaternion(-0f, -1.4603138e-06f, -0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 485919380; m_SourcePrefab GUID 9b2427eedaee08f4e912e8c38b0ed785
        new Placement("SM_Prop_CardboardBox_Wall_01", new Vector3(-11.710007f, 3.243f, -0.016998291f), new Quaternion(-0f, -0.70710576f, -0f, 0.70710784f), new Vector3(0.8775736f, 0.8775736f, 0.8775736f)),
        // Reference Transform 489113789; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(5.138977f, 6.25303f, -1.6849365f), new Quaternion(-0f, 0.7901838f, -0f, -0.6128699f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 491357347; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(-11.020004f, 9.260031f, 1.3239975f), new Quaternion(0f, 0.6128721f, 0f, 0.7901821f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 496394841; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(-2.6920013f, 9.260031f, 0.71900177f), new Quaternion(0f, -0.478805f, 0f, 0.8779213f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 507362208; m_SourcePrefab GUID fb6803ed082ed824a953a7b5546c613f
        new Placement("SM_Prop_Vice_01", new Vector3(-10.123993f, 0.927f, -2.8259964f), new Quaternion(-0f, 0.70710677f, -0f, 0.7071068f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 508026396; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892
        new Placement("SM_Prop_Television_Flat_01", new Vector3(-11.150009f, 10.097001f, -2.8040009f), new Quaternion(-0f, 0.9988228f, -0f, 0.048508413f), new Vector3(0.54462725f, 0.54462683f, 0.54462725f)),
        // Reference Transform 529746270; m_SourcePrefab GUID d1ca87288ad45a5479c469f358ed6781
        new Placement("SM_Prop_Papers_02", new Vector3(-9.945007f, 3.257f, -3.5270004f), new Quaternion(-0f, 0.8888809f, -0f, 0.4581383f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 532036358; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e
        new Placement("SM_Prop_CardboardBox_02", new Vector3(-11.554001f, 3.49f, -1.7730026f), new Quaternion(-0f, 0.6568426f, -0f, 0.7540277f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 534126066; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(-0.18499756f, 3.253f, -4.481003f), new Quaternion(-0.10486552f, 0.7553223f, 0.037102412f, 0.64584434f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 588907998; m_SourcePrefab GUID 193ece0069db35d4299e1a7c7ceb0b49
        new Placement("SM_Prop_Bookshelf_01", new Vector3(1.3329926f, 6.244f, 5.1080017f), new Quaternion(-0f, 1f, -0f, 2.9504295e-06f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 604895284; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-4.0829926f, 9.678378f, 0.06099701f), new Quaternion(0.7029447f, -0.076593004f, 0.076596834f, 0.7029475f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 652396648; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e
        new Placement("SM_Prop_CardboardBox_02", new Vector3(-8.574005f, 0.475f, -0.53299713f), new Quaternion(0f, 0.3093569f, 0f, 0.95094603f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 664014685; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-4.955002f, 4.101f, -2.6589966f), new Quaternion(0.8954841f, -0f, 0.4450935f, 0f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 678572607; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(1.0319977f, 6.248f, -4.5069733f), new Quaternion(-0f, 0.70710486f, -0f, 0.7071087f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 687410355; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-8.533997f, 3.6743772f, -4.6600037f), new Quaternion(-0.022690747f, 0.7067437f, -0.7067414f, -0.022695156f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 704473457; m_SourcePrefab GUID 43fc67994ff37d049ba7df9cf4715237
        new Placement("SM_Prop_Medical_Shelf_02", new Vector3(2.0820007f, 9.227f, -5.2269974f), new Quaternion(-0f, 0.017805459f, -0f, 0.9998415f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 724827812; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f
        new Placement("SM_Prop_Papers_03", new Vector3(-0.81199646f, 3.255524f, 1.2440033f), new Quaternion(-0f, 0.3841484f, -0f, 0.92327136f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 783637197; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(-11.261993f, 9.257001f, -1.3199997f), new Quaternion(-0.04791587f, 0.9907745f, 0.10038637f, -0.07741123f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 789838505; m_SourcePrefab GUID 460021f9062a51b4c8ce90b5741f5775
        new Placement("SM_Prop_WorkShelf_01", new Vector3(8.380005f, 6.238f, 4.2009964f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 790844881; m_SourcePrefab GUID d1ca87288ad45a5479c469f358ed6781
        new Placement("SM_Prop_Papers_02", new Vector3(-5.0590057f, 3.257f, 2.3899994f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 798701183; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892
        new Placement("SM_Prop_Television_Flat_01", new Vector3(1.1369934f, 7.1000004f, -4.5069733f), new Quaternion(-0f, 0.7405758f, -0f, -0.6719728f), new Vector3(0.54462683f, 0.54462683f, 0.54462683f)),
        // Reference Transform 810011611; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892
        new Placement("SM_Prop_Television_Flat_01", new Vector3(-6.867035f, 7.1000004f, 4.1480103f), new Quaternion(-0f, 0.7405758f, -0f, -0.6719728f), new Vector3(0.54462683f, 0.54462683f, 0.54462683f)),
        // Reference Transform 816061240; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(3.182022f, 9.245001f, -0.027015686f), new Quaternion(-0f, 1f, -0f, 4.157424e-06f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 864916435; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e
        new Placement("SM_Prop_CardboardBox_02", new Vector3(4.739975f, 6.497f, 4.796051f), new Quaternion(0f, -0.6429585f, 0f, 0.76590097f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 873523676; m_SourcePrefab GUID d6112e16f7e54344099937776b3d7f38
        new Placement("SM_Prop_Dresser_03", new Vector3(6.3190002f, 3.285f, 4.0339966f), new Quaternion(-0f, -0.70710576f, -0f, 0.70710784f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 886483508; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-9.632004f, 9.245001f, -2.4329834f), new Quaternion(-0f, -0.7071067f, -0f, 0.7071068f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 890009150; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-4.925003f, 6.248f, -3.038002f), new Quaternion(-0f, 0.78501785f, -0f, 0.61947316f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 899182682; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(2.707016f, 9.245001f, -1.0940018f), new Quaternion(-0f, 0.11705923f, -0f, 0.99312496f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 919937144; m_SourcePrefab GUID 193ece0069db35d4299e1a7c7ceb0b49
        new Placement("SM_Prop_Bookshelf_01", new Vector3(-11.608994f, 9.241f, 0.41999817f), new Quaternion(-0f, 0.70710576f, -0f, 0.70710784f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 929303213; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-6.9720306f, 6.248f, 4.1480103f), new Quaternion(-0f, 0.70710486f, -0f, 0.7071087f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 940961888; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f
        new Placement("SM_Prop_Papers_03", new Vector3(4.6749573f, 6.251f, 3.962059f), new Quaternion(-0f, 0.49398306f, -0f, 0.86947155f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 944990965; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(1.404007f, 3.241f, -4.3690033f), new Quaternion(-0f, -0.70710677f, -0f, 0.7071068f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 946455853; m_SourcePrefab GUID b4fc790295bd2ba47b7b9820b821c2a3
        new Placement("SM_Prop_ShopShelf_Wall_01", new Vector3(-2.6289978f, 3.234f, -2.1169968f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(2.5572104f, 2.557211f, 2.557211f)),
        // Reference Transform 955942494; m_SourcePrefab GUID d1ca87288ad45a5479c469f358ed6781
        new Placement("SM_Prop_Papers_02", new Vector3(-11.033997f, 3.257f, -1.1809998f), new Quaternion(0f, 0.8694701f, 0f, -0.49398553f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 966704610; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e
        new Placement("SM_Prop_CardboardBox_02", new Vector3(-11.460007f, 3.49f, -4.9020004f), new Quaternion(0f, 0.3093569f, 0f, 0.95094603f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 974015276; m_SourcePrefab GUID 9311c2216a86fc049b817877941d991a
        new Placement("SM_Prop_CoffeeTable_02", new Vector3(1.625f, 3.201f, 1.1429977f), new Quaternion(0f, -0.2473472f, -0f, 0.9689269f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 980256517; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-6.477997f, 3.241f, 1.7850037f), new Quaternion(-0f, -0.6194709f, -0f, 0.78501964f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 984593357; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-7.8650055f, 3.241f, 4.4260025f), new Quaternion(-0f, 0.6350427f, -0f, 0.77247703f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 993536577; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(-9.340622f, 6.26f, -3.1199875f), new Quaternion(-0.11022731f, 0.37652713f, -0.01494089f, 0.91970325f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 998158517; m_SourcePrefab GUID dbcab12f42b76414db49660110c4bca6
        new Placement("SM_Prop_Crate_Wall_01", new Vector3(0.4900055f, 0.18299997f, -3.834999f), new Quaternion(-0f, 0.027811255f, -0f, 0.99961317f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1000770964; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-11.150009f, 9.245001f, -2.9090042f), new Quaternion(-0f, -1.4603138e-06f, -0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1018679856; m_SourcePrefab GUID 3942d04f71c806e47869062034bb6e78
        new Placement("SM_Prop_BarrelStack_02", new Vector3(5.8930054f, 3.253f, -0.70700073f), new Quaternion(-0f, -0.70710576f, -0f, 0.70710784f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1045574698; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f
        new Placement("SM_Prop_Papers_03", new Vector3(-5.1799927f, 9.258f, 3.2809982f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1076826506; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(3.3770142f, 9.257001f, 0.6419754f), new Quaternion(-0.019617511f, 0.9348914f, 0.109491736f, -0.33705303f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1105972976; m_SourcePrefab GUID 364c964818c66da4aaf23d96ab7f3227
        new Placement("SM_Prop_Workbench_01", new Vector3(-10.554001f, 0.17700005f, -2.3710175f), new Quaternion(-0f, 0.70710677f, -0f, 0.7071068f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1113393037; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-6.496002f, 6.248f, 2.6299973f), new Quaternion(-0f, -1.3411044e-06f, -0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1119486086; m_SourcePrefab GUID b4fc790295bd2ba47b7b9820b821c2a3
        new Placement("SM_Prop_ShopShelf_Wall_01", new Vector3(-7.8919983f, 9.238001f, 4.3050003f), new Quaternion(-0f, 0.70710576f, -0f, 0.70710784f), new Vector3(2.5572104f, 2.557211f, 2.557211f)),
        // Reference Transform 1122968809; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(-3.5039978f, 6.25303f, 0.069000244f), new Quaternion(-0f, 0.8779199f, -0f, 0.4788075f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1133069415; m_SourcePrefab GUID dbcab12f42b76414db49660110c4bca6
        new Placement("SM_Prop_Crate_Wall_01", new Vector3(-4.110977f, 9.225f, -4.223984f), new Quaternion(-0f, 0.7071098f, -0f, -0.7071037f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1138533750; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-6.599991f, 3.241f, 4.286003f), new Quaternion(0f, -0.70710677f, 0f, 0.7071068f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1143401285; m_SourcePrefab GUID 491b8f3587293c74d80d7566981beb57
        new Placement("SM_Prop_Gas_Table_01", new Vector3(6.0879974f, 0.235f, -0.22599792f), new Quaternion(-0f, -0.70710677f, -0f, 0.7071068f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1154242377; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f
        new Placement("SM_Prop_Papers_03", new Vector3(-10.556f, 9.258f, -4.322998f), new Quaternion(-0f, 0.8694701f, -0f, -0.49398556f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1155381516; m_SourcePrefab GUID 546272eb637cd10439d135b577eeb829
        new Placement("SM_Prop_PowerBoxes_01", new Vector3(-7.0079956f, 0.053f, -4.6869965f), new Quaternion(-0f, -2.0116563e-06f, -0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1170148410; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(-5.9309998f, 3.253f, 4.0910034f), new Quaternion(-0.06355117f, -0.42273173f, -0.091293946f, 0.8994023f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1198274190; m_SourcePrefab GUID b4fc790295bd2ba47b7b9820b821c2a3
        new Placement("SM_Prop_ShopShelf_Wall_01", new Vector3(-2.9390259f, 6.241f, 1.8959885f), new Quaternion(-0f, 1f, -0f, 2.9504295e-06f), new Vector3(2.5572104f, 2.557211f, 2.557211f)),
        // Reference Transform 1198396898; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e
        new Placement("SM_Prop_CardboardBox_02", new Vector3(-3.9799957f, 9.494f, 2.9100037f), new Quaternion(-0f, 0.89116806f, -0f, 0.4536733f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1240111025; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(-9.125f, 6.26f, -0.66400146f), new Quaternion(-0.04652887f, 0.98961467f, 0.10103636f, -0.0910471f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1278038057; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(5.013031f, 9.257001f, -1.6409988f), new Quaternion(-0.109491855f, 0.3370557f, -0.019617205f, 0.93489045f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1319668268; m_SourcePrefab GUID 546272eb637cd10439d135b577eeb829
        new Placement("SM_Prop_PowerBoxes_01", new Vector3(6.7409973f, 0.052999973f, -2.867775f), new Quaternion(0f, 0.70710766f, 0f, -0.7071059f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1321845093; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-7.544998f, 3.241f, 2.2600021f), new Quaternion(-0f, 0.70710486f, -0f, 0.7071087f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1334018736; m_SourcePrefab GUID 4c607c68ae6b1ba469147aa839864a91
        new Placement("SM_Prop_Couch_01", new Vector3(2.3090057f, 0.20499998f, 2.9749985f), new Quaternion(-0f, 1f, -0f, 4.3213363e-06f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1347057475; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(2.2970123f, 6.248f, -4.646965f), new Quaternion(-0f, 0.7724788f, -0f, -0.6350405f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1405429147; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(-10.819977f, 6.25303f, -4.1870193f), new Quaternion(-0f, 0.32850376f, -0f, 0.94450265f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1407457091; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(5.3480225f, 9.245001f, 0.2930069f), new Quaternion(-0f, 0.9952666f, -0f, 0.097182214f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1433232710; m_SourcePrefab GUID f6433493c51db7a4c9ea72ac5084e6c6
        new Placement("SM_Prop_Car_Wrecked_01", new Vector3(-10.074997f, 0.231f, 1.5599976f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1456918778; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(4.9389954f, 9.260031f, 3.8259964f), new Quaternion(-0f, 0.94450176f, -0f, -0.32850644f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1484133880; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(-7.641037f, 6.26f, 4.34301f), new Quaternion(-0.09129395f, 0.8994011f, 0.06355082f, 0.42273432f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1485134120; m_SourcePrefab GUID d1ca87288ad45a5479c469f358ed6781
        new Placement("SM_Prop_Papers_02", new Vector3(-9.945007f, 9.259477f, -3.5270004f), new Quaternion(0f, 0.8888809f, 0f, 0.4581383f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1502903693; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(4.9389954f, 3.2575538f, 3.8259964f), new Quaternion(0f, 0.94450176f, 0f, -0.3285064f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1540281029; m_SourcePrefab GUID d1ca87288ad45a5479c469f358ed6781
        new Placement("SM_Prop_Papers_02", new Vector3(-5.0590057f, 9.259477f, 2.3899994f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1576214363; m_SourcePrefab GUID d1ca87288ad45a5479c469f358ed6781
        new Placement("SM_Prop_Papers_02", new Vector3(-11.033997f, 9.259477f, -1.1809998f), new Quaternion(-0f, 0.8694701f, -0f, -0.49398556f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1582633629; m_SourcePrefab GUID 855aa742b30e06d4598bd9cb0efad304
        new Placement("SM_Prop_Medical_Shelf_01", new Vector3(2.947998f, 6.23f, 4.2350006f), new Quaternion(0f, 0.98226494f, 0f, -0.1874983f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1591574105; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-5.9730377f, 6.248f, 4.1480026f), new Quaternion(-0f, -0.7071067f, -0f, 0.7071068f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1603138354; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e
        new Placement("SM_Prop_CardboardBox_02", new Vector3(-4.095001f, 9.494f, 1.7579956f), new Quaternion(-0f, 0.996214f, -0f, -0.08693515f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1607825435; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(0.36302185f, 6.26f, -4.311989f), new Quaternion(-0.09129395f, 0.8994011f, 0.06355082f, 0.42273432f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1626717425; m_SourcePrefab GUID 546272eb637cd10439d135b577eeb829
        new Placement("SM_Prop_PowerBoxes_01", new Vector3(8.681f, 12.151f, 4.4869995f), new Quaternion(-0f, 1f, -0f, 2.9504295e-06f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1627092821; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892
        new Placement("SM_Prop_Television_Flat_01", new Vector3(-6.7049866f, 4.093f, 4.286003f), new Quaternion(-0f, 0.6719749f, 0f, 0.74057394f), new Vector3(0.5446271f, 0.54462683f, 0.5446271f)),
        // Reference Transform 1661335031; m_SourcePrefab GUID dbcab12f42b76414db49660110c4bca6
        new Placement("SM_Prop_Crate_Wall_01", new Vector3(-4.6069946f, 6.228f, -4.9489975f), new Quaternion(0f, -5.9604645e-06f, 0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1705698978; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f
        new Placement("SM_Prop_Papers_03", new Vector3(-5.0690002f, 6.251f, -1.6050034f), new Quaternion(-0f, 0.9232725f, -0f, -0.3841457f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1714218173; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(-3.1890259f, 6.26f, -3.7080078f), new Quaternion(-0.06355053f, -0.42273676f, -0.09129412f, 0.8994f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1731479606; m_SourcePrefab GUID d1ca87288ad45a5479c469f358ed6781
        new Placement("SM_Prop_Papers_02", new Vector3(5.1529846f, 6.2524757f, 0.82006073f), new Quaternion(-0f, 0.49398306f, -0f, 0.86947155f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1736186638; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(2.9659576f, 6.6813774f, 4.439041f), new Quaternion(0.7067414f, 0.022693133f, -0.022688663f, 0.70674384f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1740048682; m_SourcePrefab GUID d1ca87288ad45a5479c469f358ed6781
        new Placement("SM_Prop_Papers_02", new Vector3(-0.8220062f, 6.2524757f, -2.750969f), new Quaternion(-0f, 1f, -0f, 2.9504295e-06f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1745948214; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(-2.6920013f, 3.2575538f, 0.71900177f), new Quaternion(-0f, -0.478805f, -0f, 0.8779213f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1746416326; m_SourcePrefab GUID 8cf7dbac822facd4d9c758d78a18eab5
        new Placement("SM_Prop_Seat_01", new Vector3(-8.213989f, 3.253f, 2.4549866f), new Quaternion(-0.09129395f, 0.8994011f, 0.06355082f, 0.42273432f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1758930720; m_SourcePrefab GUID 9b2427eedaee08f4e912e8c38b0ed785
        new Placement("SM_Prop_CardboardBox_Wall_01", new Vector3(1.2839966f, 0.18599999f, 4.001999f), new Quaternion(-0f, 9.685754e-07f, -0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1770888979; m_SourcePrefab GUID 193ece0069db35d4299e1a7c7ceb0b49
        new Placement("SM_Prop_Bookshelf_01", new Vector3(7.748001f, 3.247f, -4.007004f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1783964226; m_SourcePrefab GUID 43fc67994ff37d049ba7df9cf4715237
        new Placement("SM_Prop_Medical_Shelf_02", new Vector3(5.5429993f, 3.234f, -4.8059998f), new Quaternion(-0f, -0.33565298f, -0f, 0.9419857f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1809351864; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(0.9279938f, 3.241f, -2.850998f), new Quaternion(0f, 1f, 0f, -1.5199182e-06f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1837485209; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(-11.020004f, 3.2575538f, 1.3239975f), new Quaternion(-0f, 0.6128721f, -0f, 0.7901821f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1842814894; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f
        new Placement("SM_Prop_Papers_03", new Vector3(-5.1799927f, 3.255524f, 3.2809982f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1924107867; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(0.29699707f, 9.260031f, -2.0189972f), new Quaternion(0f, 0.99505377f, 0f, 0.09933783f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1928641538; m_SourcePrefab GUID 193ece0069db35d4299e1a7c7ceb0b49
        new Placement("SM_Prop_Bookshelf_01", new Vector3(-5.878998f, 3.237f, -5.6309967f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1974688082; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892
        new Placement("SM_Prop_Television_Flat_01", new Vector3(-7.4400024f, 4.093f, 2.2600021f), new Quaternion(-0f, 0.7405758f, -0f, -0.6719728f), new Vector3(0.54462683f, 0.54462683f, 0.54462683f)),
        // Reference Transform 1986386350; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-11.149994f, 9.245001f, -1.9100037f), new Quaternion(0f, 1f, 0f, -1.5199182e-06f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 1988945329; m_SourcePrefab GUID 460021f9062a51b4c8ce90b5741f5775
        new Placement("SM_Prop_WorkShelf_01", new Vector3(3.8630066f, 0.20300001f, 4.191002f), new Quaternion(0f, 1f, 0f, -5.8114523e-07f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 2009117834; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(0.29699707f, 3.2575538f, -2.0189972f), new Quaternion(-0f, 0.99505377f, -0f, 0.09933783f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 2016453530; m_SourcePrefab GUID ccaf6e6b649b6e648be75d7f9669a07e
        new Placement("SM_Prop_CardboardBox_02", new Vector3(-1.9790039f, 9.494f, 2.1760025f), new Quaternion(-0f, 0.99763584f, -0f, 0.06872179f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 2055776163; m_SourcePrefab GUID e47bf5bcf0d7a5a48bb0e204c3cd5892
        new Placement("SM_Prop_Television_Flat_01", new Vector3(-3.9630127f, 7.1000004f, -3.513031f), new Quaternion(0f, 0.6719708f, 0f, 0.74057764f), new Vector3(0.54462683f, 0.54462683f, 0.54462683f)),
        // Reference Transform 2071136706; m_SourcePrefab GUID dbcab12f42b76414db49660110c4bca6
        new Placement("SM_Prop_Crate_Wall_01", new Vector3(0.45399475f, 3.221f, 1.8669968f), new Quaternion(-0f, 1f, -0f, 2.9504295e-06f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 2074310076; m_SourcePrefab GUID c723cea1cc7e17c458ee3273c262a38f
        new Placement("SM_Prop_Papers_03", new Vector3(-0.81199646f, 9.258f, 1.2440033f), new Quaternion(0f, 0.3841484f, 0f, 0.92327136f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 2086011248; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(0.07499695f, 7.1080003f, 2.808998f), new Quaternion(-0.44509614f, 0f, 0.8954828f, -0f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 2099717150; m_SourcePrefab GUID bafdd8ac341644c408dc1a01ac60f63f
        new Placement("SM_Prop_Papers_01", new Vector3(5.098999f, 9.260031f, -2.1279984f), new Quaternion(0f, -0.13784644f, 0f, 0.9904536f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 2107240510; m_SourcePrefab GUID 460021f9062a51b4c8ce90b5741f5775
        new Placement("SM_Prop_WorkShelf_01", new Vector3(-2.1699677f, 0.203f, -4.431999f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 2131942619; m_SourcePrefab GUID 1aa6f917d1681b548964ea51691f0713
        new Placement("SM_Prop_Couch_02", new Vector3(2.4850006f, 0.21600002f, -3.862999f), new Quaternion(-0f, 0.25012854f, -0f, 0.96821266f), new Vector3(1f, 1f, 1f)),
        // Reference Transform 2132450650; m_SourcePrefab GUID 35df7bdec7b38e64c8533780e80dc1ac
        new Placement("SM_Prop_Desk_02", new Vector3(-9.056625f, 6.248f, -1.3345871f), new Quaternion(-0f, 0.99063677f, -0f, 0.13652404f), new Vector3(1f, 1f, 1f)),
    };
}
