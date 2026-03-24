using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ColumnView : MonoBehaviour
{
    [Header("配置")]
    public float cardSpacing = 74f;
    [Header("拖拽吸附视觉")]
    public GameObject hoverGreenFrame; // 鼠标悬停时的绿色预警框
    public GameObject errorRedFrame;   // 放置失败时的红色错误框
    
    [Header("数据")]
    public int columnIndex;
    public List<CardView> cards = new List<CardView>();

    public void AddCard(CardView cardView)
    {
        cardView.IsInHand = false;
        cardView.transform.SetParent(transform);
        cards.Add(cardView);
        cardView.currentColumn = this;
        cardView.transform.localScale = Vector3.one;
        UpdateLayout();
    }

    public List<CardView> RemoveCardsFrom(CardView startCard)
    {
        int index = cards.IndexOf(startCard);
        List<CardView> removed = new List<CardView>();
        if (index < 0) return removed;
        for (int i = index; i < cards.Count; i++)
        {
            removed.Add(cards[i]);
        }
        
        cards.RemoveRange(index, cards.Count - index);
        return removed;
    }

    // 更新布局
    public void UpdateLayout()
    {
        float currentY = 0f;
        
        for (int i = 0; i < cards.Count; i++)
        {
            CardView cardView = cards[i];
            cardView.transform.localPosition = new Vector3(0, -currentY, 0);
            cards[i].transform.SetSiblingIndex(i);
            currentY += cardSpacing;
        }
        // 🔥🔥🔥 核心：触发过渡动画 🔥🔥🔥
        RefreshCardContentStates();
    }

    public bool RevealLastCard()
    {
        if (cards.Count > 0)
        {
            CardView last = cards[^1];
            if (!last.isFaceUp)
            {
                last.SetFaceUp(true);
                UpdateLayout();
                return true;
            } 
        }

        return false;
    }

    public CardView GetTopCard()
    {
        if (cards.Count == 0) return null;
        return cards[^1];
    }
    
    private void RefreshCardContentStates()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            CardView currentCard = cards[i];

            // 只有正面朝上的牌才需要处理
            if (currentCard.isFaceUp)
            {
                // 判断条件：是否还有下一张牌？
                // i < cards.Count - 1 意味着它上面还有牌 -> 被压住 -> 压缩 (变小)
                // 否则 -> 没被压住 -> 展开 (变大)
                bool hasCardAbove = (i < cards.Count - 1);

                // 🔥 这里调用！
                // CardView 内部会判断状态是否改变，如果改变了就会 StartCoroutine(AnimateContent(...))
                currentCard.SetCompressedState(hasCardAbove);
                if (!currentCard.TryGetComponent<CanvasGroup>(out var cg))
                {
                    cg = currentCard.gameObject.AddComponent<CanvasGroup>();
                }
                cg.blocksRaycasts = true;
                
                // if (hasCardAbove)
                // {
                //     if (currentCard.TryGetComponent<GraphicRaycaster>(out var raycaster)) Destroy(raycaster);
                //     if (currentCard.TryGetComponent<Canvas>(out var canvas)) Destroy(canvas);
                // }
            }
        }
        RefreshComboBadge();
    }
    /// <summary>
    /// 🔥 核心逻辑：从下往上扫，计算连击并决定是否亮起角标
    /// </summary>
    private void RefreshComboBadge()
    {
        // 1. 先把当前列所有牌的角标全部强行关掉 (洗牌)
        foreach (var card in cards)
        {
            card.SetComboBadge(0, false);
        }

        if (cards.Count == 0) return;

        // 2. 拿到视觉上最底下的那张牌（也就是数组的最后一个元素）
        CardView topCard = cards[^1]; 
        
        // 如果最底下的牌都没翻开，那就不可能有连击
        if (!topCard.isFaceUp) return;

        string targetCategory = topCard.categoryId;
        // int comboCount = 0;
        // bool hasHeader = false;
        
        int wordCount = 0;
        CardView baseCard = null;
        // 3. 从最下面开始，一层层往上扒，看看连在一起的同类牌有几张
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            CardView c = cards[i];
            
            // 只要遇到没翻开的，或者不是同类的，连击瞬间断掉
            if (!c.isFaceUp || c.categoryId != targetCategory) break;
            
            if (c.type == CardType.Normal)
            {
                wordCount++;
            }
            // comboCount++;
            // 检查这串牌里有没有带头的“分类牌”
            // if (c.type == CardType.Category) hasHeader = true; 
            baseCard = c;
        }
        if (baseCard == null) return; // 安全校验
        // 4. 判断是否达成“大满贯全收集”！
        // 你的游戏里，一个完美的分类 = 1张头牌(Category) + N张词语牌(Normal)
        int requiredWords = 999;
        if (ChainPlayArea.Instance != null)
        {
            requiredWords = ChainPlayArea.Instance.GetCategoryTotalCount(targetCategory);
        }
        
        // 全收集条件：必须有头牌带队，且 总连击数 == 词语数量 + 1(头牌)
        bool isFull = wordCount >= requiredWords;

        // 5. 如果连击达标，命令最底下那张牌把角标亮起来！
        if (wordCount >= 5 || isFull)
        {
            baseCard.SetComboBadge(wordCount, isFull);
        }
    }
    /// <summary>
    /// 获取点击卡牌及其附属卡牌
    /// </summary>
    public List<CardView> GetDragList(CardView clickedCard)
    {
        List<CardView> result = new List<CardView>();
        int index = cards.IndexOf(clickedCard);
        if (index == -1) return result;

        // 核心修复逻辑：
        // 如果点的是【分类头牌】，往回找，把被压住的同类【普通牌】都带上
        if (clickedCard.type == CardType.Category)
        {
            result.Add(clickedCard); // 先加头牌
            
            // 倒序遍历（因为在List里 index越小通常是被压在下面的）
            for (int i = index - 1; i >= 0; i--)
            {
                CardView cardBelow = cards[i];
                // 只要是同类且是普通牌，就加入
                if (cardBelow.categoryId == clickedCard.categoryId && cardBelow.type == CardType.Normal)
                {
                    result.Insert(0, cardBelow); // 插到前面，保持 [子牌...头牌] 的顺序
                }
                else 
                {
                    break; // 遇到异类就停
                }
            }
        }
        else // 如果点的是普通牌，就只带上它上面的牌（常规接龙逻辑）
        {
            for (int i = index; i < cards.Count; i++)
            {
                result.Add(cards[i]);
            }
        }
        return result;
    }
    // 专门给拖拽失败回弹用的，不走 AddCard 的重排逻辑
    public void ReturnCardsFromDrag(List<CardView> returnedCards)
    {
        foreach (var card in returnedCards)
        {
            card.transform.SetParent(transform);
            // 放回原来的层级顺序（根据它在 cards 列表中的原本位置）
            int originalIndex = cards.IndexOf(card);
            if(originalIndex >= 0) card.transform.SetSiblingIndex(originalIndex);
        }
        
        // 只恢复位置，千万不要调用 UpdateLayout() 或者改变底下牌的压缩状态！
        float currentY = 0f;
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].transform.localPosition = new Vector3(0, -currentY, 0);
            currentY += cardSpacing;
        }
    }
    /// <summary>
    /// 当有卡牌拖拽到自己上方时调用
    /// </summary>
    public void SetHoverHighlight(bool isHovering)
    {
            hoverGreenFrame.SetActive(isHovering);
    }
    /// <summary>
    /// 放置失败时触发的红色警告 + 震动动画
    /// </summary>
    public void ShowErrorFeedback()
    {

            errorRedFrame.SetActive(true);
            
            // 杀掉旧动画，防止连击报错
            errorRedFrame.transform.DOKill();
            if (errorRedFrame.TryGetComponent<Image>(out var img))
            {
                img.DOKill();
                Color c = img.color;
                c.a = 1f; // 初始完全不透明
                img.color = c;
                
                // 红色框停留 0.15 秒后，慢慢消散
                img.DOFade(0f, 0.35f).SetDelay(0.15f).OnComplete(() => errorRedFrame.SetActive(false));
            }

            // 加一个微小的左右晃动（摇头）效果，极其生动！
            // transform.DOKill(false); // 不杀掉其他动画，只杀位移
            // transform.DOShakePosition(0.3f, new Vector3(8f, 0, 0), 20, 90, false, true);
            //
            // 顺便可以播个错误音效
            // AudioManager.Instance.PlaySoundEffect("ErrorDrop");
        
    }
    
    public void Clear()
    {
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            CardView card = cards[i];
            if (card != null)
            {
                card.SetHoverHighlight(false);
                CardPoolManager.Instance.ReturnCardPrefab(card);
            }
        }
        
        // foreach (Transform child in transform)
        // {
        //     Destroy(child.gameObject);
        // }
        SetHoverHighlight(false);
        if (errorRedFrame != null) errorRedFrame.SetActive(false);
        
        cards = new List<CardView>();
    }
}
