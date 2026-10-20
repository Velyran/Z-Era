using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 层级右键菜单工具：禁用/启用选中物体及其所有子物体上的
/// 项目脚本组件。静态类，无需挂载到任何物体。
///
/// 菜单入口（在 Hierarchy 中右键物体，或选中后 Ctrl+点击）：
///   GameObject/禁用脚本（含子物体）
///   GameObject/启用脚本（含子物体）
///
/// 设计取舍：
///   · 只处理 Assembly-CSharp（本项目的游戏脚本程序集），
///     不碰 Unity 内置组件（Transform/Animator/Camera 等），
///     避免误删误伤导致物体损坏；
///   · 采用"禁用"（enabled = false）而非删除组件，
///     Inspector 里的配置与引用全部保留，可随时恢复；
///   · 支持多选，整批操作收拢为一次 Undo，Ctrl+Z 全撤。
/// </summary>
public static class SelectionScriptToggle
{
    // 本项目的游戏脚本程序集名（无 asmdef 时默认为此）。
    private const string GameAssemblyName = "Assembly-CSharp";

    private const string DisableMenuPath =
        "GameObject/禁用脚本（含子物体）";

    private const string EnableMenuPath =
        "GameObject/启用脚本（含子物体）";

    // 保护名单：这些管理器被其他系统按 Instance 静态访问，
    // 禁用会让依赖方收到空引用，一律跳过。
    private static readonly HashSet<string> ProtectedTypes =
        new HashSet<string>
        {
            "QuestManager",
            "DialogueFlags",
            "SettingsManager",
            "SceneTransitionController",
            "PickupController",
            "NoiseManager",
            "BulletHitMarkerManager",
            "BulletHoleManager",
            "TipPanelController",
            "TaskPanelController",
            "SettingsUIRoot",
            "ComicIntroController"
        };

    // ============ 禁用 ============

    [MenuItem(DisableMenuPath, false, 0)]
    private static void DisableScripts()
    {
        int componentCount = Toggle(false, out int objectCount);

        Debug.Log(
            "SelectionScriptToggle：已禁用 " +
            $"{objectCount} 个物体上的 {componentCount} 个脚本组件。"
        );
    }

    [MenuItem(DisableMenuPath, true)]
    private static bool DisableScriptsValidate()
    {
        return HasSelection();
    }

    // ============ 启用 ============

    [MenuItem(EnableMenuPath, false, 0)]
    private static void EnableScripts()
    {
        int componentCount = Toggle(true, out int objectCount);

        Debug.Log(
            "SelectionScriptToggle：已启用 " +
            $"{objectCount} 个物体上的 {componentCount} 个脚本组件。"
        );
    }

    [MenuItem(EnableMenuPath, true)]
    private static bool EnableScriptsValidate()
    {
        return HasSelection();
    }

    // ============ 公共逻辑 ============

    private static bool HasSelection()
    {
        return Selection.gameObjects != null &&
            Selection.gameObjects.Length > 0;
    }

    // enable 为 false 禁用、true 启用。
    // 返回处理的组件数，objectCount 输出涉及的物体数。
    private static int Toggle(bool enable, out int objectCount)
    {
        Undo.SetCurrentGroupName(
            enable ? "Enable Scripts" : "Disable Scripts"
        );

        int undoGroup = Undo.GetCurrentGroup();

        int components = 0;
        objectCount = 0;

        foreach (GameObject target in Selection.gameObjects)
        {
            // 只处理场景中的物体，跳过工程窗口里的预制体资产。
            if (!target.scene.IsValid())
            {
                continue;
            }

            bool touchedThisObject = false;

            // true 参数把未激活的子物体也纳入，
            // 保证隐藏物体上的脚本一起被禁用。
            MonoBehaviour[] behaviours =
                target.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (!IsGameScript(behaviour))
                {
                    continue;
                }

                // 已经是目标状态的不重复记 Undo。
                if (behaviour.enabled == enable)
                {
                    continue;
                }

                Undo.RecordObject(behaviour, enable ? "Enable" : "Disable");

                behaviour.enabled = enable;

                components++;
                touchedThisObject = true;
            }

            if (touchedThisObject)
            {
                objectCount++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        return components;
    }

    // 判断是否为项目自己的脚本：
    // 1) 所属程序集必须是 Assembly-CSharp（排除 Unity 内置组件）；
    // 2) 不在保护名单内（排除被静态访问的管理器）。
    private static bool IsGameScript(MonoBehaviour behaviour)
    {
        if (behaviour == null)
        {
            return false;
        }

        MonoScript script =
            MonoScript.FromMonoBehaviour(behaviour);

        if (script == null)
        {
            return false;
        }

        System.Type type = script.GetClass();

        if (type == null)
        {
            return false;
        }

        System.Reflection.Assembly assembly = type.Assembly;

        if (assembly == null ||
            assembly.GetName().Name != GameAssemblyName)
        {
            return false;
        }

        return !ProtectedTypes.Contains(type.Name);
    }
}
