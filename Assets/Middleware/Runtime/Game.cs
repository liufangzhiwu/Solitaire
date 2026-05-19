using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Middleware.Runtime.Analytics;
using Newtonsoft.Json;
using UnityEngine;
#if UNITY_HUAWEI
using UnityEngine.HuaweiAppGallery;
#endif

namespace Middleware
{
    public enum CommonErrorType
    {
        LoginFail,
        ExitPopup,
    }
    public class Game : MonoBehaviour
    {
        public static Game Instance;
        public static IAds Ads { private set; get; }
        public static IAccounts Accounts { private set; get; }
        public static IAnalytics Analytics { private set; get; }
        public static IShop Shop { private set; get; }
        
       [SerializeField] private Transform uiRoot;
       [HideInInspector] public static bool IsNetworkActive { get; private set; }
        public CommonErrorType CurrentErrorType { private set; get; }

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<UnityTimer>();
#if UNITY_HUAWEI && !UNITY_EDITOR
            HuaweiGameService.AppInit();
#endif
        }
        private IEnumerator Start()
        {
            yield return null;
            // StartCoroutine(CheckNetworkConnection());
        }

        public void InitGame()
        {
            CreateAccounts();
        }
        public IEnumerator InitManagers()
        {
            CreateAd();
            CreateAnalytic();
            yield return new WaitForSeconds(1.2f);
	        GameDataManager.Instance.Init();
	        //AudioManager.Instance.Init();
	        LimitTimeManager.Instance.Init();
        }
        private void CreateAccounts()
        {
            
#if UNITY_ANDROID && !UNITY_HUAWEI
            Accounts = new Account_android();
#elif UNITY_HUAWEI
            Accounts = new Account_huaweiandroid();
#elif UNITY_OPENHARMONY
            Accounts = new Account_harmony();
#else
            Accounts = new Account_android();
#endif
            Accounts.Init(0.01f);
        }
        private void CreateAd()
        {
    #if UNITY_ANDROID && !UNITY_HUAWEI
            Ads = new Ads_android();
    #elif UNITY_HUAWEI 
            Ads = new Ads_huawei();
    #elif UNITY_IOS
            Ads = new Ads_none();
    #elif UNITY_OPENHARMONY
            Ads = new Ads_harmony();
    #endif
            Ads.Init(0.2f);
        }
    
        private void CreateAnalytic()
        {
    #if UNITY_ANDROID
            Analytics = new Analytics_android();
    #elif UNITY_IOS
            Analytics = new Analytics_ios();
    #elif UNITY_OPENHARMONY
            Analytics = new Analytics_harmony();
    #endif
            Analytics.Init(1f);
        }
        
        private void CreateShop()
        {
    #if UNITY_ANDROID
            // Shop = new Shop_harmony();
    #elif UNITY_IOS
            Shop = new Shop_harmony();
    #elif UNITY_OPENHARMONY
            Shop = new Shop_harmony();
    #endif
            Shop.Init(1.5f);
        }
        
        public static void PauseGame()
        {
            Time.timeScale = 0;
            AudioListener.pause = true;
            Ads.IsPlaying = true;
        }
    
        public static void ResumeGame()
        {
            Time.timeScale = 1;
            AudioListener.pause = false; 
            Ads.IsPlaying = false;
        }
        
        public static string GetUniqueId()
        {
#if UNITY_OPENHARMONY
            var filePath = Path.Combine(Application.persistentDataPath, "files", "oaid.txt");
            if (!File.Exists(filePath)) return null;
            return File.ReadAllText(filePath).Trim();
#else
            return SystemInfo.deviceUniqueIdentifier;
#endif
        }
        public void ShowLoginErrorPanel()
        {
            if(uiRoot == null) return;
            
            CurrentErrorType = CommonErrorType.LoginFail;
            GameObject pg = Resources.Load<GameObject>("Privacy/NetErrorView");
            GameObject ps = Instantiate(pg, uiRoot.transform);
            ps.SetActive(true);
        }
        public void ShowQuitGamePanel()
        {
            if(uiRoot == null) return;
            
            CurrentErrorType = CommonErrorType.ExitPopup;
            GameObject pg = Resources.Load<GameObject>("Privacy/NetErrorView");
            GameObject ps = Instantiate(pg, uiRoot.transform);
            ps.SetActive(true);
        }
        
        private IEnumerator CheckNetworkConnection()
        {
            WaitForSeconds wait = new WaitForSeconds(0.5f);
            while (true)
            {
                bool isSuccess = false;
                Ping ping = new Ping("8.8.8.8");
                float timeout = 3.0f;
                float startTime = Time.time;

                // 等待Ping完成或超时
                while (!ping.isDone && Time.time - startTime < timeout)
                {
                    yield return null;
                }

                // 关键修改：明确超时和成功的条件
                if (ping.isDone && ping.time > 0 && ping.time < 2000)
                {
                    isSuccess = true;
                }
                else
                {
                    isSuccess = false;
                }

                // 释放Ping资源（Unity需手动销毁）
                ping.DestroyPing();
                ping = null;

                IsNetworkActive = isSuccess;
                // Debug.Log("PrivacyGuidance 网络状态: " + (IsNetworkActive ? "已连接" : "未连接"));

                yield return wait;
            }
        }
        public  void StopCheckNetCoroutine()
        {
            StopCoroutine(CheckNetworkConnection());
        }
    }

}

