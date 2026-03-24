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
    public static CardPoolManager _instance;

    public static CardPoolManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CardPoolManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("CardPoolManager");
                    _instance = go.AddComponent<CardPoolManager>();
                }
            }
            _instance.InitPoolIfNeeded();
            return _instance;
        }
    }
    private GameObject _cardPrefab;
    private ObjectPool _pool;
    private bool _isInitialized;
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            InitPoolIfNeeded();
        }else if(_instance != this)
            Destroy(gameObject);
        _cardPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "Cardinfo");
        _pool = new ObjectPool(_cardPrefab, ObjectPool.CreatePoolContainer(transform, "CardPool"), 30, PoolBehaviour.GameObject);
    }

    private void InitPoolIfNeeded()
    {
        if (_isInitialized) return;
        
        _cardPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "Cardinfo");
        _pool = new ObjectPool(_cardPrefab, ObjectPool.CreatePoolContainer(transform, "CardPool"), 30, PoolBehaviour.GameObject);
        _isInitialized = true;
    }

    public CardView GetCardPrefab(Transform parent = null)
    {
        InitPoolIfNeeded();
        return _pool.GetObject<CardView>(parent);
    }

    public void ReturnCardPrefab(CardView cardPrefab)
    {
        if (cardPrefab == null) return;
        // cardPrefab.transform.SetParent(transform,false);
        cardPrefab.transform.DOKill();
        InitPoolIfNeeded();
        if (cardPrefab.TryGetComponent<PoolObject>(out var poolObj))
        {
            _pool.ReturnObjectToPool(poolObj);
        }
        else
        {
            Destroy(cardPrefab.gameObject);
        }
    }
}
