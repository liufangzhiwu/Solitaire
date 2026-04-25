using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum CardType
{
    Normal,
    Category
}
public enum MoveErrorType
{
    None,
    TargetIsCategory,       // 目标是分类牌（不能压）
    SourceIsCategory,       // 拖动的是分类牌（不能放非空列）
    CategoryMismatch,       // 分类不匹配
    SlotIsFull,             // 槽位满了
    Unknown                 // 其他未知错误
}

public class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{

    [Header("UI References - 双面渲染控制")]
    [SerializeField] private GameObject frontFaceObj; // 🔥 拖入新建的 FrontFace 节点
    [SerializeField] private Image frontBgImage;      // 🔥 拖入 FrontFace 节点 (获取它的Image组件)
    [SerializeField] private GameObject backFace;     // 拖入 BackFace 节点
    [SerializeField] private RectTransform contentRoot; // 拖入 ContentRoot
    [SerializeField] private GameObject dragGreenFrame;
    public GameObject hoverGreenFrame; // 别人悬停在自己头上时的绿框
    public GameObject errorRedFrame;   // 别人往自己头上放失败时的红框
    
    [Header("UI References - 文字")]
    [SerializeField] public Text bigText;
    [SerializeField] private Text smallText;
    
    [Header("UI References - 图片")]
    [SerializeField] public Image bigImage;
    [SerializeField] private Image smallImage;

    [Header("UI References - 其他")]
    [SerializeField] private Text countText;
    [Header("UI References - 连击角标")]
    [SerializeField] private GameObject comboBadgeObj;     // 角标根节点 (放在卡牌右上角)
    [SerializeField] private Text comboCountText;          // 显示数量的文本 (如 5, 6...)
    [SerializeField] private GameObject comboCheckmarkObj; // 显示打勾的图片
    
    [Header("Anchors")] 
    [SerializeField] private Transform centerAnchor;
    [SerializeField] private Transform topAnchor;
    [SerializeField] private Transform rightAnchor;

    [Header("Assets")]
    [SerializeField] private Sprite normalBgSprite;
    [SerializeField] private Sprite categoryBgSprite;
    [SerializeField] private Sprite wastePileBgSprite; // 🔥 废牌堆的专属背景
    
    [Header("Runtime Data")] 
    public string cardId;
    public string categoryId;
    public CardType type = CardType.Normal;
    public bool isFaceUp = false;
    public bool usedIcon = false;
    public bool IsInHand { get; set; } = false;
    
    // 引用所属的列 (方便交互)
    public ColumnView currentColumn;
   
    private bool _isCompressed = false;
    private Coroutine _animCoroutine;
    private Coroutine _visualTransitionCoroutine;
    
    // 👇 🔥 新增：用于记录角标数据和拖拽状态
    private int _comboCount = 0;
    private bool _isFullCombo = false;
    private bool _isDragging = false;
    // 删除了 Awake 里的 GetComponent<Image>()，因为根节点不再挂载 Image

    /// <summary>
    /// 初始化卡牌数据
    /// </summary>
    public void Initialization(string wId, string cId, bool faceUp, int totalCount)
    {
        name = $"{wId}";
        cardId = wId;
        categoryId = cId;
        type = wId == cId ? CardType.Category : CardType.Normal;
        
        countText.gameObject.SetActive(type == CardType.Category);
        countText.text = "0/" + totalCount.ToString();
        currentColumn = null;
        // 🔥 将背景图赋值给 FrontFace 的 Image
        // if (frontBgImage != null)
        // {
        //     frontBgImage.sprite = (currentZone == CardZone.WastePile) ? wastePileBgSprite : 
        //                           ((type == CardType.Category) ? categoryBgSprite : normalBgSprite);
        // }
        
        _isCompressed = false;
        if (contentRoot != null && centerAnchor != null)
        {
            contentRoot.localPosition = centerAnchor.localPosition;
            contentRoot.localScale = centerAnchor.localScale;
        }
        
        bigImage.sprite = null;
        smallImage.sprite = null;
        SetupContentDisplay();
        UpdateVisualState(); // 刷新显隐
        SetCompressedState(false, true);
        SetFaceUp(faceUp);
        SetComboBadge(0, false);
    }

    private void SetupContentDisplay()
    {
        if (bigText) bigText.text = cardId;
        if (smallText) smallText.text = cardId;

        usedIcon = ChainPlayArea.Instance.ShouldShowIcon(cardId);

        if (usedIcon)
        {
            Sprite bigSprite = ChainStageController.Instance.GetIconSprite(cardId, "(L)");
            if (bigSprite != null)
            {
                usedIcon = true;
                bigImage.sprite = bigSprite;
                smallImage.sprite = ChainStageController.Instance.GetIconSprite(cardId, "(S)");
                bigImage.SetNativeSize();
                smallImage.SetNativeSize();
            }
            else
            {
                usedIcon = false;
            }
        }
    }

    /// <summary>
    /// 翻牌控制（🔥 极度节省性能的写法）
    /// </summary>
    public void SetFaceUp(bool faceUp)
    {
        isFaceUp = faceUp;
        // 直接开关正反面的根节点，盖住时绝不渲染正面内容
        if (frontFaceObj != null) frontFaceObj.SetActive(faceUp);
        if (backFace != null) backFace.SetActive(!faceUp);
    }

    /// <summary>
    /// 设置压缩/展开状态 (普通牌被压住时内容上滑)
    /// </summary>
    public void SetCompressedState(bool isCompressed, bool immediate = false)
    {
        if (_isCompressed == isCompressed && !immediate) return;
        
        _isCompressed = isCompressed;
        UpdateBackground();
        
        if (!gameObject.activeInHierarchy) 
        {
            immediate = true;
        }
        if (_errorCoroutine != null)
        {
            immediate = true;
        }
        // if (_errorCoroutine != null)
        // {
        //     StopCoroutine(_errorCoroutine);
        //     _errorCoroutine = null;
        //     if (frontFaceObj != null) frontFaceObj.transform.localPosition = Vector3.zero;
        //     if (backFace != null) backFace.transform.localPosition = Vector3.zero;
        // }
        
        if(_animCoroutine != null) StopCoroutine(_animCoroutine);

        Transform target;
        if (isCompressed)
        {
            if (IsInHand)
            {
                target = rightAnchor;
                if (smallText)
                {
                    char firstChar = string.IsNullOrEmpty(cardId) ? ' ' : cardId[0];
                    bool isEnglish = (firstChar >= 'a' && firstChar <= 'z') || (firstChar >= 'A' && firstChar <= 'Z');
                    if (isEnglish)
                    {
                        // 英文：不换行，直接整个单词旋转 -90 度
                        smallText.text = cardId;
                        smallText.transform.localEulerAngles = new Vector3(0, 0, -90);
                        smallText.lineSpacing = 1f;
                    }
                    else
                    {
                        // 中文：保留原来的竖排逻辑
                        smallText.text = GetVerticalString(cardId);
                        smallText.transform.localEulerAngles = Vector3.zero;
                        smallText.lineSpacing = 0.85f;
                    }
                }
            }
            else
            {
                target = topAnchor;
                if (smallText)
                {
                    smallText.text = cardId;
                    smallText.lineSpacing = 1f;
                }
            }
        }
        else
        {
            target = centerAnchor;
            smallText.transform.localEulerAngles = Vector3.zero;
        }
    
        if (immediate)
        {
            UpdateVisualState();
            contentRoot.localPosition = target.localPosition;
            contentRoot.localScale = target.localScale;
            contentRoot.localRotation = target.localRotation;
        }
        else
        {
            _animCoroutine = StartCoroutine(AnimateContent(target));
        }
    }

    private IEnumerator AnimateContent(Transform target)
    {
        float duration = 0.2f;
        float time = 0;
        Vector3 startPos = contentRoot.localPosition;
        Vector3 startScale = contentRoot.localScale;
        Quaternion startRot = contentRoot.localRotation;
        
        PreAnimateSetup();
        
        float targetSmallAlpha = _isCompressed ? 1f : 0f;
        float targetBigAlpha   = _isCompressed ? 0f : 1f;
        float startSmallAlpha = GetGroupAlpha(true);
        float startBigAlpha   = GetGroupAlpha(false);
        
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float smoothT = t * t * (3f - 2f * t);
            
            contentRoot.localPosition = Vector3.Lerp(startPos, target.localPosition, smoothT);
            contentRoot.localScale = Vector3.Lerp(startScale, target.localScale, smoothT);
            contentRoot.localRotation = Quaternion.Lerp(startRot, target.localRotation, smoothT);
            SetGroupAlpha(true, Mathf.Lerp(startSmallAlpha, targetSmallAlpha, smoothT));
            SetGroupAlpha(false, Mathf.Lerp(startBigAlpha, targetBigAlpha, smoothT));
            yield return null;
        }
        contentRoot.localPosition = target.localPosition;
        contentRoot.localScale = target.localScale;
        contentRoot.localRotation = target.localRotation;
        SetGroupAlpha(true, targetSmallAlpha);
        SetGroupAlpha(false, targetBigAlpha);
        UpdateVisualState();
    }

    private void SetGroupAlpha(bool isSmallGroup, float alpha)
    {
        if (usedIcon)
        {
            Image img = isSmallGroup ? smallImage : bigImage;
            if (img) {
                Color c = img.color; 
                c.a = alpha; 
                img.color = c;
            }
        }
        else
        {
            Text txt = isSmallGroup ? smallText : bigText;
            if (txt) {
                Color c = txt.color; 
                c.a = alpha; 
                txt.color = c;
            }
        }
    }

    private float GetGroupAlpha(bool isSmallGroup)
    {
        if (usedIcon)
        {
            Image img = isSmallGroup ? smallImage : bigImage;
            return img != null ? img.color.a : 0;
        }
        else
        {
            Text txt = isSmallGroup ? smallText : bigText;
            return txt != null ? txt.color.a : 0;
        }
    }

    private void PreAnimateSetup()
    {
        if (usedIcon)
        {
            if (smallImage) smallImage.gameObject.SetActive(true);
            if (bigImage) bigImage.gameObject.SetActive(true);
        }
        else
        {
            if (smallText) smallText.gameObject.SetActive(true);
            if (bigText) bigText.gameObject.SetActive(true);
        }
    }
    
    public void UpdateVisualState()
    {
        bool showSmall = _isCompressed;
        bool showBig = !_isCompressed;

        if (smallImage) 
        { 
            Color c = smallImage.color; c.a = 1f; smallImage.color = c; 
            smallImage.gameObject.SetActive(usedIcon && showSmall); 
        }
        if (bigImage)   
        { 
            Color c = bigImage.color; c.a = 1f; bigImage.color = c; 
            bigImage.gameObject.SetActive(usedIcon && showBig); 
        }
        if (smallText)  
        { 
            Color c = smallText.color; c.a = 1f; smallText.color = c; 
            smallText.gameObject.SetActive(!usedIcon && showSmall); 
        }
        if (bigText)    
        { 
            Color c = bigText.color; c.a = 1f; bigText.color = c; 
            bigText.gameObject.SetActive(!usedIcon && showBig); 
        }
    }

    private string GetVerticalString(string originText)
    {
        if (string.IsNullOrEmpty(originText)) return "";
        return string.Join("\n", originText.ToCharArray());
    }
    
    /// <summary>
    /// 统一管理卡牌背景图的逻辑
    /// </summary>
    public void UpdateBackground()
    {
        if (frontBgImage == null) return;
        
        // 1. 在废牌区，且被其他牌压住（处于压缩状态）
        // if (currentZone == CardZone.WastePile && _isCompressed)
        if (_isCompressed)
        {
            frontBgImage.sprite = wastePileBgSprite; // 对应你的 hand_insert_bg
        }
        // 2. 没被压住（废牌区最上面），或者在下方的列中
        else
        {
            frontBgImage.sprite = (type == CardType.Category) ? categoryBgSprite : normalBgSprite;
        }
    }
    /// <summary>
    /// 设置卡牌被拎起来拖拽时的状态
    /// </summary>
    public void SetDragHighlight(bool isDragging)
    {
        _isDragging = isDragging;
        dragGreenFrame.SetActive(isDragging);
        // 拖拽状态改变时，检查是否需要显示/隐藏角标
        UpdateComboBadgeDisplay();
    }
    public void SetHoverHighlight(bool isHovering)
    {
        if (hoverGreenFrame != null) hoverGreenFrame.SetActive(isHovering);
    }
    // 👇 🔥 新增：被错误放置时的爆红框方法
    public void ShowErrorFeedback()
    {
        if (errorRedFrame != null)
        {
            errorRedFrame.SetActive(true);
            errorRedFrame.transform.DOKill();
            if (errorRedFrame.TryGetComponent<Image>(out var img))
            {
                img.DOKill();
                Color c = img.color; c.a = 1f; img.color = c;
                img.DOFade(0f, 0.35f).SetDelay(0.15f).OnComplete(() => errorRedFrame.SetActive(false));
            }
        }
        
        // 直接复用你之前写好的绝赞摇头动画！
        // PlayErrorAnimation(); 
    }
    /// <summary>
    /// 接收 ColumnView 传来的连击数据（只记录，不一定马上显示）
    /// </summary>
    public void SetComboBadge(int count, bool isFull)
    {
        _comboCount = count;
        _isFullCombo = isFull;
        UpdateComboBadgeDisplay();
    }
    
    #region 🔥 核心交互：将事件转发给管理器
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isFaceUp || ChainPlayArea.Instance == null || ChainPlayArea.Instance.IsDraggingProgress) return;
        transform.DOKill();
        if (TryGetComponent<RectTransform>(out var rt)) rt.DOKill();
        // 🔥 玩家摸到牌的瞬间，立刻杀死残留的抖动
        StopErrorAnimation();
        
        // if (TryGetComponent<Canvas>(out var myCanvas))
        // {
        //     myCanvas.sortingOrder = 3000; 
        // }
        ChainPlayArea.Instance.OnCardBeginDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isFaceUp || ChainPlayArea.Instance == null) return;
        ChainPlayArea.Instance.OnCardDrag(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isFaceUp || ChainPlayArea.Instance == null) return;
        ChainPlayArea.Instance.OnCardEndDrag(this, eventData);
    }
    #endregion

    #region 🔥 区域视觉状态管理 (大小 & 背景)

    /// <summary>
    /// 核心方法：改变卡牌的视觉状态（大小、背景），带平滑动画
    /// </summary>
    public void UpdateZoneVisuals(bool inWastePile, bool immediate = false)
    {
        UpdateBackground();
        
        // Vector3 targetScale = inWastePile ? new Vector3(0.94f, 0.94f, 1f) : Vector3.one;
        Vector3 targetScale = Vector3.one;
        if (immediate)
        {
            if (_visualTransitionCoroutine != null) StopCoroutine(_visualTransitionCoroutine);
            transform.localScale = targetScale;
        }
        else
        {
            if (_visualTransitionCoroutine != null) StopCoroutine(_visualTransitionCoroutine);
            _visualTransitionCoroutine = StartCoroutine(TransitionVisualsCoroutine(targetScale));
        }
    }

    private IEnumerator TransitionVisualsCoroutine(Vector3 targetScale)
    {
        float duration = 0.25f;
        float time = 0;
        Vector3 startScale = transform.localScale;
        
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float smoothT = t * t * (3f - 2f * t);

            transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
            yield return null;
        }

        transform.localScale = targetScale;
        _visualTransitionCoroutine = null;
    }
    
    /// <summary>
    /// 控制卡牌右上角“连击/收集完成”角标的显示
    /// </summary>
    public void UpdateComboBadgeDisplay()
    {
        if (comboBadgeObj == null) return;
        
        // 只有连击数 >= 5 或者 全收集了，才显示角标
        bool shouldShow = _isDragging && (_comboCount >= 5 || _isFullCombo);
        if (!shouldShow)
        {
            comboBadgeObj.SetActive(false);
            comboCheckmarkObj.SetActive(false);
            return;
        }
        if (_isFullCombo)
        {
            // 全收集：显示对勾，隐藏数字
            if (comboCheckmarkObj) comboCheckmarkObj.SetActive(true);
            comboBadgeObj.SetActive(false);
            // 可以加个小动画，让打勾弹出来更爽
            comboBadgeObj.transform.DOKill();
            comboBadgeObj.transform.localScale = Vector3.one;
            comboBadgeObj.transform.DOPunchScale(new Vector3(0.3f, 0.3f, 0), 0.3f);
        }
        else
        {
            // 连击中：显示数字，隐藏对勾
            if (comboCheckmarkObj) comboCheckmarkObj.SetActive(false);
            if (comboBadgeObj)
            {
                comboBadgeObj.SetActive(true);
                comboCountText.gameObject.SetActive(true);
                comboCountText.text = _comboCount.ToString();
            }
        }
    }
    #endregion

    #region 🔥 错误抖动动画
    private Coroutine _errorCoroutine;
    public void PlayErrorAnimation()
    {
        // SetCompressedState(_isCompressed, true);
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        if (_errorCoroutine != null) StopCoroutine(_errorCoroutine);
        
        _errorCoroutine = StartCoroutine(DoErrorShake());
    }
    
    private IEnumerator DoErrorShake()
    {
        float duration = 0.3f;
        float magnitude = 15f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 计算带有阻尼的左右偏移量
            float x = Mathf.Sin(elapsed * 50f) * magnitude * (1 - elapsed / duration);
            Vector3 offset = new Vector3(x, 0f, 0f);
            if (frontFaceObj != null) frontFaceObj.transform.localPosition = offset;
            if (backFace != null) backFace.transform.localPosition = offset;
            if (contentRoot != null)
            {
                // 找出当前内容正确的归属地
                Transform targetAnchor = centerAnchor;
                if (_isCompressed) targetAnchor = IsInHand ? rightAnchor : topAnchor;
                
                contentRoot.localPosition = targetAnchor.localPosition + offset;
            }
            yield return null;
        }
        // 抖动结束，将所有组件完美强行复位
        if (frontFaceObj != null) frontFaceObj.transform.localPosition = Vector3.zero;
        if (backFace != null) backFace.transform.localPosition = Vector3.zero;
        if (contentRoot != null)
        {
            Transform targetAnchor = centerAnchor;
            if (_isCompressed) targetAnchor = IsInHand ? rightAnchor : topAnchor;
            contentRoot.localPosition = targetAnchor.localPosition;
        }
        _errorCoroutine = null;
    }
    

    // 🔥 新增：强行终止错误抖动并复位
    public void StopErrorAnimation()
    {
        if (_errorCoroutine != null)
        {
            StopCoroutine(_errorCoroutine);
            _errorCoroutine = null;
            
            // 完美复位
            if (frontFaceObj != null) frontFaceObj.transform.localPosition = Vector3.zero;
            if (backFace != null) backFace.transform.localPosition = Vector3.zero;
            if (contentRoot != null)
            {
                Transform targetAnchor = centerAnchor;
                if (_isCompressed) targetAnchor = IsInHand ? rightAnchor : topAnchor;
                contentRoot.localPosition = targetAnchor.localPosition;
            }
        }
    }
    #endregion
}