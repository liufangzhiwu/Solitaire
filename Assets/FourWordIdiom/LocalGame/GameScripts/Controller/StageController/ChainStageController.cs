using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Middleware;
using Newtonsoft.Json;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// 关卡管理系统（非MonoBehaviour单例）
/// 功能：
/// 1. 管理所有关卡数据加载与切换 (包含滑动窗口缓存)
/// 2. 处理关卡进度保存
/// 3. 图片资源预加载与智能释放
/// </summary>
public class ChainStageController
{
    #region 单例实现

    private static readonly Lazy<ChainStageController> _instance =
        new Lazy<ChainStageController>(() => new ChainStageController());

    public static ChainStageController Instance => _instance.Value;
    
    private ChainStageController() 
    {
    }
    #endregion

    #region  关卡缓存
    // 关卡总数
    private int _countStage;     
    // 缓存窗口大小
    private const int CACHE_WINDOW_SIZE = 5;
    private readonly string[] PRELOAD_SIZES = new string[] { "(L)", "(S)" };
    // 配置缓存：Key=关卡ID
    private readonly Dictionary<int, ChainStageInfo> _stageConfigCache = new Dictionary<int, ChainStageInfo>(CACHE_WINDOW_SIZE);
    // 图片缓存：Key=图片名, Value=Sprite引用
    private readonly Dictionary<string, Sprite> _spriteRamCache = new Dictionary<string, Sprite>();
    // Icon映射表
    private Dictionary<string, string> _loadedIcons;
    #endregion
    
    #region 运行时数据
    
    public ChainStageInfo CurrStageInfo { get; private set; } // 当前关卡配置数据
    public ChainStageProgressData CurrStageData { get; private set; } // 当前关卡进度数据   
    public bool IsFirstEnterStage { get; private set; } = true; // 是否首次进入当前关卡
    #endregion

    #region 属性封装
    /// <summary>
    /// 当前关卡编号（代理保存系统的当前关卡）
    /// </summary>
    public int CurrentStage
    {
        get => GameDataManager.Instance.UserData.CurrentStage;
        private set => GameDataManager.Instance.UserData.CurrentStage = value;
    }
    #endregion

    #region 初始化配置
    public void Initialize()
    {
        LoadStageManifest();
        LoadWordIconsConfig();

        GetAndUpdateStageCache(CurrentStage > 0 ? CurrentStage : 1);
        AdvancedBundleLoader.SharedInstance.LoadAtlas("ui_stageicon", "UI_stageIcon");
    }
    /// <summary>
    /// 加载词语图片
    /// </summary>
    private void LoadWordIconsConfig()
    {
        if (_loadedIcons != null) return;
        _loadedIcons = new Dictionary<string, string>();
        
        // 从AssetBundle中加载CSV文件
        TextAsset defCsvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "config_level_icon");
        var lines = defCsvFile.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            var key =  values[2].Trim();
            _loadedIcons.TryAdd(key, values[0].Trim());
        }
    }
    /// <summary>
    /// 加载关卡清单
    /// </summary>
    private void LoadStageManifest()
    {
        TextAsset levelManifest = AdvancedBundleLoader.SharedInstance.LoadTextFile(ToolUtil.GetLanguageBundle(), "level_manifest");
        string[] levelCount =  JsonConvert.DeserializeObject<string[]>(levelManifest.text);
        _countStage = levelCount.Length - 1;
    }
    #endregion
    
    #region 关卡管理 (核心：缓存与预加载)
    /// <summary>
    /// 设置当前关卡数据
    /// </summary>
    /// <param name="stageIndex">关卡编号</param>
    public void SetStageDataOld(int stageIndex)
    {
        IsFirstEnterStage = GameDataManager.Instance.IsNewLevelEntry(stageIndex);
        CurrStageInfo = GetAndUpdateStageCache(stageIndex);
        CurrStageData = GameDataManager.Instance.RetrieveLevelProgress(CurrStageInfo);
        // 记录关卡开始时间
        GameDataManager.Instance.UserData.curStageStartTime = DateTime.Now.ToString();
        // AnalyticMgr.SetCommonProperties();
        // 首次进入关卡的特殊处理
        if (!GameDataManager.Instance.UserData.curIsEnter)
        {
            GameDataManager.Instance.UserData.curStageOnlineTime = 0;
            // 可在此处添加分析事件...
            // AnalyticMgr.LevelStart();
            GameDataManager.Instance.UserData.curIsEnter = true;
        }
        // CheckRateUsConditions(stageIndex);
        // CoroutineRunner.StartCoroutine(HandlerStageData(stageIndex));
    }
    
    /// <summary>
    /// 【核心入口】切换关卡 PrepareAndEnterStage
    /// 包含：显示Loading -> 准备数据 -> 预加载图片 -> 清理旧资源 -> 进入游戏
    /// </summary>
    public void SetStageData(int stageIndex)
    {
        CoroutineRunner.StartCoroutine(EnterStageRoutine(stageIndex));
    }

    private IEnumerator EnterStageRoutine(int stageIndex)
    {
        CurrStageInfo = GetAndUpdateStageCache(stageIndex);
        IsFirstEnterStage = GameDataManager.Instance.IsNewLevelEntry(stageIndex);
        CurrStageData = GameDataManager.Instance.RetrieveLevelProgress(CurrStageInfo);
        // SmartReleaseSprites(stageIndex);
        yield return null;
        // yield return PreloadCurrentStageSprites(CurrStageInfo);
        
        GameDataManager.Instance.UserData.curStageStartTime = DateTime.Now.ToString();
        // AnalyticMgr.SetCommonProperties();
        // 首次进入关卡的特殊处理
        if (!GameDataManager.Instance.UserData.curIsEnter)
        {
            GameDataManager.Instance.UserData.curStageOnlineTime = 0;
            // 可在此处添加分析事件...
            // AnalyticMgr.LevelStart();
            GameDataManager.Instance.UserData.curIsEnter = true;
        }
        // CheckRateUsConditions(stageIndex);
        // CoroutineRunner.StartCoroutine(HandlerStageData(stageIndex));
    }

    /// <summary>
    /// 获取缓存中的关卡配置，并维护滑动窗口
    /// </summary>
    private ChainStageInfo GetAndUpdateStageCache(int currentStageId)
    {
        if (!_stageConfigCache.ContainsKey(currentStageId))
        {
            var info = CreateStageInfoInternal(currentStageId);
            _stageConfigCache.Add(currentStageId, info);
        }
        CoroutineRunner.StartCoroutine(PreloadNextStagesConfigRoutine(currentStageId));
        CleanUpOldStagesConfig(currentStageId);
        
        return _stageConfigCache[currentStageId];
    }
    
    /// <summary>
    /// 内部创建逻辑
    /// </summary>
    private ChainStageInfo CreateStageInfoInternal(int stageId)
    {
        int actualStageId = CalculateActualStageId(stageId);
        return new ChainStageInfo(actualStageId, stageId);
    }

    /// <summary>
    /// 预加载后续关卡的配置数据 (纯数据，极快)
    /// </summary>
    private IEnumerator PreloadNextStagesConfigRoutine(int currentStageId)
    {
        for (int i = 1; i < CACHE_WINDOW_SIZE; i++)
        {
            int targetId = currentStageId + i;
            if (!_stageConfigCache.ContainsKey(targetId))
            {
                var info = CreateStageInfoInternal(targetId);
                _stageConfigCache.Add(targetId, info);
                // 这里分帧，避免一帧内创建太多对象
                yield return null;
            }
        }
    }
    /// <summary>
    /// 清理旧的关卡配置数据
    /// </summary>
    private void CleanUpOldStagesConfig(int currentStageId)
    {
        // 简单的清理策略：移除当前关卡 - 5 之前的关卡
        int threshold = currentStageId - CACHE_WINDOW_SIZE;
        var keysToRemove = _stageConfigCache.Keys.Where(k => k < threshold).ToList();
        
        foreach (var key in keysToRemove)
        {
            _stageConfigCache.Remove(key);
        }
    }

    /// <summary>
    /// 计算实际关卡ID（处理循环关卡逻辑）
    /// </summary>
    private int CalculateActualStageId(int stageId)
    {
        if (_countStage <= 0) return stageId; // 防止未加载时除零
        
        // 未超过总关卡数直接返回
        if (stageId <= _countStage)
            return stageId;

        // 计算循环关卡ID
        int startStage = _countStage - AppGameSettings.LoopLevelStart;
        if (startStage <= 0) startStage = 1;
        
        int overflow = stageId - startStage;
        return startStage + (overflow % (_countStage - startStage));
    }
    #endregion

    #region 图片资源管理
    /// <summary>
    /// 【预加载】加载当前关卡所需的所有 Sprite
    /// </summary>
    private IEnumerator PreloadCurrentStageSprites(ChainStageInfo stageInfo)
    {
        HashSet<string> wordsToLoad = new HashSet<string>();
        foreach (var cardId in stageInfo.GetAllCardIds())
        {
            wordsToLoad.Add(cardId);
        }

        int loadCount = 0;
        foreach (var word in wordsToLoad)
        {
            string baseName = word;
            if (_loadedIcons != null && _loadedIcons.TryGetValue(word, out string mappedName))
            {
                baseName = mappedName;
            }

            foreach (var size in PRELOAD_SIZES)
            {
                string cacheKey = word + size;
                if (_spriteRamCache.ContainsKey(cacheKey)) continue;

                string atlasName = baseName + size;
                Sprite sp = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(atlasName, "UI_stageIcon");
                if (sp == null)
                    sp = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(baseName, "UI_stageIcon");
                
                if (sp == null)
                    sp = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(word + size, "UI_stageIcon");
                
                if (sp != null)
                {
                    _spriteRamCache[cacheKey] = sp;
                }
            
                loadCount++;
                Debug.Log($"{word} 加载了图片 {cacheKey} -> {sp?.name}" );
            }
        
            if(loadCount % 6 == 0) yield return null;
        }
    }
    /// <summary>
    /// 【智能释放】清理那些“过去用过，且未来N关都用不上”的图片资源
    /// 解决了“玩过的关卡在什么时间从内存中移除”的问题
    /// </summary>
    private void SmartReleaseSprites(int currentStageId)
    {
        HashSet<string> protectionSet = new HashSet<string>();
        for (int i = 0; i < CACHE_WINDOW_SIZE; i++)
        {
            int checkStageId = currentStageId + i;
            if (_stageConfigCache.TryGetValue(checkStageId, out ChainStageInfo stageInfo))
            {
                foreach (var cardId in stageInfo.GetAllCardIds())
                {
                    protectionSet.Add(cardId);
                }
            }
        }
        List<string> keysToRemove = new List<string>();
        foreach (var kvp in _spriteRamCache)
        {
            string cacheKey = kvp.Key; // 例如 "card_101(L)"
            bool isProtected = false;
            // 遍历所有可能的后缀来还原 word
            foreach(var size in PRELOAD_SIZES) 
            {
                if (cacheKey.EndsWith(size))
                {
                    string originalWord = cacheKey.Substring(0, cacheKey.Length - size.Length);
                    if (protectionSet.Contains(originalWord))
                    {
                        isProtected = true;
                        break;
                    }
                }
            }
            // 如果不在保护名单，标记删除
            if (!isProtected)
            {
                keysToRemove.Add(cacheKey);
            }
        }

        if (keysToRemove.Count > 0)
        {
            foreach (var key in keysToRemove)
            {
                _spriteRamCache.Remove(key);
            }
            Resources.UnloadUnusedAssets();
        }
    }
    /// <summary>
    /// 获取词语的图片
    /// </summary>
    public Sprite GetIconSprite(string word, string size = "(L)")
    {
        // string cacheKey = word + size;
        // if (_spriteRamCache.TryGetValue(cacheKey, out Sprite cachedSp))
        // {
        //     return cachedSp;
        // }
        string baseName = word;
        if (_loadedIcons != null && _loadedIcons.TryGetValue(word, out string mappedName))
        {
            baseName = mappedName;
        }
        // 拼图集名
        string atlasName = baseName + size;
        Sprite fallbackSp = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(atlasName, "UI_stageIcon");
        if (fallbackSp == null)
            fallbackSp = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(baseName,"UI_stageIcon");

        // if (fallbackSp != null)
        //     _spriteRamCache[cacheKey] = fallbackSp;
        
        return fallbackSp;
    }
    #endregion
    
    
    #region 关卡流程控制
    /// <summary>
    /// 重置当前关卡
    /// </summary>
    public void ResetCurrentStage()
    {
        CurrStageData = GameDataManager.Instance.ResetLevelProgress(CurrStageInfo);
        // 记录关卡开始时间
        GameDataManager.Instance.UserData.curStageStartTime = DateTime.Now.ToString();
    }
    /// <summary>
    /// 完成关卡主逻辑
    /// </summary>
    public void CompleteStage(int stageNumber, bool isComplete = false)
    {
        SystemManager.Instance.HidePanel(PanelType.ChainPlayArea);
        if (isComplete)
        {
            CoroutineRunner.StartCoroutine(CompleteStageRoutine(stageNumber));
        }
        else
        {
            SystemManager.Instance.ShowPanel(PanelType.FailedPanel);
        }
    }

    /// <summary>
    /// 关卡完成协程
    /// </summary>
    private IEnumerator CompleteStageRoutine(int stageNumber)
    {
        // 更新进度
        if (stageNumber == CurrentStage)
        {
            GameDataManager.Instance.UserData.UpdateStage();
        }
        // 发放过关奖励
        GameDataManager.Instance.UserData.UpdateGold(AppGameSettings.LevelCompleteBonus, false, false
            ,"结算获得");
        GameDataManager.Instance.CommitGameData();
        // 播放效果
        // yield return PlayCompletionEffects(stageNumber);
        yield return null;
        SystemManager.Instance.ShowPanel(PanelType.SuccessPanel);
        // 更新任务
        // DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedPassLevel, 1);
    }

    /// <summary>
    /// 播放关卡完成效果
    /// </summary>
    private IEnumerator PlayCompletionEffects(int stageNumber)
    {
        EventDispatcher.Instance.TriggerChoicePuzzleSetStatus(false);
        AudioManager.Instance.PlaySoundEffect("PassStage");

        yield return new WaitForSeconds(0.7f);
        
        // 计算耗时
        DateTime startTime = DateTime.Parse(GameDataManager.Instance.UserData.curStageStartTime);
        float duration = (float)(DateTime.Now - startTime).TotalSeconds + 
                         GameDataManager.Instance.UserData.curStageOnlineTime;

        // 发送分析事件（示例）
        AnalyticMgr.LevelCompleted(duration);
        
        // if (StageNumber >= 20)
        // {                
        //     // 显示插屏广告
        //     Game.Ads.ShowInterstitial((bool issuccess) => 
        //     {
        //         if (issuccess)
        //         {
        //             AnalyticMgr.InsetAdSuccess("关卡插屏");
        //             GameDataManager.MainInstance.UserData.totalSeeAds++;
        //         }
        //         else
        //         {
        //             AnalyticMgr.InsetAdFail("关卡插屏");
        //         }
        //     });
        // }

        // 更新每日计数
        GameDataManager.Instance.UserData.dayPassStageCount++;
        

        yield return new WaitForSeconds(1f);

        // 播放过关视频
        // EnhancedVideoController.Instance.PlayVideo();

        // UI切换
        SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        SystemManager.Instance.HidePanel(PanelType.GamePlayArea);

        yield return new WaitForSeconds(0.8f);

        SystemManager.Instance.ShowPanel(PanelType.StageFinishView);
        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
    }

    /// <summary>
    /// 检查评分弹窗条件
    /// </summary>
    private void CheckRateUsConditions(int StageIndex)
    {
        var userData = GameDataManager.Instance.UserData;

        // 第9关首次触发
        if (StageIndex == 9 && userData.showRateusCount <= 0)
        {
            SystemManager.Instance.ShowPanel(PanelType.AppRating);
            return;
        }

        // 每日通关条件
        if (userData.dayPassStageCount == 9 && 
            userData.showRateusCount < 3 &&
            !string.IsNullOrEmpty(userData.showRateusTime))
        {
            DateTime lastTime = DateTime.Parse(userData.showRateusTime).Date;
            if ((DateTime.Now.Date - lastTime).TotalDays >= 1)
            {
                SystemManager.Instance.ShowPanel(PanelType.AppRating);
            }
        }
    }
    /// <summary>
    /// 手牌变化
    /// </summary>
    /// <param name="stockData">牌堆</param>
    /// <param name="wasteCards">弃牌</param>
    public void SyncHandState(List<string> stockData, List<CardView> wasteCards)
    {
        CurrStageData.stockCardIds = stockData;
        CurrStageData.wasteCardIds.Clear();
        foreach (CardView wasteCard in wasteCards)
        {
            CurrStageData.wasteCardIds.Add(wasteCard.cardId);
        }
        // Debug.Log("调用了手牌 " + string.Join(",", CurrStageData.wasteCardIds) );
    }
    /// <summary>
    /// 列区域
    /// </summary>
    /// <param name="columns"></param>
    public void SyncTableauState(List<ColumnView> columns)
    {
        CurrStageData.tableauColumns.Clear();
        foreach (var column in columns)
        {
            ColumnData colData = new ColumnData{cards = new List<string>()};
            for (int i = column.cards.Count - 1; i >= 0; i--) 
            {
                colData.cards.Add(column.cards[i].cardId);
            }
            CurrStageData.tableauColumns.Add(colData);
        }
        // Debug.Log("调用了列存储 " + CurrStageData.tableauColumns.Count);
    }

    public void SyncCategoryState(List<CategorySlotView> categorySlots, int completedCategoriesCount)
    {
        CurrStageData.categorySlots.Clear();
        CurrStageData.finishedCategoryCount = completedCategoriesCount;
        foreach (var slot in categorySlots)
        {
            CategoryData slotData = new CategoryData{ wordsData =  new List<WordData>()};
            if (slot.isOccupied)
            {
                slotData.categoryId = slot.categoryId;
                int count = slot.GetCurrentCount();
                for (int i = 0; i < count; i++)
                {
                    if (i == 0) 
                        slotData.wordsData.Add(new WordData{wordId = slot.currentHeaderId});
                    else
                        slotData.wordsData.Add(new WordData{wordId = "placeholder"});
                }
            }
            else
            {
                slotData.categoryId = "";
            }
            CurrStageData.categorySlots.Add(slotData);
        }
        // Debug.Log("调用了存储槽 " +  CurrStageData.categorySlots.Count);
    }
    #endregion


}

/// <summary>
/// 协程运行辅助类（替代MonoBehaviour协程支持）
/// </summary>
public static class CoroutineRunner
{
    private class Runner : MonoBehaviour { }
    
    private static Runner _instance;
    
    public static Coroutine StartCoroutine(IEnumerator routine)
    {
        if (_instance == null)
        {
            _instance = new GameObject("CoroutineRunner")
                .AddComponent<Runner>();
            UnityEngine.Object.DontDestroyOnLoad(_instance.gameObject);
        }
        return _instance.StartCoroutine(routine);
    }
}