using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class LearningGuide : UIWindow
{
    [SerializeField] private GameObject background; // 背景 
    [SerializeField] private RectTransform arrow;   // 箭头
    [SerializeField] private RectTransform dianShouTable; // 点击的手
    [SerializeField] private Text tipText; // 提示的文本
    
    [HideInInspector]
    [Tooltip("当前使用的教学工具对象")]
    public GameObject activeToolObject;
    // 🔥 1.用来记录所有被提升了层级的物体，方便事后清理
    private List<GameObject> _elevatedObjects = new List<GameObject>();
    // private CanvasGroup canvasGroup;
    private GameObject _currentGhostObj;     // 当前显示的幻影物体
    private Coroutine _currentGuideCoroutine; // 当前的动画协程
    private Coroutine _hintCoroutine;
    private int _currentProgress;

    protected override void Awake()
    {
        base.Awake();
        // canvasGroup = GetComponent<CanvasGroup>();
        // canvasGroup.blocksRaycasts = false;
        // canvasGroup.alpha = 1f;
    }

    // Start is called before the first frame updat
    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
       Canvas canvas = dianShouTable.GetComponent<Canvas>();
       canvas.sortingLayerName = UIPanelLayer.TopPanel;
    }
    private void Update()
    {
        // 如果当前正在显示引导，且玩家点击了鼠标左键(或触摸屏幕)
        if (_currentGuideCoroutine != null && Input.GetMouseButtonDown(0))
        {
            // 方案 A：一点击就消失 (比较清爽)
            StopGuide();
        
            // 方案 B：你也可以检查点击的是不是UI，或者是否点击了有效的卡牌
            // 但通常为了体验流畅，玩家只要动了，提示就该消失
        }
    }
    /// <summary>
    /// 设置需要展示的步骤
    /// </summary>
    /// <param name="step">步骤</param>
    public void NextStepTutorial(int step)
    {
        Debug.LogWarning("当前步数" + step);
        // SetCanvasLayer(UIPanelLayer.BasePanel);
        StopGuide(); // 清理旧动画
        _currentProgress = step;
        bool isActionFound = false;
        switch (_currentProgress)
        {
            case 1:
                isActionFound = ShowCategoryMoveGuide();
                break;
            case 2:
            case 3:
                    isActionFound = ShowCollectSameCategoryGuide();
                break;
            case 4:
                isActionFound = ShowCategoryMoveGuide();
                    break;
                case 5:
            case 6: 
            case 7:
            case 8:
                    isActionFound = ShowCollectSameCategoryGuide();
                    break;
                   
            case 9:
                isActionFound = ShowColumnStackingGuide();
                break;
            case 10:
                // 1. 先问问废牌区：那张翻开的牌能用吗？
                // (FindWasteSmartMoveAction 已经在 ShowSmartHintWithText 里证明了优先级更高)
                if (FindWasteSmartMoveAction(out CardView wasteCard, out Transform target))
                {
                    // 能用！打断原本的翻牌教学，改教移动
                    PlayGuideAnim(wasteCard, target);
                    isActionFound = true;
                
                    // 改一下文字，让玩家知道为什么要移动
                    tipText.text = "好运气！翻出来的这张牌正好能用，快拖进去！";
                }
                else
                {
                    // 2. 废牌不能用，才继续教翻牌
                    isActionFound = ShowStockDrawGuide();
                    Debug.LogWarning("废牌堆进入了, 但是翻牌了");
                }
                break;
            case 11:
                case 12:
                case 13:
                case 14:
                    isActionFound = ShowWasteSmartMoveGuide();
                break;
            case 15:
                isActionFound = ShowEmptyColumnGuide();
                break;
            case 16:
                 ShowFinalSuccess(); // 显示通关提示
                 isActionFound = true;
                break;
            default:
                // 如果超过了步骤，不做任何引导，或者关闭界面
                if (step > 16) 
                {
                    // 可以选择在这里直接关闭，或者由 Manager 关闭
                    // gameObject.SetActive(false); 
                }
                break;
        }
       
        // --- B. 设置提示文字 ---
        if (isActionFound)
        {
            // 如果找到了教程要求的动作，显示正常的教程文案
            ShowTipText(step);
        }
        else
        {
            // 🔥 C. 关键优化：如果教程要求的动作做不到（比如卡住了）
            // 1. 修改文案：提示玩家先做别的
            // tipText.text = "当前无法进行教程操作，试试其他移动来打破僵局！";
            
            // 2. 启动智能替补，随便找个能动的教给玩家
            // ShowSmartHintWithText();
            ShowAutoGuideByPriority();
            // 注意：此时 CurrentTutorial 进度不会变，玩家操作完这个“替补动作”后
            // ChainGuideSystem 会再次调用 NextStepTutorial，再次检查能不能做教程动作
        }
    }

    /// <summary>
    /// 道具提示
    /// </summary>
    public void PropTutorial(int step)
    {
        
        Debug.LogWarning("试试道具吧");
        
    }

    private void SetCanvasLayer(GameObject active, string layer = UIPanelLayer.TopPanel)
    {
        if (active == null) return;
        
        if (!active.TryGetComponent<Canvas>(out var canvas))
        {
            canvas = active.AddComponent<Canvas>();
        }
        canvas.overrideSorting = true;
        canvas.sortingLayerName = layer;
        canvas.sortingOrder = 100;
        if (!active.TryGetComponent<UnityEngine.UI.GraphicRaycaster>(out var raycaster))
        {
            active.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        // 🔥 2. 记录下来，方便拖拽/引导结束后清理
        if (!_elevatedObjects.Contains(active))
        {
            _elevatedObjects.Add(active);
        }
    }
    
    /// <summary>
    /// 最后一步：仅显示提示文字，不显示手指
    /// </summary>
    private void ShowFinalSuccess()
    {
        StopGuide(); // 确保没有手指在飞
        // 这里不需要做额外的动画，因为 ShowTipText 已经把文字设置成 "完成所有分类后通关！" 了
        // 你也可以在这里加一个特效，或者让 tipText 闪烁一下
        tipText.transform.DOShakeScale(0.5f, 0.2f);
    }
    
    // =========================================================
    // 新增：教程 - 移动到空列 (周转/救急逻辑)
    // 策略：当没有其他消除或合并机会时，提示把牌移到空列，以翻开下面的牌
    // =========================================================
    public bool ShowEmptyColumnGuide()
    {
        StopGuide();

        if (FindEmptyColumnMoveAction(out CardView sourceCard, out Transform targetTransform))
        {
            PlayGuideAnim(sourceCard, targetTransform);
            return true;
        }
        else
        {
            Debug.LogWarning("[LearningGuide] 没有找到有价值的移动到空列的操作");
            return false;
        }
    }
    // =========================================================
    // 新增：教程 - 废牌区智能移动引导
    // 优先级：
    // 1. 放入分类槽 (同类归档 或 类牌开槽)
    // 2. 放入列 (同类堆叠)
    // 3. 放入列 (空列搬运)
    // =========================================================

    public bool ShowWasteSmartMoveGuide()
    {
        if (FindWasteSmartMoveAction(out CardView sourceCard, out Transform targetTransform))
        {
            PlayGuideAnim(sourceCard, targetTransform);
            return true;
        }
        else
        {
            Debug.LogWarning("[LearningGuide] 废牌区没有可移动的合法路径");
            return false;
        }
    }
    public bool ShowStockDrawGuide()
    {
        var playArea = ChainPlayArea.Instance;
        if (playArea == null) return false;

        // 1. 获取牌堆按钮的 Transform
        // 假设 HandAreaController 有 stockButton 或 stockTransform
        // 根据你之前的代码：handArea.stockButton
        var handArea = playArea.handArea;
        if (handArea == null || handArea.stockButton == null) 
        {
            return false;
        }
        // 1. 检查牌堆（Stock）里是否还有牌
        // stockData 是存放待抽卡牌ID的列表
        bool hasStockCards = handArea.stockData?.Count > 0;
        // 2. 检查废牌区（Waste）里是否还有牌
        // (通常规则是：牌堆空了，点击空位可以把废牌收回来重洗)
        bool hasWasteCards = handArea.wasteCards?.Count > 0;
        if (!hasStockCards && !hasWasteCards)
        {
            Debug.Log("[LearningGuide] 牌堆和废牌区均为空，停止抽牌引导");
            return false; 
        }
        // // 检查牌堆里是否还有牌 (stockData.Count > 0)
        // bool hasStockCards = handArea.stockData.Count > 0;
        // 如果没牌了，可能要提示点击 "重置" 按钮，逻辑类似
        StopGuide();
            // 2. 播放点击动画
        _currentGuideCoroutine = StartCoroutine(LoopClickAnimation(handArea.stockButton.transform));
        return true;
    }

    /// <summary>
    /// 对外接口：展示 "将列中的牌移动到另一列的同类牌上" 的引导
    /// </summary>
    public bool ShowColumnStackingGuide()
    {
        if (FindColumnStackingAction(out CardView sourceCard, out Transform targetTransform))
        {
            // 🔥 直接复用之前的通用动画播放器
            PlayGuideAnim(sourceCard, targetTransform);
            return true;
        }
        else
        {
            Debug.LogWarning("[LearningGuide] 未找到可以在列之间堆叠的同类牌");
            return false;
        }
    }
    /// <summary>
    /// 对外接口：开启“将分类牌移至槽位”的教程引导
    /// </summary>
    public bool ShowCategoryMoveGuide()
    {
        // 1. 查找合法的移动操作
        if (FindPlayableCategoryMove(out CardView sourceCard, out Transform targetTransform))
        {
            SetCanvasLayer(sourceCard.gameObject);
            SetCanvasLayer(targetTransform.gameObject, UIPanelLayer.PopPanel);
            // 2. 开始播放幻影动画
            _currentGuideCoroutine = StartCoroutine(LoopCardMoveAnimation(sourceCard, targetTransform));
            return true;
        }
        else
        {
            Debug.LogWarning("[LearningGuide] 未找到可移动到槽位的分类牌，无法播放引导。");
            return false;
        }
    }
    /// <summary>
    /// 教程：将列中的普通牌，归类到已有的分类槽中
    /// (对应你的需求：槽里有分类 -> 列里找同类 -> 移动)
    /// </summary>
    public bool ShowCollectSameCategoryGuide()
    {
        if (FindCollectableAction(out CardView sourceCard, out Transform targetTransform))
        {
            SetCanvasLayer(sourceCard.gameObject);
            SetCanvasLayer(targetTransform.gameObject, UIPanelLayer.PopPanel);
            PlayGuideAnim(sourceCard, targetTransform);
            return true;
        }
        else if (FindColumnStackingAction(out CardView sourceCard2, out Transform targetTransform2))
        {
            SetCanvasLayer(sourceCard2.gameObject);
            SetCanvasLayer(targetTransform2.gameObject, UIPanelLayer.PopPanel);
            PlayGuideAnim(sourceCard2, targetTransform2);
            return true;
        }
        else 
        {
            Debug.LogWarning("[LearningGuide] 未找到可消除的同类卡牌组合");
            return false;
        }
    }
    /// <summary>
    /// 【全能替补】当特定教程无法执行时，寻找任何合法的移动
    /// </summary>
    public void ShowSmartHint()
    {
        // 1. 优先尝试消除 (最爽的)
        if (FindCollectableAction(out var c1, out var t1)) { PlayGuideAnim(c1, t1); return; }
        
        // 2. 尝试分类牌归位 (开局必备)
        if (FindPlayableCategoryMove(out var c2, out var t2)) { PlayGuideAnim(c2, t2); return; }

        // 3. 尝试废牌利用 (让废牌动起来)
        if (FindWasteSmartMoveAction(out var c3, out var t3)) { PlayGuideAnim(c3, t3); return; }

        // 4. 尝试列堆叠 (整理牌桌)
        if (FindColumnStackingAction(out var c4, out var t4)) { PlayGuideAnim(c4, t4); return; }
        
        // 5. 尝试移动到空列 (救急)
        if (FindEmptyColumnMoveAction(out var c5, out var t5)) { PlayGuideAnim(c5, t5); return; }

        // 6. 最后：提示抽牌
        ShowStockDrawGuide(); 
    }
    
    #region  查找目标部分
    /// <summary>
    /// 查找策略：寻找 [非空列] -> [空列] 的移动
    /// 过滤条件：只推荐能“翻开新牌”或“拆分长龙”的移动，忽略单张牌平移
    /// </summary>
    private bool FindEmptyColumnMoveAction(out CardView foundCard, out Transform foundTarget)
    {
        foundCard = null;
        foundTarget = null;
        var playArea = ChainPlayArea.Instance;
        if (playArea == null) return false;

        var columns = playArea.tableauArea.columns;

        // 1. 先找一个空列作为目标
        ColumnView emptyCol = null;
        foreach (var col in columns)
        {
            if (col.cards.Count == 0)
            {
                emptyCol = col;
                break; // 找到一个空列就够了
            }
        }

        // 如果没有空列，这个策略直接无效
        if (emptyCol == null) return false;

        // 2. 再找一个合适的源卡牌
        foreach (var sourceCol in columns)
        {
            // 不能是自己移给自己
            if (sourceCol == emptyCol) continue;
            
            // 如果列是空的，跳过
            if (sourceCol.cards.Count == 0) continue;

            // 如果列里只有一张牌，且下面没有背面牌（即 Count==1），移到空列是无意义的
            // 除非你想做极端的“腾挪”，但通常教程不推荐这种废操作
            if (sourceCol.cards.Count <= 1) continue;

            // 获取该列最顶部的牌 (可拖拽的那张)
            CardView topCard = sourceCol.GetTopCard();
            
            if (topCard != null && topCard.isFaceUp)
            {
                // 找到了！
                // 这张牌移动后，能露出下面的牌（无论是背面还是正面，都能改变局势）
                foundCard = topCard;
                foundTarget = emptyCol.transform; // 目标是空列的基座位置
                return true;
            }
        }

        return false;
    }
    /// <summary>
    /// 查找策略：检查废牌区顶部的牌，是否能放入任意一个分类槽
    /// </summary>
    private bool FindWasteSmartMoveAction(out CardView foundCard, out Transform foundTarget)
    {
        foundCard = null;
        foundTarget = null;
        var playArea = ChainPlayArea.Instance;
        if (playArea == null) return false;

        // 1. 获取废牌区最顶层的一张牌
        var handArea = playArea.handArea;
        if (handArea.wasteCards.Count == 0) return false;

        // 假设 wasteCards 列表的最后一个是顶层可见的牌
        CardView topWasteCard = handArea.wasteCards[handArea.wasteCards.Count - 1];

        // 2. 遍历所有分类槽，看能否放进去
        foreach (var slot in playArea.goalArea.allSlots)
        {
            // 逻辑 A: 槽位是空的
            if (!slot.isOccupied)
            {
                // 如果废牌是“分类头牌(Category Type)”，则可以放
                if (topWasteCard.type == CardType.Category)
                {
                    foundCard = topWasteCard;
                    foundTarget = slot.transform;
                    return true;
                }
            }
            // 逻辑 B: 槽位已有分类
            else
            {
                // 1. 槽位没满
                // 2. 废牌不是分类头牌 (防止头牌叠头牌)
                // 3. ID 必须匹配 (同类)
                if (!slot.IsFull() && 
                    topWasteCard.type != CardType.Category && 
                    topWasteCard.categoryId == slot.categoryId)
                {
                    foundCard = topWasteCard;
                    foundTarget = slot.transform;
                    return true;
                }
            }
        }

        return false;
    }
    /// <summary>
    /// 查找策略：寻找两个不同列，它们的顶层牌是同一分类
    /// </summary>
    private bool FindColumnStackingAction(out CardView foundCard, out Transform foundTarget)
    {
        foundCard = null;
        foundTarget = null;
        var playArea = ChainPlayArea.Instance;
        if (playArea == null) return false;

        var columns = playArea.tableauArea.columns;

        // 双重循环遍历所有列：寻找 Source -> Target
        foreach (var sourceCol in columns)
        {
            CardView sourceCard = sourceCol.GetTopCard();
            
            // 源卡牌检查：必须存在、正面朝上
            // (通常教程不建议把分类头牌(Category)叠在别人身上，看你具体策划需求，这里暂时允许)
            if (sourceCard == null || !sourceCard.isFaceUp) continue;

            foreach (var targetCol in columns)
            {
                // 1. 不能移动给自己
                if (sourceCol == targetCol) continue;

                CardView targetCard = targetCol.GetTopCard();

                // 2. 目标列检查
                // 你的游戏规则 IsValidMoveWithReason 中提到：
                // "error = MoveErrorType.TargetIsCategory" -> 不能压在分类牌(Category)上
                // 所以 targetCard 不能是 Category 类型
                if (targetCard == null) continue; // 暂时不考虑移动到空列，只做堆叠教程
                if (!targetCard.isFaceUp) continue;
                if (targetCard.type == CardType.Category) continue; 

                // 3. 核心匹配：必须同类 (CategoryId 相同)
                if (sourceCard.categoryId == targetCard.categoryId)
                {
                    foundCard = sourceCard;
                    // 目标位置是目标卡牌的位置（这会形成堆叠效果）
                    foundTarget = targetCard.transform; 
                    return true;
                }
            }
        }

        return false;
    }
    /// <summary>
    /// 策略：寻找 [已激活的槽位] 和 [列中匹配的同类牌]
    /// </summary>
    private bool FindCollectableAction(out CardView foundCard, out Transform foundTarget)
    {
        foundCard = null;
        foundTarget = null;
        var playArea = ChainPlayArea.Instance;
        if (playArea == null) return false;

        // 1. 遍历所有槽位，寻找 "已激活且没满" 的槽
        // (因为我们要找的是已经有分类的，不是空的)
        foreach (var slot in playArea.goalArea.allSlots)
        {
            if (!slot.isOccupied) continue; // 跳过空槽
            if (slot.IsFull()) continue;    // 跳过满槽

            string targetCategoryId = slot.categoryId;

            // 2. 拿着这个 ID，去所有列里找匹配的牌
            foreach (var column in playArea.tableauArea.columns)
            {
                CardView topCard = column.GetTopCard();
                
                // 必须有牌，且正面朝上
                if (topCard == null || !topCard.isFaceUp) continue;

                // 核心匹配逻辑：
                // A. 类别 ID 必须相同
                // B. 通常我们移动的是普通牌(Word)到分类(Category)里，防止把另一个头牌移进去(看你具体规则)
                if (topCard.categoryId == targetCategoryId && topCard.type != CardType.Category)
                {
                    foundCard = topCard;
                    foundTarget = slot.transform;
                    return true; // 找到一组就立刻返回
                }
            }
        }

        return false;
    }
    
    /// <summary>
    /// 核心查找逻辑：在列中寻找分类牌，并匹配空槽位
    /// </summary>
    private bool FindPlayableCategoryMove(out CardView foundCard, out Transform foundTarget)
    {
        foundCard = null;
        foundTarget = null;

        var playArea = ChainPlayArea.Instance;
        if (playArea == null) return false;

        // 遍历所有列
        foreach (var column in playArea.tableauArea.columns)
        {
            // 获取该列所有牌
            foreach (var card in column.cards)
            {
                // 条件1：必须是正面朝上
                if (!card.isFaceUp) continue;

                // 条件2：必须是分类牌 (CardType.Category) 
                // 注意：根据你的逻辑，CardView应该有type字段
                if (card.type == CardType.Category)
                {
                    // 找到了分类牌，现在去 goalArea 找个能放的位置
                    CategorySlotView targetSlot = FindTargetSlotFor(card, playArea.goalArea.allSlots);
                    
                    if (targetSlot != null)
                    {
                        foundCard = card;
                        foundTarget = targetSlot.transform;
                        return true; // 找到第一个就返回
                    }
                }
            }
        }

        return false;
    }
    /// <summary>
    /// 为指定的分类牌寻找合适的目标槽位
    /// </summary>
    private CategorySlotView FindTargetSlotFor(CardView card, List<CategorySlotView> allSlots)
    {
        foreach (var slot in allSlots)
        {
            // 规则1：如果是空槽，可以放
            if (!slot.isOccupied)
            {
                return slot;
            }
            // 规则2：如果不是空槽，必须 ID 相同 (虽然一般分类牌只能放空槽，但为了逻辑严谨加上判断)
            else if (slot.categoryId == card.categoryId)
            {
                // 注意：如果槽位已有分类牌，通常不能再叠分类牌，这里根据你的游戏规则调整
                // 你的代码 handleDropToSlot 中写着：if (target.isOccupied) { if (categoryCard != null) return false; }
                // 所以分类牌只能放空槽。
                continue; 
            }
        }
        return null;
    }
    #endregion
    #region 动画部分

    /// <summary>
    /// 播放动画
    /// </summary>
    /// <param name="card"></param>
    /// <param name="target"></param>
    private void PlayGuideAnim(CardView card, Transform target)
    {
        _currentGuideCoroutine = StartCoroutine(LoopCardMoveAnimation(card, target));
    }
    
    // =========================================================
    // 点击动画核心 (原地缩放模拟点击)
    // =========================================================

    private IEnumerator LoopClickAnimation(Transform target)
    {

   
        // 1. 实例化手指
        
        // 2. 设置层级 (确保在最上层)
        if (!dianShouTable.TryGetComponent<Canvas>(out var canvas))
            canvas = dianShouTable.gameObject.AddComponent<Canvas>();
        
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "UpPopTwoPanel"; 
        canvas.sortingOrder = 1001; // 比幻影更高一点

        // 3. 初始设置
        dianShouTable.gameObject.SetActive(true);
        // 如果手指预制体本身有点偏，可以在这里调整 pivot 或 position 偏移
        
        WaitForSeconds wait = new WaitForSeconds(0.2f);

        while (true)
        {
            // 实时跟随目标位置 (防止UI适配导致位置变化)
            dianShouTable.transform.position = target.position;
            
            // 动画：模拟按下 (Scale 1.0 -> 0.8 -> 1.0)
            float timer = 0f;
            float duration = 0.6f;
            
            while (timer < duration)
            {
                timer += Time.deltaTime;
                if (dianShouTable == null) yield break;

                // 使用 PingPong 实现缩放
                // t 从 0 到 1 再到 0
                float t = Mathf.PingPong(timer * 2 / duration, 1f); 
                
                // 缩放范围 0.8 ~ 1.0 (根据你的手指素材调整)
                float scale = Mathf.Lerp(1.0f, 0.8f, t);
                dianShouTable.transform.localScale = Vector3.one * scale;

                yield return null;
            }

            // 停顿一下，让玩家看清楚
            yield return wait;
        }
    }
    
    private IEnumerator LoopCardMoveAnimation(CardView originalCard, Transform target)
    {
        // 1. 创建幻影 (保持原始外观)
        _currentGhostObj = Instantiate(originalCard.gameObject, transform); // 挂在当前Guide节点下
        
        // 2. 清理逻辑组件
        Destroy(_currentGhostObj.GetComponent<CardView>());
        if (_currentGhostObj.GetComponent<Button>()) Destroy(_currentGhostObj.GetComponent<Button>());

        // 3. 确保显示层级 (Canvas override)
        if (!_currentGhostObj.TryGetComponent<Canvas>(out var canvas)) canvas = _currentGhostObj.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "UpPopTwoPanel"; 
        canvas.sortingOrder = 1000;

        // 4. 设置透明度和穿透
        CanvasGroup canvasGroup = _currentGhostObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = _currentGhostObj.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false; // 关键：让鼠标穿透
        canvasGroup.alpha = 0f;

        WaitForSeconds wait = new WaitForSeconds(0.5f);

        while (true)
        {
            Vector3 startPos = originalCard.transform.position;
            Vector3 endPos = target.position; // 实时获取目标位置，防止目标移动

            _currentGhostObj.transform.position = startPos;
            
            float duration = 1.2f;
            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                if (_currentGhostObj == null) yield break;

                // 动画曲线：前20%淡入，中间移动，后20%淡出
                float progress = time / duration;
                
                // 移动
                float ease = progress * progress * (3f - 2f * progress); // SmoothStep
                _currentGhostObj.transform.position = Vector3.Lerp(startPos, endPos, ease);

                // 透明度控制
                if (progress < 0.2f) canvasGroup.alpha = Mathf.Lerp(0, 0.8f, progress / 0.2f);
                else if (progress > 0.8f) canvasGroup.alpha = Mathf.Lerp(0.8f, 0, (progress - 0.8f) / 0.2f);
                else canvasGroup.alpha = 0.8f;

                yield return null;
            }
            yield return wait;
        }
    }

    /// <summary>
    /// 移动手到目标位置
    /// </summary>
    public void MoveHandToTile(Transform transform)
    {
        if (transform == null) return;

        dianShouTable.GetComponent<Canvas>().sortingLayerName = UIPanelLayer.TipsPanel;
        // RectTransform movingRect = DianShouTable.GetComponent<RectTransform>();
        RectTransform targetRect = transform.GetComponent<RectTransform>();

        // 获取目标物体的四个世界坐标角落
        Vector3[] targetCorners = new Vector3[4];
        targetRect.GetWorldCorners(targetCorners);
        //for(int i = 0; i < targetCorners.Length; i++)
        //{
        //    Debug.Log($"目标的坐标 {i}: " + targetCorners[i]);
        //}
        // 直接使用目标物体的右下角坐标
        Vector3 targetBottomRight = targetCorners[3];
        // 将移动物体直接设置到目标位置
        dianShouTable.transform.position = targetBottomRight;
    } 
    // 清理当前教程
    private void StopGuide()
    {
        // 1. 停止协程
        if(_currentGuideCoroutine != null) StopCoroutine(_currentGuideCoroutine);
        _currentGuideCoroutine = null;
        
        // 2. 销毁幻影 (Card Ghost)
        if(_currentGhostObj != null) Destroy(_currentGhostObj);
        _currentGhostObj = null;
        
        // 3. 🔥🔥 隐藏手指并停止 DOTween 动画 🔥🔥
        if (dianShouTable != null)
        {
            // 停止手指上的所有 DOTween 动画 (防止手指还在缩放)
            dianShouTable.DOKill(); 
            // 强制隐藏
            dianShouTable.gameObject.SetActive(false); 
        }
        // 🔥 4. 恢复所有物体的原始层级！
        // if (background != null) background.SetActive(false);
        // RestoreCanvasLayers();
    }
    #endregion
/// <summary>
/// 【核心逻辑】基于优先级的智能引导系统
/// 摒弃固定的 Step 流程，根据当前盘面自动推荐最优解
/// </summary>
public void ShowAutoGuideByPriority()
{
    StopGuide(); // 先清理旧动画
    if (background != null) background.SetActive(true);
    // ------------------------------------------------------
    // 优先级 1：消除/收集 (最爽的操作，永远优先)
    // ------------------------------------------------------
    // 场景：槽位有分类 -> 列里有同类牌 -> 飞进去
    if (FindCollectableAction(out var c1, out var t1)) 
    { 
        activeToolObject = c1.gameObject;
        SetCanvasLayer(c1.gameObject);
        SetCanvasLayer(t1.gameObject, UIPanelLayer.PopPanel);
        PlayGuideAnim(c1, t1); 
        tipText.text = "发现可收集的卡牌！将它移动到对应分类槽中。";
        return; // ⛔ 命中即停止
    }

    // ------------------------------------------------------
    // 优先级 2：分类归位 (开局或腾出空槽后)
    // ------------------------------------------------------
    // 场景：有空槽 -> 列里有分类头牌(Category) -> 飞进去
    if (FindPlayableCategoryMove(out var c2, out var t2))
    {
        activeToolObject = c2.gameObject;
        SetCanvasLayer(c2.gameObject);
        SetCanvasLayer(t2.gameObject, UIPanelLayer.PopPanel);
        PlayGuideAnim(c2, t2);
        tipText.text = "将分类卡放入槽位，开启新的收集目标！";
        return; 
    }
    
    // ------------------------------------------------------
    // 优先级 3：废牌区捡漏 (变废为宝)
    // ------------------------------------------------------
    // 场景：废牌区翻开的牌 -> 可以直接飞入分类槽
    if (FindWasteSmartMoveAction(out var c3, out var t3)) 
    { 
        activeToolObject = c3.gameObject;
        SetCanvasLayer(c3.gameObject);
        SetCanvasLayer(t3.gameObject, UIPanelLayer.PopPanel);
        PlayGuideAnim(c3, t3); 
        tipText.text = "好运气！废牌区的这张牌正好可以用，快拖进去！";
        return; 
    }

    // ------------------------------------------------------
    // 优先级 4：列堆叠/整理 (整理盘面，露出下面的牌)
    // ------------------------------------------------------
    // 场景：列A顶牌 -> 列B顶牌 (同类堆叠)
    if (FindColumnStackingAction(out var c4, out var t4)) 
    { 
        activeToolObject = c4.gameObject;
        SetCanvasLayer(c4.gameObject);
        SetCanvasLayer(t4.gameObject, UIPanelLayer.PopPanel);
        PlayGuideAnim(c4, t4); 
        tipText.text = "同类卡牌可以堆叠在一起，试试整理一下牌桌！";
        return; 
    }
    // ------------------------------------------------------
    // 优先级 5：翻牌 (实在没招了才翻牌)
    // ------------------------------------------------------
    // 场景：场上无牌可动 -> 引导点击牌堆
    if (ShowStockDrawGuide())
    {
        activeToolObject = ChainPlayArea.Instance.handArea.stockButton.gameObject;
        SetCanvasLayer(activeToolObject);
        // 文案可以根据废牌区有没有牌稍微变一下，也可以通用
        tipText.text = "当前牌桌没有可移动的牌了，点牌堆翻一张新牌吧！";
        return;
    }
    
    // ------------------------------------------------------
    // 优先级 6：移动到空列 (救急策略)
    // ------------------------------------------------------
    // 场景：有空列 -> 把别列的牌移过来 (通常用于拆分长龙)
    if (FindEmptyColumnMoveAction(out var c5, out var t5)) 
    { 
        activeToolObject = c5.gameObject;
        SetCanvasLayer(c5.gameObject);
        SetCanvasLayer(t5.gameObject, UIPanelLayer.PopPanel);
        PlayGuideAnim(c5, t5); 
        tipText.text = "利用空白列来转移卡牌，寻找新的机会。";
        return; 
    }

   

    // ------------------------------------------------------
    // 优先级 7：死局 / 通关判断
    // ------------------------------------------------------
    // 这里需要判断是赢了还是输了
    if (ChainPlayArea.Instance.IsGameOver()) // 你需要自己实现这个判断，比如看 goalArea 是否满了
    {
        ShowFinalSuccess();
    }
    else
    {
        tipText.transform.parent.gameObject.SetActive(false);
        // 真的死局了
        tipText.text = "当前似乎无解，试试重置关卡？";
    }
}
    private void ShowTipText(int step)
    {
        string text = "";
        switch (step)
        {
            case 1:
            case 2:
            case 3:
            case 4:
            case 5:
                text = "你的目标是移动所有词语至指定分类，从分类卡开始堆叠他们，拖动卡片去移动吧！";
                break;
            case 6:
            case 7:
            case 8:
                text = "漂亮！两列之间的卡片也可以互相移动，属于同一类的卡片可以堆叠在一块";
                break;
            case 9:
            case 10:
                text = "卡牌用完了？点击右上角的牌堆去翻牌";
                break;
            case 11:
            case 12:
            case 13:
            case 14:
                text = "当你完成一个类别后，这个类别会消失，然后你可以将新的分类卡放在卡槽上";
                break;
            case 15:
                text = "完美！看到空白列了吗？可以移动整列卡牌至空白列";
                break;
            case 16:
            default:
                text = "完成所有分类后通关！";
                break;
        }

        tipText.text = text;
    }
    // 新增：带文案的智能提示
    public void ShowSmartHintWithText()
    {
        // 我们可以在这里根据找到的操作类型，给更精准的提示
        if (FindCollectableAction(out var c1, out var t1)) 
        { 
            PlayGuideAnim(c1, t1); 
            tipText.text = "先试试消除这些卡牌！";
            return; 
        }

        if (FindPlayableCategoryMove(out var c2, out var t2))
        {
            PlayGuideAnim(c2, t2);
            tipText.text = "先试试消除这些卡牌！";
            return; 
        }
        
        if (FindWasteSmartMoveAction(out var c3, out var t3)) 
        { 
            PlayGuideAnim(c3, t3); 
            tipText.text = "把废牌区的牌移进来试试！";
            return; 
        }
        if (FindColumnStackingAction(out var c4, out var t4)) 
        { 
            PlayGuideAnim(c4, t4); 
            tipText.text = "试试整理一下列中的牌！";
            return; 
        }
        // 最后尝试抽牌
        if (ShowStockDrawGuide())
        {
            tipText.text = "没有可移动的牌了，点牌堆抽一张！";
            return;
        }

        // 实在没招了
        tipText.text = "当前似乎无解，试试重置关卡？";
    }
    protected override void OnDisable()
    {
        StopGuide();
        base.OnDisable();
    }
    
    // 🔥 3. 清理强加在卡牌和槽位上的最高层级
    public void RestoreCanvasLayers()
    {
        foreach (var obj in _elevatedObjects)
        {
            if (obj != null)
            {
                // 销毁为了新手引导强行添加的组件
                if (obj.TryGetComponent<UnityEngine.UI.GraphicRaycaster>(out var raycaster))
                {
                    Destroy(raycaster);
                }
                if (obj.TryGetComponent<Canvas>(out var canvas))
                {
                    Destroy(canvas);
                }
            }
        }
        _elevatedObjects.Clear();
    }
    
    // 🔥 新增 1：检查是否有强引导（黑屏是否激活）
    public bool IsStrictGuideActive()
    {
        return background != null && background.activeInHierarchy;
    }

    // 🔥 新增 2：检查目标物体是不是被高亮的那个“正确答案”
    public bool IsTargetElevated(GameObject obj)
    {
        if (obj == null) return false;
        return _elevatedObjects.Contains(obj);
    }

    // 🔥 新增 3：玩家如果乱扔扔错了，重新唤醒动画继续教他
    public void ResumeGuideAnim()
    {
        // 直接重新触发一次当前的正确引导即可
        ShowAutoGuideByPriority();
    }
}