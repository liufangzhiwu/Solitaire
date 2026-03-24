using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// UI系统管理器 - 负责所有UI面板的加载、显示、隐藏和层级管理
/// </summary>
public class SystemManager : MonoBehaviour
{
    #region 枚举和事件定义
    
    /// <summary>
    /// 面板状态枚举
    /// </summary>
    public enum PanelState
    {
        Null,   // 空状态
        Show,   // 显示状态
        Hide    // 隐藏状态
    }

    /// <summary>
    /// UI面板事件委托
    /// </summary>
    public delegate void PanelSystemEventHandler(object sender, PanelEventArgs args);
    
    /// <summary>
    /// UI面板事件参数类
    /// </summary>
    public class PanelEventArgs : EventArgs
    {
        public string PanelName;    // 面板名称
        public PanelState State;    // 面板状态
        public string PanelType;    // 面板类型
    }

    #endregion

    #region 单例实现

    public static SystemManager Instance;
  

    #endregion

    #region 成员变量

    private Dictionary<string, UIWindow> _loadedPanels = new Dictionary<string, UIWindow>(); // 已加载面板字典
    private List<string> _pendingShowPanels = new List<string>(); // 等待显示的面板队列
    private Configuration _panelConfig; // UI配置数据
    public Transform _uiRoot; // UI根节点
    public Camera MainCamera;     // 主摄像机引用

    public event PanelSystemEventHandler PanelEvent; // UI面板事件

    #endregion

    #region 生命周期

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        InitializeUIRoot();
    }

    private void Start()
    {
        InitializePanelEvents();
        LoadPanelConfiguration();
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 显示指定面板
    /// </summary>
    public UIWindow ShowPanel(string panelName)
    {
        if (string.IsNullOrEmpty(panelName)) return null;
        
        if (_loadedPanels.TryGetValue(panelName, out UIWindow panel))
        {
            // 👇 🔥 核心改造 3：捞出来发现肉体已经被 Destroy 了！
            if (panel == null)
            {
                Debug.LogWarning($"[UI修复] 发现被异常销毁的面板: {panelName}，正在清理并重建...");
                _loadedPanels.Remove(panelName); // 把死数据踢出字典
                
                // 此时 panel 是 null，它会自动进入下面的重建 if 分支
            }
        }
        if(panel == null)
        {
            panel = LoadAndInstantiatePanel(panelName);
            if (panel == null) return null;
            
            _loadedPanels[panelName] =  panel;
            InitializePanelInfo(panel, panelName);
        }
        else
        {
            panel.gameObject.SetActive(true);
        }
       
        RaisePanelEvent(panel, PanelState.Show);
        return panel;
    }

    /// <summary>
    /// 隐藏指定面板
    /// </summary>
    public void HidePanel(string panelName, bool useAnimation = true, UnityAction onComplete = null)
    {
        if (!_loadedPanels.ContainsKey(panelName)) return;

        UIWindow panel = _loadedPanels[panelName];
        
        if (onComplete != null)
            panel.AddCloseListener(onComplete);

        if (useAnimation)
            panel.Close();
        else
            panel.OnHideAnimationEnd();
    }

    /// <summary>
    /// 检查指定类型面板是否正在显示
    /// </summary>
    public bool IsPanelTypeShowing(string excludePanel = "")
    {
        bool isShowing = false;
        List<string> deadPanels = null;
        foreach (var kvp in _loadedPanels)
        {
            UIWindow panel = kvp.Value;
            if (panel == null)
            {
                if (deadPanels == null) deadPanels = new List<string>();
                deadPanels.Add(kvp.Key);
                continue;
            }
            if (!string.IsNullOrEmpty(excludePanel) && panel.WindowName == excludePanel) 
                continue;
                
            if (panel.IsWindowVisible && (panel.WindowCategory == UIPanelLayer.UpPopPanel || panel.WindowCategory == UIPanelLayer.PopPanel))
            {
                isShowing = true;
                break; // 找到了就不往下找了，准备去执行下面的清理工作
            }
        }
        if (deadPanels != null)
        {
            foreach (var deadKey in deadPanels)
            {
                _loadedPanels.Remove(deadKey);
            }
        }
        
        return isShowing;
    }

    /// <summary>
    /// 检查指定面板是否正在显示
    /// </summary>
    public bool PanelIsShowing(string panelName)
    {
        return _loadedPanels.ContainsKey(panelName) && 
               _loadedPanels[panelName].IsWindowVisible;
    }

    /// <summary>
    /// 获取指定的面板
    /// </summary>
    public UIWindow GetPanel(string panelName)
    {
        return _loadedPanels.ContainsKey(panelName) ?  _loadedPanels[panelName] : null;
    }
    #endregion

    #region 私有方法

    private void InitializeUIRoot()
    {
        if (_uiRoot == null)
        {
            GameObject rootObj = new GameObject("UIRoot");
            _uiRoot = rootObj.transform;
            DontDestroyOnLoad(rootObj);
        }
    }

    private void LoadPanelConfiguration()
    {
        if (_panelConfig == null)
        {
            _panelConfig = AdvancedBundleLoader.SharedInstance
                .LoadScriptableObject("objects", "InterfaceConfigs") as Configuration;
        }
    }

    private void InitializePanelEvents()
    {
        PanelEvent += (sender, args) =>
        {
            if (args.PanelType == UIPanelLayer.PopPanel.ToString() && 
                args.State == PanelState.Hide)
            {
                HandlePopupPanelHidden(sender as UIWindow);
            }
        };
    }

    private void HandlePopupPanelHidden(UIWindow closedPanel)
    {
        var visiblePopups = new List<UIWindow>();
        
        foreach (var panel in _loadedPanels.Values)
        {
            if (panel.IsWindowVisible&& 
                panel.WindowCategory == UIPanelLayer.PopPanel && 
                panel != closedPanel)
            {
                visiblePopups.Add(panel);
            }
        }

        float delay = 0.1f;
        DOTween.To(() => delay, x => delay = x, 0, 1f).OnComplete(() =>
        {
            if (visiblePopups.Count == 0 && _pendingShowPanels.Count > 0)
            {
                ShowPanel(_pendingShowPanels[0]);
                _pendingShowPanels.RemoveAt(0);
            }
        });
    }

    private UIWindow LoadAndInstantiatePanel(string panelName)
    {
        Debug.Log("正在尝试实例化的面板是: " + panelName);
        var panelData = _panelConfig.GetViewsData(panelName);
        if (panelData.prefab == null)
        {
            AdvancedBundleLoader.SharedInstance.LoadAtlas(
                panelData.spriteAtlasName.ToLower(), 
                panelName);
                
            panelData.prefab = AdvancedBundleLoader.SharedInstance.LoadGameObject(
                panelData.bundleName.ToLower(), 
                panelName);
        }

        if (panelData.prefab == null)
        {
            Debug.LogError($"Failed to load panel: {panelName}");
            return null;
        }
        
        GameObject panelObj = Instantiate(panelData.prefab, _uiRoot);
        return panelObj.GetComponent<UIWindow>();
    }

    private void InitializePanelInfo(UIWindow panel, string panelName)
    {
        if (string.IsNullOrEmpty(panel.WindowName))
            panel.SetWindowName(panelName);
            
        if (panel.WindowCategory == null)
        {
            var panelData = _panelConfig.GetViewsData(panelName);
            panel.SetWindowCategory(panelData.panelLayer);
        }
    }

    private void RaisePanelEvent(UIWindow panel, PanelState state)
    {
        PanelEvent?.Invoke(panel, new PanelEventArgs
        {
            PanelName = panel.WindowName,
            State = state,
            PanelType = panel.WindowCategory.ToString()
        });
    }
  
    #endregion
}