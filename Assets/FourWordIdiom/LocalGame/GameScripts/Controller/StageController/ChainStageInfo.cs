using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Middleware;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

#region 数据结构

[Serializable]
public class LevelData
{
    public int levelId;
    public int slotsDefault;
    public int slotsRewarded;
    public int movesLimit;
    public bool isRandom;
    public List<CategoryData> categories;
    public List<string> stock;
    public List<ColumnData> cardColumns;
}

[Serializable]
public class CategoryData
{
    public string categoryId;
    public bool icon;
    public List<WordData> wordsData;
}
[Serializable]
public class WordData
{
    public string wordId;       //  词语id/内容 （如 "Gorilla"）
    public bool icon;           // 是否显示图标
}

[Serializable]
public class ColumnData
{
    public List<string> cards;
}
#endregion

/// <summary>
/// 关卡信息管理类 - 负责加载、解析和提供关卡数据
/// </summary>
public class ChainStageInfo
{
    // private const string StageDirectory = "stage_2026_1";
    #region 私有字段

    private TextAsset _StageFile;       // 关卡文本资源 safsadfs
    private readonly int _StageNumber;  // 关卡编号
    private readonly int _StageInfoId;  // 关卡配置ID
    private string StageDirectory 
    {
        get 
        {
            string lang = ToolUtil.GetLanguageBundle();
            if (lang.Equals("english", StringComparison.OrdinalIgnoreCase))
            {
                return "stage_20260424_en"; // 英文关卡目录
            }
            else
            {
                return "stage_2026_1";    // 默认/中文关卡目录
            }
        }
    }
    #endregion

    #region 公有属性

    /// <summary> 棋盘数据 </summary>
    public LevelData CurrBoardData { get; private set; }
    
    /// <summary> 关卡编号 </summary>
    public int StageNumber => _StageNumber;
    
    #endregion

    #region 构造函数

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="stageInfoId">关卡配置ID, 配置文件id</param>
    /// <param name="stageNumber">关卡编号, 显示关卡</param>
    public ChainStageInfo(int stageInfoId, int stageNumber)
    {
        _StageInfoId = stageInfoId;
        _StageNumber = stageNumber;
        CoroutineRunner.StartCoroutine( LoadStageData()); // 立即加载数据
    }

    public List<string> GetAllCardIds()
    {
        return new List<string>();
    }
    #endregion
    
    #region 私有方法

    /// <summary>
    /// 加载关卡数据
    /// </summary>
    private IEnumerator LoadStageData()
    {
        string filename = $"关卡_{_StageInfoId}.json";
        string filepath = ToolUtil.GetPlatformAdaptedPath(filename, StageDirectory);
        yield return LoadLevelCoroutine(filepath, (string text) =>
        {
            Debug.Log("关卡文本： " + text);
            string cleanText = text.Trim(); 
    
            // 有时候 BOM 去不掉，需要强制处理
            if (cleanText.StartsWith("\uFEFF")) 
            {
                cleanText = cleanText.Substring(1);
            }
            
            CurrBoardData = JsonConvert.DeserializeObject<LevelData>(cleanText);
        });
        
    }
    public IEnumerator LoadLevelCoroutine(string fullPath, System.Action<string> onComplete)
    {
        // 发起请求 (就像访问网页一样访问本地文件)
        using (UnityWebRequest www = UnityWebRequest.Get(fullPath))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // 读取成功，返回文本内容
                onComplete?.Invoke(www.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"读取失败: {fullPath}\n错误: {www.error}");
            }
        }
    }

    #endregion
}