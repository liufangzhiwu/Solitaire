using System;
using System.Collections.Generic;
using System.IO;
using Middleware.Runtime.Analytics;
using Newtonsoft.Json;
using UnityEngine;

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
        public CommonErrorType CurrentErrorType { private set; get; }

        private void Awake()
        {
            Instance = this;
            
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<UnityTimer>();
        }

        public void InitGame()
        {
            CreateAccounts();
            CreateAd();
        }
        public void InitManagers()
        {
            CreateAnalytic();
	        GameDataManager.Instance.Init();
	        //AudioManager.Instance.Init();
	        LimitTimeManager.Instance.Init();
        }
        private void CreateAccounts()
        {
            
#if UNITY_ANDROID
            Accounts = new Account_android();
#elif UNITY_huawei
            Accounts = new Account_huaweiandroid();
            Accounts.Init(0.2f);
#elif UNITY_OPENHARMONY
            Accounts = new Account_harmony();
#endif
            Accounts.Init(0.2f);
        }
        private void CreateAd()
        {
    #if UNITY_ANDROID
            Ads = new Ads_android();
    #elif UNITY_IOS
            Ads = new Ads_ios();
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
            Shop = new Shop_android();
    #elif UNITY_IOS
            Shop = new Shop_ios();
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
    }

}

