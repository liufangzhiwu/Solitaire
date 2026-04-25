using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class HandAreaController : MonoBehaviour
{
    
    [Header("UI 组件")] 
    public Button stockButton;    // 牌堆按钮
    public GameObject stockPile;
    public Text stockCountText;   // 剩余牌量文本
    
    [Header("牌堆视觉")] 
    public Transform stockRoot;
    public int maxStackVisual = 2;
    public Vector2 stackOffset = new Vector2(-4f, -4f);
    public List<GameObject> visualStackCards = new List<GameObject>();
    
    private GameObject _stockBackPrefab;
    private ObjectPool _stockBackPool;
    public ObjectPool StockBackPool
    {
        get
        {
            if (_stockBackPool == null)
            {
                if (_stockBackPrefab == null)
                    _stockBackPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem","Stockback");
                
                // 注意：这里第二个参数我帮你填了 stockRoot，让它默认生成在牌堆节点下，更干净
                _stockBackPool = new ObjectPool(_stockBackPrefab, stockRoot, 3, PoolBehaviour.GameObject);
            }
            return _stockBackPool;
        }
    }
    
    [Header("废牌堆")] 
    public float fanSpacing = 74f;     // 扇形展开的间距 (正数，代码里会处理方向)
    public float pileSpacing = 5f;     // 底部堆叠的微小偏移 (模拟厚度)
    public int visibleFanCount = 3;    // 可以看到多少张展开的牌(不含顶牌)
    public int visiblePileCount = 2;   // 底部保留多少张堆叠显示
    public Transform wasteRoot;       // 废牌堆
    public List<CardView> wasteCards = new List<CardView>();
    public List<string> stockData = new List<string>();
    
    private Dictionary<string, string> wordToCategoryMap;
    private Dictionary<string, int> categoryTotalCounts;
    
    // 动画锁，防止连续点击导致动画错乱
    private bool isDealing = false;
    private CardView _dealingCard;
    private void Awake()
    {
        stockButton.onClick.AddListener(OnStockClicked);
        var init = StockBackPool;
    }

    public void InitHand(List<string> stockList, List<string> wasteList, Dictionary<string, string> map, Dictionary<string, int> categoryCounts)
    {
        ClearHand();
        
        stockData = new List<string>(stockList);
        List<string> wasteies = new List<string>(wasteList);
        wordToCategoryMap = map;
        categoryTotalCounts = categoryCounts;
        
        if (wasteies.Count > 0)
        {
            foreach (var cardId in wasteies)
            {
                string catId = map.GetValueOrDefault(cardId, cardId);
                CardView cardView = CardPoolManager.Instance.GetCardPrefab(wasteRoot);
                cardView.IsInHand = true;
                cardView.Initialization(cardId,catId, true, categoryTotalCounts.GetValueOrDefault(catId, 0));
                // cardView.transform.localPosition= Vector3.one;
                
                cardView.UpdateZoneVisuals(true, true);
                wasteCards.Add(cardView);
            }
            
            RefreshWasteVisual();
        }
        UpdateStockVisual();

    }

    // 点击发牌
    public void OnStockClicked()
    {
        if (isDealing) return;
        isDealing = true;
        
        AudioManager.Instance.PlaySoundEffect("FlipCard");
        stockButton.transform.DOScale(new Vector3(0.95f, 0.95f, 0.95f), 0.11f).OnComplete(() =>
        {
            if (stockData.Count > 0)
            {
                if (ChainPlayArea.Instance.currentSteps <= 0)
                {
                    isDealing = false; // 🔥 没步数了要记得解锁
                    return;
                }
                
                foreach (var card in wasteCards)
                {
                    if (card.TryGetComponent<CanvasGroup>(out var cg)) cg.blocksRaycasts = false;
                }
                string cardId = stockData[0];
                stockData.RemoveAt(0);
                
                UpdateStockVisual();
                // StartCoroutine(DealCardRoutine(cardId));
                ChainPlayArea.Instance.ConsumeStep();
                // 执行发牌动画
                DealCardWithTween(cardId);
            }
            else if (wasteCards.Count > 0)
            {
                RecycleWasteToStock();
            }
            else
            {
                isDealing = false;
            }
            ChainPlayArea.Instance.NotifyPlayerAction();
            stockButton.transform.DOScale(Vector3.one, 0.11f);
        });
        ChainPlayArea.Instance.ResetIdleTimer();
    }
    private void RecycleWasteToStock()
{
    if (wasteCards.Count == 0) return;

    // --- 🎥 配置调整 ---
    float gatherDuration = 0.2f;
    float flyDuration = 0.3f; // 缩短飞行，让节奏更紧凑
    float stagger = 0.03f;
    
    Vector2 gatherLocalPos = wasteCards[0].GetComponent<RectTransform>().anchoredPosition;
    Vector3 targetPos = stockRoot.position;

    // 1. 立即计算逻辑数据，防止动画期间 stockData 为空导致的 UI 消失
    List<string> tempIds = wasteCards.Select(c => c.cardId).ToList();
    // 洗牌逻辑移到这里
    for (int i = 0; i < tempIds.Count; i++) {
        string temp = tempIds[i];
        int randomIndex = Random.Range(i, tempIds.Count);
        tempIds[i] = tempIds[randomIndex];
        tempIds[randomIndex] = temp;
    }

    Sequence recycleSeq = DOTween.Sequence();
    recycleSeq.SetId(this);

    for (int i = 0; i < wasteCards.Count; i++)
    {
        CardView card = wasteCards[i];
        if (card.TryGetComponent<CanvasGroup>(out var cg)) cg.blocksRaycasts = false;

        // 赋予飞行层级
        Canvas myCanvas = card.GetComponent<Canvas>();
        if(myCanvas == null) myCanvas = card.gameObject.AddComponent<Canvas>();
        myCanvas.overrideSorting = true;
        myCanvas.sortingLayerName = "PopPanel";
        myCanvas.sortingOrder = 3000 + i;

        RectTransform rt = card.GetComponent<RectTransform>();

        // 阶段 A：收拢
        recycleSeq.Insert(0, rt.DOAnchorPos(gatherLocalPos, gatherDuration).SetEase(Ease.OutQuad));
        
        // 阶段 B：飞回
        float flyDelay = gatherDuration + (wasteCards.Count - 1 - i) * stagger;
        recycleSeq.Insert(flyDelay, card.transform.DOMove(targetPos, flyDuration).SetEase(Ease.InQuad));
        
        // 空中翻转与缩放
        recycleSeq.Insert(flyDelay, card.transform.DOScaleX(0, flyDuration * 0.5f).OnComplete(() => card.SetFaceUp(false)));
        recycleSeq.Insert(flyDelay + flyDuration * 0.5f, card.transform.DOScaleX(1f, flyDuration * 0.5f));

        // 核心修复：一旦到达位置，立即执行回收逻辑，不依赖总序列的 OnComplete
        recycleSeq.InsertCallback(flyDelay + flyDuration, () => {
            // card.gameObject.SetActive(false);
            myCanvas.overrideSorting = false;
            // 此时可以提前归还一张卡牌到池中，释放压力
        });
    }

    // 整个序列结束后的逻辑处理
    recycleSeq.OnComplete(() =>
    {
        // 彻底清理 wasteCards 引用
        foreach (var card in wasteCards)
        {
            if (card != null)
            {
                card.transform.localScale = Vector3.one;
                CardPoolManager.Instance.ReturnCardPrefab(card);
            }
        }
        
        wasteCards.Clear();
        stockData.Clear();
        stockData.AddRange(tempIds);
        
        // 刷新视觉：此时 stockData 已有值，数字和厚度会立即显示
        UpdateStockVisual();
        RefreshWasteVisual();
        
        isDealing = false;
    });
}
    private void RecycleWasteToStock4()
    {
        if (wasteCards.Count == 0) return;

        // --- 🎥 核心动画导演节奏配置 ---
        float gatherDuration = 0.2f;  
        float gatherStagger = 0.02f;  
        float pauseBeforeFly = 0.1f;  
        // 稍微加长一点点飞行时间，让“大动作”有时间完美展示
        float flyDuration = 0.35f;    
        float flyStagger = 0.05f;     

        Vector2 gatherLocalPos = wasteCards[0].GetComponent<RectTransform>().anchoredPosition;
        Vector3 targetPos = stockRoot.position;

        // 1. 立即计算逻辑数据，防止动画期间 stockData 为空导致的 UI 消失
        List<string> tempIds = wasteCards.Select(c => c.cardId).ToList();
        // 洗牌逻辑移到这里
        for (int i = 0; i < tempIds.Count; i++) {
            string temp = tempIds[i];
            int randomIndex = Random.Range(i, tempIds.Count);
            tempIds[i] = tempIds[randomIndex];
            tempIds[randomIndex] = temp;
        }
        
        Sequence recycleSeq = DOTween.Sequence();
        recycleSeq.SetId(this);
        recycleSeq.SetLink(gameObject, LinkBehaviour.KillOnDisable);
        
        stockPile.SetActive(true);
        stockCountText.text = ""; // 飞行时先清空数字，增强代入感
        
        float flyPhaseStartTime = (wasteCards.Count * gatherStagger) + gatherDuration + pauseBeforeFly;

        for (int i = 0; i < wasteCards.Count; i++)
        {
            CardView card = wasteCards[i];
            
            if (card.TryGetComponent<CanvasGroup>(out var cg)) cg.blocksRaycasts = false;
            
            // 👇 🔥 核心修复 1：赋予飞行特权！让它们绝对盖住牌堆按钮！
            if (!card.TryGetComponent<Canvas>(out var myCanvas))
                myCanvas = card.gameObject.AddComponent<Canvas>();
            if (!card.TryGetComponent<GraphicRaycaster>(out var raycaster))
                card.gameObject.AddComponent<GraphicRaycaster>();
            
            myCanvas.overrideSorting = true;
            myCanvas.sortingLayerName = "PopPanel"; 
            myCanvas.sortingOrder = 3000 + i; // 保持内部叠放顺序

            RectTransform rt = card.GetComponent<RectTransform>();
            rt.DOKill();
            card.transform.DOKill();

            // 【阶段 A：折扇式合牌】
            float gatherDelay = (wasteCards.Count - 1 - i) * gatherStagger;
            recycleSeq.Insert(gatherDelay, rt.DOAnchorPos(gatherLocalPos, gatherDuration).SetEase(Ease.OutQuart));
            
            // 【阶段 B：流水线式飞回】
            int reverseIndex = wasteCards.Count - 1 - i;
            float cardFlyStartTime = flyPhaseStartTime + (reverseIndex * flyStagger);

            // 1. 飞向牌堆
            recycleSeq.Insert(cardFlyStartTime, card.transform.DOMove(targetPos, flyDuration).SetEase(Ease.InOutQuad));
            
            // 2. 空中同步翻面 (前半段合上，后半段展开)
            recycleSeq.Insert(cardFlyStartTime, card.transform.DOScaleX(0f, flyDuration * 0.5f).SetEase(Ease.InSine).OnComplete(() =>
            {
                card.SetFaceUp(false); 
            }));
            recycleSeq.Insert(cardFlyStartTime + flyDuration * 0.5f, card.transform.DOScaleX(1f, flyDuration * 0.5f).SetEase(Ease.OutSine));

            // 3. 👇 🔥 核心修复 2：动作大一点！模拟抛物线
            // 飞行的前半段，卡牌 Y轴 猛然放大到 1.3 倍，就像被高高抛起！
            recycleSeq.Insert(cardFlyStartTime, card.transform.DOScaleY(1.3f, flyDuration * 0.5f).SetEase(Ease.OutQuad));
            // 飞行的后半段，卡牌重重落下，变回 1.0 原大小
            recycleSeq.Insert(cardFlyStartTime + flyDuration * 0.5f, card.transform.DOScaleY(1f, flyDuration * 0.5f).SetEase(Ease.InQuad));

            // 4. 到达终点瞬间隐藏，并卸载特权，防止污染对象池
            recycleSeq.InsertCallback(cardFlyStartTime + flyDuration, () => {
                 // card.gameObject.SetActive(false);
                 if (card.TryGetComponent<Canvas>(out var canvas)) canvas.overrideSorting = false;
            });
        }

        // 【阶段 C：清算与重新洗牌】
        recycleSeq.OnComplete(() =>
        {
            List<string> tempIdss = new List<string>();
            foreach (var card in wasteCards)
            {
                tempIdss.Add(card.cardId);
                card.transform.localScale = Vector3.one; // 恢复正常大小
                // card.gameObject.SetActive(true);         // 恢复可见性
                CardPoolManager.Instance.ReturnCardPrefab(card);
            }
            
            // 洗牌算法
            for (int i = 0; i < tempIdss.Count; i++)
            {
                string temp = tempIdss[i];
                int randomIndex = Random.Range(i, tempIdss.Count);
                tempIdss[i] = tempIdss[randomIndex];
                tempIdss[randomIndex] = temp;
            }
            
            stockData.AddRange(tempIdss);
            wasteCards = new List<CardView>(); 
            
            UpdateStockVisual();
            RefreshWasteVisual();
            
            isDealing = false; 
        });
    }
    private void RecycleWasteToStock2()
    {
        List<string> tempIds = new List<string>();
        foreach (var card in wasteCards)
        {
            tempIds.Add(card.cardId);
            CardPoolManager.Instance.ReturnCardPrefab(card);
        }
        // 洗牌算法 (Fisher-Yates)
        for (int i = 0; i < tempIds.Count; i++)
        {
            string temp = tempIds[i];
            int randomIndex = Random.Range(i, tempIds.Count);
            tempIds[i] = tempIds[randomIndex];
            tempIds[randomIndex] = temp;
        }
        stockData.AddRange(tempIds);
        wasteCards.Clear();
        UpdateStockVisual();
        RefreshWasteVisual();
        // LayoutRebuilder.ForceRebuildLayoutImmediate(wasteRoot.GetComponent<RectTransform>());
    }
    private void RecycleWasteToStock3()
    {
        if (wasteCards.Count == 0) return;

        // 动画时间设置
        float gatherDuration = 0.25f; // 收拢到一起的时间
        float flyDuration = 0.35f;    // 飞回牌堆的时间

        // 👇 🔥 核心修复：收拢目标点改为【最顶上/最外侧】那张牌的本地 UI 坐标！绝对不会乱跑！
        Vector2 gatherLocalPos = wasteCards[0].GetComponent<RectTransform>().anchoredPosition;
        
        // 最终飞回的目标点（牌堆中心全局坐标）
        Vector3 targetPos = stockRoot.position;

        Sequence recycleSeq = DOTween.Sequence();
        
        // 遍历所有废牌，分配动画指令
        foreach (var card in wasteCards)
        {
            // 1. 切断射线，防止在天上飞的时候被玩家拖走
            if (card.TryGetComponent<CanvasGroup>(out var cg)) cg.blocksRaycasts = false;
            
            // 2. 获取 UI 变换组件并杀死旧动画
            RectTransform rt = card.GetComponent<RectTransform>();
            rt.DOKill();
            card.transform.DOKill();

            // 阶段 A：向顶牌收拢合上 (使用更精准的 UI 坐标动画)
            recycleSeq.Insert(0, rt.DOAnchorPos(gatherLocalPos, gatherDuration).SetEase(Ease.OutCubic));
            
            // 阶段 B：收拢完毕的瞬间，把牌翻过去（背面朝上）
            recycleSeq.InsertCallback(gatherDuration, () => card.SetFaceUp(false));

            // 阶段 C：集体飞回牌堆
            recycleSeq.Insert(gatherDuration, card.transform.DOMove(targetPos, flyDuration).SetEase(Ease.InQuad));
            recycleSeq.Insert(gatherDuration, card.transform.DOScale(0.9f, flyDuration)); 
        }

        // 阶段 D：全部飞行结束后，进行数据清理和洗牌
        recycleSeq.OnComplete(() =>
        {
            List<string> tempIds = new List<string>();
            foreach (var card in wasteCards)
            {
                tempIds.Add(card.cardId);
                card.transform.localScale = Vector3.one; // 恢复正常大小
                CardPoolManager.Instance.ReturnCardPrefab(card);
            }
            
            // 洗牌算法 (Fisher-Yates)
            for (int i = 0; i < tempIds.Count; i++)
            {
                string temp = tempIds[i];
                int randomIndex = Random.Range(i, tempIds.Count);
                tempIds[i] = tempIds[randomIndex];
                tempIds[randomIndex] = temp;
            }
            
            stockData.AddRange(tempIds);
            wasteCards = new List<CardView>(); // 切断引用
            
            UpdateStockVisual();
            RefreshWasteVisual();
            
            isDealing = false; // 动画彻底结束，解锁操作！
        });
    }
    // 使用 DOTween 替代协程，更稳定
    private void DealCardWithTween(string cardId)
    {
        string catId = wordToCategoryMap.GetValueOrDefault(cardId, cardId);
        
        _dealingCard = CardPoolManager.Instance.GetCardPrefab(stockRoot.parent);
        _dealingCard.IsInHand = true;
        _dealingCard.Initialization(cardId, catId, false, categoryTotalCounts.GetValueOrDefault(catId, 0));
        _dealingCard.transform.position = stockRoot.position;
        _dealingCard.transform.localScale = Vector3.one;

        CardView animatingCard = _dealingCard;
        
        Vector3 targetLocalPos = CalculateTargetLocalPos(wasteCards.Count);
        Vector3 targetWorldPos = wasteRoot.TransformPoint(targetLocalPos);

        Sequence seq = DOTween.Sequence();
        seq.SetId(this);
        seq.SetLink(animatingCard.gameObject, LinkBehaviour.KillOnDisable);
        // 我们稍微收紧一点时间，让动作更凌厉
        float duration = 0.4f; 
        
        // 1. 直达目标：保持 OutQuad (平滑减速)
        seq.Insert(0, animatingCard.transform.DOMove(targetWorldPos, duration).SetEase(Ease.OutQuad));

        // ==========================================
        // 👇 🔥 核心改造：初始爆发力的微微放大效果
        // ==========================================
        
        // 【第 1 段：爆发（快速放大）】
        // 在前 0.12 秒（约 30% 的路程），将 Y轴（高度）微微放大 1.15 倍，模拟卡牌离开桌面时的“弹跳”或“被捏起”的瞬间
        seq.Insert(0, animatingCard.transform.DOScaleY(1.15f, duration * 0.3f).SetEase(Ease.OutQuad));
        
        // 【第 2 段：回归（缓慢变回原样）】
        // 从 0.12 秒开始，用剩下的时间，让卡牌慢慢变回 1.0 的标准高度，确保落地时完美归位
        seq.Insert(duration * 0.3f, animatingCard.transform.DOScaleY(1f, duration * 0.7f).SetEase(Ease.OutQuad));
        // ==========================================


        // 2. 空中同步翻面：时间完美等分，一半合上一半展开
        // 前半段：牌面压扁至0
        seq.Insert(0, animatingCard.transform.DOScaleX(0f, duration * 0.5f).SetEase(Ease.InSine).OnComplete(() =>
        {
            // 飞到中途时，瞬间换成正面贴图
            animatingCard.SetFaceUp(true); 
        }));
        
        // 后半段：牌面展开
        // (保持了之前那一点点 OutBack(1.2f) 的微回弹，保留“啪”地拍桌上的力量感)
        seq.Insert(duration * 0.5f, animatingCard.transform.DOScaleX(1f, duration * 0.5f).SetEase(Ease.OutBack, 1.2f));
        seq.OnComplete(() =>
        {
            AddCardToWaste(animatingCard);
            animatingCard.transform.localScale = Vector3.one;
            // cardScript.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            animatingCard.UpdateZoneVisuals(true, true);
            isDealing = false;
            EventDispatcher.Instance.TriggerCardDragResult(null, animatingCard,true);

            // 清理全局指针
            if (_dealingCard == animatingCard) _dealingCard = null;
        });
    }
    // 辅助：计算新卡牌在废牌区的理论本地坐标
    private Vector3 CalculateTargetLocalPos(int targetIndex)
    {
        // 这里的逻辑要和 RefreshWasteVisual 保持一致
        // 因为还没加入列表，所以 totalCount 暂时设为当前 count + 1
        int simulatedTotalCount = wasteCards.Count + 1;
        
        float pivotOffset = 130f; // 默认值，或者获取 Prefab 的宽度
        // 简单起见用固定值，或者假设第一张牌的宽度
        if (wasteCards.Count > 0)
        {
            RectTransform rt = wasteCards[0].GetComponent<RectTransform>();
            pivotOffset = rt.rect.width * rt.pivot.x;
        }

        float stackBaseX = -pivotOffset;
        int stackTopIndex = Mathf.Max(0, simulatedTotalCount - 1 - visibleFanCount);

        float targetX = 0f;
        // 如果它是最后一张(targetIndex)，那它肯定是在最右边的 Fan 区
        // 除非牌很少，都在 Pile 区
        if (targetIndex <= stackTopIndex)
        {
            int distFromStack = stackTopIndex - targetIndex;
            targetX = stackBaseX + distFromStack * pileSpacing;
        }
        else
        {
            int distFromStack = targetIndex - stackTopIndex;
            targetX = stackBaseX - distFromStack * fanSpacing;
        }
        
        return new Vector3(targetX, 0, 0);
    }
    
    // 新增：拖拽时暂时将牌从废牌区脱离，触发下方的牌展开
    public void RemoveCardFromWaste(CardView card)
    {
        if (wasteCards.Contains(card))
        {
            wasteCards.Remove(card);
            // if (card.TryGetComponent<Canvas>(out var myCanvas))
            // {
            //     myCanvas.overrideSorting = false;
            // }
            RefreshWasteVisual(); // 🔥 这行会让底下的牌立刻知道自己成了老大，文字弹回中间！
        }
    }
    
    // 刷新牌堆的厚度显示
    public void UpdateStockVisual()
    {
        foreach (var obj in visualStackCards)
        {
            StockBackPool.ReturnObjectToPool(obj.GetComponent<PoolObject>());
        }
        // foreach (Transform child in stockRoot)
        // {
        //     Destroy(child.gameObject);
        // }
        visualStackCards.Clear();

        int count = stockData.Count;
        int visualThicknessCount = count > 0 ? Mathf.Min(count - 1, maxStackVisual) : 0;
        
        for (int i = 0; i < visualThicknessCount; i++)
        {
            GameObject back = StockBackPool.GetObject(stockRoot);
            back.transform.localPosition = stackOffset * (i + 1);
            back.transform.SetAsFirstSibling();
            visualStackCards.Add(back);
        }
        
        stockCountText.text = count.ToString();
        stockPile.SetActive(count > 0);
        
        stockButton.interactable = stockData.Count > 0 || wasteCards.Count > 0;
        ChainStageController.Instance.SyncHandState(stockData, wasteCards);
    }

    // 添加进废牌堆
    public void AddCardToWaste(CardView newCard)
    {
        if (wasteCards.Contains(newCard))
        {
            wasteCards.Remove(newCard);
        }
        newCard.transform.SetParent(wasteRoot, true);
        // newCard.transform.SetAsLastSibling();
        wasteCards.Add(newCard);
        RefreshWasteVisual();
        ChainStageController.Instance.SyncHandState(stockData, wasteCards);
    }
    
    public void OnCardUsed(CardView card)
    {
        if (wasteCards.Contains(card))
        {
            wasteCards.Remove(card);
            RefreshWasteVisual();
            ChainStageController.Instance.SyncHandState(stockData, wasteCards);
        }
    }
    
    // 刷新废牌堆布局 & 射线阻挡逻辑
    private void RefreshWasteVisual()
    {
        int totalCount =  wasteCards.Count;
        if (totalCount == 0) return;
        
        float pivotOffset = 130f;
        if (totalCount > 0 && wasteCards[0] != null)
        {
            RectTransform rt = wasteCards[0].GetComponent<RectTransform>();
            pivotOffset = rt.rect.width * rt.pivot.x;
        }
        float stackBaseX = -pivotOffset;
        int stackTopIndex = Mathf.Max(0, totalCount - 1 - visibleFanCount);
        // 这里的逻辑是：只显示最近的 (1 + 4 + 3) = 8 张
        int maxVisibleTotal = 1 + visibleFanCount + visiblePileCount;
        int minShowIndex = Mathf.Max(0, totalCount - maxVisibleTotal);
        
        for (int i = 0; i < totalCount; i++)
        {
            CardView card = wasteCards[i];
            RectTransform rt = card.GetComponent<RectTransform>();
            
            bool shouldShow = (i >= minShowIndex);
            if(card.gameObject.activeSelf != shouldShow) card.gameObject.SetActive(shouldShow);
            card.transform.SetSiblingIndex(i);
            if(!shouldShow) continue;
            
            // 计算坐标 X
            float targetX = 0f;
            if (i <= stackTopIndex)
            {
                int distFromStack = stackTopIndex - i;
                targetX = stackBaseX + (distFromStack * pileSpacing);
            }
            else
            {
                int distFromStack = i - stackTopIndex;
                targetX = stackBaseX - distFromStack * fanSpacing;
            }
         
            rt.DOKill();
            // 👇 🔥 核心终极补丁：不仅要有 Canvas，还必须强行修改它的渲染层 (Sorting Layer)！
            if (!card.TryGetComponent<Canvas>(out var myCanvas))
            {
                myCanvas = card.gameObject.AddComponent<Canvas>();
            }
            if (!card.TryGetComponent<GraphicRaycaster>(out var raycaster))
            {
                card.gameObject.AddComponent<GraphicRaycaster>();
            }
            
            // 🔥 这三行是灵魂：开启覆盖、设为拖拽层的顶级面板层、设置递增Order保证不打架
            myCanvas.overrideSorting = true;
            // 获取和 dragLayer 一模一样的无敌层级 (通常是 "PopPanel")
            myCanvas.sortingLayerName = "PopPanel"; 
            myCanvas.sortingOrder = 2000 + i;
            
            Tween moveTween = rt.DOAnchorPos(new Vector2(targetX, 0), 0.2f).SetEase(Ease.OutQuad);
            moveTween.SetId(this);
            moveTween.OnComplete(() => {
                if (card != null && card.TryGetComponent<Canvas>(out var canvas))
                {
                    if (canvas.sortingLayerName == "PopPanel")
                    {
                        canvas.overrideSorting = false;
                    }
                }
            });
            card.transform.localScale = Vector3.one;
            // card.transform.SetAsLastSibling();
            
            bool isTop = (i == totalCount - 1);
            card.IsInHand = true; 
            card.SetCompressedState(!isTop, true);
            if (!card.TryGetComponent<CanvasGroup>(out CanvasGroup cg))
            {
                cg = card.gameObject.AddComponent<CanvasGroup>();
            }
            cg.blocksRaycasts = isTop;
        }
        // UpdateDraggableState();
    }
    
    // 更新可拖拽状态
    private void UpdateDraggableState()
    {
        for (int i = 0; i < wasteCards.Count; i++)
        {
            if (!wasteCards[i].gameObject.activeSelf) continue;
            CardView card = wasteCards[i];
            bool isTop = (i == wasteCards.Count - 1);
            if (!card.TryGetComponent<CanvasGroup>(out CanvasGroup cg))
            {
                // 如果没获取到，out 出来的 cg 是 null，这里直接添加
                cg = card.gameObject.AddComponent<CanvasGroup>();
            }
            cg.blocksRaycasts = isTop;
            card.SetCompressedState(!isTop, false);
        }
    }

    /// <summary>
    /// 【提示用】获取当前手牌区最上面的一张牌
    /// </summary>
    public CardView GetCurrentCard()
    {
        return wasteCards.LastOrDefault();
    }

    /// <summary>
    /// 【撤回用】将卡牌放回手牌区
    /// </summary>
    public void ReturnCard(CardView card)
    {
        if (card == null) return;
        
        card.transform.SetParent(wasteRoot, false);
        card.transform.SetAsLastSibling();
        card.isFaceUp = true;
        card.currentColumn = null;
        card.IsInHand = true; // 🔥 确保它知道自己回到了手牌区
        card.SetCompressedState(false, true);
        
        card.transform.localScale = Vector3.one;
        card.transform.localPosition = Vector3.zero;
        card.UpdateZoneVisuals(true, false);
        wasteCards.Add(card);
        RefreshWasteVisual();
        ChainStageController.Instance.SyncHandState(stockData, wasteCards);
    }

    public void ClearHand()
    {
        DOTween.Kill(this);
        isDealing = false;
        if (_dealingCard != null)
        {
            _dealingCard.transform.DOKill();
            _dealingCard.transform.localScale = Vector3.one; // 强行把拍扁的牌救回来
            CardPoolManager.Instance.ReturnCardPrefab(_dealingCard);
            _dealingCard = null;
        }
        
        List<CardView> tempWasteCards = new List<CardView>(wasteCards);
        wasteCards = new List<CardView>();
        foreach (var card in tempWasteCards)
        {
            if (card != null)
            {
                card.transform.DOKill();
                if (card.TryGetComponent<RectTransform>(out var rt)) rt.DOKill();
                
                card.transform.localScale = Vector3.one; // 保证进对象池时是正常的
                CardPoolManager.Instance.ReturnCardPrefab(card);
            }
        }
        
        List<GameObject> tempVisuals = new List<GameObject>(visualStackCards);
        visualStackCards.Clear();
        foreach (var obj in tempVisuals)
        {
            if (obj != null)
            {
                if(obj.TryGetComponent<PoolObject>(out var poolObject))
                    _stockBackPool.ReturnObjectToPool(poolObject);
                else
                    Destroy(obj);
            }
        }

        stockData = new List<string>();
        
        // 清理时重置文本
        stockPile.SetActive(false);
    }

    private void OnDisable()
    {
        ClearHand();
    }
}
