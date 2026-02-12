using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 集中管理游戏内事件的发布与订阅
/// </summary>
public class EventDispatcher:MonoBehaviour
{
    public static EventDispatcher Instance;
    
    #region 事件声明区域
    // 当关卡开始时
    private Action<int> _onLevelStarted;
    // 参数1: 移动的卡牌数据, 参数2: 目标位置/类别
    private Action<CardView, MonoBehaviour, bool> _onCardDragResult;
    private Action<string> _onShowSelectedPuzzle;
    private Action<string, List<int[]>> _onLetterSelected;
    private Action<List<int[]>, bool> _onPlayChoicePuzzle;
    private Action<int, bool> _onChangeGoldUI;
    private Action _onFakeBonusEvent;
    private Action<string> _onRemoveNotePuzzle;
    private Action<bool> _onUpdateRewardPuzzle;
    private Action<bool, bool> _onUpdateLayerCoin;
    
    /// <summary>蝶园改变</summary>
    private Action _onButterflyGardenChange;
    
    /// <summary>
    /// 设置选中词语展示区显状态
    /// </summary>
    private Action<bool> _onChoicePuzzleSetStatus;
    private Action _onCheckShowTutorial;
    private Action<bool> _onChangeTopRaycast;
    private Action _onChangeHeadIconUpdateUI;
    
    #endregion

    private void Awake()
    {
        Instance = this;
    }

    #region 公共事件接口
    /// <summary> 关卡开始事件 </summary>
    public event Action<int> OnLevelStarted
    {
        add => _onLevelStarted += value;
        remove => _onLevelStarted -= value;
    }
    /// <summary> 卡牌放置事件 </summary>
    public event Action<CardView, MonoBehaviour, bool> OnCardDragResult
    {
        add => _onCardDragResult += value;
        remove => _onCardDragResult -= value;
    }
    
    /// <summary>显示选中的词语事件</summary>
    public event Action<string> OnShowSelectedPuzzle
    {
        add => _onShowSelectedPuzzle += value;
        remove => _onShowSelectedPuzzle -= value;
    }

    /// <summary>字母选中事件</summary>
    public event Action<string, List<int[]>> OnLetterSelected
    {
        add => _onLetterSelected += value;
        remove => _onLetterSelected -= value;
    }

    /// <summary>播放字块矩阵动画事件</summary>
    public event Action<List<int[]>, bool> OnPlayChoicePuzzle
    {
        add => _onPlayChoicePuzzle += value;
        remove => _onPlayChoicePuzzle -= value;
    }

    /// <summary>金币数量更新事件</summary>
    public  event Action<int, bool> OnChangeGoldUI
    {
        add => _onChangeGoldUI += value;
        remove => _onChangeGoldUI -= value;
    }


    /// <summary>移出生词本词语事件</summary>
    public  event Action<string> OnRemoveNotePuzzle
    {
        add => _onRemoveNotePuzzle += value;
        remove => _onRemoveNotePuzzle -= value;
    }

    /// <summary>更新奖励词语事件</summary>
    public event Action<bool> OnUpdateRewardPuzzle
    {
        add => _onUpdateRewardPuzzle += value;
        remove => _onUpdateRewardPuzzle -= value;
    }

    /// <summary>更新金币层级事件</summary>
    public event Action<bool, bool> OnUpdateLayerCoin
    {
        add => _onUpdateLayerCoin += value;
        remove => _onUpdateLayerCoin -= value;
    }

    /// <summary>设置词语展示区状态事件</summary>
    public event Action<bool> OnChoicePuzzleSetStatus
    {
        add => _onChoicePuzzleSetStatus += value;
        remove => _onChoicePuzzleSetStatus -= value;
    }

    /// <summary>检查新手引导事件</summary>
    public event Action OnCheckShowTutorial
    {
        add => _onCheckShowTutorial += value;
        remove => _onCheckShowTutorial -= value;
    }

    /// <summary>切换顶部射线检测事件</summary>
    public event Action<bool> OnChangeTopRaycast
    {
        add => _onChangeTopRaycast += value;
        remove => _onChangeTopRaycast -= value;
    }
    
    /// <summary>
    /// 头像切换时UI界面刷新
    /// </summary>
    public event Action OnChangeHeadIconUpdateUI  
    {
        add => _onChangeHeadIconUpdateUI += value;
        remove => _onChangeHeadIconUpdateUI -= value;
    }
    /// <summary>蝶园改变事件</summary>
    public event Action OnButterflyGardenChange
    {
        add => _onButterflyGardenChange += value;
        remove => _onButterflyGardenChange -= value;
    }
    #endregion

    #region 事件触发方法

    public void TriggerLevelStarted(int level)
        => _onLevelStarted?.Invoke(level);
    public void TriggerCardDragResult(CardView card, MonoBehaviour target, bool result)
        => _onCardDragResult?.Invoke(card, target, result);
    
    public void TriggerShowSelectedPuzzle(string puzzle)
        => _onShowSelectedPuzzle?.Invoke(puzzle);

    public void TriggerLetterSelected(string letter, List<int[]> positions)
        => _onLetterSelected?.Invoke(letter, positions);

    public void TriggerPlayChoicePuzzle(List<int[]> positions, bool state)
        => _onPlayChoicePuzzle?.Invoke(positions, state);

    public void TriggerChangeGoldUI(int amount, bool animate)
        => _onChangeGoldUI?.Invoke(amount, animate);

    public  void TriggerFakeBonusEvent()
        => _onFakeBonusEvent?.Invoke();

    public void TriggerRemoveNotePuzzle(string puzzle)
        => _onRemoveNotePuzzle?.Invoke(puzzle);

    public void TriggerUpdateRewardPuzzle(bool state)
        => _onUpdateRewardPuzzle?.Invoke(state);

    public void TriggerUpdateLayerCoin(bool immediate, bool animate)
        => _onUpdateLayerCoin?.Invoke(immediate, animate);

    public void TriggerChoicePuzzleSetStatus(bool visible)
        => _onChoicePuzzleSetStatus?.Invoke(visible);

    public void TriggerCheckShowTutorial()
        => _onCheckShowTutorial?.Invoke();

    public void TriggerChangeTopRaycast(bool enable)
        => _onChangeTopRaycast?.Invoke(enable);
    
    public void TriggerChangeHeadIconUpdateEvent()
        => _onChangeHeadIconUpdateUI?.Invoke();
    
    /// <summary>
    /// 蝶园改变事件
    /// </summary>
    public void TriggerChangeButterflyGarden()
        => _onButterflyGardenChange?.Invoke();
    #endregion
}