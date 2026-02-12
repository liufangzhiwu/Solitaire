using System.Collections;
using Middleware;
using UnityEngine;
using UnityEngine.UI;


public class PrivacyScreen : UIWindow
{               
    [AutoAssign] private Button btn_next; // 关闭按钮    
    [AutoAssign] private HyperlinkText txt_link;
    [AutoAssign] private Text txt_tip;
    [AutoAssign] private Text txt_next;

    protected override void InitializeUIComponents()
    {
        AutoAssign.AutoInject(this);
        btn_next.AddClickAction(OnClosePanel); // 绑定关闭按钮事件
    }

    protected void Start()
    {       
        //设置点击回调
        txt_link.onHyperlinkClick = OnClickText;
        InitLanguage();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
    }
    
    private void InitLanguage()
    {
        txt_tip.text = MultilingualManager.Instance.GetString("PrivacyAgreement02");
        txt_link.text = MultilingualManager.Instance.GetString("PrivacyAgreement01");
        txt_next.text = MultilingualManager.Instance.GetString("PrivacyAgreement03");
    }

    
    private void OnClickText(string url)
    {
        Debug.Log("点击"+url);
        Application.OpenURL(url);
    }

    private void OnClosePanel()
    {
        //GameCoreManager.Instance.ShowGamePanel();
        ShowGamePanel();
        base.Close(); // 隐藏面板
    }
    
    private void ShowGamePanel()
    {
        ChainStageController.Instance.SetStageData(GameDataManager.Instance.UserData.CurrentStage);
        SystemManager.Instance.ShowPanel(PanelType.ChainPlayArea);
    }
}
