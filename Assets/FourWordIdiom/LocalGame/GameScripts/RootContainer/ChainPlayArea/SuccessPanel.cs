using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class SuccessPanel : UIWindow
{   
    [Header("UI Elements")]
    [SerializeField] private Button levelBtn;
    [SerializeField] private GameObject coinGo;
    // Start is called before the first frame update
    private void Start()
    {
        levelBtn.AddClickAction(LoadNextLevel);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        levelBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Level") + " " + ChainStageController.Instance.CurrentStage.ToString();
        ChainStageController.Instance.SetStageData(ChainStageController.Instance.CurrentStage);

        coinGo.GetComponentInChildren<Text>().text = AppGameSettings.LevelCompleteBonus.ToString();
        StartCoroutine(PlayRewardSequence());
        Game.Ads.ShowInterstitial((bool result) =>
        {
            
        });
    }
    
    private void LoadNextLevel()
    {
        SystemManager.Instance.HidePanel(PanelType.ChainPlayArea);
        StartCoroutine(EnterGame());
    }

    private IEnumerator EnterGame()
    {
        yield return new WaitForSeconds(0.5f);
        SystemManager.Instance.HidePanel(PanelType.SuccessPanel, false, () =>
        {
            SystemManager.Instance.ShowPanel(PanelType.ChainPlayArea);
        });
    }

    /// <summary>
    /// 播放奖励获取序列动画
    /// </summary>
    private IEnumerator PlayRewardSequence()
    {
        AudioManager.Instance.PlaySoundEffect("success");
        yield return new WaitForSeconds(0.5f);
        PlayGoldFlyAnimation();
    }
    
    /// <summary>
    /// 播放金币飞入动画
    /// </summary>
    public void PlayGoldFlyAnimation()
    {            
        CustomFlyInManager.Instance.FlyInGold(coinGo.transform, () =>
        {
            EventDispatcher.Instance.TriggerChangeGoldUI(AppGameSettings.LevelCompleteBonus, true);
        });
    }
}
