using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class NetErrorView : MonoBehaviour
{
    [SerializeField] private HyperlinkText _descriptionText;
    [SerializeField] private Button quitBtn;
    [SerializeField] private Button CancleButton;
    
    [SerializeField] private Button tryAgainBtn;
    // Start is called before the first frame update
    void Start()
    {
        tryAgainBtn.AddClickAction(OnTryAgainClick);
        quitBtn.AddClickAction(OnQuitGameClick);
        CancleButton.AddClickAction(OnCancleClick);
    }

    private void OnEnable()
    {
        switch (Game.Instance.CurrentErrorType)
        {
            case CommonErrorType.LoginFail:
                ShowLoginErrorPanel();
                break;
            case CommonErrorType.ExitPopup:
                ShowQuitGamePanel();
                break;
        }
    }

    public void ShowQuitGamePanel()
    {
        // string exitText =  MultilingualManager.Instance.GetString("ExitPopup");
        // if(!string.IsNullOrEmpty(exitText))
        _descriptionText.text = "确定退出游戏吗？";
        
        quitBtn.gameObject.SetActive(true);
        CancleButton.gameObject.SetActive(true);
        tryAgainBtn.gameObject.SetActive(false);
    }
    
    public void ShowLoginErrorPanel()
    {
        // string failText =  MultilingualManager.Instance.GetString("LoginFail");
        // if (!string.IsNullOrEmpty(failText))
            _descriptionText.text = "登录失败";
        
        quitBtn.gameObject.SetActive(false);
        CancleButton.gameObject.SetActive(false);
        tryAgainBtn.gameObject.SetActive(true);
    }

    private void OnTryAgainClick()
    {
        Game.Accounts.Login(true);
        transform.gameObject.SetActive(false);
    }
    
    private void OnQuitGameClick()
    {
       Application.Quit();
    }

    private void OnCancleClick()
    {
        transform.gameObject.SetActive(false);
    }

}
