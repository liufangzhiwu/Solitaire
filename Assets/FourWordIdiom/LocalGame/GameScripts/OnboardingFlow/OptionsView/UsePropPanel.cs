using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UsePropPanel : UIWindow
{
    [Header("UI Elements")] 
    [SerializeField] private Text propName;
    [SerializeField] private Transform propValue;

    [SerializeField] private Button closeBtn;
    [SerializeField] private Button rewardBtn;

    private ToolInfo _toolInfo;
    private Action<bool> _finishedCallback;
    protected override void InitializeUIComponents()
    {
        base.InitializeUIComponents();
        closeBtn.AddClickAction(()=>Close());
        rewardBtn.AddClickAction(OnRewardClick);
    }

    public void Setup(ToolInfo toolInfo, Action<bool> finishedCallback)
    {
        _toolInfo = toolInfo;
        _finishedCallback = finishedCallback;

        if (_toolInfo.type == "Hint")
        {
            propValue.GetChild(0).gameObject.SetActive(true);
            propValue.GetChild(1).gameObject.SetActive(false);
            propName.text = "提示道具";
          
        }else if (_toolInfo.type == "Undo")
        {
            propValue.GetChild(0).gameObject.SetActive(false);
            propValue.GetChild(1).gameObject.SetActive(true);
            propName.text = "撤回道具";
        }

        rewardBtn.GetComponentInChildren<Text>().text = _toolInfo.cost.ToString();
    }
    
    private void OnRewardClick()
    {
        if(GameDataManager.Instance.UserData.Gold <_toolInfo.cost)
        {
            MessageSystem.Instance.ShowTip(MultilingualManager.Instance.GetString("TipGoldInsufficient"));
            return ;
        }
        GameDataManager.Instance.UserData.UpdateGold(-_toolInfo.cost, false, true, "购买道具");
        if (_toolInfo.type == "Hint")
        {
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, 1, "道具购买");
        }else if (_toolInfo.type == "Undo")
        {
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Undoes, 1, "道具购买");
        }
        _finishedCallback?.Invoke(true);
        Close();
    }

    public override void Close(CloseMethod method = CloseMethod.Default)
    {
        _toolInfo = null;
        _finishedCallback = null;
        base.Close(method);
    }
}
