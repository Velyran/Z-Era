using UnityEditor;
using UnityEngine;

/// <summary>
/// 选中物体快捷操作工具：
///   侧键3（后退键）→ 给所选物体添加 MeshCollider 并设为静态；
///   侧键4（前进键）→ 聚焦到所选物体，功能与 F 相同。
///
/// 覆盖范围（受 Unity 公开 API 限制，非全窗口）：
///   1) Scene 视图内 —— SceneView.duringSceneGui
///   2) Hierarchy 面板的层级条目上 ——
///      EditorApplication.hierarchyWindowItemOnGUI
/// Project / Inspector 等面板 Unity 无公开鼠标事件 API，无法覆盖。
///
/// Unity 的 MenuItem 快捷键语法不支持鼠标按键，
/// 所以侧键监听必须走上述 GUI 回调。
/// 菜单 Tools/Chapter01/为选中物体添加 MeshCollider 并设为静态
/// 保留为侧键3 的等价入口。
///
/// 与 Chapter01EnvColliderTool 的区别：
/// 本工具不生成 _Collision 子物体，而是把碰撞体挂在
/// 选中物体自己的 MeshRenderer 同级上，结构更干净，
/// 适合已经筛好的一批静态环境物体。
///
/// PolygonApocalypse 的模型 FBX 导入设置 addColliders: 0，
/// 实例本身不带任何碰撞体，因此这里直接用同一份
/// sharedMesh 挂凹体 MeshCollider（convex = false）。
/// 静态建筑无 Rigidbody，concave 合法。
///
/// 只处理选中物体自身：如果它没有 MeshFilter
/// （例如纯空物体、或者网格在子物体上），
/// 会跳过并在 Console 给出提示，不往下递归子物体。
/// </summary>
[InitializeOnLoad]
public static class SelectionColliderTool
{
    private const string MenuPath =
        "Tools/Chapter01/为选中物体添加 MeshCollider 并设为静态";

    // 植被是镂空装饰模型，不参与碰撞。
    private const string OvergrowthKeyword = "Overgrowth";

    // Unity 的 Event.button：0 左键、1 右键、2 中键，
    // 3 = 侧键（后退键）、4 = 侧键（前进键）。
    private const int SideButtonBack = 3;
    private const int SideButtonForward = 4;

    // 编辑器加载时挂上两处 GUI 事件监听。
    static SelectionColliderTool()
    {
        // 覆盖 Scene 视图。
        SceneView.duringSceneGui += OnSceneGui;

        // 覆盖 Hierarchy 面板（鼠标悬停在层级条目上时触发）。
        // 注意：只在条目行上有效，不在条目之间的空白区。
        EditorApplication.hierarchyWindowItemOnGUI +=
            OnHierarchyItemGui;
    }

    // ==== Scene 视图 ====
    private static void OnSceneGui(SceneView view)
    {
        HandleEvent();
    }

    // ==== Hierarchy 面板 ====
    private static void OnHierarchyItemGui(
        int instanceID,
        Rect selectionRect
    )
    {
        HandleEvent();
    }

    // 统一的侧键分发逻辑。
    private static void HandleEvent()
    {
        Event current = Event.current;

        // 这两个回调在每个 GUI 事件都会跑（Layout/Repaint/...），
        // 所以必须第一时间过滤掉非 MouseDown，避免无谓开销。
        if (current == null ||
            current.type != EventType.MouseDown)
        {
            return;
        }

        if (current.button != SideButtonBack &&
            current.button != SideButtonForward)
        {
            return;
        }

        // 侧键3：给所选物体添加 MeshCollider 并设为静态。
        if (current.button == SideButtonBack)
        {
            // 没有选中物体时不消费事件，避免抢走其他编辑器操作。
            if (Selection.gameObjects == null ||
                Selection.gameObjects.Length == 0)
            {
                return;
            }

            Run();

            current.Use();
            return;
        }

        // 侧键4：聚焦所选物体（等价于 F）。
        SceneView view = SceneView.lastActiveSceneView;

        if (view != null)
        {
            view.FrameSelected();
        }

        current.Use();
    }

    [MenuItem(MenuPath, false, 20)]
    private static void AddColliderAndStatic()
    {
        Run();
    }

    // 菜单校验：没有选中物体时置灰。
    [MenuItem(MenuPath, true)]
    private static bool AddColliderAndStaticValidate()
    {
        return Selection.gameObjects != null &&
            Selection.gameObjects.Length > 0;
    }

    private static void Run()
    {
        GameObject[] selection = Selection.gameObjects;

        if (selection == null ||
            selection.Length == 0)
        {
            Debug.LogWarning(
                "SelectionColliderTool：请先在 Hierarchy 中选中物体"
            );

            return;
        }

        // 用统一 Undo 组，一次 Ctrl+Z 可撤销整批操作。
        Undo.SetCurrentGroupName(
            "Add MeshCollider And Static"
        );

        int undoGroup = Undo.GetCurrentGroup();

        int colliderCount = 0;
        int staticCount = 0;
        int skipped = 0;

        foreach (GameObject target in selection)
        {
            // 跳过工程窗口里的预制体资产（不在场景中）。
            if (!target.scene.IsValid())
            {
                skipped++;
                continue;
            }

            if (ProcessTarget(target))
            {
                colliderCount++;
            }

            if (SetStatic(target))
            {
                staticCount++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log(
            "SelectionColliderTool：为 " +
            $"{selection.Length - skipped} 个选中物体" +
            "添加 MeshCollider，" +
            $"其中 {staticCount} 个已设为静态" +
            (skipped > 0 ? $"（跳过 {skipped} 个非场景物体）" : "")
        );
    }

    private static bool ProcessTarget(GameObject target)
    {
        if (target.name.Contains(OvergrowthKeyword))
        {
            // 植被是镂空装饰模型，不参与碰撞
            Debug.LogWarning(
                "SelectionColliderTool：跳过植被 " +
                target.name
            );

            return false;
        }

        MeshFilter meshFilter =
            target.GetComponent<MeshFilter>();

        if (meshFilter == null ||
            meshFilter.sharedMesh == null)
        {
            Debug.LogWarning(
                "SelectionColliderTool：" + target.name +
                " 自身没有 MeshFilter / sharedMesh，" +
                "跳过（网格可能在子物体上）"
            );

            return false;
        }

        MeshCollider collider =
            target.GetComponent<MeshCollider>();

        if (collider == null)
        {
            // Undo.AddComponent 自带撤销注册。
            collider =
                Undo.AddComponent<MeshCollider>(target);
        }

        collider.sharedMesh =
            meshFilter.sharedMesh;

        // 凹体网格必须开启 convex=false 才能贴合；
        // 静态建筑不用 Rigidbody，concave 合法。
        collider.convex = false;

        return true;
    }

    // 把目标物体标记为静态（Inspector 的 Static 复选框）。
    private static bool SetStatic(GameObject target)
    {
        if (target.isStatic)
        {
            return false;
        }

        Undo.RecordObject(target, "Set Static");

        // isStatic = true 会勾上全部 Static 子项
        // （Lightmap / Occluder / Occludee / Batching / Navigation）。
        target.isStatic = true;

        return true;
    }
}
