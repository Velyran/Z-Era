using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Small two-floor hospital dressing tool. Measured reference floor windows and
/// conservative local x/z placements are verified against building-only mesh probes.
/// </summary>
public static class Chapter01SmallHospitalLayout
{
    private const string Folder = "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/";
    private const string Prefix = "Chapter01_SmallHospital_";
    private const string Label = "Generate Chapter01 small hospital wards";
    private const string TargetName = "SM_Bld_Commercial_Small_01 (2)";
    private const string TargetGuid = "cdc1a057ab9e2284ca9b28f28a9a5b5a";
    private static readonly Vector3 Anchor = new Vector3(163f, 0f, 43f);

    private readonly struct Item
    {
        public readonly string Name;
        public readonly int Floor;
        public readonly Vector3 Position;
        public readonly float Yaw;

        public Item(string name, int floor, float x, float z, float yaw, int seed, float yawSpread = 7f)
        {
            Name = name;
            Floor = floor;
            Position = new Vector3(x, 0f, z);
            Yaw = yaw + (DeterministicYaw(seed) / 359f * 2f - 1f) * yawSpread;
        }
    }

    // Fill only from measured evidence. This is deliberately not a guessed dimension/elevation.
    private static readonly float[] FloorHeight = { 0.38f, 4.30f };

    [MenuItem("Tools/Chapter01/小楼医院 - 清除本楼装饰")]
    public static void ClearToolDressing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = OpenChapterScene();
        if (!scene.IsValid() || !scene.isLoaded) return;
        Transform building = FindTarget(scene);
        if (building == null) { Debug.LogError("SmallHospitalLayout：未找到唯一指定小楼医院。"); return; }

        List<Transform> owned = FindToolRoots(building, true);
        if (owned == null) return;
        if (!EditorUtility.DisplayDialog("清除小楼医院工具装饰",
            "仅清除本建筑下 Chapter01_SmallHospital_ 前缀且来自 Props 的工具对象。其他既有装饰保留并继续占位。操作可一次 Undo 撤销，不自动保存。",
            "清除", "取消")) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Clear Chapter01 small hospital dressing");
        Undo.RegisterFullObjectHierarchyUndo(building.gameObject, "Clear Chapter01 small hospital dressing");
        foreach (Transform root in owned) Undo.DestroyObjectImmediate(root.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        Debug.Log("SmallHospitalLayout：已清除本楼工具装饰 " + owned.Count + " 件，可用 Ctrl+Z 撤销。", building);
    }

    [MenuItem("Tools/Chapter01/小楼医院 - 补加MeshCollider")]
    public static void AddCollidersToExisting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = OpenChapterScene();
        if (!scene.IsValid() || !scene.isLoaded) return;
        Transform building = FindTarget(scene);
        if (building == null) { Debug.LogError("SmallHospitalLayout：未找到唯一指定小楼医院。"); return; }
        List<Transform> roots = FindToolRoots(building, true);
        if (roots == null) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add small hospital MeshColliders");
        Undo.RegisterFullObjectHierarchyUndo(building.gameObject, "Add small hospital MeshColliders");
        int filters = 0;
        foreach (Transform root in roots) filters += AddMeshColliders(root.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        Debug.Log("SmallHospitalLayout：已为 " + roots.Count + " 个工具根对象补加 MeshCollider（网格 " + filters + " 个）。", building);
    }

    [MenuItem("Tools/Chapter01/小楼医院 - 生成两层病房")]
    public static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = OpenChapterScene();
        if (!scene.IsValid() || !scene.isLoaded) return;
        Transform building = FindTarget(scene);
        if (building == null) { Debug.LogError("SmallHospitalLayout：未找到唯一指定小楼医院。"); return; }
        List<Item> items = Layout();
        if (items.Count == 0)
        {
            Debug.LogWarning("SmallHospitalLayout：Layout 尚待填入经证据确认的两层 floor 高度及 x/z；未修改场景。", building);
            return;
        }
        if (FloorHeight.Length != 2) { Debug.LogError("SmallHospitalLayout：必须提供两层 floor 高度。"); return; }

        var assets = new Dictionary<string, GameObject>();
        foreach (Item item in items)
        {
            if (assets.ContainsKey(item.Name)) continue;
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + item.Name + ".prefab");
            if (asset == null) { Debug.LogError("SmallHospitalLayout：Props 资源缺失 " + item.Name + "，未修改场景。"); return; }
            assets.Add(item.Name, asset);
        }
        List<Transform> oldRoots = FindToolRoots(building, true);
        if (oldRoots == null) return;
        if (!EditorUtility.DisplayDialog("生成小楼医院两层病房",
            "仅替换本建筑下 Chapter01_SmallHospital_ 工具对象；其他既有装饰保留并占位。操作可一次 Undo 撤销，场景不会自动保存。",
            "生成", "取消")) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(Label);
        Undo.RegisterFullObjectHierarchyUndo(building.gameObject, Label);
        var probes = new List<GameObject>();
        var solids = new List<MeshCollider>();
        try
        {
            BuildBuildingProbes(building, probes, solids);
            if (solids.Count == 0) throw new InvalidOperationException("没有可验证的建筑网格；停止生成。");
            foreach (Transform old in oldRoots) Undo.DestroyObjectImmediate(old.gameObject);

            var occupied = new List<Bounds>();
            foreach (Transform child in building)
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                if (source != null && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) == TargetGuid) continue;
                Bounds existing;
                if (child.gameObject.activeInHierarchy && TryBounds(child.gameObject, out existing)) occupied.Add(existing);
            }
            GameObject clearance = new GameObject("SmallHospitalClearanceProbe") { hideFlags = HideFlags.HideAndDontSave };
            probes.Add(clearance);
            BoxCollider box = clearance.AddComponent<BoxCollider>();
            box.isTrigger = true;
            int[] counts = new int[2];
            int[] planned = new int[2];
            int[] rejected = new int[2];
            foreach (Item plannedItem in items) planned[plannedItem.Floor]++;
            foreach (Item item in items)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(assets[item.Name], building);
                Undo.RegisterCreatedObjectUndo(instance, Label);
                instance.name = Prefix + (item.Floor + 1) + "F_" + item.Name;
                instance.transform.localPosition = item.Position + Vector3.up * FloorHeight[item.Floor];
                instance.transform.localRotation = Quaternion.Euler(0f, item.Yaw, 0f);
                instance.transform.localScale = Vector3.one;
                Bounds bounds;
                float support;
                string reason = null;
                if (!TryBounds(instance, out bounds) || !Support(bounds, building, solids, FloorHeight[item.Floor], out support)) reason = "占地无同层楼板支撑";
                else
                {
                    instance.transform.position += Vector3.up * (support + 0.0005f - bounds.min.y);
                    TryBounds(instance, out bounds);
                    clearance.transform.position = bounds.center;
                    box.size = Vector3.Max(bounds.size, Vector3.one * 0.0001f);
                    foreach (MeshCollider solid in solids)
                    {
                        Vector3 direction; float distance;
                        if (Physics.ComputePenetration(box, clearance.transform.position, Quaternion.identity, solid, solid.transform.position, solid.transform.rotation, out direction, out distance) && distance > 0.0005f)
                        { reason = "建筑结构净空不足"; break; }
                    }
                    if (reason == null)
                        foreach (Bounds other in occupied)
                            if (bounds.Intersects(other)) { reason = "与其他道具占地重叠"; break; }
                }
                if (reason != null)
                {
                    Debug.LogWarning("SmallHospitalLayout：跳过 " + instance.name + "：" + reason, building);
                    Undo.DestroyObjectImmediate(instance);
                    rejected[item.Floor]++;
                    continue;
                }
                AddMeshColliders(instance);
                occupied.Add(bounds);
                counts[item.Floor]++;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            }
            if (counts[0] == 0 || counts[1] == 0) throw new InvalidOperationException("有楼层没有任何道具通过几何检查，已回滚。");
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = building.gameObject;
            Debug.Log("SmallHospitalLayout：1F 计划=" + planned[0] + "，创建=" + counts[0] + "，拒绝=" + rejected[0] + "；2F 计划=" + planned[1] + "，创建=" + counts[1] + "，拒绝=" + rejected[1] + "。已检查建筑支撑、净空、既有装饰占位和道具重叠，未自动保存。", building);
        }
        catch (Exception exception) { Undo.RevertAllDownToGroup(group); Debug.LogException(exception); }
        finally { foreach (GameObject probe in probes) if (probe != null) UnityEngine.Object.DestroyImmediate(probe); }
    }

    private static Scene OpenChapterScene()
    {
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/3_Chapter01.unity");
        if (!scene.IsValid() || !scene.isLoaded) Debug.LogError("SmallHospitalLayout：请打开 Chapter01 场景。");
        return scene;
    }

    private static Transform FindTarget(Scene scene)
    {
        Transform found = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != TargetName || Vector3.Distance(t.position, Anchor) > 0.1f) continue;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            if (source == null || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) != TargetGuid) continue;
            if (found != null) { Debug.LogError("SmallHospitalLayout：目标不唯一。", t); return null; }
            found = t;
        }
        return found;
    }

    private static List<Transform> FindToolRoots(Transform building, bool validateChildren)
    {
        var roots = new List<Transform>();
        for (int i = 0; i < building.childCount; i++)
        {
            Transform child = building.GetChild(i);
            if (!child.name.StartsWith(Prefix, StringComparison.Ordinal)) continue;
            if (!IsOwnedToolRoot(child))
            {
                Debug.LogError("SmallHospitalLayout：发现前缀对象但其 prefab 不来自 Props，已中止：" + child.name, child);
                return null;
            }
            if (validateChildren && !ChildrenAreSourcePrefab(child))
            {
                Debug.LogError("SmallHospitalLayout：工具对象附加了非源子物体，已中止：" + child.name, child);
                return null;
            }
            roots.Add(child);
        }
        return roots;
    }

    private static bool IsOwnedToolRoot(Transform root)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(root.gameObject);
        return source != null && AssetDatabase.GetAssetPath(source).StartsWith(Folder, StringComparison.Ordinal)
            && root.name.EndsWith("_" + source.name, StringComparison.Ordinal);
    }

    private static bool ChildrenAreSourcePrefab(Transform root)
    {
        GameObject rootSource = PrefabUtility.GetCorrespondingObjectFromSource(root.gameObject);
        string path = rootSource == null ? string.Empty : AssetDatabase.GetAssetPath(rootSource);
        foreach (Transform descendant in root.GetComponentsInChildren<Transform>(true))
        {
            if (descendant == root) continue;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(descendant.gameObject);
            if (source == null || AssetDatabase.GetAssetPath(source) != path) return false;
        }
        return true;
    }

    private static void BuildBuildingProbes(Transform building, List<GameObject> probes, List<MeshCollider> solids)
    {
        foreach (MeshFilter mesh in building.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mesh.sharedMesh == null || !mesh.gameObject.activeInHierarchy) continue;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(mesh.gameObject);
            if (source == null || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) != TargetGuid) continue;
            if (mesh.name.IndexOf("Overgrowth", StringComparison.OrdinalIgnoreCase) >= 0 || mesh.name.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            GameObject probe = new GameObject("SmallHospitalGeometryProbe") { hideFlags = HideFlags.HideAndDontSave };
            probes.Add(probe);
            probe.transform.SetPositionAndRotation(mesh.transform.position, mesh.transform.rotation);
            probe.transform.localScale = mesh.transform.lossyScale;
            MeshCollider collider = probe.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh.sharedMesh;
            solids.Add(collider);
        }
    }

    private static int AddMeshColliders(GameObject instance)
    {
        int count = 0;
        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            MeshCollider collider = filter.GetComponent<MeshCollider>();
            if (collider == null) collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
            Undo.RecordObject(collider, "Add small hospital dressing MeshCollider");
            collider.sharedMesh = filter.sharedMesh;
            collider.enabled = true;
            collider.isTrigger = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            count++;
        }
        return count;
    }

    private static bool TryBounds(GameObject instance, out Bounds bounds)
    {
        bounds = new Bounds(); bool found = false;
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled) continue;
            if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }

    private static bool Support(Bounds bounds, Transform building, List<MeshCollider> solids, float expected, out float support)
    {
        support = float.MinValue;
        float min = float.MaxValue;
        float expectedWorld = building.TransformPoint(new Vector3(0f, expected, 0f)).y;
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

    private static int DeterministicYaw(int seed)
    {
        unchecked
        {
            uint x = (uint)seed + 0x9E3779B9u;
            x ^= x >> 16; x *= 0x7FEB352Du; x ^= x >> 15; x *= 0x846CA68Bu; x ^= x >> 16;
            return (int)(x % 360u);
        }
    }

    private static List<Item> Layout()
    {
        var list = new List<Item>();
        // Measured Small01 bounds: keep every anchor inside x[-6.3,5.8], z[-4,4].
        // Beds form orderly rows; chairs and papers deliberately vary by deterministic seed.
        for (int floor = 0; floor < 2; floor++)
        {
            float zA = -2.65f, zB = 2.55f;
            float[] xs = { -5.3f, -1.8f, 1.7f, 5.0f };
            float[] bedYawA = floor == 0 ? new[] { 75f, 100f, 75f, 100f } : new[] { 0f, 15f, 0f, 15f };
            float[] bedYawB = floor == 0 ? new[] { 260f, 285f, 260f, 285f } : new[] { 175f, 195f, 175f, 195f };
            for (int i = 0; i < xs.Length; i++)
            {
                string bed = (floor == 1 && i == 3) ? "SM_Prop_Bed_Gurney_01" : "SM_Prop_Bed_Hospital_01";
                list.Add(new Item(bed, floor, xs[i], zA, bedYawA[i], 101 + floor * 37 + i));
                list.Add(new Item(bed, floor, xs[i], zB, bedYawB[i], 151 + floor * 41 + i));
                list.Add(new Item("SM_Prop_Medical_IVStand_01", floor, xs[i] + (i % 2 == 0 ? 0.85f : -0.85f), zA + 0.75f, 0f, 201 + floor * 43 + i));
            }
            list.Add(new Item("SM_Prop_Desk_01", floor, -4.65f, 0.0f, floor == 0 ? 90f : 270f, 301 + floor));
            list.Add(new Item("SM_Prop_Desk_02", floor, 4.25f, 0.0f, floor == 0 ? 270f : 90f, 311 + floor));
            for (int i = 0; i < 6; i++)
            {
                float x = -5.4f + (i % 3) * 4.0f;
                float z = i < 3 ? -0.55f : 0.75f;
                list.Add(new Item("SM_Prop_Chair_Metal_01", floor, x, z, i < 3 ? 90f : 270f, 401 + floor * 17 + i));
            }
            list.Add(new Item("SM_Prop_WheelChair_01", floor, -5.45f, 3.55f, 180f, 451 + floor));
            list.Add(new Item("SM_Prop_Medical_Shelf_01", floor, 5.05f, 3.55f, 180f, 461 + floor));
            list.Add(new Item("SM_Prop_Medical_Shelf_02", floor, 5.05f, -3.55f, 0f, 471 + floor));
            list.Add(new Item("SM_Prop_Papers_01", floor, -3.3f, -0.2f, 0f, 481 + floor));
            list.Add(new Item("SM_Prop_Papers_02", floor, 3.0f, 0.9f, 0f, 491 + floor));
            list.Add(new Item("SM_Prop_CardboardBox_02", floor, -5.4f, -3.55f, 0f, 501 + floor));
        }
        return list;
    }
}
