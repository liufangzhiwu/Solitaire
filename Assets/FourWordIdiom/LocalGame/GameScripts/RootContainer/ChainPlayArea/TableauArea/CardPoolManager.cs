using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 卡片对象池
/// </summary>
public class CardPoolManager : MonoBehaviour
{
    public static CardPoolManager Instance;
 
    private GameObject _cardPrefab;

    private ObjectPool _pool;

    private void Awake()
    {
        Instance = this;
        
        _cardPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "Cardinfo");
        _pool = new ObjectPool(_cardPrefab, transform, 30, PoolBehaviour.GameObject);
    }

    public CardView GetCardPrefab(Transform parent = null)
    {
        return _pool.GetObject<CardView>(parent);
    }

    public void ReturnCardPrefab(CardView cardPrefab)
    {
        cardPrefab.transform.SetParent(transform,false);
        cardPrefab.transform.DOKill();
        _pool.ReturnObjectToPool(cardPrefab.GetComponent<PoolObject>());
    }
}
