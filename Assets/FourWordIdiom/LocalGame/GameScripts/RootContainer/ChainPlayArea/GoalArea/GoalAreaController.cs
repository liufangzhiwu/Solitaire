using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Newtonsoft.Json;
using UnityEngine;

public class GoalAreaController : MonoBehaviour
{
    [Header("配置")] public GameObject categorySlotPrefab;
    public Transform slotContainer;

    public List<CategorySlotView> allSlots = new List<CategorySlotView>();

    private ObjectPool _slotPool;

    public ObjectPool SlotPool
    {
        get
        {
            if (_slotPool == null)
            {
                if (categorySlotPrefab == null)
                    categorySlotPrefab =
                        AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "CateSlotView");

                _slotPool = new ObjectPool(categorySlotPrefab, slotContainer, 3, PoolBehaviour.GameObject);
            }

            return _slotPool;
        }
    }

    private void Awake()
    {
        var init = SlotPool;
    }

    // ==========================================
    // 1. 只生成空的分类槽（建底座）
    // ==========================================
    public void InitGoalSlotsEmpty(int defaultCount, Action<string> onCategoryCompleted)
    {
        ClearSlots();

        for (int i = 0; i < defaultCount; i++)
        {
            CategorySlotView slotView = SlotPool.GetObject<CategorySlotView>();
            slotView.InitEmpty(); // 全部初始化为空白状态
            slotView.OnCategoryCompleted = onCategoryCompleted;
            allSlots.Add(slotView);
        }
    }

    // ==========================================
    // 2. 新增：根据存档数据，从牌堆飞入分类槽的动画
    // ==========================================
    public IEnumerator DealGoalCardsAnim(List<CategoryData> savedSlots, Dictionary<string, int> categoryTotalCounts,
        Transform deckTransform)
    {
        if (savedSlots == null || savedSlots.Count == 0) yield break;

        float flightDuration = 0.25f; // 飞行时间
        float staggerTime = 0.1f; // 每张牌飞出的间隔
        float totalDelay = 0f;
        bool hasFlewCard = false;

        for (int i = 0; i < allSlots.Count; i++)
        {
            if (i >= savedSlots.Count) continue;

            CategoryData data = savedSlots[i];
            if (string.IsNullOrEmpty(data.categoryId)) continue;

            int count = data.wordsData != null ? data.wordsData.Count : 0;
            // if (count == 0) continue; // 存档里这个槽是空的，跳过

            hasFlewCard = true;
            CategorySlotView slotView = allSlots[i];
            string catId = data.categoryId;

            // 提取原来要展示的那张牌的 ID
            string headerId = catId;
            if (data.wordsData != null && data.wordsData.Count > 0)
            {
                headerId = data.wordsData[0].wordId;
            }

            // 👇 🔥 核心招式：召唤一张替身卡牌用来播放飞行！
            CardView flyingCard = CardPoolManager.Instance.GetCardPrefab(slotView.transform);
            int total = categoryTotalCounts.GetValueOrDefault(catId, 5);
            flyingCard.Initialization(headerId, catId, true, total);

            // 初始位置设在右上角牌堆
            flyingCard.transform.position = deckTransform.position;

            Sequence seq = DOTween.Sequence();
            seq.SetDelay(totalDelay);

            // 飞向对应的分类槽
            seq.Append(flyingCard.transform.DOMove(slotView.transform.position, flightDuration).SetEase(Ease.OutCubic));
            seq.Join(flyingCard.transform.DOScale(Vector3.one * 0.9f, flightDuration)); // 稍微缩放适配槽位

            seq.OnComplete(() =>
            {
                // 飞行结束的瞬间，调用原来的逻辑恢复真实数据并显示！
                slotView.RestoreState(catId, count, total, headerId);
                slotView.PlayHighlightEffect(); // 播放一下高光特效，打击感拉满

                // 替身卡牌完成使命，回归对象池
                flyingCard.transform.localScale = Vector3.one;
                CardPoolManager.Instance.ReturnCardPrefab(flyingCard);
            });

            totalDelay += staggerTime;
        }

        // 如果真的有牌飞出，就等它们全部飞完再进行下一步
        if (hasFlewCard)
        {
            yield return new WaitForSeconds(totalDelay + flightDuration);
        }
    }

    public void InitGoalSlots(int defaultCount, List<CategoryData> savedSlots,
        Dictionary<string, int> categoryTotalCounts, Action<string> onCategoryCompleted)
    {
        ClearSlots();

        for (int i = 0; i < defaultCount; i++)
        {
            CategorySlotView slotView = SlotPool.GetObject<CategorySlotView>();

            bool hasSaveData = (savedSlots != null && i < savedSlots.Count);
            if (hasSaveData && !string.IsNullOrEmpty(savedSlots[i].categoryId))
            {
                CategoryData data = savedSlots[i];
                string catId = data.categoryId;
                int count = data.wordsData != null ? data.wordsData.Count : 0;
                string headerId = catId;
                if (data.wordsData != null && data.wordsData.Count > 0)
                {
                    headerId = data.wordsData[0].wordId;
                }

                slotView.RestoreState(catId, count, categoryTotalCounts.GetValueOrDefault(catId, 5), headerId);
            }
            else
            {
                slotView.InitEmpty();
            }

            slotView.OnCategoryCompleted = onCategoryCompleted;
            allSlots.Add(slotView);
        }
    }

    public void ClearSlots()
    {
        StopAllCoroutines();
        foreach (var slot in allSlots)
        {
            if (slot.TryGetComponent<Canvas>(out var canvas))
            {
                canvas.sortingOrder = 0;
                canvas.sortingLayerName = "Default";
            }

            slot.ClearSlot();
        }

        allSlots.Clear();
        SlotPool?.ReturnAllObjectsToPool();
    }
}