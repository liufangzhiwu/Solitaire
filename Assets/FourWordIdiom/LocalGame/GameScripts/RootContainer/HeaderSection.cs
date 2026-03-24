using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;


public class HeaderSection : UIWindow
{
    public Button GmBtn;
    public Button SetBtn;
    public Button BackBtn;
    public Button ShopBtn;
    public Button PuzzlebookBtn;
    public Button LevelPuzzleBtn;
    public GameObject GoldImage;
    public Text Goldtxt;       

    // Start is called before the first frame update
    protected void Start()
    {
        InitUI();
        InitializeButtons();       
    }

    private void InitUI(int value=0,bool isanim=false)
    {
        if(value>0&&isanim)
        {
            StartCoroutine(AnimateCoinAddition(value));
        }
        else
        {
            Goldtxt.text = GameDataManager.Instance.UserData.Gold.ToString();
        }
    }
    
    private IEnumerator AnimateCoinAddition(int amount)
    {
        int startValue = GameDataManager.Instance.UserData.Gold-amount;
        int targetValue = GameDataManager.Instance.UserData.Gold;
        float duration = 0.2f; // 动画持续时间
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration); // 归一化
            int currentValue = Mathf.RoundToInt(Mathf.Lerp(startValue, targetValue, t));
            Goldtxt.text = currentValue.ToString();
            yield return null;
        }
        Goldtxt.text = targetValue.ToString(); // 确保最终值正确显示
    }

    private void InitializeButtons()
    {       
        SetBtn.AddClickAction(OnSetClick);
        BackBtn.AddClickAction(OnBackClick);
        ShopBtn.AddClickAction(OnShopClick);
        // if (ShopBtn.TryGetComponent(out Canvas canvas))
        // {
        //     canvas.sortingLayerName = UIPanelLayer.TopPanel;
        // }
#if Unity_ShowLog || UNITY_EDITOR
        GmBtn.AddClickAction(OnGmClick, "", false);
#endif
        PuzzlebookBtn.AddClickAction(OnSetClick);
        LevelPuzzleBtn.AddClickAction(OnClickStagePuzzleScreen);
    }

    protected override void OnEnable()
    {
        EventDispatcher.Instance.OnUpdateLayerCoin += UpdateCoinLayer;
        EventDispatcher.Instance.OnChangeGoldUI += InitUI;
        EventDispatcher.Instance.OnChangeTopRaycast += ChangeTopRaycast;
        bool ishomeshow = SystemManager.Instance.PanelIsShowing(PanelType.PrimaryInterface);
        PuzzlebookBtn.gameObject.SetActive(!ishomeshow);
        GmBtn.gameObject.SetActive(ishomeshow);
        BackBtn.gameObject.SetActive(!ishomeshow);
        SetBtn.gameObject.SetActive(ishomeshow);

        CustomFlyInManager.Instance.GoldObj = GoldImage.gameObject;

        // if (SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView))
        // {
        //     BackBtn.GetComponent<Image>().sprite =AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Home");
        // }
        // else
        // {
        //     BackBtn.GetComponent<Image>().sprite =AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_back");
        // }
        EventDispatcher.Instance.TriggerChangeTopRaycast(true);
        EventDispatcher.Instance.TriggerChangeGoldUI(0,false);       

        //LevelPuzzleBtn.gameObject.SetActive(SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView));
        
        // 启用时开始重复调用 (1秒延迟，每秒1次)
        // StartCoroutine(CheckLevelPuzzleVisibility());
    }
    
    private IEnumerator CheckLevelPuzzleVisibility()
    {
        yield return new WaitForSeconds(0.5f);  
        bool isgameshow = SystemManager.Instance.PanelIsShowing(PanelType.GamePlayArea)||
                          SystemManager.Instance.PanelIsShowing(PanelType.StageFinishView) ||
                            SystemManager.Instance.PanelIsShowing(PanelType.ChessFinishView);
        
        LevelPuzzleBtn.GetComponent<CanvasGroup>().alpha = 0f;
        
        // bool hasLevelWords = false;
        // while (true)
        // {
        //     if (isgameshow)
        //     {
        //         hasLevelWords = ChainStageController.Instance.CurrStageData.FoundTargetPuzzles.Count > 0;
        //         // Debug.Log("当前模式： " + GameDataManager.Instance.UserData.levelMode +" "+ hasLevelWords);
        //         LevelPuzzleBtn.gameObject.SetActive(hasLevelWords);
        //         LevelPuzzleBtn.GetComponent<CanvasGroup>().DOFade(1f,0.2f);
        //     }
        //     yield return new WaitForSeconds(1f);  
        // }
    }

    /// <summary>
    /// 更改金币显示层级
    /// </summary>
    private void UpdateCoinLayer(bool istop,bool isshopbtnEnable=true)
    {
        GameObject coinObj = ShopBtn.gameObject;
        Canvas canvas= coinObj.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas= coinObj.AddComponent<Canvas>();
            coinObj.AddComponent<GraphicRaycaster>();
        }
           
        if (istop)
        {
            canvas.overrideSorting=true;
            canvas.sortingLayerName="TipsPanel";
            canvas.sortingOrder = 100;
        }
        else
        {
            canvas.overrideSorting=true;
            canvas.sortingLayerName="TopPanel";
            canvas.sortingOrder = 1;
        }
        
        ShopBtn.enabled = isshopbtnEnable;
    }
    
    private void OnClickPuzzleVocabulary()
    {
        SystemManager.Instance.ShowPanel(PanelType.WordVocabularyScreen);
        ChainGuideSystem.Instance.CloseGuide();
    }
    
    private void OnClickStagePuzzleScreen()
    {
        SystemManager.Instance.ShowPanel(PanelType.LevelWordScreen);
        ChainGuideSystem.Instance.CloseGuide();
    }

    private void OnGmClick()
    {
        //string localIP = GetLocalIPAddress();
        //bool isloaclIp = IsInLocalNetwork(localIP);
        //bool isloaclIp = IsLocalDevice();
        if (true) 
        {
            SystemManager.Instance.ShowPanel(PanelType.DebugMenu);
        }
        //Debug.Log("TP-LINK 5G 当前IP地址: " + localIP + "设备是否在局域网内: " + isloaclIp);
    }
   

    private void OnSetClick()
    {
        SystemManager.Instance.ShowPanel(PanelType.OptionsView);
        ChainGuideSystem.Instance.CloseGuide();
    }

    private void OnShopClick()
    {
        SystemManager.Instance.ShowPanel(PanelType.RewardAdsScreen);
        // SystemManager.Instance.ShowPanel(PanelType.ShopScreen);
        ChainGuideSystem.Instance.CloseGuide();
    }

    private void OnBackClick()
    {
        base.Close();
        transform.GetComponent<HeaderSection>().AddCloseListener(() =>
        {
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
            ChangeBackBtnState(false);
        });

        if (SystemManager.Instance.PanelIsShowing(PanelType.SuccessPanel))
        {
            SystemManager.Instance.HidePanel(PanelType.SuccessPanel);
        }
        if (SystemManager.Instance.PanelIsShowing(PanelType.ChainPlayArea))
        {
            SystemManager.Instance.HidePanel(PanelType.ChainPlayArea);
            GameDataManager.Instance.UserData.UpdateOnlineStageTime();
        }    
        
        
        ChainGuideSystem.Instance.CloseGuide();
    }

    public void ChangeBackBtnState(bool isshow)
    {
        BackBtn.gameObject.SetActive(isshow);
        SetBtn.gameObject.SetActive(!isshow);
        //LevelPuzzleBtn.gameObject.SetActive(!isshow);
    }

    private void ChangeTopRaycast(bool isblock)
    {
        
        transform.GetComponent<CanvasGroup>().blocksRaycasts = isblock;
    }
    
    protected override void OnDisable()
    {
        base.OnDisable();
        //EventManager.ChangeBackBtnHandler -= ChangeBackBtnState;
        CustomFlyInManager.Instance.GoldObj = null;
        EventDispatcher.Instance.OnChangeGoldUI -= InitUI;
        EventDispatcher.Instance.OnUpdateLayerCoin -= UpdateCoinLayer;
        EventDispatcher.Instance.OnChangeTopRaycast -= ChangeTopRaycast;
        LevelPuzzleBtn.gameObject.SetActive(false);
        // 禁用时取消调用
        CancelInvoke(nameof(CheckLevelPuzzleVisibility));
    }

}



