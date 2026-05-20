using System;
using System.Collections;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.HuaweiAppGallery;
using UnityEngine.HuaweiAppGallery.Listener;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public enum GameFlowStatus 
{
    NotStarted,
    
    // --- 1. 初始化阶段 ---
    Initializing,
    InitFailed,
    
    // --- 2. 更新检查阶段 ---
    CheckingUpdate,
    UpdateRequired, // 需要更新，等待用户操作
    
    // --- 3. 登录阶段 ---
    LoginReady,
    LoggingIn,
    SilentFailed,
    LoginFailed,
        
    // 获取用户信息
    GetGamePlayer,
    GetGamePlayerFailed,
    // 上传角色信息
    GamePlayerSave,
    GamePlayerSaveFailed,
    // --- 4. 完成状态 ---
    Ready // 所有流程完成，游戏可以启动
}

/// <summary>
/// 游戏加载控制器
/// 主要功能：
/// 1. 管理游戏初始化加载流程
/// 2. 显示加载进度和提示信息
/// 3. 预加载关键游戏资源
/// 与原LoadPanel的主要差异：
/// - 完全重构的加载流程管理
/// - 新增资源依赖系统
/// - 改进进度反馈机制
/// </summary>
public class LoadingController : MonoBehaviour
{
    [Header("UI组件引用")]
    [SerializeField] private Text loadingHintText;    // 加载提示文本
    [SerializeField] private Text loadingHintName;
    [SerializeField] private Slider progressSlider;   // 进度条组件
    //[SerializeField] private RectTransform indicatorIcon; // 进度指示图标
    [SerializeField] private RectTransform cardRect;  // 卡牌节点
    [SerializeField] private RectTransform shadowRect; // 👇 🔥 新增：卡牌的阴影节点
    [Header("加载配置")]
    [SerializeField] private float minLoadingTime = 1.5f; // 最小加载时间(秒)
    // [SerializeField] private int randomHintCount = 20;    // 随机提示数量
    [SerializeField] private float arcHeight = 80f;      // 抛物线跳跃的高度
    [SerializeField] private float inPlaceJumpHeight = 80f; // 原地跳跃的高度
    [SerializeField] private float arcDuration = 1.0f;    // 每次抛物线移动的时长
    [SerializeField] private float jumpDuration = 0.5f;   // 原地跳跃旋转的时长
    
    private AsyncOperation _sceneLoadOperation;        // 场景加载操作
    private float _loadStartTime;                      // 加载开始时间

    public GameFlowStatus flowStatus = GameFlowStatus.NotStarted;
    private int _retryAttempt = 0;
    private const int MAX_RETRIES = 3; // 设置最大重试次数
    private const float RETRY_DELAY = 1.0f; // 重试间隔（秒）
    
    private void Start()
    {
        if(progressSlider != null) progressSlider.value = 0f;
        loadingHintText.text = "";
        loadingHintName.text = "";
        StartCoroutine(InitializeLoadingProcess());
    }

    /// <summary>
    /// 初始化加载流程
    /// </summary>
    IEnumerator InitializeLoadingProcess()
    {
        yield return null;
        _loadStartTime = Time.time;
        GameDataManager.Instance.LoadPlayerProfile();
        InitializeLocalization();
        UnityMainThreadDispatcher.Instance();
        yield return new WaitForSeconds(0.01f);
        #if UNITY_HUAWEI 
        if (GameDataManager.Instance.UserData.IsAgreePrivacy == false)
        {
            GameObject pg = Resources.Load<GameObject>("Privacy/PrivacyGuidance");
            GameObject ps = Instantiate(pg, transform);
            ps.SetActive(true);
        }
        yield return new WaitUntil(()=>GameDataManager.Instance.UserData.IsAgreePrivacy);
        yield return InitializeGameService();
        HuaweiGameService.ShowFloatWindow();
        #endif
        Game.Instance.InitGame();
        SetupRandomLoadingHint();
        StartCoroutine(LoadingSequence());
        yield return new WaitForSeconds(0.1f);
        Game.Accounts.Login();
        yield return new WaitUntil(() => Game.Accounts.IsLogin);
        yield return Game.Instance.InitManagers();
        AnalyticMgr.SetLoginUser(Game.Accounts.UserId);
    }
 
    /// <summary>
    /// 初始化本地化系统
    /// </summary>
    private void InitializeLocalization()
    {
        MultilingualManager.Instance.LoadLocalization();
        // MultilingualManager.Instance.LoadLocalizationNameTable();
        MultilingualManager.Instance.InitbiddenWords();
    }

    /// <summary>
    /// 设置随机加载提示
    /// </summary>
    private void SetupRandomLoadingHint()
    {
        int id = Random.Range(1, 12);
        string sid = id < 10 ? "0" + id : id.ToString();
        loadingHintText.text = MultilingualManager.Instance.GetString("loadText" + sid);
        loadingHintName.text = MultilingualManager.Instance.GetString("loadText101");
    }

    /// <summary>
    /// 主加载序列协程
    /// </summary>
    private IEnumerator LoadingSequence()
    {
        // 并行执行模拟加载和实际加载
        Coroutine simulation = StartCoroutine(PlayCardAnimationSequence());
        Coroutine loading = StartCoroutine(LoadEssentialResources());
      
        yield return simulation;
        yield return loading;
        Debug.Log("模拟已结束, 但登录似乎未成功 =>" + Game.Accounts.IsLogin);
        yield return new WaitUntil(() => Game.Accounts.IsLogin);
        FinalizeLoading();
    }
    /// <summary>
    /// 🌟 核心：卡牌多段跳跃与进度条同步的动画序列
    /// </summary>
    private IEnumerator PlayCardAnimationSequence2()
    {
        if (cardRect == null || progressSlider == null) yield break;

        // 1. 计算坐标点 (利用进度条的实际世界宽度来计算移动距离)
        Vector3 startPos = cardRect.position;
        float worldWidth = progressSlider.GetComponent<RectTransform>().rect.width * progressSlider.transform.lossyScale.x;
        
        Vector3 midPos = startPos + new Vector3(worldWidth / 2f, 0, 0); // 中间点
        Vector3 endPos = startPos + new Vector3(worldWidth, 0, 0);      // 终点

        // 2. 创建 DOTween 序列
        Sequence seq = DOTween.Sequence();

        // 【阶段 1】：起点原地跳起并旋转 360 度
        seq.Append(cardRect.DOJump(startPos, inPlaceJumpHeight, 1, jumpDuration));
        seq.Join(cardRect.DORotate(new Vector3(0, 0, -360), jumpDuration, RotateMode.FastBeyond360).SetRelative());

        // 【阶段 2】：抛物线飞到中间，同时进度条填到 50%
        seq.Append(cardRect.DOJump(midPos, arcHeight, 1, arcDuration).SetEase(Ease.Linear));
        seq.Join(progressSlider.DOValue(0.5f, arcDuration).SetEase(Ease.Linear));

        // 【阶段 3】：中间原地跳起并旋转 360 度
        seq.Append(cardRect.DOJump(midPos, inPlaceJumpHeight, 1, jumpDuration));
        seq.Join(cardRect.DORotate(new Vector3(0, 0, -360), jumpDuration, RotateMode.FastBeyond360).SetRelative());

        // 【阶段 4】：抛物线飞到末尾，同时进度条填到 100%
        seq.Append(cardRect.DOJump(endPos, arcHeight, 1, arcDuration).SetEase(Ease.Linear));
        seq.Join(progressSlider.DOValue(1.0f, arcDuration).SetEase(Ease.Linear));

        // 【阶段 5】：终点原地跳起并旋转 360 度，完美落地！
        seq.Append(cardRect.DOJump(endPos, inPlaceJumpHeight, 1, jumpDuration));
        seq.Join(cardRect.DORotate(new Vector3(0, 0, -360), jumpDuration, RotateMode.FastBeyond360).SetRelative());

        // 等待整个动画序列播放完毕
        bool animFinished = false;
        seq.OnComplete(() => animFinished = true);
        yield return new WaitUntil(() => animFinished);
    }
    
    /// <summary>
    /// 🌟 核心：卡牌单次跳跃 + 翻滚抛物线直达终点 (精简流畅版)
    /// </summary>
    private IEnumerator PlayCardAnimationSequence()
    {
        if (cardRect == null || progressSlider == null) yield break;

        Vector2 startPos = cardRect.anchoredPosition;
        float totalWidth = progressSlider.GetComponent<RectTransform>().rect.width;
        
        // 减去一半宽度，让中心点完美停在进度条末尾
        float offset = cardRect.rect.width / 2f; 
        float travelDistance = totalWidth - offset;
        // 我们只需要一个终点，不再需要中间点(midPos)了
        Vector2 endPos = startPos + new Vector2(travelDistance + 50, 0);
        
        // 👇 计算阴影的目标X坐标 (保持它与卡牌相同的X轴移动距离)
        // 核心：让阴影保持和卡牌一样的相对距离，算出它自己的专属终点
        Vector2 shadowStartPos = shadowRect.anchoredPosition;
        Vector2 shadowEndPos = shadowStartPos + (endPos - startPos);
        
        Sequence seq = DOTween.Sequence();

        // 【阶段 1】：起点原地跳起并旋转 1 圈 (热身动作)
        seq.Append(cardRect.DOJumpAnchorPos(startPos, inPlaceJumpHeight, 1, jumpDuration));
        // 🔥 阴影 Y 轴跟着一起起跳！
        seq.Join(shadowRect.DOJumpAnchorPos(shadowStartPos, inPlaceJumpHeight, 1, jumpDuration));
        
        seq.Join(cardRect.DORotate(new Vector3(0, 0, -360), jumpDuration, RotateMode.FastBeyond360).SetRelative());
        seq.Join(shadowRect.DORotate(new Vector3(0, 0, -360), jumpDuration, RotateMode.FastBeyond360).SetRelative());
        
        // 【阶段 2】：以一条大抛物线直接飞到末尾，同时在空中持续翻滚！
        // 把之前的两段飞行时间合并 (arcDuration * 2) 保证节奏不至于太快
        float flightTime = arcDuration * 2f; 

        // 1. 大抛物线飞跃 (Ease.InOutSine 会让起步和降落更丝滑)
        seq.Append(cardRect.DOJumpAnchorPos(endPos, arcHeight, 1, flightTime).SetEase(Ease.InOutSine));
        // 2. 🔥 阴影也划出一模一样的大抛物线 (X轴和Y轴同步飞跃！)
        seq.Join(shadowRect.DOJumpAnchorPos(shadowEndPos, arcHeight, 1, flightTime).SetEase(Ease.InOutSine));
        
        // 2. 进度条同步拉满
        seq.Join(progressSlider.DOValue(1.0f, flightTime).SetEase(Ease.InOutSine));
        
        // 3. 🔥 在空中边飞边滚！(转 2 圈也就是 -720 度，使用 Linear 保持匀速滚动的车轮感)
        seq.Join(cardRect.DORotate(new Vector3(0, 0, -720), flightTime, RotateMode.FastBeyond360).SetRelative().SetEase(Ease.Linear));
        seq.Join(shadowRect.DORotate(new Vector3(0, 0, -720), flightTime, RotateMode.FastBeyond360).SetRelative().SetEase(Ease.Linear));        // 2. 阴影跟着转两圈
       
        // 等待整个动画序列播放完毕
        bool animFinished = false;
        seq.OnComplete(() => animFinished = true);
        yield return new WaitUntil(() => animFinished);
    }
    /// <summary>
    /// 模拟加载进度（确保最小加载时间）
    /// </summary>
    private IEnumerator SimulateLoadingProgress()
    {
        float elapsedTime = 0;
        float progress = 0;

        while (progress < 1f)
        {
            elapsedTime = Time.time - _loadStartTime;
            progress = Mathf.Clamp01(elapsedTime / minLoadingTime);
            progressSlider.value = progress;
            yield return null;
        }
    }

    /// <summary>
    /// 加载核心游戏资源
    /// </summary>
    private IEnumerator LoadEssentialResources()
    {
        Debug.Log("开始预加载游戏资源");
        //LoadFont();
        // 加载字体资源
        AdvancedBundleLoader.SharedInstance.LoadFont(
             "stagefonts",
             "FZKTK");
        // loadingHintText.font = mainFont;
        
        // 加载通用图片
        yield return AdvancedBundleLoader.SharedInstance.LoadAtlas(
            "ui_universal",
            "UI_Universal");
        
        // 并行加载其他关键资源
        // yield return AdvancedBundleLoader.SharedInstance.LoadAtlas(
        //     "effect_sprite",
        //     "trailAlta");

        yield return AdvancedBundleLoader.SharedInstance.LoadMaterialResource(
            "effectsitemmats",
            "Circle");

        //预加载关卡文件
        ChainStageController.Instance.Initialize();

        // 开始场景加载
        yield return LoadMainSceneAsync();
    }
    
    /// <summary>
    /// 异步加载主场景
    /// </summary>
    private IEnumerator LoadMainSceneAsync()
    {
        _sceneLoadOperation = SceneManager.LoadSceneAsync("GameLobby");
        _sceneLoadOperation!.allowSceneActivation = false;

        yield return new WaitUntil(() => _sceneLoadOperation.progress >= 0.9f);
    }
    
    /// <summary>
    /// 完成加载流程
    /// </summary>
    private void FinalizeLoading()
    {
        Debug.Log("所有资源加载完成进入主场景, 时间：" + (Time.time -  _loadStartTime));
        _sceneLoadOperation.allowSceneActivation = true;
    }

     private IEnumerator InitializeGameService()
    {
        Action<GameFlowStatus> statusSetter = (status) => { flowStatus = status; };
        if (_retryAttempt >= MAX_RETRIES)
        {
            flowStatus = GameFlowStatus.InitFailed;
            Debug.LogError($"初始化游戏服务失败,并退出游戏");
            Application.Quit();
            yield break;
        }

        _retryAttempt++;
        flowStatus = GameFlowStatus.Initializing;
        HuaweiGameService.Init(new AntiAddictionHandler(), new InitHandler(statusSetter, () =>
        {
            flowStatus = GameFlowStatus.InitFailed;
            StartCoroutine(RetryAfterDelay(RETRY_DELAY));
            Debug.LogError($"初始化游戏服务失败，重试次数：{_retryAttempt}");
        }));
        yield return new WaitUntil(() => flowStatus is GameFlowStatus.CheckingUpdate);
        Debug.Log($"进入检查更新流程");
        HuaweiGameService.CheckUpdate(new CheckUpdateListener(statusSetter));
        yield return new WaitUntil(() => flowStatus is GameFlowStatus.LoginReady);
        Debug.Log($"检查更新流程完成");
     
    }
    
    // 助手协程：用于在重试前等待一段时间
    private IEnumerator RetryAfterDelay(float delay)
    {
        MessageSystem.Instance.ShowTip($"等待 {delay} 秒后重试...");
        yield return new WaitForSeconds(delay);
    
        // 🔑 关键：重新启动初始化流程
        StartCoroutine(InitializeGameService());
    }
    
    // 🔑 1. 定义局部实现类 IAntiAddictionListener
    private class AntiAddictionHandler : IAntiAddictionListener
    {
        public void OnExit()
        {
            Debug.Log("防沉迷退出回调：退出应用。");
            GameDataManager.Instance.CommitGameData();
            Application.Quit();
        }
    }
    
        // 🔑 2. 定义局部实现类 IInitListener
    private class InitHandler : IInitListener
    {
        private readonly Action<GameFlowStatus> _onCompletedCallback;
        private readonly Action  _onRetryAction;

        public InitHandler(Action<GameFlowStatus> onCompletedCallback, Action onRetryAction = null)
        {
            _onCompletedCallback = onCompletedCallback;
            _onRetryAction = onRetryAction;
        }
        public void OnSuccess()
        {
            HuaweiGameService.ShowFloatWindow();
            _onCompletedCallback?.Invoke(GameFlowStatus.CheckingUpdate);
        }

        public void OnFailure(int code, string message)
        {
            string msg = $"JosAppsClient init failed, code:{code} message:{message}";
            Debug.Log(msg);
            switch (code)
            {
                case 7002:
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        // MessageSystem.Instance.ShowTip("请检查网络");
                        _onCompletedCallback?.Invoke(GameFlowStatus.InitFailed);
                    });
                    break;
                case 7401:
                    _onCompletedCallback?.Invoke(GameFlowStatus.InitFailed);
                    Application.Quit();
                    break;
                case 907135003:
                    _onRetryAction?.Invoke();
                    break;
                default:
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                         // MessageSystem.Instance.ShowTip(msg);
                        _onCompletedCallback?.Invoke(GameFlowStatus.InitFailed);
                    });
                    break;
            }
        }
    }
    
    private class CheckUpdateListener : ICheckUpdateListener
    {
        private readonly Action<GameFlowStatus> _onCompletedCallback;

        public CheckUpdateListener(Action<GameFlowStatus> onCompletedCallback)
        {
            _onCompletedCallback = onCompletedCallback;
        }
        public  void OnUpdateInfo(AndroidJavaObject intent)
        {
            if (intent !=null)
            {
                int status = intent.Call<int>("getIntExtra", "status", 0);
                if (status==0)
                {
                    // 无需更新，直接进入下一阶段：登录
                    _onCompletedCallback?.Invoke(GameFlowStatus.LoginReady);
                }
                else if (status == 7)
                {
                    // 发现更新，等待用户操作或退出
                    _onCompletedCallback.Invoke(GameFlowStatus.UpdateRequired);
                    AndroidJavaObject apkUpgradeInfo = intent.Call<AndroidJavaObject>("getSerializableExtra", "updatesdk_update_info");
                    HuaweiGameService.ShowUpdateDialog(apkUpgradeInfo, true);
                    // bool isExit = intent.Call<bool>("getBooleanExtra", ",", false);
                    // TODO
                }
                else
                {
                    _onCompletedCallback?.Invoke(GameFlowStatus.LoginReady);
                }
            }
            else
            {
                _onCompletedCallback?.Invoke(GameFlowStatus.LoginReady);
            }
        }

        public void OnMarketInstallInfo(AndroidJavaObject intent)
        {
           
        }

        public void OnMarketStoreError(int responseCode)
        {
           
        }

        public void OnUpdateStoreError(int responseCode)
        {
           
        }
    }
}
