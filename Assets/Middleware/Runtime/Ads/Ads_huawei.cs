#if UNITY_HUAWEI
using UnityEngine;
using System;
using HuaweiService;
using HuaweiService.ads;

namespace Middleware
{
    public class Ads_huawei : IAds
    {
        public bool IsPlaying { get; set; } //用于判断是否正在展示广告，如果在广告中进入后台则不发送埋点
        private string _uniqueId;
        Define.AdKey _currentAdKey;
        
        private RewardAd _cachedRewardAd;         // 缓存的激励广告对象
        private DateTime _cacheTime;              // 缓存创建的时间
        private bool _isLoadingReward;            // 是否正在加载激励广告（防止重复请求）
        private bool _isShowingReward;            // 是否正在处理展示流程（防止快速点击）
        private bool _isUserWaiting;
        private const double CacheExpireHours = 1.0; // 缓存过期时间（1小时）
        private MRewardAdStatusListener _currentRewardListener;
        public void Init(float delay)
        {
           
            UnityTimer.Delay(delay, () =>
            {
                _uniqueId = Game.GetUniqueId();
                #if !UNITY_EDITOR
                LoadRewardAd(Define.AdKey.RewardAdIdStoreGold, false);
                #endif
            });
        }

        public bool IsReady(Define.AdKey key)
        {
            return IsCacheValid();
        }

        public void ShowReward(Define.AdKey key, Action<bool> callback)
        {
            #if UNITY_EDITOR
            callback.Invoke(true);
            return;
            #endif
            if (_isShowingReward)
            {
                Debug.Log("[Ads_huawei] ShowReward ignored: Action in progress.");
                if (_isLoadingReward && _isUserWaiting)
                {
                    MessageSystem.Instance.ShowTip("广告加载中, 请稍等");
                }
                return;
            }
            IsPlaying = true;
            _isShowingReward = true;
            _isUserWaiting = true;
            _currentAdKey = key;
            _completeCallback = callback;

            if (IsCacheValid())
            {
                Debug.Log("[Ads_huawei] ShowReward: Cache Hit.");
                ShowCacheAd();
            }
            else
            {
                Debug.Log("[Ads_huawei] ShowReward: Cache Miss. Loading new ad.");
                // 缓存无效或不存在，发起加载并自动播放
                MessageSystem.Instance.ShowLoadingAnimation();
                LoadRewardAd(key, true);
            }
       
        }

        private void LoadRewardAd(Define.AdKey key, bool autoShow)
        {
            if (_isLoadingReward) return;
            if (!autoShow && IsCacheValid()) return;

            _isLoadingReward = true;
            _currentAdKey = key;
                 
            RewardAd ad = new RewardAd(new Context(), GetAdId(key));
            ad.setMobileDataAlertSwitch(false);
            AdParam adParam = new AdParam.Builder().build();
            
            MRewardLoadListener listener = new MRewardLoadListener(this,ad, autoShow);
            ad.loadAd(adParam, listener);
        }

        public void ShowInterstitial(Action<bool> callback)
        {
            #if UNITY_EDITOR
            callback?.Invoke(true);
            return;
            #endif
            _completeCallback = callback;
            InterstitialAd ad = new InterstitialAd(new Context());
            ad.setAdId(GetAdId(Define.AdKey.InterstitialAdId));
            ad.setAdListener(new MAdListener(ad, _completeCallback));
            ad.loadAd(new AdParam.Builder().build());
        }

        public void LoadBannerAD()
        {
            Debug.Log("华为的安卓没有实现横幅广告");
        }


        public void ShowBanner()
        {
            // 未实现的功能
            return;
        }

        public void HideBanner()
        {
            if(!_isBannerShow) return;
            _isBannerShow = false;
        }
        
        #region 通用逻辑
        private Action<bool> _completeCallback;
        private bool _isBannerShow;
        
        private string GetAdId(Define.AdKey key)
        {
            var adId = "";
            switch (key)
            {
                case Define.AdKey.InterstitialAdId:
                    adId = Define.ConfigHuaweiAndroid.TestInterstitialAdId;
                    break;
                default:
                    adId = Define.ConfigHuaweiAndroid.TestRewardAdId;
                    break;
            }
            return adId;
        }

        private bool IsCacheValid()
        {
            if (_cachedRewardAd == null) return false;
            if ((DateTime.Now - _cacheTime).TotalHours >= CacheExpireHours)
            {
                Debug.Log("[Ads_huawei] reward ad expired");
                _cachedRewardAd = null;
                return false;
            }
            return true;
        }

        private void ShowCacheAd()
        {
            if (_cachedRewardAd == null)
            {
                LoadRewardAd(_currentAdKey, true);
                return;
            }

            RewardAd ad = _cachedRewardAd;
            _cachedRewardAd = null;
            _currentRewardListener = new MRewardAdStatusListener(this, _completeCallback);
            ad.show(new Context(), _currentRewardListener);
        }
        // 加载成功回调
        private void OnRewardAdLoaded(RewardAd ad, bool autoShow)
        {
            _isLoadingReward = false;
            
            // 更新缓存
            _cachedRewardAd = ad;
            _cacheTime = DateTime.Now;

            if (autoShow || _isUserWaiting)
            {
                ShowCacheAd();
            }
            else
            {
                Debug.Log("[Ads_huawei] Preload success.");
            }
        }

        // 加载失败回调
        private void OnRewardAdFailedToLoad(int errorCode)
        {
            _isLoadingReward = false;

            // 如果是准备播放时失败，需要回调给业务层并重置展示状态
            if (_isUserWaiting)
            {
                IsPlaying = false;
                _isShowingReward = false;
                _isUserWaiting = false;
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    MessageSystem.Instance.HideLoadingAnimation();
                    UnityTimer.Delay(0.2f, () => 
                    {
                        MessageSystem.Instance.ShowTip("广告加载失败, 请稍后重试.");
                        _completeCallback?.Invoke(false);
                        _completeCallback = null;
                    });
                }
                );
            }
        }

        // 广告打开回调（在此处预加载下一个视频）
        private void OnAdOpened()
        {
            _isUserWaiting = false;
            // 注意：此时 autoShow = false，仅进行预加载
            LoadRewardAd(_currentAdKey, false);
        }

        // 广告关闭或展示失败回调
        private void OnAdFinishedOrClosed()
        {
            IsPlaying = false;
            Debug.Log("[Ads_huawei] OnAdFinishedOrClosed "+IsPlaying);
            _isShowingReward = false; // 重置展示状态，允许下一次点击
            _isUserWaiting = false;
        }
        #endregion
        private class MRewardLoadListener : RewardAdLoadListener
        {
            private Ads_huawei _parent;
            private RewardAd _ad;
            private bool _autoShow;

            public MRewardLoadListener(Ads_huawei parent, RewardAd ad, bool autoShow)
            {
                this._parent = parent;
                this._ad = ad;
                this._autoShow = autoShow;
            }

            public override void onRewardAdFailedToLoad(int errorCode)
            {
                Debug.Log($"[MRewardLoadListener]RewardAdFailedToLoad errorCode:{errorCode}");
                _parent.OnRewardAdFailedToLoad(errorCode);
            }
            
            public override void onRewardedLoaded()
            {
                Debug.Log($"[MRewardLoadListener]RewardedLoaded ...");
                _parent.OnRewardAdLoaded(_ad, _autoShow);
            }
        }
        
        private class MRewardAdStatusListener : RewardAdStatusListener
        {
            private readonly Ads_huawei _parent;
            private Action<bool> _callback;
            private bool _hasRewarded = false; // 【关键修改】新增标志位
            
            private const string TAG = "[HuaweiAdsListener]";
            
            public MRewardAdStatusListener(Ads_huawei parent, Action<bool> callback)
            {
                this._parent = parent;
                this._callback = callback;
                this._hasRewarded = false; // 初始化
                Debug.Log($"{TAG} Listener created. Callback valid? {callback != null}");
            }
            public override void onRewardAdOpened()
            {
                _parent.OnAdOpened();
                // base.onRewardAdOpened();
                // MessageSystem.Instance.ShowTip($"[激励广告被打开]RewardAdOpened show");
                Debug.Log($"{TAG} onRewardAdOpened | ThreadID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            }
            public override void onRewardAdClosed()
            {
                _parent.OnAdFinishedOrClosed();
                int tid = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Debug.Log($"{TAG} onRewardAdClosed called | ThreadID: {tid}");
                
               
                Debug.Log($"{TAG} Enqueueing Close callback (false)...");
                //可以领取奖励关闭回调
                // base.onRewardAdClosed();
                // MessageSystem.Instance.ShowTip($"[激励广告被关闭]RewardAdClosed");
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    Debug.Log($"{TAG} [MainThread] Executing Close logic. | ThreadID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                    if (MessageSystem.Instance != null)
                    {
                        MessageSystem.Instance.HideLoadingAnimation();
                    }
                    else
                    {
                        Debug.LogError($"{TAG} MessageSystem.Instance is NULL!");
                    }
                    UnityTimer.Delay(0.5f, () =>
                    {
                        Action<bool> tempCallback = _callback;
                        if (_callback != null)
                        {
                            if (_hasRewarded)
                            {
                                tempCallback.Invoke(true);
                                Debug.Log($"{TAG} 结算：正常看完，发放奖励！");
                            }
                            else
                            {
                                MessageSystem.Instance?.ShowTip("广告播放中断!");
                                tempCallback.Invoke(false);
                                Debug.Log($"{TAG} 结算：玩家中途关闭，未发奖。");
                            }
                            _callback = null; // 确保引用释放
                        }
                        else
                        {
                            Debug.LogWarning($"{TAG} Callback is NULL, cannot invoke.");
                        }
                    });
                });
            }
            public override void onRewardAdFailedToShow(int arg0)
            {
                _parent.OnAdFinishedOrClosed();
                Debug.LogError($"{TAG} onRewardAdFailedToShow | ErrorCode: {arg0} | ThreadID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                // base.onRewardAdFailedToShow(arg0);
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    Debug.Log($"{TAG} [MainThread] Executing Failed logic.");
                    MessageSystem.Instance.HideLoadingAnimation();
                    UnityTimer.Delay(0.2f, () =>
                    {
                        MessageSystem.Instance.ShowTip("广告加载失败, 请稍后重试.");
                        _callback?.Invoke(false);
                        _callback = null;
                    });
                }
                );
                // MessageSystem.Instance.ShowTip($"[激励广告展示失败] RewardAdFailedToShow errorCode:{arg0}");
            }
            public override void onRewarded(Reward arg0)
            {
                // 标记已经获得奖励
                _hasRewarded = true;
                Debug.Log($"{TAG} onRewarded | Reward: | ThreadID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                // base.onRewarded(arg0);
                // MessageSystem.Instance.ShowTip($"[激励广告完成] RewardAdFailedToShow errorCode:{arg0}");
                // UnityMainThreadDispatcher.Instance().Enqueue(() =>
                // {
                //     Debug.Log($"{TAG} [MainThread] Executing Rewarded logic (true).");
                //     MessageSystem.Instance.HideLoadingAnimation();
                //     if (_callback != null)
                //     {
                //         _callback.Invoke(true); // 成功发放奖励
                //         _callback = null;       // 发放后安全置空
                //         Debug.Log($"{TAG} Callback(true) invoked.");
                //     }
                //     // _callback = null; // 【防御性编程】调用后置空，防止后续重复调用
                // }
                // );
            }
        }
        
        private class MAdListener : AdListener
        {
            private readonly InterstitialAd _ad;
            private readonly Action<bool> _callback;
            public MAdListener(InterstitialAd ad, Action<bool> callback = null): base()
            {
                this._ad = ad;
                this._callback = callback;
            }

            public override void onAdClicked()
            {
                // base.onAdClicked();
                // MessageSystem.Instance.ShowTip("AdListener Ad Clicked");
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    MessageSystem.Instance.HideLoadingAnimation();
                });
            }

            public override void onAdClosed()
            {
                // base.onAdClosed();
                // MessageSystem.Instance.ShowTip("AdListener Ad Closed");
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    //_callback?.Invoke(false);
                    MessageSystem.Instance.HideLoadingAnimation();
                });
            }

            public override void onAdFailed(int arg0)
            {
                // MessageSystem.Instance.ShowTip("AdListener Ad failed to load with error code "+ arg0);
                // base.onAdFailed(arg0);
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    MessageSystem.Instance.HideLoadingAnimation();
                    _callback?.Invoke(false);
                });
            }

            public override void onAdImpression()
            {
                // base.onAdImpression();
                // MessageSystem.Instance.ShowTip("AdListener onAdImpression");
            }

            public override void onAdLeave()
            {
                // base.onAdLeave();
                // MessageSystem.Instance.ShowTip("AdListener Ad Leave");
            }

            public override void onAdLoaded()
            {
                // base.onAdLoaded();
                 // MessageSystem.Instance.ShowTip("AdListener onAdLoaded");
                _ad.show(new Context());
            }

            public override void onAdOpened()
            {
                // base.onAdOpened();
                // MessageSystem.Instance.ShowTip("AdListener Ad Opened");
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    MessageSystem.Instance.HideLoadingAnimation();
                    _callback?.Invoke(true);
                });
            }
        }
    }
}
#endif