using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UsePropPanel : UIWindow
{
    [Header("UI Elements")] 
    [SerializeField] private Text title;
    [SerializeField] private Text propName;
    [SerializeField] private Transform propValue;

    [SerializeField] private Button closeBtn;
    [SerializeField] private Button coinBtn;
    [SerializeField] private Button adsBtn;

    private ToolInfo _toolInfo;
    private Action<bool> _finishedCallback;
    protected override void InitializeUIComponents()
    {
        base.InitializeUIComponents();
        closeBtn.AddClickAction(()=>Close());
        coinBtn.AddClickAction(OnCoinRewardClick);
        adsBtn.AddClickAction(OnAdsRewardClick);
    }

    private void Start()
    {
        string titleText = MultilingualManager.Instance.GetString("Item");
        if (!string.IsNullOrEmpty(titleText))
        {
            title.text = titleText;
        }
    }

    public void Setup(ToolInfo toolInfo, Action<bool> finishedCallback)
    {
        _toolInfo = toolInfo;
        _finishedCallback = finishedCallback;

        if (_toolInfo.type == "Hint")
        {
            propValue.GetChild(0).gameObject.SetActive(true);
            propValue.GetChild(1).gameObject.SetActive(false);
            propName.text = MultilingualManager.Instance.GetString("HintItem");
          
        }else if (_toolInfo.type == "Undo")
        {
            propValue.GetChild(0).gameObject.SetActive(false);
            propValue.GetChild(1).gameObject.SetActive(true);
            propName.text = MultilingualManager.Instance.GetString("RecallItem");
        }

        coinBtn.GetComponentInChildren<Text>().text = _toolInfo.cost.ToString();
        // if (GameDataManager.Instance.UserData.Gold < _toolInfo.cost)
        // {
        //     propName.text = "金币不足";
        //     coinBtn.transform.GetChild(0).GetComponent<Image>().sprite =
        //         AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("ads");
        //     coinBtn.GetComponentInChildren<Text>().text = "观看获取";
        //     coinBtn.GetComponentInChildren<Text>().fontSize = 62;
        // }
    }
    
    private void OnCoinRewardClick()
    {
        if(GameDataManager.Instance.UserData.Gold <_toolInfo.cost)
        {
            // Game.Ads.ShowReward(Define.AdKey.RewardAdIdStoreGold,UpdateAdsRewardHandler);
            MessageSystem.Instance.ShowTip(MultilingualManager.Instance.GetString("TipGoldInsufficient"));
            return ;
        }
        if (_toolInfo.type == "Hint")
        {
            GameDataManager.Instance.UserData.UpdateGold(-_toolInfo.cost, false, true, "购买提示道具");
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, 1, "提示道具购买");
        }else if (_toolInfo.type == "Undo")
        {
            GameDataManager.Instance.UserData.UpdateGold(-_toolInfo.cost, false, true, "购买测绘道具");
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Undotool, 1, "撤回道具购买");
        }
        _finishedCallback?.Invoke(true);
        Close();
    }

    private void OnAdsRewardClick()
    {
        Game.Ads.ShowReward(Define.AdKey.RewardAdIdStoreGold,UpdateAdsRewardHandler);
    }
    private void UpdateAdsRewardHandler(bool result)
    {
        if (result)
        {
            if (_toolInfo.type == "Hint")
            {
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, 1, "看广告获取提示道具");
            }else if (_toolInfo.type == "Undo")
            {
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Undotool, 1, "看广告获取撤回道具");
            }
            _finishedCallback?.Invoke(true);
        }
        else
        {
            MessageSystem.Instance.ShowTip(MultilingualManager.Instance.GetString("失败了, 请稍后再试!"));
        }
        
        Close();
    }

    public override void Close(CloseMethod method = CloseMethod.Default)
    {
        _toolInfo = null;
        _finishedCallback = null;
        // rewardBtn.GetComponentInChildren<Text>().fontSize = 94;
        base.Close(method);
    }
}
