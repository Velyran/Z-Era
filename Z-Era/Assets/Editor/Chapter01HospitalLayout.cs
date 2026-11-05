using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Single-building hospital replacement. No YAML writes, no random offsets or guessed tilt lift.</summary>
public static class Chapter01HospitalLayout
{
    private const string Folder = "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/";
    private const string Prefix = "Chapter01_Hospital_";
    private const string Label = "Replace Chapter01 hospital interior";
    private static readonly Vector3 Anchor = new Vector3(119.8f, 0f, 46.8f);
    private readonly struct Item
    {
        public readonly string Name;
        public readonly int Floor;
        public readonly Vector3 Position;
        public readonly Vector3 Euler;
        public Item(string name, int floor, float x, float z, float yaw, float roll = 0)
        { Name = name; Floor = floor; Position = new Vector3(x, 0, z); Euler = new Vector3(0, yaw, roll); }
    }
    // Heights are search windows from the validated reference placements, NOT final floor elevations.
    private static readonly float[] ReferenceFloor = { 0.18f, 3.24f, 6.24f, 9.24f };

    [MenuItem("Tools/Chapter01/医院优化 - 清除本建筑工具装饰")]
    public static void ClearToolDressing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/3_Chapter01.unity");
        if (!scene.IsValid() || !scene.isLoaded) { Debug.LogError("HospitalLayout：请打开 Chapter01 场景。"); return; }
        Transform building = FindTarget(scene);
        if (building == null) { Debug.LogError("HospitalLayout：未找到指定医院。"); return; }
        if (!EditorUtility.DisplayDialog("清除医院工具装饰", "仅清除本建筑下 Chapter01_Hospital_ / Chapter01_Reference_ 工具预制体。自定义子物体将移到建筑下保留；可一次 Undo 撤销，不自动保存。", "清除", "取消")) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Clear Chapter01 hospital dressing");
        Undo.RegisterFullObjectHierarchyUndo(building.gameObject, "Clear Chapter01 hospital dressing");
        int removed = 0;
        for (int i = building.childCount - 1; i >= 0; i--)
        {
            Transform child = building.GetChild(i);
            if (!child.name.StartsWith("Chapter01_Reference_", StringComparison.Ordinal) && !child.name.StartsWith(Prefix, StringComparison.Ordinal)) continue;
            if (!IsOwnedToolRoot(child))
            {
                Debug.LogError("HospitalLayout：发现同名前缀但非工具对象，未清除：" + child.name, child);
                continue;
            }
            Undo.DestroyObjectImmediate(child.gameObject);
            removed++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("HospitalLayout：已清除本建筑工具装饰 " + removed + " 件，可用 Ctrl+Z 撤销。", building);
    }

    [MenuItem("Tools/Chapter01/医院优化 - 为现有工具装饰补加 MeshCollider")]
    public static void AddCollidersToExisting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/3_Chapter01.unity");
        Transform building = scene.IsValid() && scene.isLoaded ? FindTarget(scene) : null;
        if (building == null) { Debug.LogError("HospitalLayout：未找到指定医院。"); return; }
        Undo.RegisterFullObjectHierarchyUndo(building.gameObject, "Add hospital dressing MeshColliders");
        int objects = 0;
        for (int i = 0; i < building.childCount; i++)
        {
            Transform child = building.GetChild(i);
            if (!child.name.StartsWith("Chapter01_Hospital_", StringComparison.Ordinal) && !child.name.StartsWith("Chapter01_Reference_", StringComparison.Ordinal)) continue;
            AddMeshColliders(child.gameObject);
            objects++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("HospitalLayout：已为现有工具装饰补加 MeshCollider，处理 " + objects + " 个根对象。", building);
    }

    [MenuItem("Tools/Chapter01/医院优化 - 替换参考布置（四层单栋测试）")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/3_Chapter01.unity");
        if (!scene.IsValid() || !scene.isLoaded) { Debug.LogError("HospitalLayout：请打开 Chapter01 场景。"); return; }
        Transform building = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            bool buildingNameMatches = t.name == "SM_Bld_Commercial_Medium_01"
                || t.name.StartsWith("SM_Bld_Commercial_Medium_01 (", StringComparison.Ordinal);
            if (!buildingNameMatches || Vector3.Distance(t.position, Anchor) > 0.1f) continue;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            if (source == null || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) != "5aed343e83179d249af6934614d6f0ea") continue;
            if (building != null) { Debug.LogError("HospitalLayout：目标不唯一。"); return; }
            building = t;
        }
        if (building == null) { Debug.LogError("HospitalLayout：未找到指定医院。"); return; }
        if (Vector3.Distance(building.lossyScale, Vector3.one) > 0.001f || Vector3.Dot(building.up, Vector3.up) < 0.999f)
        { Debug.LogError("HospitalLayout：本测试仅支持竖直、世界缩放为1的建筑。"); return; }
        List<Item> items = Layout();
        var assets = new Dictionary<string, GameObject>();
        foreach (Item item in items)
        {
            if (assets.ContainsKey(item.Name)) continue;
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + item.Name + ".prefab");
            if (asset == null) { Debug.LogError("HospitalLayout：资源缺失 " + item.Name + "，未修改场景。"); return; }
            assets.Add(item.Name, asset);
        }
        if (!EditorUtility.DisplayDialog("替换单栋医院装饰", "仅替换该建筑下 Chapter01_Reference_ / Chapter01_Hospital_ 前缀的工具对象。建筑、剧情物件及其他现有物体不删除。操作可一次 Undo 撤销，场景不会自动保存。", "替换", "取消")) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(Label);
        Undo.RegisterFullObjectHierarchyUndo(building.gameObject, Label);
        var probes = new List<GameObject>();
        var solids = new List<MeshCollider>();
        var occupied = new List<Bounds>();
        int removed = 0;
        int[] counts = new int[4];
        int[] planned = new int[4];
        int[,] rejectedByReason = new int[4, 3];
        foreach (Item item in items) planned[item.Floor]++;
        int rejected = 0;
        try
        {
            // Hidden, non-saved collision copies of the building's own meshes only.
            // Never raycast the whole scene (props, terrain and upper floors cannot hijack the support hit).
            foreach (MeshFilter mesh in building.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mesh.sharedMesh == null || !mesh.gameObject.activeInHierarchy) continue;
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(mesh.gameObject);
                if (source == null || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) != "5aed343e83179d249af6934614d6f0ea") continue;
                if (mesh.name.IndexOf("Overgrowth", StringComparison.OrdinalIgnoreCase) >= 0 || mesh.name.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                GameObject probe = new GameObject("HospitalGeometryProbe") { hideFlags = HideFlags.HideAndDontSave };
                probes.Add(probe);
                probe.transform.SetPositionAndRotation(mesh.transform.position, mesh.transform.rotation);
                probe.transform.localScale = mesh.transform.lossyScale;
                MeshCollider collider = probe.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh.sharedMesh;
                solids.Add(collider);
            }
            if (solids.Count == 0) throw new InvalidOperationException("没有可验证的建筑网格；停止替换。");
            for (int i = building.childCount - 1; i >= 0; i--)
            {
                Transform child = building.GetChild(i);
                if (!child.name.StartsWith("Chapter01_Reference_", StringComparison.Ordinal) && !child.name.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                if (!IsOwnedToolRoot(child))
                {
                    Debug.LogError("HospitalLayout：发现同名前缀但非本工具预制体对象 " + child.name + "，已停止替换，未删除任何对象。", child);
                    Undo.RevertAllDownToGroup(group);
                    return;
                }
                foreach (Transform descendant in child.GetComponentsInChildren<Transform>(true))
                {
                    if (descendant == child) continue;
                    GameObject descendantSource = PrefabUtility.GetCorrespondingObjectFromSource(descendant.gameObject);
                    GameObject rootSource = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                    if (descendantSource == null || AssetDatabase.GetAssetPath(descendantSource) != AssetDatabase.GetAssetPath(rootSource))
                    {
                        Debug.LogError("HospitalLayout：工具对象下存在非预制体子物体 " + descendant.name + "，已停止替换。", descendant);
                        Undo.RevertAllDownToGroup(group);
                        return;
                    }
                }
                Undo.DestroyObjectImmediate(child.gameObject);
                removed++;
            }
            // Preserve and reserve space for narrative/custom props that are not owned by these tools.
            foreach (Transform child in building)
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                if (source != null && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) == "5aed343e83179d249af6934614d6f0ea") continue;
                Bounds existing;
                if (child.gameObject.activeInHierarchy && TryBounds(child.gameObject, out existing)) occupied.Add(existing);
            }
            GameObject boxObject = new GameObject("HospitalClearanceProbe") { hideFlags = HideFlags.HideAndDontSave };
            probes.Add(boxObject);
            BoxCollider box = boxObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            foreach (Item item in items)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(assets[item.Name], building);
                Undo.RegisterCreatedObjectUndo(instance, Label);
                instance.name = Prefix + (item.Floor + 1) + "F_" + item.Name;
                instance.transform.localPosition = item.Position + Vector3.up * ReferenceFloor[item.Floor];
                instance.transform.localRotation = Quaternion.Euler(item.Euler);
                instance.transform.localScale = Vector3.one;
                Bounds bounds;
                float support;
                string reason;
                if (!TryBounds(instance, out bounds) || !Support(bounds, building, solids, ReferenceFloor[item.Floor], out support))
                    reason = "占地无同层楼板支撑";
                else
                {
                    // Millimetre contact tolerance only; do not float every object 1.5 cm above the floor.
                    float contactTolerance = item.Name.StartsWith("SM_Prop_Papers_", StringComparison.Ordinal) ? 0.001f : 0.0005f;
                    instance.transform.position += Vector3.up * (support + contactTolerance - bounds.min.y);
                    TryBounds(instance, out bounds);
                    reason = null;
                    boxObject.transform.position = bounds.center;
                    box.size = Vector3.Max(bounds.size, Vector3.one * 0.0001f);
                    foreach (MeshCollider solid in solids)
                    {
                        Vector3 direction; float distance;
                        if (Physics.ComputePenetration(box, boxObject.transform.position, Quaternion.identity, solid, solid.transform.position, solid.transform.rotation, out direction, out distance) && distance > 0.0005f)
                        { reason = "建筑结构净空不足"; break; }
                    }
                    if (reason == null)
                        foreach (Bounds other in occupied)
                            if (bounds.Intersects(other)) { reason = "与其他道具占地重叠"; break; }
                }
                if (reason != null)
                {
                    Debug.LogWarning("HospitalLayout：跳过 " + (item.Floor + 1) + "F " + item.Name + " " + item.Position + "：" + reason, building);
                    Undo.DestroyObjectImmediate(instance);
                    rejected++;
                    int reasonIndex = reason == "占地无同层楼板支撑" ? 0 : reason == "建筑结构净空不足" ? 1 : 2;
                    rejectedByReason[item.Floor, reasonIndex]++;
                    continue;
                }
                AddMeshColliders(instance);
                occupied.Add(bounds);
                counts[item.Floor]++;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            }
            if (Array.Exists(counts, count => count == 0)) throw new InvalidOperationException("有楼层没有任何道具通过几何检查，已回滚整次替换；请回传 Console 日志。");
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = building.gameObject;
            for (int floor = 0; floor < 4; floor++)
                Debug.Log("HospitalLayout v3：" + (floor + 1) + "F 计划=" + planned[floor] + "，创建=" + counts[floor]
                    + "，跳过[无支撑/建筑净空/道具重叠]=" + rejectedByReason[floor, 0] + "/" + rejectedByReason[floor, 1] + "/" + rejectedByReason[floor, 2], building);
            Debug.Log("HospitalLayout：移除旧工具装饰 " + removed + " 件；创建 " + string.Join("/", counts) + " 件（1F急诊/2F病房/3F病房/4F办公），跳过 " + rejected + " 件。已检查网格支撑、建筑净空和道具包围盒重叠；尚需视觉验收，未自动保存。", building);
        }
        catch (Exception exception) { Undo.RevertAllDownToGroup(group); Debug.LogException(exception); }
        finally { foreach (GameObject probe in probes) if (probe != null) UnityEngine.Object.DestroyImmediate(probe); }
    }

    private static Transform FindTarget(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            bool nameMatches = t.name == "SM_Bld_Commercial_Medium_01" || t.name.StartsWith("SM_Bld_Commercial_Medium_01 (", StringComparison.Ordinal);
            if (!nameMatches || Vector3.Distance(t.position, Anchor) > 0.1f) continue;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            if (source != null && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) == "5aed343e83179d249af6934614d6f0ea") return t;
        }
        return null;
    }

    private static void AddMeshColliders(GameObject instance)
    {
        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            MeshCollider collider = filter.GetComponent<MeshCollider>();
            if (collider == null) collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
            Undo.RecordObject(collider, "Add dressing mesh collider");
            collider.sharedMesh = filter.sharedMesh;
            collider.enabled = true;
            collider.isTrigger = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }
    }

    private static bool IsOwnedToolRoot(Transform child)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
        return source != null && AssetDatabase.GetAssetPath(source).StartsWith(Folder, StringComparison.Ordinal)
            && child.name.EndsWith("_" + source.name, StringComparison.Ordinal);
    }

    private static bool TryBounds(GameObject instance, out Bounds bounds)
    {
        bounds = new Bounds(); bool found = false;
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }

    private static bool Support(Bounds bounds, Transform building, List<MeshCollider> solids, float expected, out float support)
    {
        support = float.MinValue;
        float min = float.MaxValue;
        float expectedWorld = building.TransformPoint(new Vector3(0, expected, 0)).y;
        for (int i = 0; i < 5; i++)
        {
            float x = i == 4 ? bounds.center.x : (i % 2 == 0 ? bounds.min.x + 0.02f : bounds.max.x - 0.02f);
            float z = i == 4 ? bounds.center.z : (i < 2 ? bounds.min.z + 0.02f : bounds.max.z - 0.02f);
            Ray ray = new Ray(new Vector3(x, expectedWorld + 0.45f, z), Vector3.down);
            float top = float.MinValue;
            foreach (MeshCollider solid in solids)
            {
                RaycastHit hit;
                if (solid.Raycast(ray, out hit, 0.9f) && hit.normal.y > 0.95f) top = Mathf.Max(top, hit.point.y);
            }
            if (top == float.MinValue) return false;
            support = Mathf.Max(support, top); min = Mathf.Min(min, top);
        }
        return support - min < 0.04f;
    }

    private static List<Item> Layout()
    {
        var list = new List<Item>();
        // 1F: triage to the west, treatment bays to the east, waiting seats toward the entrance.
        list.Add(new Item("SM_Prop_Desk_02", 0, -9.4f, -3.0f, 90));
        list.Add(new Item("SM_Prop_Desk_02", 0, -5.4f, -3.0f, 90));
        int[] waitingYaw = { 168, 192, 154, 206, 178, 224 };
        int waitingIndex = 0;
        foreach (float x in new[] { -9.2f, -6.0f, -2.8f })
        {
            list.Add(new Item("SM_Prop_Chair_Metal_01", 0, x, 3.7f, waitingYaw[waitingIndex++]));
            list.Add(new Item("SM_Prop_Chair_Metal_01", 0, x, 2.4f, waitingYaw[waitingIndex++]));
        }
        list.Add(new Item("SM_Prop_Bed_Gurney_01", 0, 6.0f, -2.8f, 82));
        list.Add(new Item("SM_Prop_Bed_Gurney_01", 0, 6.3f, 2.7f, 104));
        list.Add(new Item("SM_Prop_Desk_01", 0, -1.5f, -3.4f, 74));
        foreach (float z in new[] { -3.0f, 0.0f, 3.0f })
        {
            list.Add(new Item("SM_Prop_Bed_Gurney_01", 0, 3.0f, z, 90));
            list.Add(new Item("SM_Prop_Medical_IVStand_01", 0, 4.7f, z - 0.7f, 0));
        }
        list.Add(new Item("SM_Prop_Medical_Shelf_01", 0, 8.3f, -3.5f, 0));
        list.Add(new Item("SM_Prop_Medical_Shelf_02", 0, 8.3f, 3.6f, 180));
        list.Add(new Item("SM_Prop_WheelChair_01", 0, -2.0f, -2.7f, 155));
        list.Add(new Item("SM_Prop_WheelChair_01", 0, 7.2f, 0.5f, 228));
        list.Add(new Item("SM_Prop_Chair_Metal_01", 0, -4.0f, 0.8f, 225, 90));
        list.Add(new Item("SM_Prop_Sign_Emergency_01", 0, -1.0f, 4.0f, 180));
        list.Add(new Item("SM_Prop_Medical_Container_Broken_01", 0, -7.5f, -0.5f, 30));
        list.Add(new Item("SM_Prop_MedicalBox_01", 0, 6.2f, -3.5f, 12));

        // 2F is a general ward: two orderly opposing rows, six beds, central nursing/storage.
        // This is an independent layout, not a shared ward template with a few changed rotations.
        foreach (float x in new[] { -9.3f, -3.8f, 2.0f })
        {
            int[] wardYaw = { 78, 96, 118, 246, 272, 294 };
            list.Add(new Item("SM_Prop_Bed_Hospital_01", 1, x, -3.0f, wardYaw[(int)((x + 10) * 2) % wardYaw.Length]));
            list.Add(new Item("SM_Prop_Bed_Hospital_01", 1, x, 3.0f, wardYaw[((int)((x + 10) * 2) + 3) % wardYaw.Length]));
            list.Add(new Item("SM_Prop_Medical_IVStand_01", 1, x + 1.6f, -3.6f, 12));
            list.Add(new Item("SM_Prop_Medical_IVStand_01", 1, x - 1.6f, 3.6f, 344));
            list.Add(new Item("SM_Prop_Chair_Soft_01", 1, x + 1.8f, 1.6f, x < -5 ? 252 : 286));
            list.Add(new Item("SM_Prop_Desk_01", 1, x + 0.4f, 0.0f, x < -5 ? 12 : 348));
        }
        list.Add(new Item("SM_Prop_Desk_02", 1, 7.2f, -3.2f, 90));
        list.Add(new Item("SM_Prop_Seat_01", 1, 8.7f, -3.2f, 270));
        list.Add(new Item("SM_Prop_Medical_Shelf_01", 1, 7.2f, 3.8f, 180));
        list.Add(new Item("SM_Prop_Medical_Shelf_02", 1, 9.0f, 3.8f, 180));
        list.Add(new Item("SM_Prop_WheelChair_01", 1, 6.4f, 0.5f, 180));
        list.Add(new Item("SM_Prop_Medical_Container_01", 1, -6.5f, 0.9f, 0));
        list.Add(new Item("SM_Prop_CardboardBox_02", 1, 8.7f, 1.7f, 5));

        // 3F is an improvised isolation ward: a longitudinal bed cluster to the west,
        // displaced overflow gurneys in the east and a separate supply/quarantine station.
        // No bed/IV/desk/wheelchair anchor is reused from the 2F arrangement.
        list.Add(new Item("SM_Prop_Bed_Hospital_01", 2, -9.5f, -2.4f, 0));
        list.Add(new Item("SM_Prop_Bed_Hospital_01", 2, -6.2f, -2.2f, 8));
        list.Add(new Item("SM_Prop_Bed_Hospital_01", 2, -9.0f, 2.4f, 180));
        list.Add(new Item("SM_Prop_Bed_Gurney_01", 2, -5.6f, 2.3f, 164));
        list.Add(new Item("SM_Prop_Bed_Gurney_01", 2, 2.0f, -2.6f, 18));
        list.Add(new Item("SM_Prop_Bed_Gurney_BodySheet_01", 2, 5.6f, 2.3f, 172));
        list.Add(new Item("SM_Prop_Bed_Hospital_01", 2, 2.5f, 2.5f, 192));
        list.Add(new Item("SM_Prop_Medical_IVStand_01", 2, -10.8f, -2.2f, 0));
        list.Add(new Item("SM_Prop_Medical_IVStand_01", 2, -7.4f, -2.4f, 18));
        list.Add(new Item("SM_Prop_Medical_IVStand_01", 2, -10.6f, 2.6f, 0));
        list.Add(new Item("SM_Prop_Medical_IVStand_01", 2, 0.6f, -3.0f, 0));
        list.Add(new Item("SM_Prop_Medical_IVStand_01", 2, 4.0f, 2.8f, 22));
        list.Add(new Item("SM_Prop_Desk_01", 2, -2.1f, 3.5f, 180));
        list.Add(new Item("SM_Prop_Chair_Metal_01", 2, -2.2f, 2.2f, 18));
        list.Add(new Item("SM_Prop_Medical_Shelf_02", 2, 8.4f, -2.6f, 270));
        list.Add(new Item("SM_Prop_Medical_Shelf_01", 2, 8.4f, 0.0f, 270));
        list.Add(new Item("SM_Prop_WheelChair_01", 2, -3.4f, -1.1f, 138));
        list.Add(new Item("SM_Prop_WheelChair_01", 2, 6.2f, -1.8f, 216));
        list.Add(new Item("SM_Prop_Chair_Metal_01", 2, -0.5f, -0.7f, 55, 90));
        list.Add(new Item("SM_Prop_Sign_Quarantine_01", 2, -3.2f, -3.9f, 0));
        list.Add(new Item("SM_Prop_Medical_Container_Broken_01", 2, 0.2f, 1.2f, 35));
        list.Add(new Item("SM_Prop_Crate_Open_01", 2, 7.8f, 3.8f, 22));
        list.Add(new Item("SM_Prop_Medical_Container_01", 2, 8.5f, 2.2f, 0));
        list.Add(new Item("SM_Prop_MedicalBox_01", 2, -4.2f, 0.4f, 35));

        // 4F administration: six desk stations, archive shelves and a meeting/lounge corner.
        foreach (Vector3 p in new[] { new Vector3(-9.4f,0,-2.8f), new Vector3(-5.8f,0,-2.8f), new Vector3(-2.2f,0,-2.8f), new Vector3(-9.4f,0,2.8f), new Vector3(-5.8f,0,2.8f), new Vector3(-2.2f,0,2.8f) })
        {
            list.Add(new Item("SM_Prop_Desk_02", 3, p.x, p.z, p.z < 0 ? 90 : 270));
            list.Add(new Item("SM_Prop_Seat_01", 3, p.x + (p.z < 0 ? 1.3f : -1.3f), p.z, p.z < 0 ? 270 : 90));
        }
        list.Add(new Item("SM_Prop_Bookshelf_01", 3, 7.8f, -3.9f, 0));
        list.Add(new Item("SM_Prop_Bookshelf_01", 3, 5.4f, -3.9f, 0));
        list.Add(new Item("SM_Prop_Bookshelf_01", 3, 3.0f, -3.9f, 0));
        list.Add(new Item("SM_Prop_Couch_01", 3, 7.8f, 2.9f, 270));
        list.Add(new Item("SM_Prop_Couch_02", 3, 3.4f, 3.2f, 180));
        list.Add(new Item("SM_Prop_CoffeeTable_02", 3, 4.5f, 1.2f, 12));
        list.Add(new Item("SM_Prop_Chair_Metal_01", 3, -4.4f, 0.7f, 30, 90));
        list.Add(new Item("SM_Prop_Crate_Open_01", 3, 8.2f, -1.4f, 15));
        list.Add(new Item("SM_Prop_CardboardBox_02", 3, -10.6f, 0.6f, 22));

        // Extra primary furniture is attempted before litter, so papers cannot reserve furniture footprints.
        list.Add(new Item("SM_Prop_Bed_Hospital_01", 1, 5.8f, -1.0f, 8));
        list.Add(new Item("SM_Prop_Bed_Hospital_01", 1, -7.0f, 0.0f, 352));
        list.Add(new Item("SM_Prop_Bed_Hospital_01", 2, -2.7f, -2.7f, 32));
        list.Add(new Item("SM_Prop_Bed_Gurney_01", 2, 5.4f, -2.7f, 204));
        list.Add(new Item("SM_Prop_Desk_02", 2, -1.2f, 0.8f, 108));
        list.Add(new Item("SM_Prop_Desk_01", 2, 7.5f, 2.5f, 252));
        list.Add(new Item("SM_Prop_Desk_01", 3, -0.2f, 0.0f, 22));
        list.Add(new Item("SM_Prop_Desk_02", 3, 3.5f, -1.2f, 166));
        for (int floor = 0; floor < 4; floor++)
        {
            for (int i = 0; i < 8; i++)
            {
                float x = -10.4f + (i % 4) * 5.1f;
                float z = i < 4 ? -1.7f : 1.7f;
                string furniture = i % 3 == 0 ? "SM_Prop_CoffeeTable_02" : i % 3 == 1 ? "SM_Prop_Chair_Metal_01" : "SM_Prop_Seat_01";
                list.Add(new Item(furniture, floor, x + floor * 0.17f, z, (i * 53 + floor * 37 + 11) % 360));
            }
        }

        // Dense small-scale storytelling: distinct deterministic patterns per floor.
        // Main equipment is placed first; support/clearance checks remain mandatory for every item.
        for (int floor = 0; floor < 4; floor++)
        {
            for (int row = 0; row < 4; row++)
            for (int column = 0; column < 9; column++)
            {
                float x = -10.2f + column * 2.45f + (floor % 2) * 0.32f;
                float z = -1.8f + row * 1.15f + (floor - 1) * 0.13f;
                string paper = "SM_Prop_Papers_0" + (1 + (column + row + floor) % 3);
                list.Add(new Item(paper, floor, x, z, (column * 47 + row * 71 + floor * 29) % 360));
            }
            for (int i = 0; i < 10; i++)
            {
                float x = -10.5f + i * 2.3f;
                float z = i % 2 == 0 ? -4.4f : 4.4f;
                string supply = i % 4 == 0 ? "SM_Prop_CardboardBox_02" : i % 4 == 1 ? "SM_Prop_Chair_Metal_01" : i % 4 == 2 ? "SM_Prop_CoffeeTable_02" : "SM_Prop_Seat_01";
                list.Add(new Item(supply, floor, x, z, (i * 31 + floor * 29) % 360));
            }
        }
        return list;
    }
}
