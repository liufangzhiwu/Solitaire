using System.Collections;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [SerializeField] private Slider progressSlider;   // 进度条组件
    //[SerializeField] private RectTransform indicatorIcon; // 进度指示图标

    [Header("加载配置")]
    [SerializeField] private float minLoadingTime = 1.5f; // 最小加载时间(秒)
    // [SerializeField] private int randomHintCount = 20;    // 随机提示数量

    private AsyncOperation _sceneLoadOperation;        // 场景加载操作
    private float _loadStartTime;                      // 加载开始时间

    private void Start()
    {
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
        // SetupRandomLoadingHint();
        yield return null;
        #if UNITY_EDITOR
        Game.Instance.ShowLoginErrorPanel();
        #endif
        Game.Instance.InitGame();
        StartCoroutine(LoadingSequence());
        yield return new WaitUntil(() => Game.Accounts.IsLogin);
        Game.Instance.InitManagers();
        AnalyticMgr.SetLoginUser(Game.Accounts.UserId);
        
    }
 
    /// <summary>
    /// 初始化本地化系统
    /// </summary>
    private void InitializeLocalization()
    {
        MultilingualManager.Instance.LoadLocalization();
        MultilingualManager.Instance.LoadLocalizationNameTable();
        MultilingualManager.Instance.InitbiddenWords();
    }

    /// <summary>
    /// 设置随机加载提示
    /// </summary>
    private void SetupRandomLoadingHint()
    {
        int id = Random.Range(1, 21);
        string sid = id < 10 ? "0" + id : id.ToString();
        loadingHintText.text = MultilingualManager.Instance.GetString("Haiku" + sid);
    }

    /// <summary>
    /// 主加载序列协程
    /// </summary>
    private IEnumerator LoadingSequence()
    {
        // 并行执行模拟加载和实际加载
        Coroutine simulation = StartCoroutine(SimulateLoadingProgress());
        Coroutine loading = StartCoroutine(LoadEssentialResources());
      
        yield return simulation;
        yield return loading;
        Debug.Log("模拟已结束, 但登录似乎未成功 =>" + Game.Accounts.IsLogin);
        yield return new WaitUntil(() => Game.Accounts.IsLogin);
        FinalizeLoading();
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

}
