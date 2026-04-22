using Middleware;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if UNITY_IOS
using UnityEngine.iOS;
#endif

/// <summary>
/// 游戏核心管理器（单例模式）
/// 功能：
/// 1. 游戏全局初始化
/// 2. 隐私协议处理
/// 3. 设备信息检测
/// 4. 游戏流程控制
/// </summary>
public sealed class GameCoreManager: MonoBehaviour
{
    #region 单例实现
    public static GameCoreManager Instance;
    
    #endregion

    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite homeBg;
    [SerializeField] private Sprite playBg;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保持广告管理器在场景切换时不销毁
        }
    }

    #region 公共API
    
    private void Start()
    {
        StartCoroutine(InitializeGameRoutine());
        // StartCoroutine(CheckNetworkConnection());
    }
   
    private void Update()
    {
        // 监听安卓系统返回键 (全面屏侧滑)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SystemManager.Instance.ShowPanel(PanelType.QuitConfirmView);
        }
    }
    #endregion

    #region 私有方法
    /// <summary>
    /// 初始化多语言字符串
    /// </summary>
    private void InitializeLanguageStrings()
    {
        //string TimeHourText = _languageManager.GetString("TimeH") + " ";
        //string TimeMinuteText = _languageManager.GetString("TimeM");
    }

    /// <summary>
    /// 游戏初始化协程
    /// </summary>
    private IEnumerator InitializeGameRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        
        if (GameDataManager.Instance.UserData.IsFirstLaunch)
        {
// #if UNITY_ANDROID || UNITY_IOS
//             ShowPrivacyScreen();
// #else
            // ShowGamePanel();
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
// #endif
        }
        else
        {
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
        }
        
        // Game.Ads.LoadBannerAD();
    }
    
    /// <summary>
    /// 显示游戏主界面
    /// </summary>
    public void ShowGamePanel()
    {
        ChainStageController.Instance.SetStageData(GameDataManager.Instance.UserData.CurrentStage);
        SystemManager.Instance.ShowPanel(PanelType.GamePlayArea);
    }

    /// <summary>
    /// 显示隐私协议界面
    /// </summary>
    private void ShowPrivacyScreen()
    {
        SystemManager.Instance.ShowPanel(PanelType.PolicyView);
    }

    /// <summary>
    /// 切换背景图
    /// </summary>
    /// <param name="play"></param>
    public void SwitchBackground(bool play)
    {
        backgroundImage.sprite = play ? playBg : homeBg;
    }
    
    private IEnumerator CheckNetworkConnection()
    {
        WaitForSeconds wait = new WaitForSeconds(5);
        while (true)
        {
            bool isSuccess = false;
            Ping ping = new Ping("8.8.8.8");
            float timeout = 3.0f;
            float startTime = Time.time;

            // 等待Ping完成或超时
            while (!ping.isDone && Time.time - startTime < timeout)
            {
                yield return null;
            }

            // 关键修改：明确超时和成功的条件
            if (ping.isDone && ping.time > 0 && ping.time < 2000)
            {
                isSuccess = true;
            }
            else
            {
                isSuccess = false;
            }

            // 释放Ping资源（Unity需手动销毁）
            ping.DestroyPing();
            ping = null;

            // IsNetworkActive = isSuccess;
            //Debug.Log("网络状态: " + (IsNetworkActive ? "已连接" : "未连接"));

            yield return wait;
        }
    }

    private void OnDisable()
    {
       StopCoroutine(CheckNetworkConnection());
    }

    #endregion
}
