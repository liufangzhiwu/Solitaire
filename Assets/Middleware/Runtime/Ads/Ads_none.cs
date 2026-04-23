using System;
using UnityEngine;

namespace Middleware
{
    public class Ads_none : IAds
    {
        public bool IsPlaying { get; set; }

        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                SetTestDeviceIds();
                InitializeGoogleMobileAds();
            });
        }

        public bool IsReady(Define.AdKey key)
        {
            // return _rewardedAds.ContainsKey(key) && _rewardedAds[key].CanShowAd();
            return true;
        }

        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
            callback(true);
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            callback(true);
        }

        public void ShowBanner()
        {
            Debug.Log("Show the banner AD view.");
        }

        public void HideBanner()
        {
        }

        
        #region 通用逻辑

        private void SetTestDeviceIds()
        {
        }

        private void InitializeGoogleMobileAds()
        {
        }
        

        public void LoadBannerAD()
        {
        }
        

        #endregion

        #region 插屏广告
        private int _retryCountI;
        private long _retryTimerI;

        private void LoadInterstitialAd()
        {
        }
        
        #endregion
        
        private void LoadRewardedAds()
        {
            // _rewardedAds = new Dictionary<Define.AdKey, RewardedAd>();
            var enumKeys = Enum.GetNames(typeof(Define.AdKey));
            foreach (var key in enumKeys)
            {
                if(!key.StartsWith("RewardAd"))
                    continue;
                LoadRewardAd((Define.AdKey)Enum.Parse(typeof(Define.AdKey), key));
            }
        }

        private void LoadRewardAd(Define.AdKey key)
        {
        }
    }
}