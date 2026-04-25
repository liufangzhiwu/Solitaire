using System.Collections.Generic;

/// <summary>
/// 游戏核心配置（修改版，避免被识别为重复应用）
/// </summary>
public static class AppGameSettings
{
    // ===== 经济系统配置 =====
    public static int StartingGold { get; } = 100;
    public static int FirstLevel { get; } = 1;
//#if UNITY_OPENHARMONY
    public static string SystemLanguage { get; } = "ChineseSimplified";
//#else
    //public static string SystemLanguage { get; } = "Japanese";
//endif

    // ===== 关卡奖励 =====
    public static int LevelCompleteBonus { get; } = 10;  

    // ===== 道具商店 =====
    public static class ShopItems 
    {
        public static int HintCost { get; } = 80;        // 提示开销
        public static int StartingHints { get; } = 1;     // 提示初始值
        public static int UndoCost { get; } = 50;       // 撤回开销
        public static int StartingUndoes { get; } = 1;    // 初始撤回数量
        
        public static int MagicBangCost { get; } = 50;       // 魔法棒开销
        public static int StartingMagicBangs { get; } = 1;       // 魔法棒开销
        
        public static int ResetCost { get; } = 200;       
        public static int StartingResets { get; } = 1;   
        public static int AutoCompleteCost { get; } = 150;   
        public static int StartingButterflies { get; } = 0; 
    }

    // ===== 游戏机制开关 =====
    public static bool EnableComboButterflies { get; } = false;
    public static int MaxButterfliesPerLevel { get; } = 2;
    
    //每日任务中无限使用蝴蝶道具时长（分钟）
    public static int TaskButterflyUseTime { get; } = 60;     
    //竞速目标词语数量
    public static int FishTargetWordCount { get; } = 100;   

    // ===== 关卡循环设置 =====
    public static int LoopLevelStart { get; } = 100;     

    // ===== 功能解锁关卡 =====
    public static class UnlockRequirements 
    {
        public static int TimeLimitMode { get; } = 19;   
        public static int SignInRewards { get; } = 16;   
        public static int DailyMissions { get; } = 26;   
        //30关卡进入结算界面时开启(鲤鱼跃龙门活动)
        public static int FishOpenLevel { get; } = 31;
        //10关卡进入结算界面时开启（命名界面）
        public static int HeadOpenLevel { get; } = 11;
        public static int ZenOpenLevel { get; } = 11; // 11关卡开启禅模式
    }

    // ===== 任务系统 =====
    //每日任务中无限使用蝴蝶道具时长（分钟）
    public static int UnlimitedButterflyDuration { get; } = 60;  // 分钟
    /// <summary>
    /// 是否开启保留剩余任务进度
    /// </summary>
    public static bool SaveMissionProgress { get; } = false;    
    /// <summary>
    /// 是否开启保留在线时长剩余任务进度
    /// </summary>
    public static bool SaveOnlineTimeProgress { get; } = true;   

    // ===== 进度系统 =====
    public static List<int> ProgressMilestones { get; } = new List<int>() 
    {
        5, 5, 10, 10, 10, 10, 20
    };

    // ===== 设备白名单 =====
    public static HashSet<string> AuthorizedDevices { get; } = new HashSet<string>() 
    {
        // 示例设备ID（实际使用时建议加密存储）
        "",
        // ...（其余ID保持不变）
    };
    
    public static int E1_INTERVENTION { get; } = 8;  // 第1关介入的E值
  
}