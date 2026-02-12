using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using Middleware;

public class GameDataManager : SingletonMono<GameDataManager>
{
    #region 数据字段
    private UserData playerProfile = new UserData();
    private Dictionary<string, ChainStageProgressData> ChainLevelProgressDict = new Dictionary<string, ChainStageProgressData>();
    private FishUserSaveData fishUserSave = new FishUserSaveData(); 
    
    private bool dataInitialized = false;
    private bool requireFocusCheck = false;
    private DateTime lastSaveTime;
   
    #endregion

    #region 属性
    public FishUserSaveData FishUserSave { get { return fishUserSave; } }
    public UserData UserData { get { return playerProfile; } }
    #endregion

    #region Unity生命周期方法

    public override void Init()
    {
        lastSaveTime = DateTime.Now;
        Game.Analytics.OnSdkInit += AnalyticMgr.OnAnalyticsSdkInit;
        // Application.wantsToQuit += OnWantsToQuit;
    }

    private void OnApplicationFocus(bool focusStatus)
    {
        HandleFocusChange(focusStatus);
    }

    private void OnApplicationPause(bool pauseState)
    {
        HandlePauseState(pauseState);
    }

    protected override void OnApplicationQuit()
    {
        //HandleQuitEvent();
        HandleQuitEvent();
        base.OnApplicationQuit();
    }

    #endregion
    
    public void LoadPlayerProfile()
    {
        playerProfile.LoadData();
        // fishUserSave.LoadData();
        
        dataInitialized = true;
    }

    #region 关卡数据管理
    
    public bool IsNewLevelEntry(int StageNumber)
    {
        string saveFileName = ChainStageProgressData.CreateLevelIdentifier(StageNumber);
        string filePath = Path.Combine(Application.persistentDataPath, saveFileName);
        
        return !File.Exists(filePath);
    }

    public ChainStageProgressData RetrieveLevelProgress(ChainStageInfo levelDetails)
    {
        string identifier = ChainStageProgressData.CreateLevelIdentifier(levelDetails.StageNumber);

        if (!ChainLevelProgressDict.ContainsKey(identifier))
        {
            ChainStageProgressData progress = new ChainStageProgressData();
            progress.LoadFromFile(levelDetails);
            ChainLevelProgressDict[identifier] = progress;
        }

        // 无用数据转换
        return ChainLevelProgressDict[identifier];
    }
    
    // 更新拼字关卡进度
    public void UpdateLevelProgress(ChainStageProgressData progressData)
    {
        string identifier = ChainStageProgressData.CreateLevelIdentifier(progressData.stageId);
        
        if (ChainLevelProgressDict.ContainsKey(identifier))
        {
            ChainLevelProgressDict[identifier] = progressData;
        }

        // 无用更新检查
        if (progressData.stageId % 2 == 0)
        {
            Debug.Log($"更新了偶数关卡 {progressData.stageId}");
        }
    }

    public ChainStageProgressData ResetLevelProgress(ChainStageInfo stageDetails)
    {
        string identifier = ChainStageProgressData.CreateLevelIdentifier(stageDetails.StageNumber);
        ChainStageProgressData progress = new ChainStageProgressData();
        progress.InitializeFromStageInfo(stageDetails);
        ShuffleList(progress.stockCardIds);
        
        progress.isFirstEnter = false;
        ChainLevelProgressDict[identifier] = progress;
        return progress;
    }
    // 简单的洗牌算法 (Fisher-Yates)
    private void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = UnityEngine.Random.Range(0, n + 1); 
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
    #endregion

    #region 数据保存
    public void CommitGameData()
    {
        playerProfile.SaveData();
        fishUserSave.SaveData();

        string currentLevelId = ChainStageProgressData.CreateLevelIdentifier(playerProfile.CurrentStage);
        if (ChainLevelProgressDict.ContainsKey(currentLevelId))
        {
            ChainLevelProgressDict[currentLevelId].SaveToFile();
        }
    }
  
    #endregion

    #region 应用程序状态处理
    private void HandleFocusChange(bool hasFocus)
    {
        // 应用进入后台
        if (!hasFocus)
        {
            //初始化完成后才可以保存，不然保存的数据都为默认数值
            if (dataInitialized)
                CommitGameData();
       
            // if(Game.Ads.IsPlaying) return; //播放广告中
            //     AnalyticMgr.GameEnd();
                
            requireFocusCheck = true;
            // Debug.Log("应用进入后台，数据已保存");
        }
        else if (requireFocusCheck)
        {
            AnalyticMgr.GameStart();
            // Debug.Log("应用回到前台，验证数据");
            requireFocusCheck = false;
            playerProfile.CheckResetLimitTime();
        }
    }

    private void HandlePauseState(bool isPaused)
    {
        if (isPaused && dataInitialized)
        {
            CommitGameData();
            // Debug.Log("应用暂停，数据已保存");
        }
    }

    private void HandleQuitEvent()
    {
        if (dataInitialized)
        {           
            CommitGameData();
            // StartCoroutine(APIGateway.Instance.LoginApi.Logout(playerProfile, null));
            // Debug.Log("应用关闭，数据已保存" + JsonUtility.ToJson(playerProfile));
            Application.Quit();
        }
    }
    #endregion

    #region 数据清理
    public void WipeAllGameData()
    {
        PurgePersistentFiles();
     
        playerProfile.InitData();
        fishUserSave.InitData();
        ChainLevelProgressDict.Clear();
    }

    public void PurgePersistentFiles()
    {
        string storagePath = Application.persistentDataPath;

        if (Directory.Exists(storagePath))
        {
            try
            {
                string[] allFiles = Directory.GetFiles(storagePath);
                foreach (string filePath in allFiles)
                {
                    File.Delete(filePath);
                    Debug.Log($"已移除文件: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"清除存储数据时出错: {ex.Message}");
            }
        }
    }
    #endregion
}