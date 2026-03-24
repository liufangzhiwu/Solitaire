using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class LearningGuide : UIWindow
{
    [SerializeField] private GameObject background; // 背景 
    [SerializeField] private RectTransform arrow;   // 箭头
    [SerializeField] private RectTransform dianShouTable; // 点击的手
    [SerializeField] private Text tipText; // 提示的文本
    
    [HideInInspector]
    [Tooltip("当前使用的教学工具对象")]
    public GameObject activeToolObject;
    // 🔥 1.用来记录所有被提升了层级的物体，方便事后清理
    private List<GameObject> _elevatedObjects = new List<GameObject>();
    private GameObject _currentGhostObj;     // 当前显示的幻影物体
    private Coroutine _currentGuideCoroutine; // 当前的动画协程
    private int _currentProgress;
    private Coroutine _delayCoroutine;
    
    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        Canvas canvas = dianShouTable.GetComponent<Canvas>();
        canvas.sortingLayerName = UIPanelLayer.TopPanel;
    }

    private void Update()
    {
        // 👇 🔥 精准修复 1：不再用点击屏幕打断，而是检测玩家是否正在拖拽
        // 这样玩家长时间不动也不会卡死，一拖拽幻影就自动消失
        if (ChainPlayArea.Instance != null && ChainPlayArea.Instance.IsDraggingProgress)
        {
            if (_currentGuideCoroutine != null || (dianShouTable != null && dianShouTable.gameObject.activeSelf))
            {
                StopGuide(); // 只隐藏动画和手指，保留高亮层级
            }
        }
    }
    
    // 👇 🔥 精准修复 3：统一 Layer 为 TopPanel，用 sortOrder 控制目标和卡牌的上下关系！
    private void SetCanvasLayer(GameObject active, string layer = UIPanelLayer.TopPanel, int sortOrder = 100)
    {
        if (active == null) return;
        
        if (!active.TryGetComponent<Canvas>(out var canvas))
        {
            canvas = active.AddComponent<Canvas>();
        }
        canvas.overrideSorting = true;
        canvas.sortingLayerName = layer; 
        canvas.sortingOrder = sortOrder; // 使用传入的 order
        
        if (!active.TryGetComponent<UnityEngine.UI.GraphicRaycaster>(out var raycaster))
        {
            active.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        if (!_elevatedObjects.Contains(active))
        {
            _elevatedObjects.Add(active);
        }
    }

    // 👇 🔥 精准修复 2：不仅高亮自己，还要高亮下方所有跟着跑的牌
    private void ElevateCardAndStack(CardView card)
    {
        if (card == null) return;
        
        int baseOrder = 101;
        // 核心牌给 101 层，保证在目标(100)上方
        if (card.currentColumn != null)
        {
            // 1. 模拟 ChainPlayArea 的查找逻辑，找到这摞牌真正的“拖拽根节点”
            int clickedIndex = card.currentColumn.cards.IndexOf(card);
            int rootIndex = clickedIndex;
            
            // 往下(索引减小方向)寻找同类且翻开的牌
            for (int i = clickedIndex - 1; i >= 0; i--)
            {
                CardView prevCard = card.currentColumn.cards[i];
                if (!prevCard.isFaceUp) break;
                if (prevCard.categoryId == card.categoryId)
                {
                    if (prevCard.type == CardType.Category) break; // 头牌不可带走
                    rootIndex = i;
                }
                else break;
            }
            
            // 2. 从真正的根节点一直到列尾，将整串牌全部高亮！
            for (int i = rootIndex; i < card.currentColumn.cards.Count; i++)
            {
                CardView stackCard = card.currentColumn.cards[i];
                if (stackCard.isFaceUp)
                {
                    // 🔥 核心细节：给每张牌递增的 Order (101, 102, 103...)
                    // 保证它们拖拽时不仅都在底座之上，且自己内部的上下叠放也是绝对正确的！
                    SetCanvasLayer(stackCard.gameObject, UIPanelLayer.TopPanel, baseOrder++);
                }
            }
        }
        else
        {
            // 不在列里（如废牌区），单独高亮
            SetCanvasLayer(card.gameObject, UIPanelLayer.TopPanel, baseOrder);
        }
    }
    
    private void ShowFinalSuccess()
    {
        StopGuide(); 
        tipText.transform.DOShakeScale(0.5f, 0.2f);
    }

    public bool ShowStockDrawGuide()
    {
        var playArea = ChainPlayArea.Instance;
        if (playArea == null) return false;

        var handArea = playArea.handArea;
        if (handArea == null || handArea.stockButton == null) return false;
        
        bool hasStockCards = handArea.stockData?.Count > 0;
        bool hasWasteCards = handArea.wasteCards?.Count > 0;
        if (!hasStockCards && !hasWasteCards) return false; 
        
        StopGuide();
        _currentGuideCoroutine = StartCoroutine(LoopClickAnimation(handArea.stockButton.transform));
        return true;
    }
    
    #region 查找目标部分 (保持你原本代码完全不变)
    private bool FindEmptyColumnMoveAction(out CardView foundCard, out Transform foundTarget)
    {
        foundCard = null; foundTarget = null;
        var playArea = ChainPlayArea.Instance;
        if (playArea == null) return false;

        var columns = playArea.tableauArea.columns;
        ColumnView emptyCol = null;
        foreach (var col in columns) { if (col.cards.Count == 0) { emptyCol = col; break; } }
        if (emptyCol == null) return false;

        foreach (var sourceCol in columns)
        {
            if (sourceCol == emptyCol || sourceCol.cards.Count <= 1) continue;
            CardView topCard = sourceCol.GetTopCard();
            if (topCard != null && topCard.isFaceUp)
            {
                foundCard = topCard; foundTarget = emptyCol.transform; return true;
            }
        }
        return false;
    }

    private bool FindWasteSmartMoveAction(out CardView foundCard, out Transform foundTarget)
    {
        foundCard = null; foundTarget = null;
        var playArea = ChainPlayArea.Instance;
        if (playArea == null || playArea.handArea.wasteCards.Count == 0) return false;

        CardView topWasteCard = playArea.handArea.wasteCards[playArea.handArea.wasteCards.Count - 1];

        foreach (var slot in playArea.goalArea.allSlots)
        {
            if (!slot.isOccupied)
            {
                if (topWasteCard.type == CardType.Category) { foundCard = topWasteCard; foundTarget = slot.transform; return true; }
            }
            else
            {
                if (!slot.IsFull() && topWasteCard.type != CardType.Category && topWasteCard.categoryId == slot.categoryId)
                {
                    foundCard = topWasteCard; foundTarget = slot.transform; return true;
                }
            }
        }
        return false;
    }

    private bool FindColumnStackingAction(out CardView foundCard, out Transform foundTarget)
    {
        foundCard = null; foundTarget = null;
        var playArea = ChainPlayArea.Instance;
        if (playArea == null) return false;

        var columns = playArea.tableauArea.columns;
        foreach (var sourceCol in columns)
        {
            CardView sourceCard = sourceCol.GetTopCard();
            if (sourceCard == null || !sourceCard.isFaceUp) continue;

            foreach (var targetCol in columns)
            {
                if (sourceCol == targetCol) continue;
                CardView targetCard = targetCol.GetTopCard();
                if (targetCard == null || !targetCard.isFaceUp || targetCard.type == CardType.Category) continue; 

                if (sourceCard.categoryId == targetCard.categoryId)
                {
                    foundCard = sourceCard; foundTarget = targetCard.transform; return true;
                }
            }
        }
        return false;
    }

    private bool FindCollectableAction(out CardView foundCard, out Transform foundTarget)
    {
        foundCard = null; foundTarget = null;
        var playArea = ChainPlayArea.Instance;
        if (playArea == null) return false;

        foreach (var slot in playArea.goalArea.allSlots)
        {
            if (!slot.isOccupied || slot.IsFull()) continue; 
            string targetCategoryId = slot.categoryId;

            foreach (var column in playArea.tableauArea.columns)
            {
                CardView topCard = column.GetTopCard();
                if (topCard == null || !topCard.isFaceUp) continue;

                if (topCard.categoryId == targetCategoryId && topCard.type != CardType.Category)
                {
                    foundCard = topCard; foundTarget = slot.transform; return true; 
                }
            }
        }
        return false;
    }
    
    private bool FindPlayableCategoryMove(out CardView foundCard, out Transform foundTarget)
    {
        foundCard = null; foundTarget = null;
        var playArea = ChainPlayArea.Instance;
        if (playArea == null) return false;

        foreach (var column in playArea.tableauArea.columns)
        {
            foreach (var card in column.cards)
            {
                if (!card.isFaceUp) continue;
                if (card.type == CardType.Category)
                {
                    CategorySlotView targetSlot = FindTargetSlotFor(card, playArea.goalArea.allSlots);
                    if (targetSlot != null) { foundCard = card; foundTarget = targetSlot.transform; return true; }
                }
            }
        }
        return false;
    }

    private CategorySlotView FindTargetSlotFor(CardView card, List<CategorySlotView> allSlots)
    {
        foreach (var slot in allSlots)
        {
            if (!slot.isOccupied) return slot;
            else if (slot.categoryId == card.categoryId) continue; 
        }
        return null;
    }
    #endregion

    #region 动画部分
    private void PlayGuideAnim(CardView card, Transform target)
    {
        _currentGuideCoroutine = StartCoroutine(LoopCardMoveAnimation(card, target));
    }
    
    private IEnumerator LoopClickAnimation(Transform target)
    {
        if (!dianShouTable.TryGetComponent<Canvas>(out var canvas)) canvas = dianShouTable.gameObject.AddComponent<Canvas>();
        
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "UpPopTwoPanel"; 
        canvas.sortingOrder = 1001; 

        dianShouTable.gameObject.SetActive(true);
        WaitForSeconds wait = new WaitForSeconds(0.2f);

        while (true)
        {
            dianShouTable.transform.position = target.position;
            float timer = 0f;
            float duration = 0.6f;
            
            while (timer < duration)
            {
                timer += Time.deltaTime;
                if (dianShouTable == null) yield break;
                float t = Mathf.PingPong(timer * 2 / duration, 1f); 
                float scale = Mathf.Lerp(1.0f, 0.8f, t);
                dianShouTable.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            yield return wait;
        }
    }
    
    private IEnumerator LoopCardMoveAnimation(CardView originalCard, Transform target)
    {
        _currentGhostObj = Instantiate(originalCard.gameObject, transform); 
        Destroy(_currentGhostObj.GetComponent<CardView>());
        if (_currentGhostObj.GetComponent<Button>()) Destroy(_currentGhostObj.GetComponent<Button>());

        if (!_currentGhostObj.TryGetComponent<Canvas>(out var canvas)) canvas = _currentGhostObj.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "UpPopTwoPanel"; 
        canvas.sortingOrder = 1000;

        CanvasGroup canvasGroup = _currentGhostObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = _currentGhostObj.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false; 
        canvasGroup.alpha = 0f;

        WaitForSeconds wait = new WaitForSeconds(0.5f);

        while (true)
        {
            Vector3 startPos = originalCard.transform.position;
            Vector3 endPos = target.position; 

            _currentGhostObj.transform.position = startPos;
            
            float duration = 1.2f;
            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                if (_currentGhostObj == null) yield break;

                float progress = time / duration;
                float ease = progress * progress * (3f - 2f * progress); 
                _currentGhostObj.transform.position = Vector3.Lerp(startPos, endPos, ease);

                if (progress < 0.2f) canvasGroup.alpha = Mathf.Lerp(0, 0.8f, progress / 0.2f);
                else if (progress > 0.8f) canvasGroup.alpha = Mathf.Lerp(0.8f, 0, (progress - 0.8f) / 0.2f);
                else canvasGroup.alpha = 0.8f;

                yield return null;
            }
            yield return wait;
        }
    }

    public void MoveHandToTile(Transform transform)
    {
        if (transform == null) return;
        dianShouTable.GetComponent<Canvas>().sortingLayerName = UIPanelLayer.TipsPanel;
        RectTransform targetRect = transform.GetComponent<RectTransform>();
        Vector3[] targetCorners = new Vector3[4];
        targetRect.GetWorldCorners(targetCorners);
        Vector3 targetBottomRight = targetCorners[3];
        dianShouTable.transform.position = targetBottomRight;
    } 

    private void StopGuide()
    {
        if(_currentGuideCoroutine != null) StopCoroutine(_currentGuideCoroutine);
        _currentGuideCoroutine = null;
        
        if(_currentGhostObj != null) Destroy(_currentGhostObj);
        _currentGhostObj = null;
        
        if (dianShouTable != null)
        {
            dianShouTable.DOKill(); 
            dianShouTable.gameObject.SetActive(false); 
        }
    }
    #endregion
    // ==========================================
    // 🌟 1. 对外触发入口：每次被叫到，先清场并开始倒计时
    // ==========================================
    public void ShowAutoGuideByPriority()
    {
        if (_delayCoroutine != null) StopCoroutine(_delayCoroutine);
        _delayCoroutine = StartCoroutine(DelayExecuteGuide());
    }
    // ==========================================
    // 🌟 2. 核心缓冲器：清理烂摊子，安静等待 0.5 秒
    // ==========================================
    private IEnumerator DelayExecuteGuide()
    {
        // 【第一步：瞬间清场！】
        // 立刻杀掉旧的动画，剥夺所有卡牌的 Canvas 特权，并隐藏黑屏背景
        StopGuide();
        RestoreCanvasLayers();

        if (tipText != null && tipText.transform.parent != null) 
            tipText.transform.parent.gameObject.SetActive(false);
        
        if (background != null) background.SetActive(true);
        // 【第二步：挂机等待 0.5 秒】
        // 这期间玩家不会被黑屏阻挡，翻牌动画也能从容飞完、落入废牌堆
        yield return new WaitForSeconds(0.5f);

        // 【第三步：重新拉起大幕，准备引导】
        if (tipText != null && tipText.transform.parent != null) 
            tipText.transform.parent.gameObject.SetActive(true);

        // 开始真正的扫描
        ExecuteAutoGuide();
    }
    // ==========================================
    // 🌟 3. 真正的扫描逻辑（把你原本的代码搬到了这里）
    // ==========================================
    private void ExecuteAutoGuide()
    {
        if (FindCollectableAction(out var c1, out var t1)) 
        { 
            activeToolObject = c1.gameObject;
            ElevateCardAndStack(c1);
            SetCanvasLayer(t1.gameObject, UIPanelLayer.TopPanel, 100);
            PlayGuideAnim(c1, t1); 
            tipText.text = MultilingualManager.Instance.GetString("GuidingTips02");
            return; 
        }

        if (FindPlayableCategoryMove(out var c2, out var t2))
        {
            activeToolObject = c2.gameObject;
            ElevateCardAndStack(c2);
            SetCanvasLayer(t2.gameObject, UIPanelLayer.TopPanel, 100);
            PlayGuideAnim(c2, t2);
            tipText.text = MultilingualManager.Instance.GetString("GuidingTips01");
            return; 
        }
        
        if (FindWasteSmartMoveAction(out var c3, out var t3)) 
        { 
            activeToolObject = c3.gameObject;
            ElevateCardAndStack(c3);
            SetCanvasLayer(t3.gameObject, UIPanelLayer.TopPanel, 100);
            PlayGuideAnim(c3, t3); 
            tipText.text = MultilingualManager.Instance.GetString("GuidingTips05");
            return; 
        }

        if (FindColumnStackingAction(out var c4, out var t4)) 
        { 
            activeToolObject = c4.gameObject;
            ElevateCardAndStack(c4);
            SetCanvasLayer(t4.gameObject, UIPanelLayer.TopPanel, 100);
            PlayGuideAnim(c4, t4); 
            tipText.text = MultilingualManager.Instance.GetString("GuidingTips03");
            return; 
        }

        if (ShowStockDrawGuide())
        {
            activeToolObject = ChainPlayArea.Instance.handArea.stockButton.gameObject;
            SetCanvasLayer(activeToolObject, UIPanelLayer.TopPanel, 101);
            tipText.text = MultilingualManager.Instance.GetString("GuidingTips04");
            return;
        }
        
        if (FindEmptyColumnMoveAction(out var c5, out var t5)) 
        { 
            activeToolObject = c5.gameObject;
            ElevateCardAndStack(c5);
            SetCanvasLayer(t5.gameObject, UIPanelLayer.TopPanel, 100);
            PlayGuideAnim(c5, t5); 
            tipText.text = "利用空白列来转移卡牌，寻找新的机会。";
            return; 
        }

        if (ChainPlayArea.Instance.IsGameOver()) 
        {
            ShowFinalSuccess();
        }
        else
        {
            tipText.transform.parent.gameObject.SetActive(false);
            tipText.text = "当前似乎无解，试试重置关卡？";
        }
    }
    public void ShowAutoGuideByPriority2()
    {
        StopGuide(); 
        if (background != null) background.SetActive(true);

        if (FindCollectableAction(out var c1, out var t1)) 
        { 
            activeToolObject = c1.gameObject;
            ElevateCardAndStack(c1);
            SetCanvasLayer(t1.gameObject, UIPanelLayer.TopPanel, 100);
            PlayGuideAnim(c1, t1); 
            // tipText.text = "发现可收集的卡牌！将它移动到对应分类槽中。";
            tipText.text = MultilingualManager.Instance.GetString("GuidingTips02");
            return; 
        }

        if (FindPlayableCategoryMove(out var c2, out var t2))
        {
            activeToolObject = c2.gameObject;
            ElevateCardAndStack(c2);
            SetCanvasLayer(t2.gameObject, UIPanelLayer.TopPanel, 100);
            PlayGuideAnim(c2, t2);
            // tipText.text = "将分类卡放入槽位，开启新的收集目标！";
            tipText.text = MultilingualManager.Instance.GetString("GuidingTips01");
            return; 
        }
        
        if (FindWasteSmartMoveAction(out var c3, out var t3)) 
        { 
            activeToolObject = c3.gameObject;
            ElevateCardAndStack(c3);
            SetCanvasLayer(t3.gameObject, UIPanelLayer.TopPanel, 100);
            PlayGuideAnim(c3, t3); 
            // tipText.text = "好运气！废牌区的这张牌正好可以用，快拖进去！";
            tipText.text = MultilingualManager.Instance.GetString("GuidingTips05");
            return; 
        }

        if (FindColumnStackingAction(out var c4, out var t4)) 
        { 
            activeToolObject = c4.gameObject;
            ElevateCardAndStack(c4);
            SetCanvasLayer(t4.gameObject, UIPanelLayer.TopPanel, 100);
            PlayGuideAnim(c4, t4); 
            // tipText.text = "同类卡牌可以堆叠在一起，试试整理一下牌桌！";
            tipText.text = MultilingualManager.Instance.GetString("GuidingTips03");
            return; 
        }

        if (ShowStockDrawGuide())
        {
            activeToolObject = ChainPlayArea.Instance.handArea.stockButton.gameObject;
            SetCanvasLayer(activeToolObject, UIPanelLayer.TopPanel, 101);
            // tipText.text = "当前牌桌没有可移动的牌了，点牌堆翻一张新牌吧！";
            tipText.text = MultilingualManager.Instance.GetString("GuidingTips04");
            return;
        }
        
        if (FindEmptyColumnMoveAction(out var c5, out var t5)) 
        { 
            activeToolObject = c5.gameObject;
            ElevateCardAndStack(c5);
            SetCanvasLayer(t5.gameObject, UIPanelLayer.TopPanel, 100);
            PlayGuideAnim(c5, t5); 
            tipText.text = "利用空白列来转移卡牌，寻找新的机会。";
            return; 
        }

        if (ChainPlayArea.Instance.IsGameOver()) 
        {
            ShowFinalSuccess();
        }
        else
        {
            tipText.transform.parent.gameObject.SetActive(false);
            tipText.text = "当前似乎无解，试试重置关卡？";
        }
    }

    protected override void OnDisable()
    {
        if (_delayCoroutine != null) StopCoroutine(_delayCoroutine);
        StopGuide();
        base.OnDisable();
    }
    
    // 🔥 保留你原本的清理逻辑，不做任何“超前优化”，保证失败弹回机制完全不受干扰
    public void RestoreCanvasLayers()
    {
        foreach (var obj in _elevatedObjects)
        {
            if (obj != null)
            {
                if (obj.TryGetComponent<UnityEngine.UI.GraphicRaycaster>(out var raycaster))
                {
                    Destroy(raycaster);
                }
                if (obj.TryGetComponent<Canvas>(out var canvas))
                {
                    Destroy(canvas);
                }
            }
        }
        _elevatedObjects.Clear();
    }
    
    public bool IsStrictGuideActive()
    {
        return background != null && background.activeInHierarchy;
    }

    public bool IsTargetElevated(GameObject obj)
    {
        if (obj == null) return false;
        return _elevatedObjects.Contains(obj);
    }

    public void ResumeGuideAnim()
    {
        ShowAutoGuideByPriority();
    }
}