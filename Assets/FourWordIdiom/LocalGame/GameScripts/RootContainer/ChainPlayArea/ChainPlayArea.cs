using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public struct MoveRecord
{
    public CardView card;           // 被移动的卡牌
    public ColumnView fromColumn;   // 它原来所在的列 (如果是从手牌区移的，这里可能是 null)
    public ColumnView toColumn;     // 它被移到了哪个列
    public CategorySlotView toSlot;
    // 如果你有手牌区(Waste)，可能还需要记录是否来自 Waste
    public bool fromWaste;
    public bool causedReveal;
}
public class ChainPlayArea : UIWindow
{
    public static ChainPlayArea Instance { get; private set; }

    [Header("管理区域")] 
    [SerializeField] private Text levelText;
    [SerializeField] private Text stepNameText;
    [SerializeField] private Text stepsText; // 步数文本
    [SerializeField] private Text msgText;
    [SerializeField] private Button hintButton;
    [SerializeField] private Button undoButton;
    [SerializeField] private Button bangButton;
    [SerializeField] private GameObject fingerPrefab; // 手指
    
    // 分类槽
    public GoalAreaController goalArea;
    // 列槽
    public TableauAreaController tableauArea;
    // 手牌区
    public HandAreaController handArea;
    [Header("拖拽设置")] 
    public Transform dragLayer;
    public Transform graveyardRoot;
    public float dragSpacing = 74f;
    
    [Header("运行时数据")] 
    private Dictionary<string, string> wordToCategoryMap = new Dictionary<string, string>();
    private Dictionary<string, int> categoryTotalCounts = new Dictionary<string, int>();
    private int completedCategoriesCount; // 当前已消除了多少个
    private Stack<MoveRecord> _moveHistory = new Stack<MoveRecord>();
    private ChainStageProgressData currentData => ChainStageController.Instance.CurrStageData;
    private LevelData currentLevelConfig;
    public int currentSteps  { get => currentData.currentSteps; set => currentData.currentSteps = value; }     // 当前步数
    // 拖拽的数量
    private List<CardView> draggingStack = new List<CardView>();
    private ColumnView sourceColumn;     //从列来源
    private bool isDraggingFromHand = false;  // 从手牌区
    private Vector2 _dragOffset; // 👇 🔥 新增：用于记录鼠标抓取点到头牌的偏移量
    public bool IsDraggingProgress { get; private set; } = false; // 是否在拖拽中
    private bool _isGameEnded = false; // 记录游戏是否已经结算（防连弹）
    
    private bool _isHintActive;   // 当前是否使用了提示
    private bool _canUndoNow;     // 是否允许撤回
    private Coroutine _currentHintCoroutine;  // 当前正在播放的动画
    private GameObject _currentGhostObj;     // 手指物体
    // 👇 🔥 新增：用于记录拖拽时当前悬停的底座
    private CategorySlotView _lastHoverSlot;
    private ColumnView _lastHoverCol;
    private CardView _lastHoverCard;
    public DateTime StartTime;

    protected override void Awake()
    {
        base.Awake();
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (dragLayer != null)
        {
            
            dragLayer.SetAsLastSibling(); 
            // 🔥 新增：给拖拽层强行注入最高层级 Canvas！
            if (!dragLayer.TryGetComponent<Canvas>(out var dragCanvas))
            {
                dragCanvas = dragLayer.gameObject.AddComponent<Canvas>();
            }
            dragCanvas.overrideSorting = true;
            // 使用你项目里最高级的 Layer 名，这里参考了你手势用的层级
            dragCanvas.sortingLayerName = UIPanelLayer.PopPanel;
            dragCanvas.sortingOrder = 3000; // 霸道数值，保证绝对压过新手引导遮罩！
        }
            
        hintButton.AddClickAction(OnHintClick);
        undoButton.AddClickAction(OnUndoClick);
        string stepName = MultilingualManager.Instance.GetString("StepCount");
        if (!string.IsNullOrEmpty(stepName))
        {
            stepNameText.text = stepName;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        GameCoreManager.Instance.SwitchBackground(true);
        EnterGame();
    }

    // private float maxIdleTime = 50f;
    // private float idleTimer = 0f;
    private bool isHintShown = false;
    private Coroutine _hintCoroutine;
    
    // protected void Update()
    // {
    //     idleTimer += Time.deltaTime;
    //     if (idleTimer >= maxIdleTime && !isHintShown)
    //     {
    //         ShowHintText("试试使用道具吧！");
    //         _hintCoroutine = StartCoroutine(HintCoroutine());
    //         isHintShown = true;
    //     }
    // }
    //
    // private IEnumerator HintCoroutine()
    // {
    //     Vector3 originalScale = Vector3.one;
    //     Vector3 targetScale = originalScale * 1.08f;
    //     float speed = 5f; // 缩放速度
    //     while (true)
    //     {
    //         float t = Mathf.PingPong(Time.time * speed, 1f);
    //         hintButton.transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
    //         yield return null;
    //     }
    // }
    
    /// <summary>
    /// 重置待机时间, 不让弹提示
    /// </summary>
    public void ResetIdleTimer()
    {
        // idleTimer = 0f;
        if (isHintShown)
        {
            msgText.transform.parent.gameObject.SetActive(false);
            isHintShown = false;
            if (_hintCoroutine != null)
            {
                StopCoroutine(_hintCoroutine);
                hintButton.transform.localScale = Vector3.one;
                _hintCoroutine = null;
            }
        }
    }
    public void EnterGame()
    {       
        InitUI();
        CleanUp();
        StartCoroutine(StartGame(ChainStageController.Instance.CurrStageInfo.CurrBoardData));
        // AudioManager.Instance.PlaySoundEffect("BGM_Calm_Bouncy");
        StartTime = DateTime.Now;
        completedCategoriesCount = currentData.finishedCategoryCount;
        Game.Ads?.ShowBanner();
    }

    private void InitUI()
    {
        levelText.text = MultilingualManager.Instance.GetString("Level") + " " + currentData.stageId.ToString();
        msgText.transform.parent.gameObject.SetActive(false);
        UpdateStepUI();
        hintButton.transform.parent.gameObject.SetActive(currentData.stageId != 1);
        
        // 撤回按钮状态逻辑：
        // 1. 栈里有东西 AND 2. 允许撤回标记为真 AND 3. 当前没有在播放提示
        bool undoActive = (_moveHistory.Count > 0) && _canUndoNow && !_isHintActive;
        undoButton.interactable = undoActive;
        // 如果你想让提示正在播放时，撤回按钮看起来完全灰掉：
        // undoButton.GetComponent<CanvasGroup>().alpha = undoActive ? 1f : 0.5f; 
        
        hintButton.interactable = !_isHintActive;
        Transform hintip = hintButton.transform.GetChild(0);
        Transform hintcost = hintButton.transform.GetChild(1);
        if (GameDataManager.Instance.UserData.toolInfo[101].count > 0)
        {
            hintcost.gameObject.SetActive(false);
            hintip.GetChild(0).gameObject.SetActive(false);
            Text hintext = hintip.GetComponentInChildren<Text>(true);
            hintext.text = GameDataManager.Instance.UserData.toolInfo[101].count.ToString();
            hintext.gameObject.SetActive(true);
        }
        else
        {
            hintcost.gameObject.SetActive(true);
            hintcost.GetComponentInChildren<Text>().text =
                GameDataManager.Instance.UserData.toolInfo[101].cost.ToString();
            hintip.GetComponentInChildren<Text>()?.gameObject.SetActive(false);
            hintip.GetChild(0).gameObject.SetActive(true);
        }   
        
        Transform undotip = undoButton.transform.GetChild(0);
        Transform undocost = undoButton.transform.GetChild(1);
        if (GameDataManager.Instance.UserData.toolInfo[102].count > 0)
        {
            undocost.gameObject.SetActive(false);
            undotip.GetChild(0).gameObject.SetActive(false);
            Text undotext =  undotip.GetComponentInChildren<Text>(true);
            undotext .text = GameDataManager.Instance.UserData.toolInfo[102].count.ToString();
            undotext.gameObject.SetActive(true);
        }
        else
        {
            undocost.gameObject.SetActive(true);
            undocost.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[102].cost.ToString();
            undotip.GetComponentInChildren<Text>()?.gameObject.SetActive(false);
            undotip.GetChild(0).gameObject.SetActive(true);
        }
        
        Transform bangtip = bangButton.transform.GetChild(0);
        Transform bangcost = bangButton.transform.GetChild(1);
        if (GameDataManager.Instance.UserData.toolInfo[103].count > 0)
        {
            bangcost.gameObject.SetActive(false);
            bangtip.GetChild(0).gameObject.SetActive(false);
            Text bangtext =  bangtip.GetComponentInChildren<Text>(true);
            bangtext .text = GameDataManager.Instance.UserData.toolInfo[103].count.ToString();
            bangtext.gameObject.SetActive(true);
        }
        else
        {
            bangcost.gameObject.SetActive(true);
            bangcost.GetComponentInChildren<Text>().text = GameDataManager.Instance.UserData.toolInfo[103].cost.ToString();
            bangtip.GetComponentInChildren<Text>()?.gameObject.SetActive(false);
            bangtip.GetChild(0).gameObject.SetActive(true);
        }
    }

    private IEnumerator StartGame(LevelData levelData)
    {
        isDraggingFromHand = false;
        sourceColumn = null;
        draggingStack.Clear();
        currentLevelConfig = levelData;
        // 👇 🔥 核心：计算当前需要多大的缩放比例
        const float scaleFor4Cols = 1.0f;        // 3列和4列的默认比例 (271x363)
        const float scaleFor5Cols = 0.804f;      // 5列的精确缩小比例 (218x292)
        int colCount = currentData.tableauColumns.Count;
        float targetScale = colCount >= 5 ? scaleFor5Cols : scaleFor4Cols;
        Vector3 scaleVec = new Vector3(targetScale, targetScale, 1f);

        // 分别对这4个独立模块进行缩放
        // 因为我们在 Unity 里设置了神级 Pivot，它们会各自朝着正确的方向变小！
        goalArea.transform.localScale = scaleVec;
        tableauArea.transform.localScale = scaleVec;
        handArea.transform.GetChild(0).localScale = scaleVec;
        handArea.transform.GetChild(1).localScale = scaleVec;
        dragLayer.localScale = scaleVec;
        BuildMap(levelData);
        PrecalculateTotals(levelData);
        
        goalArea.InitGoalSlotsEmpty(levelData.slotsDefault, OnSingleCategoryFinished);
        // goalArea.InitGoalSlots(levelData.slotsDefault, currentData.categorySlots,categoryTotalCounts,OnSingleCategoryFinished);
        handArea.InitHand(currentData.stockCardIds, currentData.wasteCardIds, wordToCategoryMap,categoryTotalCounts );
        tableauArea.InitTableauSlots(currentData.tableauColumns.Count);
        Sequence entranceAnim = PlayUIEntranceAnim();
        
        yield return null;
        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        EventDispatcher.Instance.TriggerChangeTopRaycast(false);
        entranceAnim.Play();
        yield return entranceAnim.WaitForCompletion();
        yield return StartCoroutine(goalArea.DealGoalCardsAnim(currentData.categorySlots, categoryTotalCounts, handArea.stockRoot));
        // 改为发牌
        yield return StartCoroutine(tableauArea.DealTableauCardsAnim(
            currentData.tableauColumns, 
            wordToCategoryMap, 
            categoryTotalCounts, 
            handArea.stockRoot // 发牌起点
        ));
        
        // tableauArea.InitTableau(currentData.tableauColumns, wordToCategoryMap ,categoryTotalCounts);
      
        yield return new WaitForSeconds(0.3f);
        EventDispatcher.Instance.TriggerChangeTopRaycast(true);
        EventDispatcher.Instance.TriggerLevelStarted(currentData.stageId);
        if (currentSteps <= 0 && !IsGameOver())
        {
            Debug.Log("⚠️ 侦测到死局存档！直接弹出失败窗口！");
            // 复用你写好的延迟判定失败逻辑
            StartCoroutine(DelayCheckFail()); 
        }
    }
    
    private Sequence PlayUIEntranceAnim()
    {
        // 动画时间设置
        float duration = 0.6f;
        Sequence seq = DOTween.Sequence();
        seq.Pause();
        // 1. 步数面板：从左边 800 像素外飞进来 (使用 OutBack 会有一点点回弹的 Q 弹效果)
        if (handArea.transform.GetChild(0).TryGetComponent<RectTransform>(out var stepRt))
        {
            // .From(true) 表示以当前位置为基准，相对偏移 -800
            seq.Insert(0, stepRt.DOAnchorPosX(-800f, duration).From(true).SetEase(Ease.OutBack));
        }

        // 2. 牌堆区：从右边 800 像素外飞进来
        if (handArea.transform.GetChild(1).TryGetComponent<RectTransform>(out var pileRt))
        {
            seq.Insert(0, pileRt.DOAnchorPosX(800f, duration).From(true).SetEase(Ease.OutBack));
        }

        // 3. 分类槽：从下方移上来，并从透明到显现
        if (goalArea.TryGetComponent<RectTransform>(out var goalRt))
        {
            seq.Insert(0.2f, goalRt.DOAnchorPosY(-400f, duration).From(true).SetEase(Ease.OutCubic));
            
            // 如果没有 CanvasGroup 就自动加上，用来做渐隐渐现
            if (!goalArea.TryGetComponent<CanvasGroup>(out var goalCg)) goalCg = goalArea.gameObject.AddComponent<CanvasGroup>();
            goalCg.alpha = 1f; // 确保最终是1
            seq.Insert(0.2f, goalCg.DOFade(0f, duration).From()); // 从 0 渐变到 1
        }

        // 4. 列槽区域：从下方更深的地方移上来，并透明显现 (稍微延迟一点点，形成错落感)
        if (tableauArea.TryGetComponent<RectTransform>(out var tabRt))
        {
            seq.Insert(0.3f, tabRt.DOAnchorPosY(-600f, duration).From(true).SetEase(Ease.OutCubic));
            
            if (!tableauArea.TryGetComponent<CanvasGroup>(out var tabCg)) tabCg = tableauArea.gameObject.AddComponent<CanvasGroup>();
            tabCg.alpha = 1f;
            seq.Insert(0.3f, tabCg.DOFade(0f, duration).From());
        }
        
        // ==========================================
        // 👇 🔥 新增：按钮的 Q 弹入场动画！
        // ==========================================
        float btnStartTime = 0.4f;   // 面板滑入得差不多了，按钮开始弹
        float stagger = 0.1f;        // 每个按钮依次弹出的时间差 (啵、啵、啵的效果)
        // 把你的功能按钮加进数组里统一处理（确保变量名和你上面定义的一致）
        Button[] bottomButtons = { hintButton, undoButton, bangButton };
        for (int i = 0; i < bottomButtons.Length; i++)
        {
            if (bottomButtons[i] != null && bottomButtons[i].gameObject.activeSelf)
            {
                Transform btnT = bottomButtons[i].transform;
                
                // 提前把按钮强行缩到 0，防止第一帧走光
                btnT.localScale = Vector3.zero;

                // 🔥 核心：为每个按钮单独创建一个“弹跳子序列”
                Sequence bounceSeq = DOTween.Sequence();
                
                // 【第 1 次大弹】：从 0 猛冲到 1.25 倍
                bounceSeq.Append(btnT.DOScale(1.25f, 0.2f).SetEase(Ease.OutQuad));
                
                // 【第 1 次回缩】：缩回 0.85 倍
                bounceSeq.Append(btnT.DOScale(0.85f, 0.12f).SetEase(Ease.InOutSine));
                
                // 【第 2 次小弹】：冲到 1.1 倍
                bounceSeq.Append(btnT.DOScale(1.1f, 0.1f).SetEase(Ease.InOutSine));
                
                // 【第 2 次回缩】：缩回 0.95 倍
                bounceSeq.Append(btnT.DOScale(0.95f, 0.08f).SetEase(Ease.InOutSine));
                
                // 【最终停稳】：回到 1.0 标准大小
                bounceSeq.Append(btnT.DOScale(1.0f, 0.08f).SetEase(Ease.OutQuad));

                // 将这个子序列插入到主进场序列的指定时间点
                seq.Insert(btnStartTime + (i * stagger), bounceSeq);
            }
        }
        return seq;
        // 等待整个进场动画播完
        // bool isComplete = false;
        // seq.OnComplete(() => isComplete = true);
        // yield return new WaitUntil(() => isComplete);
    }
    
    private void PrecalculateTotals(LevelData data)
    {
        categoryTotalCounts.Clear();
        foreach (var category in data.categories)
        {
            categoryTotalCounts[category.categoryId] = category.wordsData.Count;
        }
    }
    /// <summary>
    /// 获取某个分类一共需要几张词语牌
    /// </summary>
    public int GetCategoryTotalCount(string catId)
    {
        if (categoryTotalCounts.TryGetValue(catId, out int count))
        {
            return count;
        }
        return 999; // 没查到时给个极大值，防止意外触发全收集打勾
    }

    private void BuildMap(LevelData levelData)
    {
        wordToCategoryMap.Clear();
        foreach (var cat in levelData.categories)
        {
            if (!wordToCategoryMap.ContainsKey(cat.categoryId))
            {
                wordToCategoryMap.Add(cat.categoryId, cat.categoryId);
            }

            foreach (var w in cat.wordsData)
            {
                if (!wordToCategoryMap.ContainsKey(w.wordId))
                    wordToCategoryMap.Add(w.wordId, cat.categoryId);
            }
        }
    }

    // 开始拖拽
    public void OnCardBeginDrag(CardView card, PointerEventData eventData)
    {
        NotifyPlayerAction();
        draggingStack.Clear();
        sourceColumn = card.currentColumn;
        
        // A. 从牌桌列拖拽
        if (sourceColumn != null)
        {
            isDraggingFromHand = false;
            if (!card.isFaceUp)
            {
                eventData.pointerDrag = null;
                return;
            }

            CardView startCard = FindChainRoot(card, sourceColumn);
            int index = sourceColumn.cards.IndexOf(startCard);
            // ==========================================
            // 👇 🔥 核心修复 2：防弹衣！绝不允许 -1 越界摧毁游戏！
            // ==========================================
            if (index < 0) 
            {
                Debug.LogWarning($"[极度异常] 卡牌 {card.name} 记录的 Column 中找不到它自己！已拦截崩溃。");
                eventData.pointerDrag = null; // 强行终止拖拽
                return;
            }
            for (int i = index; i < sourceColumn.cards.Count; i++)
            {
                draggingStack.Add(sourceColumn.cards[i]);
            }

            // sourceColumn.RemoveCardsFrom(startCard);
            ChainStageController.Instance.SyncTableauState(tableauArea.columns);
        }
        // B. 从手牌区拖拽 (废牌堆)
        else if (handArea.wasteCards.Contains(card))
        {
            isDraggingFromHand = true;
            draggingStack.Add(card);
            handArea.RemoveCardFromWaste(card);
        }
        else
        {
            eventData.pointerDrag = null;
            return;
        }

        foreach (var c in draggingStack)
        {
            Vector3 worldPoint = c.transform.position;
            c.transform.SetParent(dragLayer);
            c.transform.position = worldPoint;
            c.transform.localScale = Vector3.one;
            c.transform.localRotation = Quaternion.identity;
            if (c.TryGetComponent<CanvasGroup>(out CanvasGroup cg))
            {
                cg.blocksRaycasts = false;
            }
        }

        IsDraggingProgress = true;
        foreach (var c in draggingStack)
        {
            c.SetDragHighlight(true);
        }
        
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)dragLayer,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localMousePos
            ))
        {
            // 我们要保持鼠标和【整摞牌】的相对位置
            // 因为代码里移动的参照物永远是头牌 (draggingStack[0])
            // 所以 偏移量 = 头牌当前的位置 - 鼠标的位置
            CardView rootCard = draggingStack[0];
            _dragOffset = (Vector2)rootCard.transform.localPosition - localMousePos;
        }
    }

    // 拖拽中
    public void OnCardDrag(CardView card, PointerEventData eventData)
    {
        if (draggingStack.Count > 0)
        {
            // Vector3 delta = (Vector3)eventData.delta;
            var targetCard = draggingStack[0];
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)dragLayer,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint
                ))
            {
                // targetCard.transform.localPosition = localPoint;
                targetCard.transform.localPosition = localPoint + _dragOffset;
            }
            
            for (int i = 1; i < draggingStack.Count; i++)
            {
                CardView current = draggingStack[i];
                if (i > 0)
                {
                    CardView prev = draggingStack[i - 1];
                    current.transform.localPosition = prev.transform.localPosition + new Vector3(0, -dragSpacing, 0);
                }
                current.transform.SetAsLastSibling();
                if (current.TryGetComponent<Canvas>(out var myCanvas))
                {
                    myCanvas.sortingOrder = 30000 + i; 
                }
            }
            // 👇 🔥 新增：拖拽过程中的嗅探预警（底座绿框亮起）
            CardView dragHead = draggingStack[0];
            CategorySlotView currentHoverSlot = GetSlotUnderMouse(eventData, dragHead);
            ColumnView currentHoverCol = GetColumnUnderMouse(eventData, dragHead);
            CardView currentHoverCard = null; // 🔥 新增：当前悬停的牌
            
            // 为了防止同时亮起，做个简单优先级：碰到顶部分类槽就忽略下方的列
            if (currentHoverSlot != null) currentHoverCol = null;

            // 👇 🔥 核心逻辑：如果悬停在列上，且列里有牌，就让顶牌作为目标！
            if (currentHoverCol != null)
            {
                currentHoverCard = currentHoverCol.GetTopCard();
                if (currentHoverCard != null)
                {
                    // 列里有牌，强制把列底座置空，让列底座不要亮！
                    currentHoverCol = null; 
                }
            }
            
            // 刷新分类槽的悬停绿框
            if (_lastHoverSlot != currentHoverSlot)
            {
                if (_lastHoverSlot != null) _lastHoverSlot.SetHoverHighlight(false);
                if (currentHoverSlot != null) currentHoverSlot.SetHoverHighlight(true);
                _lastHoverSlot = currentHoverSlot;
            }

            // 刷新列的悬停绿框
            if (_lastHoverCol != currentHoverCol)
            {
                if (_lastHoverCol != null) _lastHoverCol.SetHoverHighlight(false);
                if (currentHoverCol != null) currentHoverCol.SetHoverHighlight(true);
                _lastHoverCol = currentHoverCol;
            }
            
            // 👇 🔥 新增：刷新顶牌绿框
            if (_lastHoverCard != currentHoverCard)
            {
                if (_lastHoverCard != null) _lastHoverCard.SetHoverHighlight(false);
                if (currentHoverCard != null) currentHoverCard.SetHoverHighlight(true);
                _lastHoverCard = currentHoverCard;
            }
        }
    }

    // 结束拖拽
    public void OnCardEndDrag(CardView card, PointerEventData eventData)
    {
        ResetIdleTimer();
        IsDraggingProgress = false;
        
        // 👇 🔥 新增：一松手，立刻关掉卡牌和底座的绿框！
        foreach (var c in draggingStack) c.SetDragHighlight(false);
        if (_lastHoverSlot != null) { _lastHoverSlot.SetHoverHighlight(false); _lastHoverSlot = null; }
        if (_lastHoverCol != null) { _lastHoverCol.SetHoverHighlight(false); _lastHoverCol = null; }
        if (_lastHoverCard != null) { _lastHoverCard.SetHoverHighlight(false); _lastHoverCard = null; }
        
        // 1. 获取最佳 Slot 和 最佳 Column
        CardView dragHead = draggingStack[0];
        CategorySlotView bestSlot = GetSlotUnderMouse(eventData, dragHead);
        ColumnView bestCol = GetColumnUnderMouse(eventData, dragHead);
        // 计算卡牌中心
        RectTransform cardRect = dragHead.GetComponent<RectTransform>();
        float heightOffset = cardRect.rect.height * dragHead.transform.lossyScale.y * 0.5f;
        Vector3 draggingCenter = dragHead.transform.position - new Vector3(0, heightOffset, 0);
        
        bool useSlot = false;
        bool useCol = false;
        float distToSlot = float.MaxValue;
        float distToCol = float.MaxValue;
        if (bestSlot != null) distToSlot = Vector3.Distance(draggingCenter, bestSlot.transform.position);
        if (bestSlot != null && bestCol != null)
        {
            if (distToSlot < distToCol) useSlot = true;
            else useCol = true;
        }
        
        if (bestCol != null)
        {
            Vector3 colTarget = bestCol.transform.position;
            if(bestCol.GetTopCard()) colTarget = bestCol.GetTopCard().transform.position;
            distToCol = Vector3.Distance(draggingCenter, colTarget);
        }
        // 判定优先级：
        // 如果两个都有，选近的。
        // 如果只有一个，选那个。
        if (bestSlot != null && bestCol != null)
        {
            if (distToSlot < distToCol) useSlot = true;
            else useCol = true;
        }
        else if (bestSlot != null) useSlot = true;
        else if (bestCol != null) useCol = true;
        
        // 🚨🚨🚨【通过 GuideSystem 中转的强引导拦截】🚨🚨🚨
        if (ChainGuideSystem.Instance != null && ChainGuideSystem.Instance.IsStrictGuideActive())
        {
            // 想放槽位，但槽位没被允许，无情拒绝
            if (useSlot && bestSlot != null && !ChainGuideSystem.Instance.IsTargetAllowed(bestSlot.gameObject))
            {
                useSlot = false; 
            }
            
            // 想放列里，但列（和顶牌）没被允许，无情拒绝
            if (useCol && bestCol != null)
            {
                bool colAllowed = ChainGuideSystem.Instance.IsTargetAllowed(bestCol.gameObject);
                bool topCardAllowed = bestCol.GetTopCard() != null && ChainGuideSystem.Instance.IsTargetAllowed(bestCol.GetTopCard().gameObject);
                
                if (!colAllowed && !topCardAllowed)
                {
                    useCol = false; 
                }
            }
        }
        
        // ---> 尝试放入 Slot
        if (useSlot)
        {
            if (IsValidMove(draggingStack, bestSlot)) 
            {
                if (!isDraggingFromHand && sourceColumn != null)
                {
                    sourceColumn.RemoveCardsFrom(draggingStack[0]);
                }
                
                OnCardMovedSuccess(draggingStack[0], sourceColumn, null, isDraggingFromHand, bestSlot,false);
                HandleDropToSlot(bestSlot, draggingStack);
                EventDispatcher.Instance.TriggerCardDragResult(dragHead, bestSlot,true);
                CleanUpDrag();  // 清理并退出
                return;
            }
            else
            {
                ReturnToSource();
                CleanUpDrag();
                HandleMoveFailure(bestSlot, null);
                EventDispatcher.Instance.TriggerCardDragResult(dragHead, bestSlot,false);
                return;
            }
        }

        if (useCol)
        {
            if (!isDraggingFromHand && bestCol == sourceColumn)
            {
                ReturnToSource();
                CleanUpDrag();
                
                // 恢复强引导
                if (ChainGuideSystem.Instance != null && ChainGuideSystem.Instance.IsStrictGuideActive())
                {
                    ChainGuideSystem.Instance.ResumeGuide();
                }
                
                // 彻底退出，不走下面的 IsValidMove，绝不扣步数！
                return; 
            }
            if (IsValidMove(draggingStack, bestCol))
            {
                if (!isDraggingFromHand && sourceColumn != null)
                {
                    sourceColumn.RemoveCardsFrom(draggingStack[0]);
                }
                // >> 放置成功 <<
                for (int i = 0; i < draggingStack.Count; i++)
                {
                    CardView c = draggingStack[i];
                    bestCol.AddCard(c);
                    c.IsInHand = false; // 确保标记离开手牌区
                    c.UpdateZoneVisuals(false, true); // 🔥 播放变大并恢复背景的动画
                    bool isTopCard = (i == draggingStack.Count - 1);
                    c.SetCompressedState(!isTopCard, true);
                    c.transform.SetAsLastSibling(); 
                    if (c.TryGetComponent<Canvas>(out var myCanvas))
                    { 
                        myCanvas.overrideSorting = false;
                    }
                }
                
                bool revealedNewCard = false; // 🔥 新增：记录是否翻开了新牌
                if (!isDraggingFromHand && sourceColumn != null && sourceColumn != bestCol)
                {
                    revealedNewCard = sourceColumn.RevealLastCard();
                }
                if(isDraggingFromHand) handArea.OnCardUsed(card);
                OnCardMovedSuccess(draggingStack[0], sourceColumn, bestCol, isDraggingFromHand,null, revealedNewCard);
                ConsumeStep();
                EventDispatcher.Instance.TriggerCardDragResult(dragHead, bestCol, true);
                
                ChainStageController.Instance.SyncTableauState(tableauArea.columns);
                CleanUpDrag();  // 清理并退出
                return;
            }
            else
            {
                ReturnToSource();
                CleanUpDrag();
                HandleMoveFailure(null, bestCol);
                EventDispatcher.Instance.TriggerCardDragResult(dragHead, bestCol, false);
                return;
            }
        }
        ReturnToSource();
        CleanUpDrag();
        if (ChainGuideSystem.Instance != null && ChainGuideSystem.Instance.IsStrictGuideActive())
        {
            ChainGuideSystem.Instance.ResumeGuide();
        }
    }
   
    private void ReturnToSource()
    {
        // >> 放置失败：回弹 <<
        if (isDraggingFromHand)
        {
            foreach (var c in draggingStack)
            {
                handArea.AddCardToWaste(c);
                c.UpdateZoneVisuals(true, false); // 🔥 确保恢复为小牌状态
                c.PlayErrorAnimation();
            }
        }
        else if (sourceColumn != null)
        {
            // 👇 核心修复：不走 AddCard，走专门的回弹方法！
            sourceColumn.ReturnCardsFromDrag(draggingStack);
            foreach (var c in draggingStack)
            {
                // sourceColumn.AddCard(c);
                c.PlayErrorAnimation();
            }

            // ChainStageController.Instance.SyncTableauState(tableauArea.columns);
        }
    }
    // 提取一个清理方法，避免重复代码
    private void CleanUpDrag()
    {
        if (draggingStack != null)
        {
            foreach (var c in draggingStack)
            {
                if (c.TryGetComponent<CanvasGroup>(out CanvasGroup cg))
                {
                    cg.blocksRaycasts = true;
                }
            }
            draggingStack.Clear();
        }
        
        sourceColumn = null;
        isDraggingFromHand = false;
    }
    /// <summary>
    /// 处理拖拽失败：分析原因并弹字
    /// </summary>
    private void HandleMoveFailure(CategorySlotView hitSlot, ColumnView hitCol)
    {
        AudioManager.Instance.PlaySoundEffect("ChoiceError_UI");
        MoveErrorType error = MoveErrorType.None;

        // 1. 如果鼠标下面是【槽位】，诊断槽位错误
        if (hitSlot != null)
        {
            // 👇 🔥 新增：触发分类槽的红框和摇头动画！
            hitSlot.ShowErrorFeedback();
            IsValidMoveWithReason(draggingStack, hitSlot, out error);
        }
        // 2. 如果鼠标下面是【列】，诊断列错误
        else if (hitCol != null)
        {
            // 👇 🔥 核心逻辑：失败时，如果列里有顶牌，就让顶牌爆红框！否则列底座爆红框！
            CardView topCard = hitCol.GetTopCard();
            // 👇 🔥 新增：触发分类槽的红框和摇头动画！
            if (topCard != null)
            {
                topCard.ShowErrorFeedback();
            }
            else
            {
                hitCol.ShowErrorFeedback(); 
            }
            IsValidMoveWithReason(draggingStack, hitCol, out error);
        }
        else
        {
            // 3. 鼠标下面是空地
            // 可以选择不提示，或者提示“无效区域”
            return;
        }

        // 4. 根据错误类型显示文本
        string msg = "";
        switch (error)
        {
            case MoveErrorType.TargetIsCategory:
                // 对应需求：移动到分类牌上失败
                msg = "无法在分类牌上方放置"; 
                break;
            
            case MoveErrorType.CategoryMismatch:
                // 对应需求：移动同一类失败 (ID不对)
                msg = "只能连接同类卡牌";   
                break;
            
            case MoveErrorType.SourceIsCategory:
                // 对应需求：分类牌移动其他位置失败 (例如想把分类牌放在非空列)
                msg = "分类牌只能移至空位";   
                break;
            
            case MoveErrorType.SlotIsFull:
                msg = "该分类槽已满";
                break;
            
            // 如果需要，可以加 default
        }

        // 显示并自动隐藏
        if (!string.IsNullOrEmpty(msg))
        {
            StartCoroutine(ButtonHintCoroutine(msg)); // 1.5秒后消失
        }
       
    }
    private void HandleDropToSlot(CategorySlotView slot, List<CardView> draggingCards)
    {
        // 临时列表：记录哪些牌被成功吸收了
        List<CardView> acceptedCards = new List<CardView>();
        // 临时列表：记录哪些牌被拒绝了（需要退回）
        List<CardView> rejectedCards = new List<CardView>();
        // 1. 基础校验：没有牌直接跳过
        if (draggingCards.Count == 0) return;
        
        var sortedCards = draggingCards.OrderByDescending(c => c.type == CardType.Category).ToList();
        foreach (var card in sortedCards)
        {
            // A. 尝试激活空槽
            if (!slot.isOccupied)
            {
                if (card.type == CardType.Category)
                {
                    int total = categoryTotalCounts.GetValueOrDefault(card.categoryId, 5);
                    slot.ActivateCategory(card, total);
                    acceptedCards.Add(card);
                    slot.PlayHighlightEffect();
                }
                else
                {
                    rejectedCards.Add(card);
                }
            }
            else
            {
                bool isMatch = card.categoryId == slot.categoryId;
                bool notHeader = card.type != CardType.Category;
                bool notFull = !slot.IsFull();
                if (isMatch && notHeader && notFull)
                {
                    slot.AddWordCard(card);
                    acceptedCards.Add(card);
                    slot.PlayHighlightEffect();
                }
                else
                {
                    rejectedCards.Add(card);
                }
            }
        }
        ChainStageController.Instance.SyncCategoryState(goalArea.allSlots, completedCategoriesCount);
        ProcessBatchMoveResults(acceptedCards, rejectedCards);
        
    }

    // 批量处理消除
    private void ProcessBatchMoveResults(List<CardView> accepted, List<CardView> rejected)
    {
        if (accepted.Count > 0)
        {
            foreach (var card in accepted)
            {
                if (isDraggingFromHand)
                {
                    handArea.OnCardUsed(card);
                }
                card.gameObject.SetActive(false);
                card.transform.SetParent(graveyardRoot, false);
                // Destroy(card.gameObject);
            }
            ConsumeStep();

            if (!isDraggingFromHand && sourceColumn != null)
            {
                sourceColumn.UpdateLayout();
                sourceColumn.RevealLastCard();
            }
        }

        if (rejected.Count > 0)
        {
            if (isDraggingFromHand)
            {
                foreach (var c in rejected)
                {
                    handArea.AddCardToWaste(c);
                }
            }
            else if (sourceColumn != null)
            {
                foreach (var c in rejected)
                {
                    if (c.TryGetComponent<CanvasGroup>(out var cg))
                    {
                        cg.blocksRaycasts = true;
                    }

                    sourceColumn.AddCard(c);
                }
            }
        }
        ChainStageController.Instance.SyncTableauState(tableauArea.columns);
        if (isDraggingFromHand)
        {
            ChainStageController.Instance.SyncHandState(handArea.stockData,handArea.wasteCards);
        }
        // draggingStack.Clear();
        // sourceColumn = null;
        // isDraggingFromHand = false;
        // 播放音效？
    }

    public void ConsumeStep()
    {
        if (currentSteps > 0)
        {
            currentSteps--;
            UpdateStepUI(true);
            if (currentSteps <= 0)
            {
                StartCoroutine(DelayCheckFail());
            }
        }
    }
    private IEnumerator DelayCheckFail()
    {
        // 延迟 0.5 秒（根据你游戏里卡牌飞入槽位动画的时间可以微调）
        yield return new WaitForSeconds(0.5f);
        
        // 🔥 关键拦截：如果在这 0.5 秒内，槽位吸收完毕并触发了胜利，就绝对不要再弹失败了！
        if (_isGameEnded || completedCategoriesCount >= categoryTotalCounts.Count)
        {
            yield break;
        }

        _isGameEnded = true; // 上锁
        Debug.Log("步数耗尽, 游戏结束! ");
        StartCoroutine(CheckCompleted(false));
    }

    public bool IsGameOver() => completedCategoriesCount == categoryTotalCounts.Count;
    // 消除通知
    private void OnSingleCategoryFinished(string catId)
    {
        completedCategoriesCount++;
        if (completedCategoriesCount >= categoryTotalCounts.Count)
        {
            if (_isGameEnded) return;
            _isGameEnded = true;
            Debug.Log("🎉 胜利！所有分类已完成！");
            StartCoroutine(CheckCompleted());
        }
    }

    private IEnumerator CheckCompleted(bool isComplete = true)
    {
        yield return new WaitForSeconds(0.2f);
        // 弹窗
        ChainStageController.Instance.CompleteStage(currentData.stageId, isComplete);
    }

    // 向上查找同类牌
    private CardView FindChainRoot(CardView clickedCard, ColumnView column)
    {
        int clickedIndex = column.cards.IndexOf(clickedCard);
        CardView currentRoot = clickedCard;
        for (int i = clickedIndex - 1; i >= 0; i--)
        {
            CardView prevCard = column.cards[i];
            if (!prevCard.isFaceUp) break;
            if (prevCard.categoryId == currentRoot.categoryId)
            {
                currentRoot = prevCard;
                if (prevCard.type == CardType.Category) break;
            }
            else break;
        }

        return currentRoot;
    }
    // 射线检测获取鼠标下分类槽的 Slot 
    private CategorySlotView GetSlotUnderMouse(PointerEventData eventData, CardView draggingCard)
    {
        RectTransform cardRect = draggingCard.GetComponent<RectTransform>();
        // float heightOffset = cardRect.rect.height * draggingCard.transform.lossyScale.y * 0.5f;
        // Vector3 draggingCenter = draggingCard.transform.position - new Vector3(0, heightOffset, 0);
        Vector3 draggingCenter = cardRect.transform.TransformPoint(cardRect.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, draggingCenter);
        PointerEventData probeData = new PointerEventData(EventSystem.current) { position = screenPoint };
        
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(probeData, results);

        CategorySlotView bestSlot = null;
        float minDistance = float.MaxValue;
        foreach (var r in results)
        {
            var slot = r.gameObject.GetComponentInParent<CategorySlotView>();
            if (slot != null)
            {
                float dist = Vector3.Distance(draggingCenter, slot.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestSlot = slot;
                }
            }
        }

        return bestSlot;
    }

    // 射线检测获取鼠标下的 Column
    private ColumnView GetColumnUnderMouse(PointerEventData eventData, CardView draggingCard)
    {
        // 1. 计算拖拽卡牌的中心点
        RectTransform cardRect = draggingCard.GetComponent<RectTransform>();
        // float heightOffset = cardRect.rect.height * draggingCard.transform.lossyScale.y * 0.5f;
        // Vector3 draggingCenter = draggingCard.transform.position - new Vector3(0, heightOffset, 0);
        Vector3 draggingCenter = cardRect.transform.TransformPoint(cardRect.rect.center);
        // 2. 射线检测 转成屏幕坐标发射射线
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, draggingCenter);
        PointerEventData probeData = new PointerEventData(EventSystem.current) { position = screenPoint };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(probeData, results);
        
        // 3. 寻找最佳候选者
        ColumnView bestCol = null;
        float minDistance = float.MaxValue;
        foreach (var r in results)
        {
            var col = r.gameObject.GetComponentInParent<ColumnView>();
            if (col != null)
            {
                Vector3 targetPos = col.transform.position;
                CardView topCard = col.GetTopCard();
                if (topCard != null)
                {
                    targetPos = topCard.transform.position;
                }

                float dist = Vector3.Distance(draggingCenter, targetPos);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestCol = col;
                }
            }
        }

        return bestCol;
    }
    
    private float _stepTextBaseY;
    private Color _originalStepColor;
    private bool _isStepBaseYInitialized = false;
    private void UpdateStepUI(bool playAnim = false)
    {
        stepsText.text = currentSteps.ToString();

        // 1. 初始化基准高度和原始颜色
        if (!_isStepBaseYInitialized)
        {
            _stepTextBaseY = stepsText.GetComponent<RectTransform>().anchoredPosition.y;
            _originalStepColor = stepsText.color; // 记录美术在面板里配好的原本颜色
            _isStepBaseYInitialized = true;
        }

        // 2. 👇 🔥 步数告急变色：最后 5 步变红，重开/撤回高于 5 步时恢复原色
        if (currentSteps <= 5)
        {
            // 如果你想用特定的暗红，可以写 new Color(1f, 0.2f, 0.2f, 1f)
            stepsText.color = Color.red; 
        }
        else
        {
            stepsText.color = _originalStepColor;
        }

        // 3. 播放跳跃动效
        if (playAnim)
        {
            RectTransform txtRt = stepsText.GetComponent<RectTransform>();
                
            // 强行打断旧动画，瞬间复位（高度和缩放全部复位，防连点）
            txtRt.DOKill();
            txtRt.anchoredPosition = new Vector2(txtRt.anchoredPosition.x, _stepTextBaseY);
            txtRt.localScale = Vector3.one;

            Sequence seq = DOTween.Sequence();
                
            // 【阶段 1：被微微拎起来】
            // 👇 🔥 高度改小：从 25f 改成了 12f，不会飞得太夸张
            seq.Append(txtRt.DOAnchorPosY(_stepTextBaseY + 6f, 0.05f).SetEase(Ease.OutQuad));
            seq.Join(txtRt.DOScale(new Vector3(0.8f, 1.2f, 1f), 0.05f).SetEase(Ease.OutQuad));
                
            // 【阶段 2：重重砸在地上】
            // 往下压扁向左右拉伸
            seq.Append(txtRt.DOAnchorPosY(_stepTextBaseY, 0.05f).SetEase(Ease.InQuad));
            seq.Join(txtRt.DOScale(new Vector3(1.3f, 0.7f, 1f), 0.05f).SetEase(Ease.InQuad));
                
            // 【阶段 3：果冻第一次回弹】
            seq.Append(txtRt.DOScale(new Vector3(0.9f, 1.1f, 1f), 0.1f).SetEase(Ease.InOutSine));
                
            // 【阶段 4：定格恢复正常】
            seq.Append(txtRt.DOScale(Vector3.one, 0.05f).SetEase(Ease.OutBack));
        }
    }
    
    // 是否需要显示图片
    public bool ShouldShowIcon(string cardId)
    {

        if (currentLevelConfig == null) return false;

        // 遍历所有分类
        foreach (var cat in currentLevelConfig.categories)
        {
            if (cat.categoryId == cardId)
            {
                return cat.icon;
            }
            
            // 遍历分类下的所有词条
            foreach (var word in cat.wordsData)
            {
                if (word.wordId == cardId)
                {
                    // 🔥 找到了！返回配置里的 icon 字段
                    return word.icon;
                }
            }
        }
    
        // 没找到，默认不显示图片
        return false;
    }

    #region 道具使用

    /// <summary>
    /// 记录卡牌的位置
    /// </summary>
    /// <param name="card"></param>
    /// <param name="fromCol"></param>
    /// <param name="toCol"></param>
    /// <param name="isFromWaste"></param>
    /// <param name="toSlot"></param>
    /// <param name="causedReveal"></param>
    public void OnCardMovedSuccess(CardView card, ColumnView fromCol, ColumnView toCol, bool isFromWaste = false, 
        CategorySlotView toSlot = null, bool causedReveal = false)
    {
        AudioManager.Instance.PlaySoundEffect("Change_Block");
        MoveRecord record = new MoveRecord
        {
            card = card,
            fromColumn = fromCol,
            toColumn = toCol,
            toSlot = toSlot,
            fromWaste = isFromWaste,
            causedReveal = causedReveal,
        };
        _moveHistory.Push(record);
        // 这里可以刷新UI，比如让撤回按钮变亮
        Debug.Log($"记录操作: {card.name} 从 {fromCol?.name} 到 {toCol?.name}");
        _canUndoNow = true;
        InitUI();
    }

    /// <summary>
    /// 撤回按钮
    /// </summary>
    public void OnUndoClick()
    {
        AudioManager.Instance.PlaySoundEffect("ResetTool");
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[102];
        if (toolInfo == null)
        {
            StartCoroutine(ButtonHintCoroutine("没有该道具!"));
            return;
        }
        if (_isHintActive || !_canUndoNow || _moveHistory.Count == 0) 
        {
            StartCoroutine(ButtonHintCoroutine("撤回只能连续使用一次"));
            return;
        }
        if (toolInfo.count <= 0)
        {
            UIWindow propPanel = SystemManager.Instance.ShowPanel(PanelType.UsePropPanel);
            propPanel.GetComponent<UsePropPanel>().Setup(toolInfo, un =>
            {   
                HandleUndo();
            });
        }
        else
        {
            HandleUndo();
        }
    }
    
    private void HandleUndo()
    {
        MoveRecord lastMove = _moveHistory.Pop();
        if (lastMove.card == null)
        {
            InitUI();
            return;
        }
        GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Undotool, -1, "关卡内使用");
     
        List<CardView> cardsToReturn = new List<CardView>();
        // A. 从当前列移除
        if (lastMove.toColumn != null)
        {
            cardsToReturn = lastMove.toColumn.RemoveCardsFrom(lastMove.card);
            lastMove.toColumn.UpdateLayout();
        }else if (lastMove.toSlot != null)
        {
            lastMove.toSlot.RemoveCard(lastMove.card);
            lastMove.card.gameObject.SetActive(true);
            cardsToReturn.Add(lastMove.card);
        }
        foreach (var c in cardsToReturn)
        {
            if (c.TryGetComponent<CanvasGroup>(out var cg))
            {
                cg.blocksRaycasts = true;
            }
        }
        
        // B. 放回原处
        if (lastMove.fromWaste)
        {
            // 放回手牌区
            foreach(var c in cardsToReturn) handArea.ReturnCard(c);
        }
        else if (lastMove.fromColumn != null)
        {
            if (lastMove.causedReveal)
            {
                CardView top = lastMove.fromColumn.GetTopCard();
                if(top != null) top.SetFaceUp(false);
            }

            foreach (var c in cardsToReturn)
            {
                lastMove.fromColumn.AddCard(c);
            }
            
            // 放回原列
            // lastMove.fromColumn.AddCard(lastMove.card);
            lastMove.fromColumn.UpdateLayout();
        }
        
        lastMove.card.UpdateVisualState();

        currentSteps++;
        UpdateStepUI(true);
        _canUndoNow = false;
        InitUI();
        // AudioManager.Instance.PlaySoundEffect("ResetTool");
        StartCoroutine(ButtonHintCoroutine(MultilingualManager.Instance.GetString("ItemTips01")));
        
    }

    /// <summary>
    /// 提示按钮
    /// </summary>
    public void OnHintClick()
    {
        AudioManager.Instance.PlaySoundEffect("HintTool");
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[101];
        if (toolInfo == null)
        {
            StartCoroutine(ButtonHintCoroutine("没有该道具!"));
            return;
        }
        if (_isHintActive) return;
        
        if (toolInfo.count <= 0)
        {
            UIWindow propPanel = SystemManager.Instance.ShowPanel(PanelType.UsePropPanel);
            propPanel.GetComponent<UsePropPanel>().Setup(toolInfo, un =>
            {   
                HandleHint();
            });
        }
        else
        {
            HandleHint();
        }
    }

    private void HandleHint()
    {
        // AudioManager.Instance.PlaySoundEffect("HintTool");
        bool found = FindAndShowHint();
        if (found)
        {
            _isHintActive = true;
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, -1, "关卡内使用");
            InitUI();
        }
        else
        {
            Debug.Log("没有找到可行的移动！");
            StartCoroutine(ButtonHintCoroutine("没有找到可行的移动！"));
            // 可以播放一个“无法移动”的音效或抖动
        }
    }
    
    private bool FindAndShowHint()
    {
        // 1. 检查手牌 (Hand) -> 任意位置
        CardView handCard = handArea.GetCurrentCard();
        if (handCard != null)
        {
            // 1.1 手牌 -> 槽位
            foreach (var slot in goalArea.allSlots)
            {
                if (IsValidMove(handCard, slot))
                {
                    _currentHintCoroutine = StartCoroutine(LoopCardMoveAnimation(handCard, slot.transform));
                    return true;
                }
            }
            // 1.2 手牌 -> 列
            foreach (var col in tableauArea.columns)
            {
                if (IsValidMove(handCard, col))
                {
                    Transform targetT = col.GetTopCard() ? col.GetTopCard().transform : col.transform;
                    _currentHintCoroutine = StartCoroutine(LoopCardMoveAnimation(handCard, targetT));
                    return true;
                }
            }
        }
        
        // 2. 检查列 (Column) -> 任意位置
        foreach (var sourceCol in tableauArea.columns)
        {
            CardView topCard = sourceCol.GetTopCard();
            if (topCard == null) continue;
            // 2.1 列 -> 槽位
            foreach (var slot in goalArea.allSlots)
            {
                if (IsValidMove(topCard, slot))
                {
                    _currentHintCoroutine = StartCoroutine(LoopCardMoveAnimation(topCard, slot.transform));
                    return true;
                }
            }
            
            // 2.2 列 -> 其他列
            // (通常不用提示头牌移动到空列，那是无意义操作，所以加个判断)
            // if (topCard.type == CardType.Category && sourceCol.cards.Count == 1) continue;
            foreach (var targetCol in tableauArea.columns)
            {
                if (sourceCol == targetCol) continue;
                if (IsValidMove(topCard, targetCol))
                {
                    Transform targetT = targetCol.GetTopCard() ? targetCol.GetTopCard().transform : targetCol.transform;
                    _currentHintCoroutine = StartCoroutine(LoopCardMoveAnimation(topCard, targetT));
                    return true;
                }
            }
        }
        
        // 3. 如果牌堆还有牌 -> 提示点击牌堆
        if (handArea.stockData.Count > 0)
        {
            ShowHintText("从牌堆中抽一张牌");
            _currentHintCoroutine = StartCoroutine(LoopFingerClickAnimation(handArea.stockButton.transform));
            return true;
        }
        
        // 4. 如果牌堆没牌了，但废牌堆有牌 -> 提示点击刷新
        if (handCard !=null)
        {
            ShowHintText("重置牌堆再试试吧");
            _currentHintCoroutine = StartCoroutine(LoopFingerClickAnimation(handArea.stockButton.transform));
            return true;
        }
        return false;
    }
    #endregion
    /// <summary>
    /// 【给提示用】包装器：判定单张牌能否放入列
    ///  判定 A: 卡牌 -> 牌桌列 (Column)
    /// </summary>
    private bool IsValidMove(CardView card, ColumnView target)
    {
        if (card == null || target == null) return false;
        // 模拟拖拽列表
        List<CardView> simulatedStack;
        if (card.currentColumn != null)
            simulatedStack = card.currentColumn.GetDragList(card);
        else
            simulatedStack = new List<CardView> { card };
        return IsValidMove(simulatedStack, target);
    }
    
    /// <summary>
    /// 【给拖拽用】真理标准：判定一摞牌能否放入列
    /// </summary>
    private bool IsValidMove(List<CardView> stack, ColumnView target)
    {
        if (stack == null || stack.Count == 0 || target == null) return false;
        
        // 情况 1: 目标列是空的
        if (target.cards.Count == 0)
        {
            return true; // 空列通常允许放入
        }
        // 情况 2: 目标列有牌
        else
        {
            // 必须同类接龙
            CardView topCard = target.GetTopCard();
            if (topCard.type == CardType.Category) return false; // 假设不能压头牌
            if (stack[0].categoryId != topCard.categoryId) return false;

            return true;
        }
    }
    /// <summary>
    /// 【给提示用】包装器：判定单张牌能否放入槽位
    /// 判定 B: 卡牌 -> 分类槽 (Goal Slot)
    /// </summary>
    private bool IsValidMove(CardView card, CategorySlotView target)
    {
        if (card == null || target == null) return false;
        // 🔥 关键：提示系统拿到单张牌时，需要模拟“如果玩家拖动它，会带起谁？”
        List<CardView> simulatedStack;
     
        if (!target.isOccupied)
        {
            return card.type == CardType.Category;
        }

        if (card.currentColumn != null)
        {
            // 如果牌在列里，让列帮我们计算拖拽列表 (处理头牌带子牌的逻辑)
            simulatedStack = card.currentColumn.GetDragList(card);
        }
        else
        {
            // 如果是手牌，就它自己
            simulatedStack = new List<CardView> { card };
        }

        // 调用真理标准
        return IsValidMove(simulatedStack, target);
    }

    /// <summary>
    /// 【给拖拽用】真理标准：判定一摞牌能否放入槽位
    /// </summary>
    private bool IsValidMove(List<CardView> stack, CategorySlotView target)
    {
        if (stack == null || stack.Count == 0 || target == null) return false;

        CardView categoryCard = stack.Find(c => c.type == CardType.Category);
        // 情况 1: 槽位是空的
        if (!target.isOccupied)
        {
            // 必须有头牌带队
            if (categoryCard == null) return false;
            // 所有人必须同类
            foreach (var c in stack) if (c.categoryId != categoryCard.categoryId) return false;
            return true;
        }
        // 情况 2: 槽位已有分类
        else
        {
            // 不能再放头牌
            if (categoryCard != null) return false;
            // 必须同类且槽位未满
            foreach (var c in stack) if (c.categoryId != target.categoryId) return false;
            if (target.IsFull()) return false;
            return true;
        }
    }
    /// <summary>
    /// 🔥 诊断版：判定列移动，并输出具体的错误原因
    /// </summary>
    public bool IsValidMoveWithReason(List<CardView> stack, ColumnView targetCol, out MoveErrorType error)
    {
        error = MoveErrorType.None;
        if (stack == null || stack.Count == 0 || targetCol == null) return false;

        // --- 情况 A: 目标列是空的 ---
        if (targetCol.cards.Count == 0) return true;

        // --- 情况 B: 目标列有牌 ---
        CardView targetTop = targetCol.GetTopCard();

        // 1. 检查目标是否为分类牌 (分类牌上不能压任何牌)
        if (targetTop.type == CardType.Category) 
        {
            error = MoveErrorType.TargetIsCategory;
            return false;
        }

        // 2. 检查 ID 是否匹配 (异类不能接龙)
        if (stack[0].categoryId != targetTop.categoryId) 
        {
            error = MoveErrorType.CategoryMismatch;
            return false;
        }
        // 注意：根据之前修改的逻辑，允许分类牌压在同类普通牌上，所以不需要检查 SourceIsCategory
        return true;
    }
    /// <summary>
    /// 🔥 诊断版：判定槽位移动，并输出错误原因
    /// </summary>
    public bool IsValidMoveWithReason(List<CardView> stack, CategorySlotView targetSlot, out MoveErrorType error)
    {
        error = MoveErrorType.None;
        if (stack == null || stack.Count == 0 || targetSlot == null) return false;

        CardView categoryCard = stack.Find(c => c.type == CardType.Category);

        if (!targetSlot.isOccupied)
        {
            // 空槽必须有头牌
            if (categoryCard == null) 
            {
                error = MoveErrorType.CategoryMismatch; // 或者定义一个 "NeedHeader"
                return false;
            }
            // 必须同类
            foreach (var c in stack) if (c.categoryId != categoryCard.categoryId) {
                error = MoveErrorType.CategoryMismatch;
                return false;
            }
            return true;
        }
        else
        {
            // 已有分类，不能再放头牌
            if (categoryCard != null) 
            {
                error = MoveErrorType.TargetIsCategory; // 这里的语境是槽位已有Category，不能再叠
                return false;
            }
            // ID匹配
            foreach (var c in stack) if (c.categoryId != targetSlot.categoryId) {
                error = MoveErrorType.CategoryMismatch;
                return false;
            }
            if (targetSlot.IsFull()) {
                error = MoveErrorType.SlotIsFull;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 动画 A: 手指点击 (用于牌堆/刷新按钮)
    /// </summary>
    private IEnumerator LoopFingerClickAnimation(Transform target)
    {
        if (fingerPrefab != null)
        {
            _currentGhostObj = Instantiate(fingerPrefab, transform.parent);
        }
        else
        {
            _currentGhostObj = new GameObject("TempFinger");
            _currentGhostObj.AddComponent<Image>().color = Color.red;
            _currentGhostObj.transform.SetParent(transform.parent, false);
        }
        if (!_currentGhostObj.TryGetComponent<Canvas>(out var canvas))
        {
            canvas = _currentGhostObj.AddComponent<Canvas>();
        }
        canvas.overrideSorting = true;
        canvas.sortingLayerName = UIPanelLayer.UpPopTwoPanel;
        
        WaitForSeconds wait = new WaitForSeconds(0.2f);
        while (true)
        {
            _currentGhostObj.transform.position = target.position;
            _currentGhostObj.transform.localScale = Vector3.one;
            float timer = 0f;
            while (timer < 0.5f) 
            {
                timer += Time.deltaTime;
                float scale = Mathf.PingPong(timer * 2, 0.2f) + 0.8f; // 0.8 ~ 1.0 缩放
                _currentGhostObj.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            yield return wait;
        }
    }
    /// <summary>
    /// 动画 B: 卡牌移动幻影
    /// </summary>
    private IEnumerator LoopCardMoveAnimation(CardView originalCard, Transform target)
    {
        if (originalCard == null || target == null) yield break;
        _currentGhostObj = Instantiate(originalCard.gameObject, originalCard.transform.parent);
        Destroy(_currentGhostObj.GetComponent<CardView>());
        if (!_currentGhostObj.TryGetComponent<Canvas>(out var canvas))
        {
            canvas = _currentGhostObj.AddComponent<Canvas>();
        }
        canvas.overrideSorting = true;
        canvas.sortingLayerName = UIPanelLayer.UpPopTwoPanel;
        
        CanvasGroup canvasGroup = _currentGhostObj.GetComponent<CanvasGroup>();
        if(canvasGroup == null) canvasGroup = _currentGhostObj.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.85f;
        // RectTransform ghostRect = _currentGhostObj.GetComponent<RectTransform>();
        WaitForSeconds wait = new WaitForSeconds(0.2f);
        while (true)
        {
            Vector3 startPos = originalCard.transform.position;
            Vector3 endPos = target.position;
            
            _currentGhostObj.transform.position = startPos;
            float moveTime = 0f;
            while (moveTime < 1.0f) 
            {
                moveTime += Time.deltaTime;
                if (_currentGhostObj == null) yield break;
                
                _currentGhostObj.transform.position = Vector3.Lerp(startPos, endPos, moveTime);
                yield return null;
            }
            yield return wait;
        }
    }

    private IEnumerator ButtonHintCoroutine(string text, float duration = 2.5f)
    {
        ShowHintText(text);
        yield return new WaitForSeconds(duration);
        msgText.transform.parent.gameObject.SetActive(false);
    }
    
    private void ShowHintText(string content)
    {
        msgText.text = content;
        msgText.transform.parent.gameObject.SetActive(true);
    }

    /// <summary>
    /// 🔥🔥🔥 核心方法：玩家进行了任何操作时调用
    /// (包括：开始拖拽卡牌、点击牌堆、点击刷新、点击撤回)
    /// </summary>
    public void NotifyPlayerAction()
    {
        if (_isHintActive)
        {
            StopCurrentHint();
            Debug.Log("玩家进行了操作， 提示中断");
        }
        
        InitUI();
    }

    private void StopCurrentHint()
    {
        if(_currentHintCoroutine != null) StopCoroutine(_currentHintCoroutine);
        if(_currentGhostObj != null) Destroy(_currentGhostObj);
        
        _isHintActive = false;
        _currentHintCoroutine = null;
        _currentGhostObj = null;
        if (hintButton) hintButton.interactable = true;
        msgText.transform.parent.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 🔥 核心修复：彻底清理所有状态、协程和临时物体
    /// </summary>
    public void CleanUp()
    {
        _isGameEnded = false; // 🔥 新增：重置结束状态
        // 1. 停止所有协程 (包括提示动画、自动消失文本、游戏流程等)
        StopAllCoroutines();
        // 🔥 新增：重玩时，强行清空手牌区残留
        // handArea?.ClearHand();
        // 👇 🔥 核心大扫除：绝对不能留到下一局去清理！在这里立刻销毁所有残影！
        if (tableauArea != null) tableauArea.ClearTableau();
        if (handArea != null) handArea.ClearHand();
        // 如果你的 goalArea 也有清理方法（比如清空槽位），也在这里调用：
        if (goalArea != null) goalArea.ClearSlots();
        
        // 2. 销毁提示系统的幻影
        if (_currentGhostObj != null) 
        {
            Destroy(_currentGhostObj);
            _currentGhostObj = null;
        }
        _currentHintCoroutine = null;
        _isHintActive = false;

        // 3. 销毁拖拽中意外残留的幻影/物体
        if (draggingStack != null && draggingStack.Count > 0)
        {
            // 如果正在拖拽时退出了，把牌还在的数据清理掉
            // 注意：这里通常不需要Destroy card，因为card属于column，
            // 稍后调用 tableauArea.Clear() 会统一销毁
            draggingStack.Clear();
        }
        IsDraggingProgress = false;
        if (ChainGuideSystem.Instance != null)
        {
            ChainGuideSystem.Instance.RestoreCanvasLayers();
        }
        // 4. 重置UI
        if (msgText && msgText.transform.parent) 
            msgText.transform.parent.gameObject.SetActive(false);
            
        if (hintButton) hintButton.interactable = true;
        
        // 5. 清理历史记录
        _moveHistory.Clear();
        
        // 6. 销毁拖拽层里可能残留的子物体
        if (dragLayer != null)
        {
            foreach (Transform child in dragLayer)
            {
                Destroy(child.gameObject);
            }
        }
        // 7. 清理墓地
        foreach (Transform ghost in graveyardRoot) Destroy(ghost.gameObject);
        Debug.Log("ChainPlayArea 清理完毕");
    }
    
    protected override void OnDisable()
    {
        CleanUp();
        Game.Ads?.HideBanner();
        base.OnDisable();
    }
}