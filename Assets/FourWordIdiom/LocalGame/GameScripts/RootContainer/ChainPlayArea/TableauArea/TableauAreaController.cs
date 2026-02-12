using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class TableauAreaController : MonoBehaviour
{
    [Header("配置")] 
    public GameObject columnPrefab;
    public GameObject cardPrefab;
    public Transform tableauContainer;
    
    public List<ColumnView> columns = new List<ColumnView>();

    private GameObject _colPrefab;
    public ObjectPool ColumnPool;
    
    public ObjectPool CardPool;
    private void Awake()
    {
        if (cardPrefab == null)
            cardPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "Cardinfo");
        if (_colPrefab == null)
            _colPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem","TableColumns");

        ColumnPool = new ObjectPool(_colPrefab.gameObject, tableauContainer, 3, PoolBehaviour.GameObject);
    }

    public void InitTableau(List<ColumnData> columnDatas, Dictionary<string, string> wordToCategoryMap , Dictionary<string, int> categoryTotalCounts)
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
                
                GameObject cardObj = Instantiate(cardPrefab, colView.transform);
                CardView cardScript = cardObj.GetComponent<CardView>();
                
                string catId = wordToCategoryMap.GetValueOrDefault(cardId, cardId);
                cardScript.Initialization(cardId, catId, isFaceUp, categoryTotalCounts.GetValueOrDefault(catId, 0));
                colView.AddCard(cardScript);
            }
        }
    }

    public void ClearTableau()
    {
        foreach (var col in columns)
        {
            col.Clear();
        }
        columns.Clear();
        ColumnPool.ReturnAllObjectsToPool();
    }
}
