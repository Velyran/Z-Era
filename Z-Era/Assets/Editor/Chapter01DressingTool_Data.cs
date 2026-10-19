using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 第一章场景建筑室内细节布置工具（测试）：
/// 仿照 3_Detailed_Reference_Scene 中 Interior_Dressing_SM_Bld_Commercial_Large_01
/// 的装饰物布局，把同一批装饰预制体按完全相同的局部坐标/旋转/缩放
/// 放到本场景的 SM_Bld_Commercial_Large_01 (1) 之下作为子物体。
///
/// 数据来源：从 3_Detailed_Reference_Scene.unity 直接解析 YAML 提取，
/// 已剔除建筑本体、玻璃、附件、植被（目标建筑已自带）。
/// </summary>
public static class Chapter01DressingTool_Data
{
    // PolygonApocalypse Props 根目录，拼接相对路径用。
    private const string AssetsRoot = "Assets";

    private const string TargetBuildingName =
        "SM_Bld_Commercial_Large_01 (1)";

    // 单条布置记录：预制体路径 + 参考场景中的局部位姿。
    private struct DressingItem
    {
        public string PrefabPath;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;

        public DressingItem(
            string prefabPath,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale
        )
        {
            PrefabPath = prefabPath;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }
    }

    // 数据来自 3_Detailed_Reference_Scene.unity 中
    // Interior_Dressing_SM_Bld_Commercial_Large_01 的子物体
    // （已剔除建筑本体与玻璃/附件/植被，目标建筑已自带）。
    private static readonly DressingItem[] Items =
    {
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Desk_01.prefab",
            new Vector3(2.937f, 0.251f, -1.562f),
            new Quaternion(0f, 0f, 0f, 1f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Desk_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Desk_01.prefab",
            new Vector3(-3.056f, 0.251f, -5.199f),
            new Quaternion(0f, 0f, 0f, 1f),
            new Vector3(-1f, 1f, 1f)
        ),
        // SM_Prop_Desk_01 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WheelChair_01.prefab",
            new Vector3(-0.556f, 0.286f, -6.011f),
            new Quaternion(0f, 0.19199555f, 0f, 0.9813958f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WheelChair_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_Damaged_01.prefab",
            new Vector3(-7.5690002f, 0.28100002f, 1.6149974f),
            new Quaternion(0f, 0.70710677f, 0f, 0.7071068f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_Damaged_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-7.525f, 0.268f, 0.527f),
            new Quaternion(0f, 0.67029977f, 0f, 0.74209046f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CoffeeTable_02.prefab",
            new Vector3(-6.134884f, 0.26018825f, 0.47599274f),
            new Quaternion(0.012733396f, 0.72777796f, 0.013838699f, 0.685555f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_CoffeeTable_02
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-7.565f, 0.268f, -0.57f),
            new Quaternion(0f, 0.80987656f, 0f, 0.5866003f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_01 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_Damaged_01.prefab",
            new Vector3(-7.598f, 0.28100002f, -4.491f),
            new Quaternion(0f, 0.75748163f, 0f, 0.65285647f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_Damaged_01 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-7.496f, 0.268f, -3.47f),
            new Quaternion(0f, 0.67029977f, 0f, 0.74209046f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_01 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CoffeeTable_02.prefab",
            new Vector3(-6.181f, 0.26018825f, -4.41f),
            new Quaternion(0.011306072f, 0.65659654f, 0.015027362f, 0.7540075f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_CoffeeTable_02 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-7.4f, 0.268f, -6.051f),
            new Quaternion(0f, 0.46941015f, 0f, 0.8829802f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_Chair_Soft_01 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_Damaged_01.prefab",
            new Vector3(-0.576004f, 0.28100002f, 1.9810076f),
            new Quaternion(0f, 1f, 0f, 1.5497205E-06f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_Damaged_01 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-1.6640015f, 0.268f, 1.9370087f),
            new Quaternion(0f, 0.99587137f, 0f, 0.090775706f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_01 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CoffeeTable_02.prefab",
            new Vector3(-1.1449928f, 0.26018822f, 0.6926222f),
            new Quaternion(0.018771233f, 0.9997629f, 0.0011362296f, -0.010979449f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_CoffeeTable_02 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Crate_Open_01.prefab",
            new Vector3(6.526f, 6.263f, 1.547f),
            new Quaternion(0f, 0.70710677f, 0f, 0.7071068f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Crate_Open_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Gurney_01.prefab",
            new Vector3(2.007347f, 0.274f, -6.172168f),
            new Quaternion(0f, 0.68439645f, 0f, 0.72911006f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bed_Gurney_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Gurney_01.prefab",
            new Vector3(-2.652f, -0.244f, -3.786f),
            new Quaternion(0f, -0.6270657f, 0f, 0.77896637f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_Bed_Gurney_01 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Gurney_01.prefab",
            new Vector3(-4.2974625f, 0.78f, 1.432f),
            new Quaternion(-0.49977395f, 0.49995103f, 0.51164335f, -0.48836058f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_Bed_Gurney_01 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(3.575f, 0.268f, -4.192f),
            new Quaternion(0f, -0.05670925f, 0f, 0.99839073f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bed_Hospital_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-6.629f, 0.268f, -2.228f),
            new Quaternion(0f, -0.682566f, 0f, 0.73082393f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_Bed_Hospital_01 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(-5.383f, 0.513f, -5.577f),
            new Quaternion(0f, -0.50037205f, 0f, 0.8658105f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_CardboardBox_02
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_03.prefab",
            new Vector3(-5.147f, 0.513f, -3.467f),
            new Quaternion(0f, 0.17279707f, 0f, 0.98495746f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_CardboardBox_03
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(-4.327f, 0.266f, -3.547f),
            new Quaternion(0f, 0.3990139f, 0f, 0.91694486f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_CardboardBox_05
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(6.733f, 0.513f, 1.405f),
            new Quaternion(0f, 0.564397f, 0f, 0.8255035f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_CardboardBox_02 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_03.prefab",
            new Vector3(3.1710014f, 0.513f, -0.14600015f),
            new Quaternion(0f, 0.9628594f, 0f, 0.27000326f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_CardboardBox_03 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(3.991f, 0.266f, -0.226f),
            new Quaternion(0f, 0.99932945f, 0f, 0.036615532f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_CardboardBox_05 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(2.244f, 0.513f, -4.778f),
            new Quaternion(0f, 0.9851566f, 0f, 0.17165807f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_CardboardBox_02 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_03.prefab",
            new Vector3(2.122f, 0.513f, -5.666f),
            new Quaternion(0f, 0.8652312f, 0f, -0.5013731f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_CardboardBox_03 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(0.998f, 0.266f, -6.241f),
            new Quaternion(0f, 0.7234857f, 0f, -0.6903394f),
            new Vector3(1.0000007f, 1f, 1.0000007f)
        ),
        // SM_Prop_CardboardBox_05 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(1.141f, 0.513f, -5.199f),
            new Quaternion(0f, 0.93075335f, 0f, -0.36564764f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_CardboardBox_02 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(-5.03f, 0.513f, -2.402f),
            new Quaternion(0f, -0.6803751f, 0f, 0.7328641f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_CardboardBox_02 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(-6.552f, 0.513f, 1.888f),
            new Quaternion(0f, -0.01728368f, 0f, 0.99985063f),
            new Vector3(0.9999998f, 1f, 0.9999998f)
        ),
        // SM_Prop_CardboardBox_02 (5)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_Wall_01.prefab",
            new Vector3(-5.2f, 0.25f, -6.421f),
            new Quaternion(0f, 0f, 0f, 1f),
            new Vector3(0.8575736f, 0.8575737f, 0.8575737f)
        ),
        // SM_Prop_CardboardBox_Wall_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_02.prefab",
            new Vector3(-2.672f, 0.263f, 1.788f),
            new Quaternion(0f, 0.98714346f, 0f, -0.15983677f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Medical_Shelf_02
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_01.prefab",
            new Vector3(0.911f, 0.264f, -2.003f),
            new Quaternion(0f, -0.56415033f, 0f, 0.8256721f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Medical_Shelf_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_ShopShelf_Wall_01.prefab",
            new Vector3(4.398f, 1.684f, -2.346f),
            new Quaternion(0f, -0.70710677f, 0f, 0.7071068f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_ShopShelf_Wall_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_ShopShelf_Wall_01.prefab",
            new Vector3(2.186f, 1.684f, -6.783f),
            new Quaternion(0f, -4.768371E-07f, 0f, 1f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_ShopShelf_Wall_01 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_ShopShelf_Wall_01.prefab",
            new Vector3(-3.441f, 1.684f, -6.783f),
            new Quaternion(0f, -4.768371E-07f, 0f, 1f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_ShopShelf_Wall_01 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_02.prefab",
            new Vector3(-1.227f, 0.263f, -4.83f),
            new Quaternion(0f, 0.44438183f, 0f, 0.8958375f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_Medical_Shelf_02 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bedframe_01.prefab",
            new Vector3(5.731f, 3.794f, -6.748f),
            new Quaternion(0.5220873f, 0.5220874f, 0.47689068f, 0.47689086f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bedframe_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Barrel_01.prefab",
            new Vector3(7.387f, 3.251f, -6.373f),
            new Quaternion(0f, 0f, 0f, 1f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Barrel_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Crate_01.prefab",
            new Vector3(7.372f, 6.263f, 1.547f),
            new Quaternion(0f, 0f, 0f, 1f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Crate_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_Damaged_01.prefab",
            new Vector3(2.256f, 6.291f, 1.094f),
            new Quaternion(0f, 0.7699779f, 0f, 0.6380705f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_Damaged_01 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(2.1990051f, 6.278f, 0.043001294f),
            new Quaternion(0f, 0.7088788f, 0f, 0.7053303f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_01 (5)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-4.588f, 6.28f, 0.023f),
            new Quaternion(0f, -0.682566f, 0f, 0.73082393f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bed_Hospital_01 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(3.911f, 6.523f, -5.234f),
            new Quaternion(0f, 0.9851566f, 0f, 0.17165807f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_CardboardBox_02 (6)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_03.prefab",
            new Vector3(3.175f, 6.523f, -5.961f),
            new Quaternion(0f, 0.8652312f, 0f, -0.5013731f),
            new Vector3(1.000001f, 1f, 1.000001f)
        ),
        // SM_Prop_CardboardBox_03 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(1.785f, 6.2759995f, -6.295f),
            new Quaternion(0f, 0.7234857f, 0f, -0.6903393f),
            new Vector3(1.0000007f, 1f, 1.0000007f)
        ),
        // SM_Prop_CardboardBox_05 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(1.022f, 6.523f, -6.212f),
            new Quaternion(0f, 0.93075335f, 0f, -0.36564764f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_CardboardBox_02 (7)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_01.prefab",
            new Vector3(1.103f, 6.2739997f, -3.17f),
            new Quaternion(0f, -0.56415033f, 0f, 0.8256721f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Medical_Shelf_01 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_02.prefab",
            new Vector3(-1.303f, 6.273f, -6.526f),
            new Quaternion(0f, 0.0627439f, 0f, 0.99802965f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Medical_Shelf_02 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WheelChair_01.prefab",
            new Vector3(-0.2f, 6.296f, -6.28f),
            new Quaternion(0f, 0.023220984f, 0f, 0.99973035f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WheelChair_01 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Gurney_01.prefab",
            new Vector3(3.015f, 6.284f, -6.26f),
            new Quaternion(0f, 0.68439645f, 0f, 0.72911006f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_Bed_Gurney_01 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_Damaged_01.prefab",
            new Vector3(-2.784f, 6.291f, -6.435f),
            new Quaternion(0f, 0.042163942f, 0f, 0.9991107f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_Damaged_01 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-3.878f, 6.278f, -6.434f),
            new Quaternion(0f, -0.048703477f, 0f, 0.9988133f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_01 (6)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-7.152f, 6.278f, -4.525f),
            new Quaternion(0f, 0.7627632f, 0f, 0.6466779f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_01 (7)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Curtain_01.prefab",
            new Vector3(-4.446f, 6.276f, -1.199f),
            new Quaternion(0f, 0f, 0f, 1f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Curtain_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-2.243f, 6.28f, -2.283f),
            new Quaternion(0f, -0.018374573f, 0f, 0.9998312f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bed_Hospital_01 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(0.868f, 6.28f, 0.732f),
            new Quaternion(0f, 0.9999942f, 0f, -0.003405769f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bed_Hospital_01 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_02.prefab",
            new Vector3(-3.539f, 6.266f, -2.169f),
            new Quaternion(0f, 0.704627f, 0f, 0.7095779f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Medical_Shelf_02 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(-0.908f, 6.2759995f, -4.165f),
            new Quaternion(0f, 0.99999726f, 0f, 0.002346347f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_CardboardBox_05 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(-0.401f, 6.2759995f, -4.15f),
            new Quaternion(0f, 0.6269231f, 0f, 0.77908117f),
            new Vector3(0.93000114f, 0.93f, 0.93000114f)
        ),
        // SM_Prop_CardboardBox_05 (5)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_Damaged_01.prefab",
            new Vector3(2.2560005f, 9.2890005f, 1.094f),
            new Quaternion(0f, 0.7699779f, 0f, 0.6380705f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_Damaged_01 (5)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(2.1990051f, 9.276f, 0.043001294f),
            new Quaternion(0f, 0.7088788f, 0f, 0.7053303f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_Chair_Soft_01 (8)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-7.1520004f, 9.276f, -4.525f),
            new Quaternion(0f, 0.7627632f, 0f, 0.6466779f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_Chair_Soft_01 (10)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Curtain_01.prefab",
            new Vector3(-1.792f, 9.274f, -1.25f),
            new Quaternion(0f, -0.18963355f, 0f, 0.9818549f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Curtain_01 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-6.835f, 9.278001f, -2.235f),
            new Quaternion(0f, -0.018374573f, 0f, 0.9998312f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bed_Hospital_01 (5)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-4.014f, 9.278001f, 0.821f),
            new Quaternion(0f, 0.9999942f, 0f, -0.003405769f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_Bed_Hospital_01 (6)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(-0.90800095f, 9.273999f, -4.165f),
            new Quaternion(0f, 0.99999726f, 0f, 0.002346347f),
            new Vector3(1.0000012f, 1f, 1.0000012f)
        ),
        // SM_Prop_CardboardBox_05 (6)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(-0.40100098f, 9.273999f, -4.15f),
            new Quaternion(0f, 0.6269231f, 0f, 0.77908117f),
            new Vector3(0.9300003f, 0.92999995f, 0.9300003f)
        ),
        // SM_Prop_CardboardBox_05 (7)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-1.703f, 9.278001f, 0.632f),
            new Quaternion(0f, 0.6354791f, 0f, 0.7721181f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bed_Hospital_01 (7)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(4.102f, 9.521f, -2.969f),
            new Quaternion(0f, 0.6924733f, 0f, 0.72144353f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_CardboardBox_02 (8)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_03.prefab",
            new Vector3(4.23f, 9.521f, -1.96f),
            new Quaternion(0f, 0.9941535f, 0f, 0.107976004f),
            new Vector3(1.000001f, 1f, 1.000001f)
        ),
        // SM_Prop_CardboardBox_03 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(4.167f, 9.273999f, 1.511f),
            new Quaternion(0f, 0.7234857f, 0f, -0.6903393f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_CardboardBox_05 (8)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(3.4039993f, 9.521f, 1.5940003f),
            new Quaternion(0f, 0.93075335f, 0f, -0.36564764f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_CardboardBox_02 (9)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_01.prefab",
            new Vector3(-7.136f, 9.272f, -0.552f),
            new Quaternion(0f, 0.121541366f, 0f, 0.9925864f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Medical_Shelf_01 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_02.prefab",
            new Vector3(-5.656f, 9.271f, -4.256f),
            new Quaternion(0f, -0.054171443f, 0f, 0.99853164f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_Medical_Shelf_02 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WheelChair_01.prefab",
            new Vector3(-1.855f, 9.294f, -4.443f),
            new Quaternion(0f, 0.99973035f, 0f, -0.023221016f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WheelChair_01 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Gurney_01.prefab",
            new Vector3(1.857f, 9.282001f, -6.328f),
            new Quaternion(0f, 0.71077484f, 0f, -0.7034196f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_Bed_Gurney_01 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-7.166f, 9.276f, -6.164f),
            new Quaternion(0f, 0.6168094f, 0f, 0.78711253f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_Chair_Soft_01 (11)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_Damaged_01.prefab",
            new Vector3(3.695f, 12.292001f, 1.608f),
            new Quaternion(0f, 0.99478835f, 0f, -0.10196127f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_Damaged_01 (6)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(2.422f, 12.279f, 1.299f),
            new Quaternion(0f, 0.97832227f, 0f, 0.20708817f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_Chair_Soft_01 (9)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_Damaged_01.prefab",
            new Vector3(-1.3040009f, 12.292001f, -4.3589997f),
            new Quaternion(0f, 0.99997085f, 0f, -0.00763926f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_Damaged_01 (7)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-2.398f, 12.279f, -4.358f),
            new Quaternion(0f, 0.9965359f, 0f, 0.083163664f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_01 (12)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-7.1520004f, 12.279f, -4.525f),
            new Quaternion(0f, 0.7627632f, 0f, 0.6466779f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_Chair_Soft_01 (13)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Curtain_01.prefab",
            new Vector3(-6.514f, 12.277f, -0.274f),
            new Quaternion(0f, -0.027888717f, 0f, 0.999611f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Curtain_01 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_Damaged_01.prefab",
            new Vector3(2.2560005f, 15.290001f, 1.094f),
            new Quaternion(0f, 0.7699779f, 0f, 0.6380705f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_Damaged_01 (8)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(2.1990051f, 15.277f, 0.043001294f),
            new Quaternion(0f, 0.7088788f, 0f, 0.7053303f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_Chair_Soft_01 (14)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-7.1520004f, 15.277f, -4.525f),
            new Quaternion(0f, 0.7627632f, 0f, 0.6466779f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_Chair_Soft_01 (15)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Curtain_01.prefab",
            new Vector3(-2.578f, 15.275001f, 0.161f),
            new Quaternion(0f, -0.6907233f, 0f, 0.72311914f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Curtain_01 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-6.327f, 15.279001f, -2.919f),
            new Quaternion(0f, 0.6939946f, 0f, 0.71998024f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bed_Hospital_01 (8)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-4.046f, 15.279001f, 0.418f),
            new Quaternion(0f, 0.0034048553f, 0f, 0.9999942f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_Bed_Hospital_01 (9)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(1.491f, 15.923f, 0.696f),
            new Quaternion(-0.639301f, 0.7688486f, -0.008240035f, 0.009909239f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bed_Hospital_01 (10)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(4.102001f, 15.522f, -2.969f),
            new Quaternion(0f, 0.6924733f, 0f, 0.72144353f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_CardboardBox_02 (10)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_03.prefab",
            new Vector3(4.2299995f, 15.522f, -1.96f),
            new Quaternion(0f, 0.9941535f, 0f, 0.107976004f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_CardboardBox_03 (5)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(4.167f, 15.275f, 1.511f),
            new Quaternion(0f, 0.7234857f, 0f, -0.6903393f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_CardboardBox_05 (11)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(3.4039993f, 15.522f, 1.5940003f),
            new Quaternion(0f, 0.93075335f, 0f, -0.36564764f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_CardboardBox_02 (11)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_01.prefab",
            new Vector3(-7.086f, 15.273001f, -1.24f),
            new Quaternion(0f, 0.5915537f, 0f, 0.8062656f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Medical_Shelf_01 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_02.prefab",
            new Vector3(-5.655998f, 15.272f, -4.256f),
            new Quaternion(0f, -0.054171443f, 0f, 0.99853164f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Medical_Shelf_02 (5)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-7.1660004f, 15.277f, -6.164f),
            new Quaternion(0f, 0.6168094f, 0f, 0.78711253f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_Chair_Soft_01 (16)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-6.57f, 12.281001f, -1.811f),
            new Quaternion(0f, -0.029913949f, 0f, 0.9995525f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bed_Hospital_01 (11)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(4.031f, 12.524f, -0.999f),
            new Quaternion(0f, 0.7105725f, 0f, 0.703624f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_CardboardBox_02 (12)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_03.prefab",
            new Vector3(4.211f, 12.524f, -1.786f),
            new Quaternion(0f, 0.9965751f, 0f, 0.08269263f),
            new Vector3(1.000001f, 1f, 1.000001f)
        ),
        // SM_Prop_CardboardBox_03 (6)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(1.7849998f, 12.276999f, -6.295f),
            new Quaternion(0f, 0.7234857f, 0f, -0.6903393f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_CardboardBox_05 (12)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(1.0219994f, 12.524f, -6.212f),
            new Quaternion(0f, 0.93075335f, 0f, -0.36564764f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_CardboardBox_02 (13)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_01.prefab",
            new Vector3(0.535f, 12.275001f, -2.869f),
            new Quaternion(0f, -0.17447357f, 0f, 0.9846619f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Medical_Shelf_01 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_02.prefab",
            new Vector3(-1.3030014f, 12.274f, -6.526f),
            new Quaternion(0f, 0.0627439f, 0f, 0.99802965f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_Medical_Shelf_02 (6)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WheelChair_01.prefab",
            new Vector3(-3.931f, 12.297f, -6.258f),
            new Quaternion(0f, 0.023220984f, 0f, 0.99973035f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WheelChair_01 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WheelChair_01.prefab",
            new Vector3(-1.8549995f, 15.295f, -4.443f),
            new Quaternion(0f, 0.99973035f, 0f, -0.023221016f),
            new Vector3(1.0000007f, 1f, 1.0000007f)
        ),
        // SM_Prop_WheelChair_01 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Gurney_01.prefab",
            new Vector3(1.8569984f, 15.283001f, -6.328f),
            new Quaternion(0f, 0.71077484f, 0f, -0.7034196f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_Bed_Gurney_01 (5)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Gurney_01.prefab",
            new Vector3(3.0149994f, 12.285001f, -6.26f),
            new Quaternion(0f, 0.68439645f, 0f, 0.72911006f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_Bed_Gurney_01 (6)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-1.607f, 12.281001f, -2.855f),
            new Quaternion(0f, 0.69399494f, 0f, 0.7199799f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bed_Hospital_01 (12)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(0.357f, 12.281001f, 1.55f),
            new Quaternion(0f, 0.7095106f, 0f, 0.70469475f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_Bed_Hospital_01 (13)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_Damaged_01.prefab",
            new Vector3(2.2560005f, 18.279001f, 1.094f),
            new Quaternion(0f, 0.7699779f, 0f, 0.6380705f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_Damaged_01 (9)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(2.1990051f, 18.266f, 0.043001294f),
            new Quaternion(0f, 0.7088788f, 0f, 0.7053303f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_Chair_Soft_01 (17)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_Damaged_01.prefab",
            new Vector3(-2.7840004f, 18.279001f, -6.435f),
            new Quaternion(0f, 0.042163942f, 0f, 0.9991107f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_Damaged_01 (10)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-3.8779984f, 18.266f, -6.434f),
            new Quaternion(0f, -0.048703477f, 0f, 0.9988133f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Chair_Soft_01 (18)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Chair_Soft_01.prefab",
            new Vector3(-7.1520004f, 18.266f, -4.525f),
            new Quaternion(0f, 0.7627632f, 0f, 0.6466779f),
            new Vector3(1.0000005f, 1f, 1.0000005f)
        ),
        // SM_Prop_Chair_Soft_01 (19)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Curtain_01.prefab",
            new Vector3(-5.052f, 18.264f, -0.23f),
            new Quaternion(0f, 0.7136642f, 0f, -0.700488f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Curtain_01 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(-0.90800095f, 18.264f, -4.165f),
            new Quaternion(0f, 0.99999726f, 0f, 0.002346347f),
            new Vector3(1.0000012f, 1f, 1.0000012f)
        ),
        // SM_Prop_CardboardBox_05 (13)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(-0.40100098f, 18.264f, -4.15f),
            new Quaternion(0f, 0.6269231f, 0f, 0.77908117f),
            new Vector3(0.9300003f, 0.92999995f, 0.9300003f)
        ),
        // SM_Prop_CardboardBox_05 (14)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-0.642f, 18.268002f, -2.406f),
            new Quaternion(0f, -0.12083343f, 0f, 0.9926728f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_Bed_Hospital_01 (17)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-4.163f, 18.268002f, 0.444f),
            new Quaternion(0f, 0.03412309f, 0f, 0.99941766f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bed_Hospital_01 (18)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(3.9109993f, 18.511002f, -5.234f),
            new Quaternion(0f, 0.9851566f, 0f, 0.17165807f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_CardboardBox_02 (16)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_03.prefab",
            new Vector3(3.1749992f, 18.511002f, -5.961f),
            new Quaternion(0f, 0.8652312f, 0f, -0.5013731f),
            new Vector3(1.000001f, 1f, 1.000001f)
        ),
        // SM_Prop_CardboardBox_03 (8)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_05.prefab",
            new Vector3(1.7849998f, 18.264f, -6.295f),
            new Quaternion(0f, 0.7234857f, 0f, -0.6903393f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_CardboardBox_05 (16)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_CardboardBox_02.prefab",
            new Vector3(1.0219994f, 18.511002f, -6.212f),
            new Quaternion(0f, 0.93075335f, 0f, -0.36564764f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_CardboardBox_02 (17)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_01.prefab",
            new Vector3(1.1030006f, 18.262001f, -3.17f),
            new Quaternion(0f, -0.56415033f, 0f, 0.8256721f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Medical_Shelf_01 (6)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_02.prefab",
            new Vector3(-1.3030014f, 18.261002f, -6.526f),
            new Quaternion(0f, 0.0627439f, 0f, 0.99802965f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_Medical_Shelf_02 (8)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WheelChair_01.prefab",
            new Vector3(-0.20000076f, 18.284f, -6.28f),
            new Quaternion(0f, 0.023220984f, 0f, 0.99973035f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WheelChair_01 (6)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Gurney_01.prefab",
            new Vector3(3.0149994f, 18.272001f, -6.26f),
            new Quaternion(0f, 0.68439645f, 0f, 0.72911006f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_Bed_Gurney_01 (8)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-2.243f, 18.268002f, -2.283f),
            new Quaternion(0f, -0.018374573f, 0f, 0.9998312f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Bed_Hospital_01 (19)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Hospital_01.prefab",
            new Vector3(-1.655f, 18.316f, 2.255f),
            new Quaternion(0.00064323767f, 0.99867445f, -0.012892302f, 0.049827036f),
            new Vector3(0.9999999f, 1f, 0.9999999f)
        ),
        // SM_Prop_Bed_Hospital_01 (20)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Bed_Gurney_01.prefab",
            new Vector3(-6.059f, 12.285001f, -6.155f),
            new Quaternion(0f, -0.68492585f, 0f, 0.7286128f),
            new Vector3(1.0000002f, 1f, 1.0000002f)
        ),
        // SM_Prop_Bed_Gurney_01 (7)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_PowerBoxes_01.prefab",
            new Vector3(6.25f, 12.176f, 1.989f),
            new Quaternion(0f, 1f, 0f, 2.533197E-07f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_PowerBoxes_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_PowerBoxes_02.prefab",
            new Vector3(6.175f, 15.152f, -6.773f),
            new Quaternion(0f, 0f, 0f, 1f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_PowerBoxes_02
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_PowerBoxes_01.prefab",
            new Vector3(6.25f, 0.182f, 1.989f),
            new Quaternion(0f, 1f, 0f, 2.533197E-07f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_PowerBoxes_01 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_ShopShelf_Wall_01.prefab",
            new Vector3(5.742f, 19.141f, 1.892f),
            new Quaternion(0f, 1f, 0f, 5.8114523E-07f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_ShopShelf_Wall_01 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_ShopShelf_Wall_01.prefab",
            new Vector3(6.996f, 19.141f, 1.892f),
            new Quaternion(0f, 1f, 0f, 5.8114523E-07f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_ShopShelf_Wall_01 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Pot_Street_01.prefab",
            new Vector3(0.31499863f, 0.239f, -6.363f),
            new Quaternion(0f, 0f, 0f, 1f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Pot_Street_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Seat_01.prefab",
            new Vector3(3.3839989f, 0.29f, -2.34f),
            new Quaternion(0f, -0.4058121f, 0f, 0.9139565f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Seat_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Seat_01.prefab",
            new Vector3(-3.0559998f, 0.267f, -6.246f),
            new Quaternion(-0.11362076f, 0.025785923f, -0.04233632f, 0.9922868f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_Seat_01 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_ShopCheckout_01.prefab",
            new Vector3(2.7989998f, 1.111f, -1.256f),
            new Quaternion(0f, -0.08228614f, 0f, 0.99660873f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_ShopCheckout_01 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_ShopCheckout_01.prefab",
            new Vector3(-2.3409996f, 1.111f, -5.019f),
            new Quaternion(0f, 0.372692f, 0f, 0.9279551f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_ShopCheckout_01 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WorkShelf_01.prefab",
            new Vector3(7.4160004f, 0.267f, 0.817f),
            new Quaternion(0f, -0.70710677f, 0f, 0.7071068f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WorkShelf_01
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WorkShelf_01.prefab",
            new Vector3(-6.709999f, 6.261f, -3.392f),
            new Quaternion(0f, -4.768371E-07f, 0f, 1f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WorkShelf_01 (1)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WorkShelf_01.prefab",
            new Vector3(-2.7659988f, 6.261f, 0.755f),
            new Quaternion(0f, 0.7071065f, 0f, 0.7071071f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WorkShelf_01 (2)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WorkShelf_01.prefab",
            new Vector3(6.8149986f, 9.257f, -6.384f),
            new Quaternion(0f, -4.768371E-07f, 0f, 1f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WorkShelf_01 (3)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WorkShelf_01.prefab",
            new Vector3(0.6969986f, 9.259f, -3.393f),
            new Quaternion(0f, -4.768371E-07f, 0f, 1f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WorkShelf_01 (4)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WorkShelf_01.prefab",
            new Vector3(5.6520004f, 21.268f, -6.384f),
            new Quaternion(0f, -4.768371E-07f, 0f, 1f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WorkShelf_01 (5)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WorkShelf_01.prefab",
            new Vector3(-3.5410004f, 12.262f, -2.438f),
            new Quaternion(0f, 0.7131012f, 0f, -0.7010611f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WorkShelf_01 (8)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WorkShelf_01.prefab",
            new Vector3(-2.7659988f, 12.262f, -0.747f),
            new Quaternion(0f, 0.7071065f, 0f, 0.7071071f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WorkShelf_01 (9)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WorkShelf_01.prefab",
            new Vector3(0.24900055f, 15.26f, 0.787f),
            new Quaternion(0f, 0.7135827f, 0f, -0.700571f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WorkShelf_01 (10)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WorkShelf_01.prefab",
            new Vector3(-6.709999f, 18.249f, -3.392f),
            new Quaternion(0f, -4.768371E-07f, 0f, 1f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WorkShelf_01 (11)
        new DressingItem(
            "Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_WorkShelf_01.prefab",
            new Vector3(-2.7659988f, 18.249f, 0.755f),
            new Quaternion(0f, 0.7071065f, 0f, 0.7071071f),
            new Vector3(1f, 1f, 1f)
        ),
        // SM_Prop_WorkShelf_01 (12)
    };

    [MenuItem("Tools/Chapter01/为 Commercial_Large_01 (1) 添加室内细节")]
    private static void AddDressing()
    {
        GameObject building =
            GameObject.Find(TargetBuildingName);

        if (building == null)
        {
            Debug.LogError(
                "Chapter01DressingTool_Data：找不到目标建筑 " +
                TargetBuildingName
            );

            return;
        }

        // 先清除本工具上一轮生成的旧装饰物，保证可重复执行。
        RemoveOldDressing(building);

        Undo.RegisterFullObjectHierarchyUndo(
            building,
            "Add Commercial_Large_01 Dressing"
        );

        List<GameObject> created =
            new List<GameObject>();

        foreach (DressingItem item in Items)
        {
            string prefabPath =
                item.PrefabPath.StartsWith(AssetsRoot + "/")
                    ? item.PrefabPath
                    : AssetsRoot + "/" + item.PrefabPath;

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath
                );

            if (prefab == null)
            {
                Debug.LogError(
                    "Chapter01DressingTool_Data：预制体缺失 " +
                    prefabPath
                );

                continue;
            }

            GameObject instance =
                (GameObject)PrefabUtility.InstantiatePrefab(
                    prefab,
                    building.transform
                );

            // 统一命名前缀，便于识别与重复执行时清理。
            instance.name =
                "Dressing_" + prefab.name;

            instance.transform.localPosition =
                item.LocalPosition;

            instance.transform.localRotation =
                item.LocalRotation;

            instance.transform.localScale =
                item.LocalScale;

            created.Add(instance);
        }

        Debug.Log(
            "Chapter01DressingTool_Data：已为 " +
            TargetBuildingName + " 添加 " +
            created.Count + " 个装饰物（计划 " +
            Items.Length + " 个）"
        );

        Selection.activeGameObject = building;
    }

    // 清除本工具生成的旧实例：命名前缀 Dressing_。
    private static void RemoveOldDressing(GameObject building)
    {
        // 先收集再销毁：边遍历边删会打乱子物体索引。
        List<Transform> old =
            new List<Transform>();

        foreach (Transform child in building.transform)
        {
            if (child.name.StartsWith("Dressing_"))
            {
                old.Add(child);
            }
        }

        foreach (Transform child in old)
        {
            Undo.DestroyObjectImmediate(child.gameObject);
        }
    }
}
