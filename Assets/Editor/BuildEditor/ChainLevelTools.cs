#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class ChainLevelTools 
{
    // static string sourceDir = "Assets/StreamingAssets/stage_2026_1";
    //
    // [MenuItem("Tools/关卡处理/生成关卡清单 (Manifest)")]
    // public static void GenerateManifest()
    // {
    //     // 确保目录存在
    //     if (!Directory.Exists(sourceDir))
    //     {
    //         Debug.LogError($"目录不存在: {sourceDir}");
    //         return;
    //     }
    //
    //     // 查找所有 json
    //     DirectoryInfo dir = new DirectoryInfo(sourceDir);
    //     FileInfo[] files = dir.GetFiles("*.json");
    //     
    //     LevelManifest manifest = new LevelManifest();
    //     manifest.levelNames = new List<string>();
    //     System.Array.Sort(files, (a, b) => CompareNatural(a.Name, b.Name));
    //     foreach (var f in files)
    //     {
    //         // 排除 manifest 自己，防止死循环
    //         if (f.Name == "LevelManifest.json") continue;
    //         manifest.levelNames.Add(f.Name);
    //     }
    //
    //     // 写入文件
    //     string json = Newtonsoft.Json.JsonConvert.SerializeObject(manifest.levelNames);
    //     string savePath = Path.Combine("Assets/FourWordIdiom/MultipleData/Localization/ChineseSimplified", "level_manifest.json");
    //     File.WriteAllText(savePath, json);
    //
    //     AssetDatabase.Refresh();
    //     Debug.Log($"✅ 清单生成完毕！共 {manifest.levelNames.Count} 关。已保存到 {savePath}");
    // }
    // 一键生成所有语言的清单
    [MenuItem("Tools/关卡处理/生成关卡清单 (Manifest - 多语言)")]
    public static void GenerateAllManifests()
    {
        // ================= 配置区域 =================
        // 参数 1: 关卡 JSON 所在的源文件夹路径
        // 参数 2: Localization 下对应的语言文件夹名称
        
        // 1. 生成中文版
        GenerateManifestForLanguage("Assets/StreamingAssets/stage_2026_1", "ChineseSimplified");
        
        // 2. 生成英文版 (假设你的英文关卡放在 stage_2026_1_en，请根据实际情况修改)
        GenerateManifestForLanguage("Assets/StreamingAssets/stage_20260424_en", "English");
        
        // 如果后续有繁体中文或其他语言，继续在这里加一行即可
        // GenerateManifestForLanguage("Assets/StreamingAssets/stage_2026_1_tw", "ChineseTraditional");
        // ===========================================

        AssetDatabase.Refresh();
        Debug.Log("🎉 所有配置语言的清单均已处理完毕！");
    }
    /// <summary>
    /// 核心逻辑：为指定语言生成关卡清单
    /// </summary>
    private static void GenerateManifestForLanguage(string sourceDir, string languageFolder)
    {
        if (!Directory.Exists(sourceDir))
        {
            Debug.LogWarning($"⚠️ 跳过: 目录不存在: {sourceDir} (如果你还没配置该语言的关卡，可忽略此警告)");
            return;
        }

        // 查找所有 json
        DirectoryInfo dir = new DirectoryInfo(sourceDir);
        FileInfo[] files = dir.GetFiles("*.json");
        
        LevelManifest manifest = new LevelManifest();
        manifest.levelNames = new List<string>();
        System.Array.Sort(files, (a, b) => CompareNatural(a.Name, b.Name));
        
        foreach (var f in files)
        {
            // 排除 manifest 自己，防止死循环
            if (f.Name == "LevelManifest.json" || f.Name == "level_manifest.json") continue;
            manifest.levelNames.Add(f.Name);
        }

        // 写入文件
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(manifest.levelNames);
        
        // 动态拼接保存路径
        string targetDir = Path.Combine("Assets/FourWordIdiom/MultipleData/Localization", languageFolder);
        if (!Directory.Exists(targetDir)) 
        {
            Directory.CreateDirectory(targetDir); // 如果对应语言文件夹不存在，自动创建
        }
        
        string savePath = Path.Combine(targetDir, "level_manifest.json");
        File.WriteAllText(savePath, json);

        Debug.Log($"✅ [{languageFolder}] 清单生成完毕！共 {manifest.levelNames.Count} 关。已保存到: {savePath}");
    }
    
    // 自然排序辅助函数
    static int CompareNatural(string x, string y)
    {
        if (x.Length != y.Length) return x.Length - y.Length;
        return x.CompareTo(y);
    }
}
#endif

[System.Serializable]
public class LevelManifest
{
    public List<string> levelNames;
}