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
    [Header("拖拽吸附视觉")]
    public GameObject hoverGreenFrame; // 鼠标悬停时的绿色预警框
    public GameObject errorRedFrame;   // 放置失败时的红色错误框
    
    [Header("运行时数据")] 
    public bool isOccupied = false;
    public string categoryId;
    public string currentHeaderId;
    private int _totalNeeded;
    private int _currentCount;

    private Sprite _slotImage;
    private Sprite _defaultImage;
    
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

        _slotImage = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("cate_active_bg","UI_MainBase");
        _defaultImage = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("card_front_bg","UI_MainBase");
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
        if (_currentCount > 0)
            content.GetComponent<Image>().sprite = _defaultImage;
        else
            content.GetComponent<Image>().sprite = _slotImage;
        
    }

    private IEnumerator CheckCompletion()
    {
        AudioManager.Instance.PlaySoundEffect("ThemeCompleted");
        Debug.Log($"分类 {categoryId} 完成！");
        if (_wordText){
            _wordText.text = MultilingualManager.Instance.GetString("Completed"); // 如果你想显示文字
            _wordText.gameObject.SetActive(true);
        }
   
        if (contentImage) contentImage.gameObject.SetActive(false); // 隐藏内容图片
        // ==========================================
        // 🌟 阶段 1：高光展示 (打勾 Q弹出现 + 外框发光)
        // ==========================================
        // if (checkMark) 
        // {
        //     checkMark.SetActive(true);
        //     // 给对勾加一个可爱的 Q 弹放大效果
        //     checkMark.transform.localScale = Vector3.zero;
        //     checkMark.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
        // }
        if (glowOutlineImage != null)
        {
            glowOutlineImage.gameObject.SetActive(true);
            glowOutlineImage.DOKill();
            Color glowColor = glowOutlineImage.color;
            glowColor.a = 0f;
            glowOutlineImage.color = glowColor;
            // 外围发光图快速亮起
            glowOutlineImage.DOFade(1f, 0.3f).SetEase(Ease.OutQuad);
        }
        yield return new WaitForSeconds(0.6f);
        // ==========================================
        // 🌟 阶段 2：整体缓慢淡出
        // ==========================================
        // 动态添加 CanvasGroup 用来控制整个槽位（含发光、打勾、背景）的透明度
        CanvasGroup nameCG = GetOrAddCanvasGroup(categoryName);
        CanvasGroup contentCG = GetOrAddCanvasGroup(content);
        // 耗时 0.8 秒，平滑地淡出到 0
        // 两个部位同时开始淡出
        if (nameCG != null) nameCG.DOFade(0f, 0.6f).SetEase(Ease.InOutQuad);
        if (contentCG != null) 
        {
            // 等待 content 彻底淡出完毕
            yield return contentCG.DOFade(0f, 0.6f).SetEase(Ease.InOutQuad).WaitForCompletion();
        }
        else
        {
            yield return new WaitForSeconds(0.6f); // 兜底防空
        }
        // ==========================================
        // 🌟 阶段 3：数据清理与视觉复位
        // ==========================================
        OnCategoryCompleted?.Invoke(categoryId);
        InitEmpty();
        if (checkMark) checkMark.SetActive(false);
        if (glowOutlineImage != null) glowOutlineImage.gameObject.SetActive(false);
        // ⚠️ 极其重要：淡出完成后，必须把它们的透明度恢复为 1！
        if (nameCG != null) nameCG.alpha = 1f; 
        if (contentCG != null) contentCG.alpha = 1f;
    }
    // 👇 新增一个小辅助方法，用来安全地获取或添加 CanvasGroup
    private CanvasGroup GetOrAddCanvasGroup(GameObject obj)
    {
        if (obj == null) return null;
        if (!obj.TryGetComponent<CanvasGroup>(out var cg))
        {
            cg = obj.AddComponent<CanvasGroup>();
        }
        return cg;
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
    
    /// <summary>
    /// 当有卡牌拖拽到自己上方时调用
    /// </summary>
    public void SetHoverHighlight(bool isHovering)
    {
       
            hoverGreenFrame.SetActive(isHovering);
        
    }
    /// <summary>
    /// 放置失败时触发的红色警告 + 震动动画
    /// </summary>
    public void ShowErrorFeedback()
    {
        
            errorRedFrame.SetActive(true);
            
            // 杀掉旧动画，防止连击报错
            errorRedFrame.transform.DOKill();
            if (errorRedFrame.TryGetComponent<Image>(out var img))
            {
                img.DOKill();
                Color c = img.color;
                c.a = 1f; // 初始完全不透明
                img.color = c;
                
                // 红色框停留 0.15 秒后，慢慢消散
                img.DOFade(0f, 0.35f).SetDelay(0.15f).OnComplete(() => errorRedFrame.SetActive(false));
            }

            // 加一个微小的左右晃动（摇头）效果，极其生动！
            // transform.DOKill(false); // 不杀掉其他动画，只杀位移
            // transform.DOShakePosition(0.3f, new Vector3(8f, 0, 0), 20, 90, false, true);
            //
            // 顺便可以播个错误音效
            // AudioManager.Instance.PlaySoundEffect("ErrorDrop");
        
    }
    /// <summary>
    /// 清理单个槽位里的卡牌，恢复到空置状态
    /// </summary>
    public void ClearSlot()
    {
        // 1. 停止当前槽位身上正在播放的所有 DOTween 动画（比如高光、缩放等）
        transform.DOKill();
        if (glowOutlineImage != null) glowOutlineImage.DOKill();
        
        InitEmpty();
        // 如果你用了一个 List (比如 collectedCards) 存了卡牌，记得 Clear 它：
        // if (collectedCards != null) collectedCards.Clear();
        
        // 3. 重置槽位的 UI 视觉（进度条归零、隐藏文字等）
        // ResetVisuals();
    }
    
    private void OnDisable()
    {
        _historyStack.Clear();
        ClearSlot();
        // _starPool.ReturnAllObjectsToPool();
    }
}
