using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class TableauAreaController : MonoBehaviour
{
    [Header("配置")] public Transform tableauContainer;

    public List<ColumnView> columns = new List<ColumnView>();

    private GameObject _colPrefab;
    private ObjectPool _colPool;

    public ObjectPool ColumnPool
    {
        get
        {
            if (_colPool == null)
            {
                if (_colPrefab == null)
                    _colPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "TableColumns");
                
                _colPool = new ObjectPool(_colPrefab.gameObject, tableauContainer, 3, PoolBehaviour.GameObject);
            }
            return _colPool;
        }
    }


    private void Awake()
    {
        var init = ColumnPool;
    }

    public void InitTableau(List<ColumnData> columnDatas, Dictionary<string, string> wordToCategoryMap,
        Dictionary<string, int> categoryTotalCounts)
    {
        ClearTableau();

        for (int i = 0; i < columnDatas.Count; i++)
        {
            // GameObject colObj = Instantiate(columnPrefab, tableauContainer);
            ColumnView colView = ColumnPool.GetObject<ColumnView>();
            colView.columnIndex = i;
            columns.Add(colView);

            List<string> cards = columnDatas[i].cards;
            for (int j = cards.Count - 1; j >= 0; j--)
            {
                string cardId = cards[j];
                bool isFaceUp = j == 0;
                CardView cardScript = CardPoolManager.Instance.GetCardPrefab(colView.transform);

                string catId = wordToCategoryMap.GetValueOrDefault(cardId, cardId);
                cardScript.Initialization(cardId, catId, isFaceUp, categoryTotalCounts.GetValueOrDefault(catId, 0));
                colView.AddCard(cardScript);
            }
        }
    }

    // ==========================================
    // 专门拆分出来的方法：只生成空的列槽（底座）
    // ==========================================
    public void InitTableauSlots(int columnCount)
    {
        ClearTableau(); // 清理上一局的残留

        for (int colIndex = 0; colIndex < columnCount; colIndex++)
        {
            ColumnView colView = ColumnPool.GetObject<ColumnView>();
            colView.columnIndex = colIndex;
            columns.Add(colView);

            // 如果挂了 Layout 布局，可以保留开启，让它自动排版这些空槽
            // 🔥 关键：如果你的列身上挂了 VerticalLayoutGroup，发牌期间必须关掉它，防止和飞牌动画打架！
            // if (colView.TryGetComponent<VerticalLayoutGroup>(out var layout)) layout.enabled = false;
            // if (colView.TryGetComponent<ContentSizeFitter>(out var fitter)) fitter.enabled = false;
        }

        // 🔥 极其关键：强制 Unity 瞬间计算好这些空列的横向排列位置！
        // 这样接下来播放进场动画时，它们才不会全挤在屏幕正中间！
        Canvas.ForceUpdateCanvases();
    }

    public IEnumerator DealTableauCardsAnim(List<ColumnData> columnDatas, Dictionary<string, string> wordToCategoryMap,
        Dictionary<string, int> categoryTotalCounts, Transform deckTransform)
    {
        // ==========================================
        // 第一步：先生成所有的“列 (Column)”，但不放牌 已拆分
        // ==========================================

        // ==========================================
        // 第二步：开始飞牌动画
        // ==========================================
        float dealDelay = 0f;
        float staggerTime = 0.04f; // 稍微调快点，0.04秒一张，手感更好
        float flightDuration = 0.25f; // 飞行速度快一点更清脆

        // 🔥 优化点：获取最多有几张牌，改为“一层一层（一行一行）”发牌，完美还原纸牌接龙效果
        int maxRows = columnDatas.Max(c => c.cards.Count);

        for (int row = 0; row < maxRows; row++)
        {
            for (int colIndex = 0; colIndex < columnDatas.Count; colIndex++)
            {
                var colData = columnDatas[colIndex];

                // 如果这一列的牌没那么长，跳过
                if (row >= colData.cards.Count) continue;

                ColumnView colView = columns[colIndex];
                int dataIndex = colData.cards.Count - 1 - row;
                var cardId = colData.cards[dataIndex];
                string catId = wordToCategoryMap.GetValueOrDefault(cardId, cardId);

                CardView cardScript = CardPoolManager.Instance.GetCardPrefab(colView.transform);

                // 初始化数据，开局发牌默认【背面朝上】
                cardScript.Initialization(cardId, catId, false, categoryTotalCounts.GetValueOrDefault(catId, 0));
                cardScript.currentColumn = colView;
                colView.cards.Add(cardScript);

                // 计算这张牌在当前列中的最终【本地相对坐标】 (假设每张牌往下压 40 像素)
                Vector3 targetLocalPos = new Vector3(0, -row * 40f, 0);

                // 🔥 动画起点：瞬间把卡牌瞬移到右上角的发牌堆
                cardScript.transform.position = deckTransform.position;
                // 设为废牌堆的大小(比如 0.8)，看起来像是从小牌堆飞出来变大的
                // cardScript.transform.localScale = new Vector3(0.8f, 0.8f, 1f); 

                // 执行 DOTween 动画
                Sequence seq = DOTween.Sequence();
                seq.SetLink(cardScript.gameObject, LinkBehaviour.KillOnDisable);
                seq.SetDelay(dealDelay); // 设置错开的起飞时间
                // seq.AppendCallback(() => AudioManager.Instance.PlaySoundEffect("DealCard")); // 到时间才播音效

                // 🔥 使用 DOLocalMove 飞向本地坐标，绝对不会歪！Ease.OutCubic 让降落有一种减速的真实感
                seq.Append(cardScript.transform.DOLocalMove(targetLocalPos, flightDuration).SetEase(Ease.OutCubic));
                seq.Join(cardScript.transform.DOScale(Vector3.one, flightDuration));

                dealDelay += staggerTime; // 增加下一张牌的延迟
            }
        }

        // ==========================================
        // 第三步：等待所有牌飞完，收尾
        // ==========================================
        yield return new WaitForSeconds(dealDelay + flightDuration);
        if (columns == null || columns.Count == 0) yield break;
        foreach (var col in columns)
        {
            CardView topCard = col.GetTopCard();
            if (topCard != null)
            {
                // 顶层牌翻面 (可以替换为你自己的翻牌动画方法)
                topCard.SetFaceUp(true);
            }

            // 重新开启列的自动排版组件
            // if (col.TryGetComponent<VerticalLayoutGroup>(out var layout)) layout.enabled = true;
            // if (col.TryGetComponent<ContentSizeFitter>(out var fitter)) fitter.enabled = true;

            col.UpdateLayout();
        }
    }

    public void ClearTableau()
    {
        StopAllCoroutines();
        foreach (var col in columns)
        {
            col.Clear();
        }

        columns.Clear();
        ColumnPool?.ReturnAllObjectsToPool();
    }
}