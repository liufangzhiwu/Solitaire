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
    
    [Header("牌堆视觉")] 
    public Transform stockRoot;
    public int maxStackVisual = 2;
    public Vector2 stackOffset = new Vector2(-4f, 5f);
    public List<GameObject> visualStackCards = new List<GameObject>();
    
    private GameObject _stockBackPrefab;
    private ObjectPool _stockBackPool;
    
    [Header("废牌堆")] 
    public float fanSpacing = 60f;     // 扇形展开的间距 (正数，代码里会处理方向)
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
    private void Awake()
    {
        stockButton.onClick.AddListener(OnStockClicked);
        _stockBackPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem","Stockback");
        _stockBackPool = new ObjectPool(_stockBackPrefab, null,3,PoolBehaviour.GameObject);
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
                cardView.currentZone = CardView.CardZone.WastePile;
                cardView.Initialization(cardId,catId, true, categoryTotalCounts.GetValueOrDefault(catId, 0));
                cardView.transform.localPosition= Vector3.one;
                
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
        
        AudioManager.Instance.PlaySoundEffect("FlipCard");
        stockButton.transform.DOScale(new Vector3(0.85f, 0.85f, 0.85f), 0.11f).OnComplete(() =>
        {
            if (stockData.Count > 0)
            {
                if (ChainPlayArea.Instance.currentSteps <= 0) return;
                isDealing = true; // 上锁
                
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
            ChainPlayArea.Instance.NotifyPlayerAction();
            stockButton.transform.DOScale(Vector3.one, 0.11f);
        });
        ChainPlayArea.Instance.ResetIdleTimer();
    }

    private void RecycleWasteToStock()
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
    
    // 使用 DOTween 替代协程，更稳定
    private void DealCardWithTween(string cardId)
    {
        string catId = wordToCategoryMap.GetValueOrDefault(cardId, cardId);
        
        CardView cardScript = CardPoolManager.Instance.GetCardPrefab(stockRoot.parent);
        cardScript.IsInHand = true;
        cardScript.currentZone = CardView.CardZone.WastePile;
        cardScript.Initialization(cardId, catId, false, categoryTotalCounts.GetValueOrDefault(catId, 0));
        cardScript.transform.position = stockRoot.position;

        Vector3 targetLocalPos = CalculateTargetLocalPos(wasteCards.Count);
        Vector3 targetWorldPos = wasteRoot.TransformPoint(targetLocalPos);

        Sequence seq = DOTween.Sequence();
        seq.SetId(this);
        seq.SetLink(cardScript.gameObject, LinkBehaviour.KillOnDisable);
        float duration = 0.55f;
        
        seq.Insert(0,cardScript.transform.DOMove(targetWorldPos, duration).SetEase(Ease.OutQuad));
        seq.Insert(0,cardScript.transform.DOScaleX(0, duration / 2).OnComplete(() =>
        {
           cardScript.SetFaceUp(true); 
        }));
        seq.Insert(duration/2,cardScript.transform.DOScaleX(1,duration / 2));
        seq.OnComplete(() =>
        {
            AddCardToWaste(cardScript);
            // cardScript.transform.localScale = Vector3.one;
            // cardScript.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            cardScript.UpdateZoneVisuals(true, true);
            isDealing = false;
            EventDispatcher.Instance.TriggerCardDragResult(null, cardScript,true);
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
            targetX = stackBaseX + (distFromStack * pileSpacing);
        }
        else
        {
            int distFromStack = targetIndex - stackTopIndex;
            targetX = stackBaseX - distFromStack * fanSpacing;
        }
        
        return new Vector3(targetX, 0, 0);
    }
    /// <summary>
    /// 已弃用
    /// </summary>
    private IEnumerator DealCardRoutine(string cardId)
    {
        string catId = wordToCategoryMap.GetValueOrDefault(cardId, cardId);
        CardView cardScript = CardPoolManager.Instance.GetCardPrefab(stockRoot.parent);
        cardScript.IsInHand = true;
        cardScript.Initialization(cardId, catId,  false, categoryTotalCounts.GetValueOrDefault(catId, 0));
        // RectTransform rect = tempCard.GetComponent<RectTransform>();
        // if(rect != null) rect.pivot = new Vector2(0.5f, 0.5f);
        cardScript.transform.position = stockRoot.position;
        GameObject placeholder = new GameObject("Placeholder", typeof(RectTransform));
        placeholder.transform.SetParent(wasteRoot, false);
        LayoutRebuilder.ForceRebuildLayoutImmediate(wasteRoot.GetComponent<RectTransform>());
        Vector3 targetPos = placeholder.transform.position;
        Destroy(placeholder);

        float duration = 0.3f;
        float time = 0;
        Vector3 startPos = cardScript.transform.position;
        Vector3 originalScale = cardScript.transform.localScale;
        while (time <　duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            cardScript.transform.position = Vector3.Lerp(startPos, targetPos, t);
            // 飞到一半翻开
            if (t < 0.5f)
            {
                float scaleT = t / 0.5f; // 0~1
                cardScript.transform.localScale = new Vector3(Mathf.Lerp(originalScale.x, 0, scaleT), originalScale.y, originalScale.z);
            }
            else
            {
                if (!cardScript.isFaceUp)
                {
                    cardScript.SetFaceUp(true); // 翻开
                }
                float scaleT = (t - 0.5f) / 0.5f; // 0~1
                cardScript.transform.localScale = new Vector3(Mathf.Lerp(0, originalScale.x, scaleT), originalScale.y, originalScale.z);
            }
            // if (t >= 0.5f && !cardScript.isFaceUp)
            // {
            //     cardScript.SetFaceUp(true);
            // }
            yield return null;
        }
        cardScript.transform.localScale = originalScale;
        cardScript.IsInHand = true;
        AddCardToWaste(cardScript);
    }
    // 新增：拖拽时暂时将牌从废牌区脱离，触发下方的牌展开
    public void RemoveCardFromWaste(CardView card)
    {
        if (wasteCards.Contains(card))
        {
            wasteCards.Remove(card);
            RefreshWasteVisual(); // 🔥 这行会让底下的牌立刻知道自己成了老大，文字弹回中间！
        }
    }
    
    // 刷新牌堆的厚度显示
    public void UpdateStockVisual()
    {
        foreach (var obj in visualStackCards)
        {
            _stockBackPool.ReturnObjectToPool(obj.GetComponent<PoolObject>());
        }
        foreach (Transform child in stockRoot)
        {
            Destroy(child.gameObject);
        }
        visualStackCards.Clear();

        int count = stockData.Count;
        int visualCount = Mathf.Min(count, maxStackVisual);
        
        for (int i = 0; i < visualCount; i++)
        {
            GameObject back = _stockBackPool.GetObject(stockRoot);
            back.transform.localPosition = stackOffset * i;
            visualStackCards.Add(back);
        }

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
        newCard.transform.SetAsLastSibling();
        // RectTransform rect = newCard.GetComponent<RectTransform>();
        // rect.pivot = new Vector2(0.5f, 1f);
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
            if(!shouldShow) continue;
            
            // B. 计算坐标 X
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
            rt.DOAnchorPos(new Vector2(targetX,0), 0.2f).SetEase(Ease.OutQuad);
            // rt.anchoredPosition = new Vector2(targetX, 0);
            card.transform.SetAsLastSibling();
            
            bool isTop = (i == totalCount - 1);
            card.IsInHand = true; 
            card.SetCompressedState(!isTop, false);
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
        
        // card.transform.localScale = Vector3.one;
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
        List<CardView> tempWasteCards = new List<CardView>(wasteCards);
        wasteCards.Clear();
        foreach (var card in tempWasteCards)
        {
            if (card != null)
                CardPoolManager.Instance.ReturnCardPrefab(card);
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
        stockData.Clear();
    }

    private void OnDisable()
    {
        ClearHand();
    }
}
