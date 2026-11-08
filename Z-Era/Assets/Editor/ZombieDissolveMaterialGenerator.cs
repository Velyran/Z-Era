using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 僵尸皮肤溶解材质批量生成工具。
/// 背景：死亡溶解用的是专门的 Shader Graph（POLYGON_Zombie_URP_Dissolve，
/// 在 POLYGON 僵尸原 shader 上加了 _DissolveAmount），只有手工做过的
/// Zombie_Dissolve（Father_Male_01 皮肤）有这份材质，其它皮肤的原材质
/// 没有溶解属性，死亡后不会溶解。
/// 本工具以 Zombie_Dissolve 为模板（shader、溶解色、噪声等参数全照抄），
/// 只把贴图属性换成各皮肤原材质自己的贴图，批量生成
/// "皮肤名_Dissolve" 材质，生成后把对应 zombie 预制体上
/// ZombieEffects 的 Mat 字段拖成对应皮肤这份即可。
/// 生成的资产放在 Assets/Materials/Zombie_Dissolve/ 下；
/// 已存在同名资产时跳过，可重复执行（幂等）。
/// </summary>
public static class ZombieDissolveMaterialGenerator
{
    // 溶解 Shader Graph：所有生成的溶解材质共用它
    private const string DissolveShaderPath =
        "Assets/Shaders/POLYGON_Zombie_URP_Dissolve.shadergraph";

    // 模板材质：溶解参数与边缘色的取值来源
    private const string TemplateMaterialPath =
        "Assets/Materials/Zombie_Dissolve.mat";

    // 皮肤原材质所在目录：递归扫描其下全部 .mat
    private const string SkinMaterialsFolder =
        "Assets/Packages/PolygonZombies/Materials";

    // 生成资产的输出目录：必须在 Resources 下，
    // 运行时 ZombieEffects 才能按"皮肤名_Dissolve"自动加载匹配
    private const string OutputFolder =
        "Assets/Resources/ZombieDissolve";

    [MenuItem("Tools/Zombie/生成全部皮肤溶解材质")]
    private static void GenerateAll()
    {
        Shader dissolveShader =
            AssetDatabase.LoadAssetAtPath<Shader>(
                DissolveShaderPath
            );

        Material template =
            AssetDatabase.LoadAssetAtPath<Material>(
                TemplateMaterialPath
            );

        if (dissolveShader == null || template == null)
        {
            Debug.LogError(
                "ZombieDissolveMaterialGenerator：找不到溶解 Shader 或" +
                    "模板材质，未生成。请检查路径配置。"
            );

            return;
        }

        string[] sourceGuids = AssetDatabase.FindAssets(
            "t:Material",
            new[] { SkinMaterialsFolder }
        );

        EnsureFolder(OutputFolder);

        int generated = 0;
        int skipped = 0;

        foreach (string guid in sourceGuids)
        {
            string sourcePath =
                AssetDatabase.GUIDToAssetPath(guid);

            Material source =
                AssetDatabase.LoadAssetAtPath<Material>(
                    sourcePath
                );

            if (source == null)
            {
                continue;
            }

            // 输出名：皮肤名_Dissolve；已存在则跳过（幂等）
            string outputPath =
                OutputFolder + "/" + source.name + "_Dissolve.mat";

            if (AssetDatabase.LoadAssetAtPath<Material>(
                    outputPath
                ) != null)
            {
                skipped++;
                continue;
            }

            Material dissolveMaterial =
                BuildDissolveMaterial(
                    dissolveShader,
                    template,
                    source
                );

            AssetDatabase.CreateAsset(
                dissolveMaterial,
                outputPath
            );

            generated++;

            Debug.Log(
                "ZombieDissolveMaterialGenerator：已生成 " +
                    outputPath,
                dissolveMaterial
            );
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            "ZombieDissolveMaterialGenerator：完成。生成 " +
                generated + " 个，跳过（已存在）" + skipped + " 个。" +
                "请把各 zombie 预制体上 ZombieEffects 的 Mat 字段" +
                "换成对应皮肤的溶解材质。"
        );
    }

    // 整体克隆模板材质（shader、溶解参数、噪声与边缘贴图等全部
    // 属性原样带走——逐属性枚举会漏掉 Shader Graph 的
    // Texture2DAsset 类属性，导致噪声贴图丢失、初始不可见），
    // 再用皮肤原材质的主贴图覆写：皮肤间唯一差异就是贴图。
    private static Material BuildDissolveMaterial(
        Shader dissolveShader,
        Material template,
        Material source
    )
    {
        Material material = new Material(template);

        // 皮肤主贴图覆写：源材质的主贴图（_MainTex）同时写进
        // 溶解 shader 的 _MainTex 与 _BaseMap（两个属性名都可能
        // 被采样，模板里两者指向同一张贴图）。
        Texture mainTexture = null;

        if (source.HasProperty("_MainTex"))
        {
            mainTexture = source.GetTexture("_MainTex");
        }

        if (mainTexture == null)
        {
            mainTexture = source.mainTexture;
        }

        if (mainTexture != null)
        {
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", mainTexture);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", mainTexture);
            }
        }

        return material;
    }

    // 确保输出目录存在（逐级创建，兼容 Unity 特殊文件夹约定）
    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string[] segments = folder.Split('/');

        string current = segments[0];

        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[i]);
            }

            current = next;
        }
    }
}
