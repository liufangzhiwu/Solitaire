#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class ChainLevelTools 
{
    static string sourceDir = "Assets/StreamingAssets/stage_2026_1";
 
    [MenuItem("Tools/生成关卡清单 (Manifest)")]
    public static void GenerateManifest()
    {
        // 确保目录存在
        if (!Directory.Exists(sourceDir))
        {
            Debug.LogError($"目录不存在: {sourceDir}");
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
            if (f.Name == "LevelManifest.json") continue;
            manifest.levelNames.Add(f.Name);
        }

        // 写入文件
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(manifest.levelNames);
        string savePath = Path.Combine("Assets/FourWordIdiom/MultipleData/Localization/ChineseSimplified", "level_manifest.json");
        File.WriteAllText(savePath, json);

        AssetDatabase.Refresh();
        Debug.Log($"✅ 清单生成完毕！共 {manifest.levelNames.Count} 关。已保存到 {savePath}");
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