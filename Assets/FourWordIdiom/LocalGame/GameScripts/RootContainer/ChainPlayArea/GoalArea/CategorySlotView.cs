using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    [Header("运行时数据")] 
    public bool isOccupied = false;
    public string categoryId;
    public string currentHeaderId;
    private int _totalNeeded;
    private int _currentCount;
    
    private Text _progressText;
    private Text _wordText;

    private readonly List<SlotHistoryData> _historyStack = new List<SlotHistoryData>();
    
    private void Awake()
    {
        if (content != null)
        {
            if (_progressText == null) _progressText = content.transform.GetChild(0).GetComponent<Text>();
            // 假设 Text 是第2个子物体，Image 是第3个子物体，或者你自己安排
            if (_wordText == null) _wordText = content.transform.GetChild(1).GetComponent<Text>();
        }
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
}
