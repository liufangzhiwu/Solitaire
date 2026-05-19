namespace Middleware
{
    public partial class Define 
    {
        #if UNITY_ANDROID && Unity_ShowLog
        public struct ConfigAndroid
        {
            public const string TestBannerAdId = "ca-app-pub-3940256099942544/6300978111";
            public const string TestInterstitialAdId = "ca-app-pub-3940256099942544/1033173712";
            public const string TestRewardAdId = "ca-app-pub-3940256099942544/5224354917";
        }
        
        public struct ConfigHuaweiAndroid
        {
            public const string TestBannerAdId = "ca-app-pub-3940256099942544/6300978111";
            public const string TestInterstitialAdId = "testb4znbuh3n2";
            public const string TestRewardAdId = "testx9dtjwj8hp";
        }
        #endif
        
        #if UNITY_ANDROID && Unity_Release
        public struct ConfigHuaweiAndroid
        {
            public const string TestBannerAdId = "ca-app-pub-3940256099942544/6300978111";
            public const string TestInterstitialAdId = "p5qv59ukvu";
            public const string TestRewardAdId = "x4vj8lndo4";
        }
        #endif 
        
        #if UNITY_IOS
        public struct ConfigIOS
        {
            public const string TestBannerAdId = "ca-app-pub-3940256099942544/2934735716";
            public const string TestInterstitialAdId = "ca-app-pub-3940256099942544/4411468910";
            public const string TestRewardAdId = "ca-app-pub-3940256099942544/1712485313";
        }
        #endif
        
        #if UNITY_OPENHARMONY
        public struct ConfigHarmony
        {
            public const string TestBannerAdId = "c7k25kjsxd";
            public const string TestInterstitialAdId = "f874la36yq";
            public const string TestRewardAdId = "h9ekpys8y7";
        }
        #endif
        
        public struct Config
        {
            public const string ThinkAppId = "2c3fcbfea2ff4842b9e526a7f2e4ee64";
            public const string ThinkServerUrl = "https://receiver.ta.thinkingdata.cn";
            public const string SingleObjName = "SingleObj";
        }
        
        
    }
}

