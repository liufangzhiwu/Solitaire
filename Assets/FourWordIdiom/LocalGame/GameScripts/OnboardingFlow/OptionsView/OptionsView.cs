using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Middleware;

public class OptionsView : UIWindow
{
    [SerializeField] private Button HideButton; // 关闭按钮
    [SerializeField] private Toggle vibrateToggle; // 震动开关
    [SerializeField] private Toggle musicToggle; // 音乐开关
    [SerializeField] private Toggle soundsToggle; // 音效开关

    [SerializeField] private Button privacyBtn; // 隐私条款按钮
    [SerializeField] private Button termsBtn; // 服务协议按钮
    [SerializeField] private Button opinionBtn; // 语言选择按钮
    [SerializeField] private Button restoreBuyBtn; // 服务协议按钮

    [SerializeField] private GameObject muHandle; // 音乐开关的视觉手柄
    [SerializeField] private GameObject soHandle; // 音效开关的视觉手柄
    [SerializeField] private GameObject viHandle; // 震动开关的视觉手柄
    [SerializeField] private Image selectImage;
    [SerializeField] private Image closeImage;

    [SerializeField] private Text VersionText;
    [SerializeField] private Text HeaderText;
    [SerializeField] private Text musicText; // 音乐文本显示
    [SerializeField] private Text soundText; // 音效文本显示
    [SerializeField] private Text vibrateText; // 震动文本显示

    [Header("游戏内控制")]
    [SerializeField] private Button redoGame;
    Sprite Opensprite;
    Sprite Closesprite;

    protected void Start()
    {
       
        AttachToggleListeners(); // 绑定开关监听器
        Opensprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("ui_tg_select", "OnboardingFlow");
        Closesprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("ui_tg_close","OnboardingFlow");
        UpdateToggleStates(false); // 启用时更新状态，不带动画
#if UNITY_OPENHARMONY
        opinionBtn.gameObject.SetActive(true);
        restoreBuyBtn.gameObject.SetActive(false);
#else
        opinionBtn.gameObject.SetActive(false);
        restoreBuyBtn.gameObject.SetActive(true);
#endif
        opinionBtn.gameObject.SetActive(false);
        restoreBuyBtn.gameObject.SetActive(false);

        string redoStr = MultilingualManager.Instance.GetString("RestartBtn");
        if (!string.IsNullOrEmpty(redoStr))
        {
            redoGame.GetComponentInChildren<Text>().text = redoStr;
        }
    }

    protected override void OnEnable()
    {
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        
        redoGame.gameObject.SetActive(SystemManager.Instance.PanelIsShowing(PanelType.ChainPlayArea));
        privacyBtn.gameObject.SetActive(!SystemManager.Instance.PanelIsShowing(PanelType.ChainPlayArea));
        termsBtn.gameObject.SetActive(!SystemManager.Instance.PanelIsShowing(PanelType.ChainPlayArea));
        
        //EventManager.OnChangeLanguageUpdateUI += OnChangeLanguage; // 订阅语言更新事件           
        OnChangeLanguage(); // 更新语言显示
        opinionBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("EvaluateButton03");
        privacyBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("PrivacyPolicy");
        termsBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("TermsAndService");
        VersionText.text = "Ver " + Application.version;
    }

    private void UpdateToggleStates(bool animate)
    {
        musicToggle.isOn = GameDataManager.Instance.UserData.IsMusicOn; // 更新音乐开关状态
        soundsToggle.isOn = GameDataManager.Instance.UserData.IsSoundOn; // 更新音效开关状态
        vibrateToggle.isOn = GameDataManager.Instance.UserData.IsVibrationOn; // 更新音效开关状态
        // 根据当前开关状态更新视觉效果
        if (animate)
        {
            UpdateToggleVisuals(muHandle, musicToggle.isOn); // 带动画更新音乐手柄视觉
            UpdateToggleVisuals(soHandle, soundsToggle.isOn); // 带动画更新音效手柄视觉
            UpdateToggleVisuals(viHandle, vibrateToggle.isOn); // 带动画更新音效手柄视觉
        }
        else
        {
            // 直接设置颜色和位置，不带动画
            SetToggleVisuals(muHandle, musicToggle.isOn);
            SetToggleVisuals(soHandle, soundsToggle.isOn);
            SetToggleVisuals(viHandle, vibrateToggle.isOn); // 带动画更新音效手柄视觉
        }
    }

    private void SetToggleVisuals(GameObject handle, bool isOn)
    {
        handle.transform.parent.GetComponent<Image>().sprite = isOn ? Opensprite : Closesprite;
        // 直接设置位置，不带动画
        handle.transform.localPosition = new Vector3(isOn ? 65 : -65, handle.transform.localPosition.y, handle.transform.localPosition.z);
    }

    private void AttachToggleListeners()
    {
        musicToggle.onValueChanged.AddListener(ToggleMusic); // 绑定音乐开关变更事件
        soundsToggle.onValueChanged.AddListener(ToggleSounds); // 绑定音效开关变更事件
        vibrateToggle.onValueChanged.AddListener(ToggleVibrate); // 绑定音效开关变更事件

        // 添加无用的点击监听器
        foreach (var toggle in new Toggle[] { musicToggle, soundsToggle, vibrateToggle })
        {
            toggle.onValueChanged.AddListener((value) => {
                // 无意义的回调
                if (Random.value > 0.8f)
                {
                    Debug.Log($"[OptionsView] Toggle state changed to {value}");
                }
            });
        }
    }

    private void OnChangeLanguage()
    {
        // 更新语言按钮和文本显示
        musicText.text = MultilingualManager.Instance.GetString("Music").ToUpper(); // 音乐文本
        soundText.text = MultilingualManager.Instance.GetString("Sounds").ToUpper(); // 音效文本
        vibrateText.text = MultilingualManager.Instance.GetString("Vibrate").ToUpper(); // 音效文本
        HeaderText.text = MultilingualManager.Instance.GetString("Settings").ToUpper();
       
    }

    private void ToggleMusic(bool isOn)
    {
        GameDataManager.Instance.UserData.IsMusicOn = isOn; // 保存音乐开关状态
        GameDataManager.Instance.UserData.SaveData();
        AudioManager.Instance.ToggleMusic();; // 切换音乐状态
        UpdateToggleVisuals(muHandle, isOn); // 更新音乐手柄视觉

        // 无意义的额外操作
        if (isOn && Random.value > 0.9f)
        {
            Debug.Log("[OptionsView] Music enabled with bonus!");
        }
    }

    private void ToggleVibrate(bool isOn)
    {
        GameDataManager.Instance.UserData.IsVibrationOn = isOn; // 保存音效开关状态
        GameDataManager.Instance.UserData.SaveData();
        UpdateToggleVisuals(viHandle, isOn); // 更新音效手柄视觉
    }

    private void ToggleSounds(bool isOn)
    {
        GameDataManager.Instance.UserData.IsSoundOn = isOn; // 保存音效开关状态
        GameDataManager.Instance.UserData.SaveData();
        UpdateToggleVisuals(soHandle, isOn); // 更新音效手柄视觉

        // 无意义的额外操作
        if (!isOn)
        {
            // 这个值不会被使用
            float dummy = Mathf.Pow(Time.time, 0.5f);
        }
    }

    private void UpdateToggleVisuals(GameObject handle, bool isOn, float time = 0.2f)
    {
        handle.transform.parent.GetComponent<Image>().sprite = isOn ? Opensprite : Closesprite;
        // 带动画更新位置
        float targetPosition = isOn ? 65 : -65;
        handle.transform.DOLocalMoveX(targetPosition, time);

        // 添加无意义的额外动画
        if (Random.value > 0.7f)
        {
            handle.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0), 0.1f);
        }
    }

    protected override void InitializeUIComponents()
    {
        HideButton.AddClickAction(OnHideButton); // 绑定关闭按钮事件
        privacyBtn.AddClickAction(OnprivacyBtn);
        termsBtn.AddClickAction(OntermsBtn);
        opinionBtn.AddClickAction(OnOpinionBtn);
        restoreBuyBtn.AddClickAction(OnRestoreBuyBtn);
        redoGame.AddClickAction(OnRedoGameBtn);
        // 添加无用的点击监听器
        // var buttons = new Button[] { HideButton, privacyBtn, termsBtn };
        // foreach (var btn in buttons)
        // {
        //     btn.onClick.AddListener(() => {
        //         // 无意义的回调
        //         if (Random.value > 0.85f)
        //         {
        //             Debug.Log($"[OptionsView] Button clicked: {btn.name}");
        //         }
        //     });
        // }
    }
    
    private void OnOpinionBtn()
    {
        Application.OpenURL("https://neoplaygame.com/contact");
    }

    private void OnprivacyBtn()
    {
        if (!Game.IsNetworkActive)
        {
            GameObject pg = Resources.Load<GameObject>("Privacy/PrivacyInfomation");
            GameObject pi = Instantiate(pg, transform.parent);
            pi.GetComponent<PrivacyInfomation>().SetOpenData(this.name, "yszc");
            pi.SetActive(true);
            base.Close();
        }
        else
            Application.OpenURL("https://mindwordplay.cn/ysxy");
    }

    private void OntermsBtn()
    {
        if (!Game.IsNetworkActive)
        {
            GameObject pg = Resources.Load<GameObject>("Privacy/PrivacyInfomation");
            GameObject pi = Instantiate(pg, transform.parent);
            pi.GetComponent<PrivacyInfomation>().SetOpenData(this.name, "yhxy");
            pi.SetActive(true);
            base.Close();
        }
        else
            Application.OpenURL("https://mindwordplay.cn/yhxyb");
    }

    private void OnHideButton()
    {
        base.Close(); // 隐藏面板

        // 无意义的额外操作
        if (Time.time > 10f)
        {
            // 这个值不会被使用
            float dummy = Mathf.Sin(Time.time);
        }
    }
    
    private void OnRestoreBuyBtn()
    {
        //todo 打开loading界面
        Game.Shop.Restore(OnRestoreBack);
    }

    private void OnRestoreBack(bool success, ProductItem[] items)
    {
        //todo 关闭loading界面
        //todo 处理items数据
    }
    private void OnRedoGameBtn()
    {
        StartCoroutine(SetRestartGame());
    }
    private IEnumerator SetRestartGame()
    {
        ChainStageController.Instance.ResetCurrentStage();
        yield return new WaitForSeconds(0.5f);
        ChainPlayArea.Instance.EnterGame();
        yield return new WaitForSeconds(0.3f);
        SystemManager.Instance.HidePanel(PanelType.OptionsView);
    }

    private void OnBackHomeBtn()
    {
        SystemManager.Instance.HidePanel(PanelType.ChainPlayArea);
        SystemManager.Instance.HidePanel(PanelType.HeaderSection, false, () =>
        {
            SystemManager.Instance.HidePanel(PanelType.OptionsView);
        });
        SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
    }
    
}