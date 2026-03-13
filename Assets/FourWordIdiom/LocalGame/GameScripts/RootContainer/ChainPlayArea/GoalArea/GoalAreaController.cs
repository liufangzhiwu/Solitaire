using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class GoalAreaController : MonoBehaviour
{
    [Header("配置")] 
    public GameObject categorySlotPrefab;
    public Transform slotContainer;

    public List<CategorySlotView> allSlots = new List<CategorySlotView>();

    private ObjectPool _slotPool;

    private void Awake()
    {
        if (categorySlotPrefab == null)
            categorySlotPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "CateSlotView");
        
        _slotPool = new ObjectPool(categorySlotPrefab, slotContainer, 3, PoolBehaviour.GameObject);
    }

    public void InitGoalSlots(int defaultCount, List<CategoryData> savedSlots, Dictionary<string, int> categoryTotalCounts, Action<string> onCategoryCompleted)
    {
        ClearSlots();
        
        for (int i = 0; i < defaultCount; i++)
        {
            CategorySlotView slotView = _slotPool.GetObject<CategorySlotView>();
            
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
        }
        allSlots.Clear();
        _slotPool.ReturnAllObjectsToPool();
    }
    
}
