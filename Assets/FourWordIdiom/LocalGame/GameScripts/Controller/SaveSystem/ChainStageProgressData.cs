using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// 关卡数据
/// </summary>
[System.Serializable]
public class ChainStageProgressData
{
    #region 核心字段
    public bool isFirstEnter = true;    // 是否首次进入关卡
    public int stageId = -1;                                  // 关卡ID

    // 当前步数
    public int currentSteps;
    // 🔥 记录已经集齐并消除的分类数量
    public int finishedCategoryCount = 0;
    // 牌桌上的列数据
    public List<ColumnData> tableauColumns = new List<ColumnData>();
    // 手牌数据 (Stock)
    public List<string> stockCardIds = new List<string>();
    // 废牌堆数据 (Waste)
    public List<string> wasteCardIds = new List<string>();
    // 分类槽数据 (Goal Slots)
    public List<CategoryData> categorySlots = new List<CategoryData>();
    #endregion
    
    #region 初始化方法
    // 从配置文件初始化
    public void InitializeFromStageInfo(ChainStageInfo stageInfo)
    {
        stageId = stageInfo.StageNumber;
        isFirstEnter = true;
        currentSteps =  stageInfo.CurrBoardData.movesLimit > 0 ? stageInfo.CurrBoardData.movesLimit : 999;
        tableauColumns = new List<ColumnData>();
        foreach (var columnData in stageInfo.CurrBoardData.cardColumns)
        {
            ColumnData newColumn = new ColumnData { cards = new List<string>() };
            newColumn.cards = new List<string>(columnData.cards);
            tableauColumns.Add(newColumn);
        }
        stockCardIds = new List<string>(stageInfo.CurrBoardData.stock);
        wasteCardIds = new List<string>();
        categorySlots = new List<CategoryData>(stageInfo.CurrBoardData.categories.Count);
        finishedCategoryCount = 0;
    }
    
    // 从本类初始化
    public void InitializeFromExisting(ChainStageProgressData sourceData)
    {
        stageId = sourceData.stageId;
        currentSteps = sourceData.currentSteps;
        tableauColumns = sourceData.tableauColumns;
        stockCardIds = sourceData.stockCardIds;
        wasteCardIds = sourceData.wasteCardIds;
        categorySlots = sourceData.categorySlots;
        isFirstEnter = sourceData.isFirstEnter;
        finishedCategoryCount = sourceData.finishedCategoryCount;
    }
    #endregion

    #region 文件操作
    public void LoadFromFile(ChainStageInfo stageInfo)
    {
        string saveFileName = CreateLevelIdentifier(stageInfo.StageNumber);
        string filePath = Path.Combine(Application.persistentDataPath, saveFileName);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning("未找到关卡进度文件，使用默认数据初始化： "+ filePath);
            Debug.LogWarning("未找到关卡进度文件，数据是： "+ JsonConvert.SerializeObject(stageInfo));
            InitializeFromStageInfo(stageInfo);
            return;
        }

        try
        {
            string encryptedJson = File.ReadAllText(filePath, Encoding.UTF8);

            string json = SecurityProvider.RestoreData(encryptedJson);

            if (!ValidateJson(json))
            {
                Debug.LogError("JSON数据格式无效");
                InitializeFromStageInfo(stageInfo);
                return;
            }

            var loadedData = JsonConvert.DeserializeObject<ChainStageProgressData>(json);
            
            if (loadedData.stageId <= 0 ) 
            {
                InitializeFromStageInfo(stageInfo);
            }
            else
            {
                InitializeFromExisting(loadedData);
            }
        }
        catch(System.Exception e)
        {
            Debug.LogError($"加载关卡数据失败: {e.Message}");
            InitializeFromStageInfo(stageInfo);
        }
    }
    public void SaveToFile()
    {
        string saveFileName = CreateLevelIdentifier(stageId);
        string filePath = Path.Combine(Application.persistentDataPath, saveFileName);
        try
        {
            // 转换数据
            string json = JsonConvert.SerializeObject(this);
            string encryptedJson = SecurityProvider.ProtectData(json);
            File.WriteAllText(filePath, encryptedJson);

            //Debug.Log($"关卡进度已保存：{filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存关卡数据失败：{e.Message}");
        }
    }

    /// <summary>
    /// 获取进度文件名称
    /// </summary>
    /// <param name="levelId"></param>
    /// <returns></returns>
    public static string CreateLevelIdentifier(int levelId)
    {
        return $"ChessStageProgress_{levelId}.json";
    }

    /// <summary>
    /// 验证JSON字符串是否有效
    /// </summary>
    private bool ValidateJson(string json)
    {
        try
        {
            JToken.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    #endregion
    
}
