using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ColumnView : MonoBehaviour
{
    [Header("配置")]
    public float cardSpacing = 40f;
    
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
                cg.blocksRaycasts = !hasCardAbove;
                
                if (hasCardAbove)
                {
                    if (currentCard.TryGetComponent<GraphicRaycaster>(out var raycaster)) Destroy(raycaster);
                    if (currentCard.TryGetComponent<Canvas>(out var canvas)) Destroy(canvas);
                }
            }
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
    
    public void Clear()
    {
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            CardView card = cards[i];
            if (card != null)
                CardPoolManager.Instance.ReturnCardPrefab(card);
        }
        
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        
        cards.Clear();
    }
}
