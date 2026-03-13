using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 教程管理系统（单例模式）
/// 版本：1.1
/// 功能说明：
/// 1. 管理游戏内新手引导流程
/// 2. 控制教程界面的显示/隐藏
/// 3. 维护教程相关资源池
/// 最后修改：2023-08-20
/// </summary>
public class ChainGuideSystem : MonoBehaviour
{
    #region 单例实现

    // 线程安全的单例实例
    public static ChainGuideSystem Instance { get; private set; }

    /// <summary>
    /// 初始化单例实例
    /// </summary>
    private void Awake()
    {
        // 单例冲突处理
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            Debug.LogWarning($"检测到重复的教程管理器实例，已自动销毁：{gameObject.name}");
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 跨场景持久化
        Debug.Log($"教程管理器初始化完成：{GetInstanceID()}");
    }

    #endregion

    #region 资源配置

    public LearningGuide guideUI; // 场景里的 UI 实例

    [Tooltip("对象池管理器（用于教程元素复用）")] private ObjectPool _objectPool;

    [Space(10)] [HideInInspector] [Tooltip("当前使用的教学工具对象")]
    public GameObject activeToolObject;

    
    public int CurrentTutorial
    {
        get => GameDataManager.Instance.UserData.TutorialProgress;
        set => GameDataManager.Instance.UserData.TutorialProgress = value;
    }

    public string toolSourceName;

    #endregion
    private void OnEnable()
    {
        EventDispatcher.Instance.OnLevelStarted += CheckTutorial;
        EventDispatcher.Instance.OnCardDragResult += OnPlayerPlacedCard;
    }

    #region 核心功能

    private void CheckTutorial(int currentLevel)
    {
        Debug.Log("启动教程检查了！ ");
        if (currentLevel == 1 && CurrentTutorial <= 100)
        {
            DisplayGuide();
            // 如果当前是0，可能要设为1开始
            int step = CurrentTutorial > 0 ? CurrentTutorial : 1;
            if(CurrentTutorial == 0) CurrentTutorial = 1; 
            
            guideUI.ShowAutoGuideByPriority();
        }
        // else if (currentLevel > 1 && CurrentTutorial <= 20)
        // {
        //     CurrentTutorial++;
        //     guideUI.PropTutorial(CurrentTutorial);
        // }
        else
        {
            CloseGuide();
        }
    }

    // 收到游戏管理器的移动事件
    private void OnPlayerPlacedCard(CardView cardView, MonoBehaviour target, bool result)
    {
        if (guideUI == null || !guideUI.gameObject.activeInHierarchy) return;
        
        if (result)
        {
            RestoreCanvasLayers();
            CurrentTutorial++;
            guideUI.ShowAutoGuideByPriority();
            // if (TryGetComponent<GraphicRaycaster>(out var raycaster)) Destroy(raycaster);
            // if (TryGetComponent<Canvas>(out var canvas)) Destroy(canvas);
            
            // bool isTargetAction = CheckIfActionMatchesTutorial(cardView, target);
            // if (isTargetAction)
            // {
            //     // 做对了本题 -> 进度前进
            //     CurrentTutorial++;
            //     if (CurrentTutorial >= 16) CloseGuide();
            //     else guideUI.NextStepTutorial(CurrentTutorial);
            // }
            // CurrentTutorial++;
            // if (target is CardView)
            // {
            //     Destroy(target.gameObject);
            //     Debug.Log("点击了手指操作： " + CurrentTutorial);
            // }
            // if (CurrentTutorial >= 16) CloseGuide();
            // else guideUI.NextStepTutorial(CurrentTutorial);
        }
    }

    /// <summary>
    /// 显示教程界面
    /// </summary>
    /// <remarks>
    /// 调用UI管理器显示指定的教程面板
    /// </remarks>
    public void DisplayGuide()
    {
        if (SystemManager.Instance != null)
        {
            UIWindow uiWindow = SystemManager.Instance.ShowPanel(PanelType.LearningGuide);
            guideUI = uiWindow.GetComponent<LearningGuide>();
            // AnalyticMgr.GuideBegin();
        }
        else
        {
            Debug.LogError("UI管理器未初始化！");
        }
    }

    /// <summary>
    /// 隐藏教程界面
    /// </summary>
    /// <remarks>
    /// 调用UI管理器隐藏教程面板，并执行清理操作
    /// </remarks>
    public void CloseGuide()
    {
        if (guideUI != null)
        {
            Destroy(guideUI.gameObject);
        }
        else if (SystemManager.Instance != null)
        {
            UIWindow panel = SystemManager.Instance.GetPanel(PanelType.LearningGuide);
            if(panel != null)
                Destroy(panel.gameObject);
        }
        else
        {
            Debug.LogError("UI管理器未初始化！");
        }
    }

    public void RestoreCanvasLayers()
    {
        if (guideUI != null)
        {
            guideUI.RestoreCanvasLayers();
        }
    }
    /// <summary>
    /// 校验玩家的操作是否符合当前教程步骤的目标
    /// </summary>
    private bool CheckIfActionMatchesTutorial(CardView card, MonoBehaviour target)
    {
        int step = CurrentTutorial;

        // 根据步骤定义“什么是正确的操作”
        switch (step)
        {
            // 阶段一：归类 (Card -> Slot)
            case 1:
            case 2:
            case 3:
            case 4:
            case 5:
                // 只要目标是 Slot，就算这一步通过
                // (更严谨的话可以判断是不是 CategorySlotView)
                return target is CategorySlotView;

            // 阶段二：列堆叠 (Card -> Column)
            case 6:
            case 7:
            case 8:
              

            // 阶段三：抽牌 (点击事件不在这里处理，略过)
            
            // 阶段四：废牌利用 (Card -> Slot/Column)
            case 11:
            case 12:
            case 13:
            case 14:
                // 只要这张牌来自废牌区 (需要 CardView 里记录一下 isFromWaste 或者 ChainPlayArea 传参时带上)
                // 假设 OnPlayerPlacedCard 没法直接知道 isFromWaste，
                // 你可能需要在 ChainPlayArea 发事件时带上 MoveRecord 信息
                // 或者简单点：只要发生了任何移动，都算过（对新手宽容点）
                // 只要目标是 Column，就算通过
                // 并且不能是空列（因为那是第15步教的）
                if (target is ColumnView col && col.cards.Count > 1) return true;
                // 注意：如果玩家把牌移到了空列，不算完成了“堆叠”任务，算没过
                return false;
            
                // return true; 
            
            // 阶段五：空列 (Card -> Empty Column)
            case 15:
                if (target is ColumnView emptyCol && emptyCol.cards.Count == 1) return true;
                return false;

            default:
                return true; // 其他情况默认通过
        }
    }
    // 1. 询问系统：当前是否处于强引导拦截状态？
    public bool IsStrictGuideActive()
    {
        if (guideUI == null || !guideUI.gameObject.activeInHierarchy) return false;
        return guideUI.IsStrictGuideActive();
    }
    // 2. 询问系统：玩家试图放下的这个目标，是教程允许的高亮目标吗？
    public bool IsTargetAllowed(GameObject targetObj)
    {
        if (guideUI == null || targetObj == null) return false;
        return guideUI.IsTargetElevated(targetObj);
    }
    // 3. 告诉系统：玩家拖拽失败了/乱扔了，请重新激活指引动画
    public void ResumeGuide()
    {
        if (guideUI != null && guideUI.gameObject.activeInHierarchy)
        {
            guideUI.ResumeGuideAnim();
        }
    }
    #endregion

    #region 生命周期

    private void OnDisable()
    {
        EventDispatcher.Instance.OnLevelStarted -= CheckTutorial;
        EventDispatcher.Instance.OnCardDragResult -= OnPlayerPlacedCard;
    }

    private void OnDestroy()
    {
        // 释放对象池资源
        if (_objectPool != null)
        {
            _objectPool.ReturnAllObjectsToPool();
            _objectPool = null;
        }

        // 单例实例清理
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion
}