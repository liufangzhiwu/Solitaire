using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Middleware;
using UnityEngine;

public class ConfigManager : MonoBehaviour
{
    private Dictionary<string,string> adjustTable=new Dictionary<string,string>();
    public static ConfigManager Instance;
    [HideInInspector] public GameObject SpineObject;
    [HideInInspector] public GameObject SpineObject2;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }            
    }

    async void Start()
    {
        //等待100毫秒（保证数据初始化成功）
        await Task.Delay(100);
        
        LoadAdjustTable();
        if (SpineObject == null)
        {
            SpineObject = Resources.Load<GameObject>("StageBox");
        }
        if (SpineObject2 == null)
        {
            SpineObject2 = Resources.Load<GameObject>("StageBox2");
        }
#if Unity_ShowLog || UNITY_EDITOR
        Debug.unityLogger.logEnabled = true;
#else
        Debug.unityLogger.logEnabled = false;
#endif
        
        Application.targetFrameRate = 60; // 平台设置为60帧
    }
    
    private void LoadAdjustTable()
    {
        // 从AssetBundle中加载CSV文件
        TextAsset csvFile = AdvancedBundleLoader.SharedInstance.LoadTextFile(ToolUtil.GetLanguageBundle(), "config_gameConfig");
        adjustTable = ToolUtil.ParseCvsLanguage(csvFile,"config_gameConfig");
    }
    
    //根据不同语言找到对应参数
    public string GetString(string key)
    {
        return adjustTable[key] ?? key;
    }       

}