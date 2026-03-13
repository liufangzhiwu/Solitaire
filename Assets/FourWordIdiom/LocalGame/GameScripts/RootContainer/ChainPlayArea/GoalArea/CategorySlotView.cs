using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class CategorySlotView : MonoBehaviour
{
    private struct SlotHistoryData
    {
        public string cardId;
        public string contentText;
        public Sprite iconSprite;
    }
    
    public Action<string> OnCategoryCompleted;
    
    [Header("Category Slot")]
    [SerializeField] private GameObject categoryName;
    [SerializeField] private GameObject content;  // 内容
    [SerializeField] private GameObject checkMark; // 完成后的对勾图标
    [SerializeField] private Image contentImage;
    
    [Header("特效引用")]
    public Image glowOutlineImage;    // 发光外框的 Image
    public Material particleMaterial;     // 粒子的贴图 (星星或圆点)
    
    [Header("运行时数据")] 
    public bool isOccupied = false;
    public string categoryId;
    public string currentHeaderId;
    private int _totalNeeded;
    private int _currentCount;
    
    private Text _progressText;
    private Text _wordText;

    private readonly List<SlotHistoryData> _historyStack = new List<SlotHistoryData>();

    // private GameObject _starBurstPrefab;
    // private ObjectPool _starPool;
    private void Awake()
    {
        if (content != null)
        {
            if (_progressText == null) _progressText = content.transform.GetChild(0).GetComponent<Text>();
            // 假设 Text 是第2个子物体，Image 是第3个子物体，或者你自己安排
            if (_wordText == null) _wordText = content.transform.GetChild(1).GetComponent<Text>();
        }
        // if(_starBurstPrefab == null)
        //     _starBurstPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("useritems", "StarBurstEffect");
        // _starPool = new ObjectPool(_starBurstPrefab, transform, 3, PoolBehaviour.GameObject);
    }

    private void OnEnable()
    {
        categoryName.SetActive(false);
        content.SetActive(false);
    }
    
    // 初始化为一个空槽位
    public void InitEmpty()
    {
        isOccupied = false;
        categoryId = "";
        _currentCount = 0;
        _historyStack.Clear();
        
        if (contentImage)
        {
            contentImage.sprite = null;
            contentImage.gameObject.SetActive(false);
        }
        
        categoryName.SetActive(false);
        if (_progressText) _progressText.text = "";
        if (_wordText) _wordText.text = "";
        
        if (checkMark) checkMark.SetActive(false); // 确保对勾也隐藏
        content.SetActive(false);
    }

    public void RestoreState(string catId, int count, int total,  string headerId)
    {
        isOccupied = true;
        categoryId = catId;
        _currentCount = count;
        _totalNeeded = total;
        currentHeaderId = headerId;
        
        categoryName.GetComponentInChildren<Text>(true).text = categoryId;
        content.SetActive(true);
        bool usedIcon = ChainPlayArea.Instance.ShouldShowIcon(headerId);
        if (usedIcon)
        {
            UpdateContentVisual(headerId, ChainStageController.Instance.GetIconSprite(headerId));
        }
        else
        {
            UpdateContentVisual(headerId, null);
        }
        
        UpdateProgressUI();
        _historyStack.Clear();
        _historyStack.Add(new SlotHistoryData 
        { 
            cardId = headerId, 
            contentText = headerId, 
            iconSprite = null 
        });
        if (_currentCount >= _totalNeeded)
        {
            // 如果恢复的数据显示已经满了 (4/4)，说明上次可能刚好在消除前退出了，
            // 或者存档没保存消除状态。这里必须手动补发一次“完成检查”！
            StartCoroutine(CheckCompletion());
        }
    }
    // 当玩家把“分类牌”拖进来时调用
    public void ActivateCategory(CardView card,  int total)
    {
        isOccupied = true;
        categoryId = card.categoryId;
        _totalNeeded = total;
        _currentCount = 0;
        
        categoryName.GetComponentInChildren<Text>(true).text = card.categoryId;
        content.SetActive(true);

        RecordAndRefresh(card);
    }

    public void AddWordCard(CardView card)
    {
        _currentCount++;
        RecordAndRefresh(card);
        
        if (_currentCount >= _totalNeeded)
        {
            StartCoroutine(CheckCompletion());
        }
    }
    // 统一入口：记录历史并刷新UI
    private void RecordAndRefresh(CardView card)
    {
        currentHeaderId = card.cardId;
        
        string showText = (card.bigImage != null) && !string.IsNullOrEmpty(card.bigText.text) ? card.bigText.text : card.cardId;
        Sprite sprite = (card.bigImage != null) ? card.bigImage.sprite : null;
        
        SlotHistoryData data = new SlotHistoryData
        {
            cardId =  card.cardId,
            contentText = showText,
            iconSprite = sprite,
        };
        _historyStack.Add(data);
        
        UpdateContentVisual(data.contentText, data.iconSprite);
        UpdateProgressUI();
    }
    
    // 重载方法：直接通过文字和图片刷新
    private void UpdateContentVisual(string text, Sprite sprite)
    {
        // 1. 优先显示图片
        if (sprite != null && contentImage != null)
        {
            contentImage.sprite = sprite;
            contentImage.gameObject.SetActive(true);
            if(_wordText) _wordText.gameObject.SetActive(false);
        }
        else
        {
            // 2. 否则显示文字
            if (_wordText)
            {
                _wordText.text = text;
                _wordText.gameObject.SetActive(true);
            }
            if(contentImage) contentImage.gameObject.SetActive(false);
        }
    }
    public void UpdateProgressUI()
    {
        categoryName.SetActive(_currentCount > 0);
        if (_wordText) _wordText.text = currentHeaderId;
        if (_progressText) _progressText.text = $"{_currentCount}/{_totalNeeded}";
    }

    private IEnumerator CheckCompletion()
    {
        AudioManager.Instance.PlaySoundEffect("ThemeCompleted");
        Debug.Log($"分类 {categoryId} 完成！");
        if (_wordText){
            _wordText.text = "已完成"; // 如果你想显示文字
            _wordText.gameObject.SetActive(true);
        }
   
        if (contentImage) contentImage.gameObject.SetActive(false); // 隐藏内容图片
        if (checkMark) checkMark.SetActive(true);
        yield return new WaitForSeconds(0.8f);
        
        OnCategoryCompleted?.Invoke(categoryId);
        InitEmpty();
        if (checkMark) checkMark.SetActive(false);
    }


    public bool IsFull()
    {
        return isOccupied && (_currentCount >= _totalNeeded); 
    }
    public int GetCurrentCount() => _currentCount;

    public void RemoveCard(CardView card)
    {
        _currentCount--;
        if (_historyStack.Count > 0)
        {
            _historyStack.RemoveAt(_historyStack.Count - 1);
        }

        if (_currentCount < 0 || _historyStack.Count == 0)
        {
            InitEmpty();
        }
        else
        {
            SlotHistoryData prev = _historyStack[_historyStack.Count - 1];
            currentHeaderId = prev.cardId;
            UpdateContentVisual(prev.contentText, prev.iconSprite);
            UpdateProgressUI();
        }
    }

    public void PlayHighlightEffect()
    {
        if (glowOutlineImage != null)
        {
            glowOutlineImage.gameObject.SetActive(true);
            glowOutlineImage.DOKill();

            Color glowColor = glowOutlineImage.color;
            glowColor.a = 0f;
            glowOutlineImage.color = glowColor;

            Sequence glowSeq = DOTween.Sequence();
            glowSeq.Append(glowOutlineImage.DOFade(1f, 0.15f).SetEase(Ease.OutQuad));
            glowSeq.AppendInterval(0.1f);
            glowSeq.Append(glowOutlineImage.DOFade(0f, 0.4f).SetEase(Ease.OutQuad));
            glowSeq.OnComplete(() => glowOutlineImage.gameObject.SetActive(false));
        }

        // _starPool.GetObject<ParticleSystem>().Play();
        // if (particleMaterial != null)
        //     SpawnUIParticles();
    }

    private void SpawnUIParticles()
    {
        RectTransform slotRect = GetComponent<RectTransform>();
        float slotWidth = slotRect.rect.width;
        float slotHeight = slotRect.rect.height;
        float maxRadius = Mathf.Max(slotWidth, slotHeight) / 2f;
        
        int particleCount = Random.Range(50, 80);
        for (int i = 0; i < particleCount; i++)
        {
            GameObject pObj = new GameObject("UIParticle");
            pObj.transform.SetParent(this.transform, false);
            pObj.transform.SetAsFirstSibling();
            pObj.layer = gameObject.layer;
            
            LayoutElement layoutElem = pObj.AddComponent<LayoutElement>();
            layoutElem.ignoreLayout = true;
            Image pImg = pObj.AddComponent<Image>();
            pImg.material = particleMaterial;
            pImg.raycastTarget = false;
            pImg.maskable = false;
            pImg.color = new Color(1f, 0.8f, 0.1f, 1f);
            float size = UnityEngine.Random.Range(50f, 100f);
            pImg.rectTransform.sizeDelta = new Vector2(size, size);
            
            // 🔥 核心防坑 3：将粒子的锚点死死钉在槽位的【正中心】
            pImg.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            pImg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            
            // ==========================================
            // 爆点算法：从槽位中心附近，向四周 360 度随机炸开！
            // ==========================================
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            
            // 起点：在中心点附近稍微散开一点
            float startRadius = UnityEngine.Random.Range(0f, 20f);
            float startX = Mathf.Cos(angle) * startRadius;
            float startY = Mathf.Sin(angle) * startRadius;
            pImg.rectTransform.anchoredPosition = new Vector2(startX, startY);

            // 终点：飞出光圈边缘外侧！
            float endRadius = maxRadius + UnityEngine.Random.Range(10f, 50f); 
            float endX = Mathf.Cos(angle) * endRadius;
            float endY = Mathf.Sin(angle) * endRadius;
            
            // 播放飞行与消散动画
            float flyDuration = UnityEngine.Random.Range(1.4f, 2.7f);
            Sequence pSeq = DOTween.Sequence();
            
            pSeq.Append(pImg.rectTransform.DOAnchorPos(new Vector2(endX, endY), flyDuration).SetEase(Ease.OutCirc));
            pSeq.Join(pImg.DOFade(0f, flyDuration).SetEase(Ease.InQuad));
            // 让它在飞行时微微旋转，效果更生动
            pSeq.Join(pImg.rectTransform.DORotate(new Vector3(0, 0, UnityEngine.Random.Range(-180f, 180f)), flyDuration, RotateMode.FastBeyond360));
            pSeq.Join(pImg.rectTransform.DOScale(Vector3.zero, flyDuration));
            
            pSeq.OnComplete(() => Destroy(pObj));
        }
    }

    private void OnDisable()
    {
        _historyStack.Clear();
        // _starPool.ReturnAllObjectsToPool();
    }
}
