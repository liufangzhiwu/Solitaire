using System;
using System.Collections;
using System.Collections.Generic;
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
    [Header("UI References - 文字")]
    [SerializeField] public Text bigText;
    [SerializeField] private Text smallText;
    
    [Header("UI References - 图片")]
    [SerializeField] public Image bigImage;
    [SerializeField] private Image smallImage;
    
    [Header("UI References - 其他")]
    [SerializeField] private Text countText;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private GameObject backFace;
    
    [Header("Anchors")] 
    [SerializeField] private Transform centerAnchor;
    [SerializeField] private Transform topAnchor;
    [SerializeField] private Transform rightAnchor;

    [Header("Assets")]
    [SerializeField] private Sprite normalBgSprite;
    [SerializeField] private Sprite categoryBgSprite;
    
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

    /// <summary>
    /// 初始化卡牌数据
    /// </summary>
    /// <param name="wId">词条ID</param>
    /// <param name="cId">分类ID</param>
    /// <param name="faceUp">是否正面朝上</param>
    /// <param name="totalCount">总数</param>
    public void Initialization(string wId, string cId, bool faceUp, int totalCount)
    {
        name = $"{wId}";
        cardId = wId;
        categoryId = cId;
        type = wId == cId ? CardType.Category : CardType.Normal;
        
        countText.gameObject.SetActive(type == CardType.Category);
        countText.text = "0/" + totalCount.ToString();
        
       transform.GetComponent<Image>().sprite = (type == CardType.Category) ? categoryBgSprite : normalBgSprite;
        
        _isCompressed = false;
        if (contentRoot !=null && centerAnchor != null)
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
    /// 设置压缩/展开状态 (普通牌被压住时内容上滑)
    /// </summary>
    public void SetCompressedState(bool isCompressed, bool immediate = false)
    {
        if (_isCompressed == isCompressed && !immediate) return;
        
        _isCompressed = isCompressed;
        if(_animCoroutine != null) StopCoroutine(_animCoroutine);

        Transform target;
        if (isCompressed)
        {
            if (IsInHand)
            {
                target = rightAnchor;
                if (smallText)
                {
                    smallText.text = GetVerticalString(cardId);
                    smallText.lineSpacing = 0.85f;
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
        
        // 1. 准备工作：在渐变开始前，必须把两个都激活，否则看不见淡入效果
        PreAnimateSetup();
        // 2. 确定透明度目标
        // 如果变成了压缩状态：小图(Small)要显示(1)，大图(Big)要隐藏(0)
        float targetSmallAlpha = _isCompressed ? 1f : 0f;
        float targetBigAlpha   = _isCompressed ? 0f : 1f;
        // 获取当前透明度作为起点 (防止动画打断时跳变)
        float startSmallAlpha = GetGroupAlpha(true); // true 代表获取 Small 组
        float startBigAlpha   = GetGroupAlpha(false); // false 代表获取 Big 组
        
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float smoothT = t * t * (3f - 2f * t);
            
            contentRoot.localPosition = Vector3.Lerp(startPos, target.localPosition, smoothT);
            contentRoot.localScale = Vector3.Lerp(startScale, target.localScale, smoothT);
            contentRoot.localRotation = Quaternion.Lerp(startRot, target.localRotation, smoothT);
            SetGroupAlpha(true, Mathf.Lerp(startSmallAlpha, targetSmallAlpha, smoothT));  // 设置小组件
            SetGroupAlpha(false, Mathf.Lerp(startBigAlpha, targetBigAlpha, smoothT));     // 设置大组件
            yield return null;
        }
        contentRoot.localPosition = target.localPosition;
        contentRoot.localScale = target.localScale;
        contentRoot.localRotation = target.localRotation;
        SetGroupAlpha(true, targetSmallAlpha);
        SetGroupAlpha(false, targetBigAlpha);
        UpdateVisualState();
    }
    /// <summary>
    /// 设置某一组(Small/Big)的透明度
    /// isSmallGroup: true=操作小物体, false=操作大物体
    /// </summary>
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

    /// <summary>
    /// 动画开始前的准备：把需要参与渐变的物体都由 SetActive(true)
    /// </summary>
    private void PreAnimateSetup()
    {
        if (usedIcon)
        {
            // 图片模式：激活大小图片
            if (smallImage) smallImage.gameObject.SetActive(true);
            if (bigImage) bigImage.gameObject.SetActive(true);
        }
        else
        {
            // 文字模式：激活大小文字
            if (smallText) smallText.gameObject.SetActive(true);
            if (bigText) bigText.gameObject.SetActive(true);
        }
    }

    public void SetFaceUp(bool faceUp)
    {
        isFaceUp = faceUp;
        contentRoot.gameObject.SetActive(faceUp);
        backFace.SetActive(!faceUp);
    }
    
    // 单独提取显示逻辑，清晰易懂
    public void UpdateVisualState()
    {
        // 目标：
        // 1. 如果是压缩状态 (_isCompressed) -> 显示 Small 系列
        // 2. 如果是展开状态 (!isCompressed) -> 显示 Big 系列
        // 3. 具体显示 Image 还是 Text，取决于 _hasIcon
        
        bool showSmall = _isCompressed;
        bool showBig = !_isCompressed;

        if (usedIcon)
        {
            // 图片模式：操作 Image 组件
            if (smallImage) smallImage.gameObject.SetActive(showSmall);
            if (bigImage) bigImage.gameObject.SetActive(showBig);
            
            // 确保文字彻底隐藏
            if (smallText) smallText.gameObject.SetActive(false);
            if (bigText) bigText.gameObject.SetActive(false);
        }
        else
        {
            // 文字模式：操作 Text 组件
            if (smallText) smallText.gameObject.SetActive(showSmall);
            if (bigText) bigText.gameObject.SetActive(showBig);

            // 确保图片彻底隐藏
            if (smallImage) smallImage.gameObject.SetActive(false);
            if (bigImage) bigImage.gameObject.SetActive(false);
        }
    }
    // 添加一个辅助方法：把字符串变成竖排
    private string GetVerticalString(string originText)
    {
        if (string.IsNullOrEmpty(originText)) return "";
        // 把字符拆开，用换行符拼起来
        // "腰果" -> "腰\n果"
        return string.Join("\n", originText.ToCharArray());
    }
    #region 🔥 核心交互：将事件转发给管理器 (ChainPlayArea)

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 1. 如果是背面，或者管理器不存在，直接不允许拖
        if (!isFaceUp || ChainPlayArea.Instance == null) return;
        // 2. 将控制权交给管理器
        ChainPlayArea.Instance.OnCardBeginDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isFaceUp || ChainPlayArea.Instance == null) return;
        
        // 告诉管理器我在移动
        ChainPlayArea.Instance.OnCardDrag(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isFaceUp || ChainPlayArea.Instance == null) return;
        
        // 告诉管理器我松手了
        ChainPlayArea.Instance.OnCardEndDrag(this, eventData);
    }
    #endregion

    /// <summary>
    /// 播放错误动画
    /// </summary>
    public void PlayErrorAnimation()
    {
        StartCoroutine(DoErrorShake(transform.GetChild(0)));
    }
    
    private IEnumerator DoErrorShake(Transform target)
    {
        Vector3 originalPos = target.localPosition;
        Vector3 centerPos = new Vector3(0f, originalPos.y, originalPos.z);
        float duration = 0.3f;
        float magnitude = 15f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float x = Mathf.Sin(elapsed * 50f) * magnitude * (1 - elapsed / duration);
            target.localPosition = centerPos + new Vector3(x, 0f, 0f);
            yield return null;
        }
        target.localPosition = centerPos;
    }
}
