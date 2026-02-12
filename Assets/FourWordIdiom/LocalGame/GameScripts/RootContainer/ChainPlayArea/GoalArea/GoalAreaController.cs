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


    public void InitGoalSlots(int defaultCount, List<CategoryData> savedSlots, Dictionary<string, int> categoryTotalCounts, Action<string> onCategoryCompleted)
    {
        foreach (Transform child in slotContainer)
        {
            Destroy(child.gameObject);
        }
        allSlots.Clear();
        
        for (int i = 0; i < defaultCount; i++)
        {
            GameObject go = Instantiate(categorySlotPrefab, slotContainer);
            CategorySlotView slotView = go.GetComponent<CategorySlotView>();
            
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
    
}
