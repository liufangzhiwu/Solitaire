using System;
using System.Collections;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RewardAdsScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Text title; // 音效文本显示
    [SerializeField] private Text count; // 语言选择文本显示
    [SerializeField] private Image AwardIcon; // 语言选择文本显示
    [SerializeField] private Image adsloading;
    [SerializeField] private Image adsIcon;
    [SerializeField] private Button ClaimBtn;
    [SerializeField] private Button ClaimAdsBtn;

    private bool isCanClaim = false;

    protected override void OnEnable()
    {
        base.OnEnable();
        UpdateCliamBtn(false);
        InitUI();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        EventDispatcher.Instance.TriggerUpdateLayerCoin(true, false);
    }

    private void InitUI()
    {
        title.text = MultilingualManager.Instance.GetString("ADPopTitle");
        ClaimAdsBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ADPopWatch");
        ClaimBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ADPopReceive");
        count.text = ""+ GameDataManager.Instance.UserData.ABseeAdsRewardCoins;
        adsloading.gameObject.SetActive(false);
        adsIcon.gameObject.SetActive(true);
        StartCoroutine(CheckIsReadyToShowAd());

        AnalyticMgr.VideoAdShow("金币弹窗广告");
    }

    IEnumerator CheckIsReadyToShowAd()
    {
        const float checkInterval = 2f;
        const int maxAttempts = 10; // 防止无限循环

        // 初始状态检查
        bool isReady = Game.Ads.IsReady(Define.AdKey.RewardAdIdStoreGold);
        bool isConnected = GameCoreManager.Instance.IsNetworkActive;

        // 立即更新UI状态
        adsIcon.gameObject.SetActive(isReady);
        adsloading.gameObject.SetActive(!isReady);

        // 如果没有网络连接，直接退出
        if (!isConnected)
        {
            yield break;
        }

        // 轮询检查
        int attempt = 0;
        while (attempt < maxAttempts && isConnected && !isReady)
        {
            yield return new WaitForSeconds(checkInterval);

            attempt++;
            isReady = Game.Ads.IsReady(Define.AdKey.RewardAdIdStoreGold);
            isConnected = GameCoreManager.Instance.IsNetworkActive;

            // 状态变化处理
            if (isReady && isConnected)
            {
                adsloading.gameObject.SetActive(false);
                yield break;
            }
        }

        // 立即更新UI状态
        adsIcon.gameObject.SetActive(isReady);
        adsloading.gameObject.SetActive(!isReady);

        // 可选：超过最大尝试次数的处理
        if (!isReady)
        {
            Debug.LogWarning($"广告加载超时，最大尝试次数 {maxAttempts} 次");
            // 可以在这里触发备用广告加载或错误处理
        }
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddClickAction(GetRewardClose); // 绑定关闭按钮事件
        ClaimAdsBtn.AddClickAction(ClickClaimBtn);
        ClaimBtn.AddClickAction(GetRewardClose);
    }

    private void ClickClaimBtn()
    {
        //FirebaseManager.Instance.VideoAdClick("商店弹窗",SaveSystem.Instance.UserData.CurrentStage.ToString());
        AnalyticMgr.VideoAdClick("金币弹窗广告");

        Game.Ads.ShowReward(Define.AdKey.RewardAdIdStoreGold, UpdateAdsRewardUI);
    }

    private void UpdateAdsRewardUI(bool isClaimed)
    {
        if (isClaimed)
        {
            UpdateCliamBtn(true);
            AnalyticMgr.VideoAdSuccess("金币弹窗广告");
            GameDataManager.Instance.UserData.totalSeeAds++;
            // DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedSeeAds,1);
        }
        else
        {
            MessageSystem.Instance.ShowTip("广告失败");
            AnalyticMgr.VideoAdFail("金币弹窗广告");
            //FirebaseManager.Instance.VideoFail("商店弹窗",SaveSystem.Instance.UserData.CurrentStage.ToString());
        }
    }

    private void UpdateCliamBtn(bool canClaimed)
    {
        isCanClaim = canClaimed;
        ClaimAdsBtn.gameObject.SetActive(!isCanClaim);
        ClaimBtn.gameObject.SetActive(isCanClaim);
    }

    IEnumerator GetAdsReward()
    {
        ClaimBtn.interactable = false;
        closeBtn.interactable = false;
        if (isCanClaim)
        {
            CustomFlyInManager.Instance.FlyInGold(AwardIcon.transform, () =>
            {
                int coins = GameDataManager.Instance.UserData.ABseeAdsRewardCoins;
                EventDispatcher.Instance.TriggerChangeGoldUI(coins, true);
                GameDataManager.Instance.UserData.UpdateGold(coins, true, true, "金币广告弹窗获得");
            });
            isCanClaim = false;

            yield return new WaitForSeconds(0.8f);
        }

        base.Close(); // 隐藏面板
    }


    private void GetRewardClose()
    {
        StartCoroutine(GetAdsReward());
    }

    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ClaimBtn.interactable = true;
        closeBtn.interactable = true;
        if (SystemManager.Instance.IsPanelTypeShowing())
            EventDispatcher.Instance.TriggerUpdateLayerCoin(true, true);
        else
        {
            EventDispatcher.Instance.TriggerUpdateLayerCoin(false, true);
        }
    }
}