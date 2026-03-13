using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class FailedPanel : UIWindow
{
    [SerializeField] private Text desc;
    [SerializeField] private Button levelBtn;
    // Start is called before the first frame update
    void Start()
    {
        levelBtn.AddClickAction(ReloadLevel);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        AudioManager.Instance.PlaySoundEffect("GameOver");
        Game.Ads.ShowInterstitial((bool result) =>
        {
            
        });
    }
    
    private void ReloadLevel()
    {
        SystemManager.Instance.HidePanel(PanelType.ChainPlayArea);
        ChainStageController.Instance.ResetCurrentStage();
        StartCoroutine(EnterGame());
    }

    private IEnumerator EnterGame()
    {
        yield return new WaitForSeconds(0.5f);
        SystemManager.Instance.HidePanel(PanelType.FailedPanel, false, () =>
        {
            SystemManager.Instance.ShowPanel(PanelType.ChainPlayArea);
        });
    }
}
