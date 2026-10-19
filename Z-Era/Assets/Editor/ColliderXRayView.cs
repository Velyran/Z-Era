using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 场景视图碰撞体 X 光显示工具（编辑器专用）：
/// 仅在 Scene view 中选中某个物体时，
/// 用 Handles 绘制忽略深度测试的碰撞体线框，
/// 让被 MeshRenderer 挡住的碰撞体也能看清。
/// 未选中的物体不画，避免整场景线框糊成一团。
///
/// 绘制范围：选中物体自身 + 其全部子物体（含隐藏物体），
/// 因此选中父级建筑即可预览整栋楼的碰撞体。
///
/// 只往 Scene view 画线，不修改任何 Collider 字段，
/// 不影响物理形状与游戏运行（Editor 脚本，不打包）。
/// 菜单开关 Tools/Chapter01/碰撞体 X 光显示 可随时关闭。
///
/// 说明：Unity 2022.3 的 Handles 没有 DrawWireMesh，
/// MeshCollider 的线框由网格顶点自绘。isReadable 为 false
/// （PolygonApocalypse 全部如此）的 FBX 在 Editor 中依然
/// 能读到顶点（Unity 文档：Editor 中访问网格数据不受
/// isReadable 限制），因此同样能画精确线框；
/// 仅当顶点确实读不到时才降级为 AABB 外框。
/// </summary>
[InitializeOnLoad]
public static class ColliderXRayView
{
    private static bool _enabled;
    private static bool _loaded;

    private const string MenuPath =
        "Tools/Chapter01/碰撞体 X 光显示";

    private const string PrefKey =
        "Z_Era_ColliderXRayView";

    // 每帧最多画的三角形边数上限，防止超大网格拖垮 Scene view。
    private const int MaxLinesPerMesh = 20000;

    static ColliderXRayView()
    {
        LoadPref();
        SceneView.duringSceneGui += OnSceneGui;
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        _enabled = !_enabled;

        SavePref();

        Debug.Log(
            "ColliderXRayView：X 光显示已" +
            (_enabled ? "开启" : "关闭")
        );

        SceneView.RepaintAll();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, _enabled);
        return true;
    }

    private static void LoadPref()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        _enabled = EditorPrefs.GetBool(PrefKey, true);
    }

    private static void SavePref()
    {
        EditorPrefs.SetBool(PrefKey, _enabled);
    }

    private static void OnSceneGui(SceneView view)
    {
        LoadPref();

        if (!_enabled)
        {
            return;
        }

        Event current = Event.current;

        // 只在重绘事件画线，别吞掉布局/输入事件。
        if (current == null ||
            current.type != EventType.Repaint)
        {
            return;
        }

        // 只画当前选中物体（含子层级）的碰撞体。
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            return;
        }

        DrawSelected(selected);
    }

    private static void DrawSelected(GameObject selected)
    {
        DrawType<BoxCollider>(selected, DrawBox);
        DrawType<SphereCollider>(selected, DrawSphere);
        DrawType<CapsuleCollider>(selected, DrawCapsule);
        DrawType<MeshCollider>(selected, DrawMesh);
    }

    // 只收集 selected 自身 + 子物体上的该类型碰撞体。
    private static void DrawType<T>(
        GameObject selected,
        System.Action<T> drawer
    ) where T : Collider
    {
        T[] found =
            selected.GetComponentsInChildren<T>(true);

        foreach (T collider in found)
        {
            if (collider != null &&
                collider.enabled)
            {
                drawer(collider);
            }
        }
    }

    private static void DrawBox(BoxCollider collider)
    {
        // 用世界矩阵画，位姿/缩放/负缩放自动跟随。
        Matrix4x4 matrix =
            collider.transform.localToWorldMatrix *
            Matrix4x4.TRS(
                collider.center,
                Quaternion.identity,
                Vector3.one
            );

        Handles.color = Color.yellow;
        Handles.zTest = CompareFunction.Always;
        Handles.matrix = matrix;

        Handles.DrawWireCube(
            Vector3.zero,
            collider.size
        );

        Handles.matrix = Matrix4x4.identity;
        Handles.zTest = CompareFunction.LessEqual;
    }

    private static void DrawSphere(SphereCollider collider)
    {
        Matrix4x4 matrix =
            collider.transform.localToWorldMatrix *
            Matrix4x4.TRS(
                collider.center,
                Quaternion.identity,
                Vector3.one
            );

        Handles.color = Color.green;
        Handles.zTest = CompareFunction.Always;
        Handles.matrix = matrix;

        Handles.DrawWireDisc(
            Vector3.zero,
            Vector3.up,
            collider.radius
        );

        Handles.DrawWireDisc(
            Vector3.zero,
            Vector3.right,
            collider.radius
        );

        Handles.DrawWireDisc(
            Vector3.zero,
            Vector3.forward,
            collider.radius
        );

        Handles.matrix = Matrix4x4.identity;
        Handles.zTest = CompareFunction.LessEqual;
    }

    private static void DrawCapsule(CapsuleCollider collider)
    {
        // 胶囊轴方向与高度按 Unity 惯例：
        // direction 0=X, 1=Y, 2=Z。
        Vector3 axis =
            collider.direction == 0
                ? Vector3.right
                : collider.direction == 1
                    ? Vector3.up
                    : Vector3.forward;

        float radius = collider.radius;

        // 半球中心距：总高 - 上下两个半径。
        float half = Mathf.Max(
            0f,
            (collider.height * 0.5f) - radius
        );

        Matrix4x4 matrix =
            collider.transform.localToWorldMatrix *
            Matrix4x4.TRS(
                collider.center,
                Quaternion.identity,
                Vector3.one
            );

        Handles.color = new Color(1f, 0.6f, 0f);
        Handles.zTest = CompareFunction.Always;
        Handles.matrix = matrix;

        Vector3 top = axis * half;
        Vector3 bottom = -axis * half;

        Handles.DrawWireDisc(top, axis, radius);
        Handles.DrawWireDisc(bottom, axis, radius);

        // 四个正交方向的竖线，构成胶囊外轮廓。
        Quaternion rot =
            Quaternion.FromToRotation(Vector3.up, axis);

        Vector3 dirA = rot * Vector3.right;
        Vector3 dirB = rot * Vector3.forward;

        Handles.DrawLine(
            top + dirA * radius,
            bottom + dirA * radius
        );

        Handles.DrawLine(
            top - dirA * radius,
            bottom - dirA * radius
        );

        Handles.DrawLine(
            top + dirB * radius,
            bottom + dirB * radius
        );

        Handles.DrawLine(
            top - dirB * radius,
            bottom - dirB * radius
        );

        Handles.matrix = Matrix4x4.identity;
        Handles.zTest = CompareFunction.LessEqual;
    }

    private static void DrawMesh(MeshCollider collider)
    {
        Mesh mesh = collider.sharedMesh;

        if (mesh == null)
        {
            return;
        }

        // Unity 文档明确：Editor 中访问网格顶点不受 isReadable 限制
        // （"Access is always allowed in the Unity Editor outside of
        // the game and rendering loop, regardless of this setting"），
        // 所以这里不检查 isReadable，PolygonApocalypse 的
        // isReadable:0 模型同样能读到精确顶点。
        // 只有真正读不到时才降级画 AABB 外框。
        Vector3[] vertices;
        int[] triangles;

        try
        {
            vertices = mesh.vertices;
            triangles = mesh.triangles;
        }
        catch (System.Exception)
        {
            // 极端情况下（编辑器循环边界）仍读不到，
            // 降级只画 AABB 外框，保证不中断 Scene view 绘制。
            DrawBounds(collider);
            return;
        }

        if (vertices == null ||
            triangles == null ||
            vertices.Length == 0 ||
            triangles.Length == 0)
        {
            DrawBounds(collider);
            return;
        }

        Handles.color = Color.cyan;
        Handles.zTest = CompareFunction.Always;

        // 用变换的世界矩阵逐顶点变换，
        // 位姿/镜像缩放（负 scale）自动跟随。
        Matrix4x4 matrix =
            collider.transform.localToWorldMatrix;

        int lineCount =
            Mathf.Min(
                triangles.Length / 3,
                MaxLinesPerMesh
            );

        Vector3[] lines =
            new Vector3[lineCount * 6];

        for (int i = 0; i < lineCount; i++)
        {
            Vector3 a = matrix.MultiplyPoint3x4(
                vertices[triangles[i * 3]]);

            Vector3 b = matrix.MultiplyPoint3x4(
                vertices[triangles[i * 3 + 1]]);

            Vector3 c = matrix.MultiplyPoint3x4(
                vertices[triangles[i * 3 + 2]]);

            lines[i * 6 + 0] = a;
            lines[i * 6 + 1] = b;
            lines[i * 6 + 2] = b;
            lines[i * 6 + 3] = c;
            lines[i * 6 + 4] = c;
            lines[i * 6 + 5] = a;
        }

        Handles.DrawLines(lines);
    }

    // 网格读不到时的降级显示：只画 AABB 外框。
    private static void DrawBounds(Collider collider)
    {
        Bounds bounds = collider.bounds;

        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        Vector3[] p = new Vector3[8];

        p[0] = center + new Vector3(-extents.x, -extents.y, -extents.z);
        p[1] = center + new Vector3(extents.x, -extents.y, -extents.z);
        p[2] = center + new Vector3(extents.x, -extents.y, extents.z);
        p[3] = center + new Vector3(-extents.x, -extents.y, extents.z);
        p[4] = center + new Vector3(-extents.x, extents.y, -extents.z);
        p[5] = center + new Vector3(extents.x, extents.y, -extents.z);
        p[6] = center + new Vector3(extents.x, extents.y, extents.z);
        p[7] = center + new Vector3(-extents.x, extents.y, extents.z);

        Handles.color = Color.cyan;
        Handles.zTest = CompareFunction.Always;
        Handles.matrix = Matrix4x4.identity;

        int[][] edges =
        {
            new[] { 0, 1 },
            new[] { 1, 2 },
            new[] { 2, 3 },
            new[] { 3, 0 },
            new[] { 4, 5 },
            new[] { 5, 6 },
            new[] { 6, 7 },
            new[] { 7, 4 },
            new[] { 0, 4 },
            new[] { 1, 5 },
            new[] { 2, 6 },
            new[] { 3, 7 }
        };

        Vector3[] lines = new Vector3[edges.Length * 2];

        for (int i = 0; i < edges.Length; i++)
        {
            lines[i * 2 + 0] = p[edges[i][0]];
            lines[i * 2 + 1] = p[edges[i][1]];
        }

        Handles.DrawLines(lines);
    }
}
