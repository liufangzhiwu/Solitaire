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
                _instance.InitPoolIfNeeded();
            }
            return _instance;
        }
    }
    private GameObject _cardPrefab;
    private ObjectPool _pool;
    private bool _isInitialized = false; // 严格的状态锁
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            InitPoolIfNeeded();
        }else if(_instance != this)
            Destroy(gameObject);
    }

    private void InitPoolIfNeeded()
    {
        if (_isInitialized) return;
        
        _cardPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "Cardinfo");
        Transform poolContainer = ObjectPool.CreatePoolContainer(transform, "CardPool");
        _pool = new ObjectPool(_cardPrefab, poolContainer, 30, PoolBehaviour.GameObject);
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
        InitPoolIfNeeded();
        // cardPrefab.transform.SetParent(transform,false);
        cardPrefab.transform.DOKill();
        
        cardPrefab.transform.localScale = Vector3.one;
        cardPrefab.gameObject.SetActive(false);
        
        if (cardPrefab.TryGetComponent<PoolObject>(out var poolObj))
        {
            _pool.ReturnObjectToPool(poolObj);
        }
        else
        {
            Destroy(cardPrefab.gameObject);
        }
    }
    
    private void OnDestroy()
    {
        // 当场景销毁或管理器被销毁时，清空静态指针，防止下个场景调用时空指针异常
        if (_instance == this)
        {
            _instance = null;
            _isInitialized = false;
        }
    }
}
