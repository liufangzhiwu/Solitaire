#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Diagnostics; // 用于执行 Python 命令
public class FontExtractTool: EditorWindow
{
    // 你的关卡文件夹路径
    static string sourceDir = "Assets/StreamingAssets/stage_2026_1";
    // === 路径配置 ===
    private string wordsFilePath = "Assets/FourWordIdiom/MultipleData/StageFonts/words.txt";
    private string inputFontPath = "Assets/FourWordIdiom/MultipleData/StageFonts/SourceHanSansSC-Bold.otf"; // 原字体路径
    private string outputFontPath = "Assets/FourWordIdiom/MultipleData/StageFonts/MiniFont.ttf";            // 瘦身后的字体路径

    // === 文本配置 ===
    private string baseChars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ,.:;!?()[]{}<>\"'’‘“”+-*/=%&|^~_@#$￥\\ \u00d7";
    private string uiTexts = "第关步数撤回提示开始游戏设置音效音乐继续重玩退出已完成没有该道具只能连续使用一次成功已返还从牌堆中抽一张牌重置" +
                             "牌堆再试试吧没有找到可行的移动无法在分类牌上方放置只能连接同类卡牌分类牌只能移至空位该分类槽已满步数耗尽游戏结束胜利所" +
                             "有分类已健康游戏忠告抵制不良拒绝盗版注意自我保护谨防受骗上当适度益脑沉迷伤身合理安排时间享受生活";
 
    // 滚动视图位置
    private Vector2 scrollPos;

    [MenuItem("Tools/关卡处理/🔤 字体裁剪工具 (Font Subsetter)")]
    public static void ShowWindow()
    {
        // 弹出窗口
        FontExtractTool window = GetWindow<FontExtractTool>("字体提取与裁剪");
        window.minSize = new Vector2(500, 600);
        window.Show();
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("📁 路径设置", EditorStyles.boldLabel);
        sourceDir = EditorGUILayout.TextField("关卡 JSON 目录:", sourceDir);
        wordsFilePath = EditorGUILayout.TextField("提取字典保存至:", wordsFilePath);
        
        EditorGUILayout.Space();
        GUILayout.Label("🔠 字体文件设置", EditorStyles.boldLabel);
        
        GUILayout.BeginHorizontal();
        inputFontPath = EditorGUILayout.TextField("16MB 原字体路径:", inputFontPath);
        if (GUILayout.Button("浏览", GUILayout.Width(50)))
        {
            string path = EditorUtility.OpenFilePanel("选择原字体", "Assets", "ttf,otf");
            if (!string.IsNullOrEmpty(path)) inputFontPath = FileUtil.GetProjectRelativePath(path);
        }
        GUILayout.EndHorizontal();

        outputFontPath = EditorGUILayout.TextField("输出 Mini 字体路径:", outputFontPath);

        EditorGUILayout.Space();
        GUILayout.Label("📝 额外文本配置", EditorStyles.boldLabel);
        
        GUILayout.Label("基础字符与标点 (Base Characters):");
        baseChars = EditorGUILayout.TextArea(baseChars, GUILayout.Height(60));

        GUILayout.Label("UI 常用字与代码提示语 (UI Texts):");
        uiTexts = EditorGUILayout.TextArea(uiTexts, GUILayout.Height(80));

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("请确保你的电脑已安装 Python，并在终端执行过：pip install fonttools", MessageType.Info);

        EditorGUILayout.Space();
        
        // 核心执行按钮
        // 1. 用稍微柔和一点的绿色，防止亮瞎眼和过曝吞字
        GUI.backgroundColor = new Color(0.2f, 0.75f, 0.2f); 
        
        // 2. 捏一个专属的按钮样式
        GUIStyle bigButtonStyle = new GUIStyle(GUI.skin.button);
        bigButtonStyle.fontSize = 14;                      // 字号调大一点
        bigButtonStyle.fontStyle = FontStyle.Bold;         // 字体加粗
        bigButtonStyle.normal.textColor = Color.white;     // 强制文字变白
        bigButtonStyle.active.textColor = Color.white;     // 按下时也是白色

        // 3. 传入我们捏好的样式 bigButtonStyle
        if (GUILayout.Button("🚀 提取文字并执行 Python 裁剪", bigButtonStyle, GUILayout.Height(40)))
        {
            ExecuteProcess();
        }
        
        // 4. 乖乖恢复默认颜色，防止影响其他 UI
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    private void ExecuteProcess()
    {
        // 1. 提取文字并生成 words.txt
        if (!ExtractWords()) return; // 如果提取失败则终止

        // 2. 检查原字体是否存在
        if (!File.Exists(inputFontPath))
        {
            EditorUtility.DisplayDialog("错误", $"找不到原字体文件，请检查路径：\n{inputFontPath}", "确定");
            return;
        }

        // 3. 执行 Python 裁剪命令
        bool success = RunPythonSubsetter();

        // 4. 刷新 Unity 资源面板
        AssetDatabase.Refresh();

        // 5. 弹窗通知与 Log
        if (success)
        {
            string msg = $"🎉 字体裁剪成功！\n\n字典文件: {wordsFilePath}\nMini 字体: {outputFontPath}";
            UnityEngine.Debug.Log($"<color=#00FF00>{msg}</color>");
            EditorUtility.DisplayDialog("执行完毕", msg, "确定");
            this.Close(); // 执行成功后自动关闭窗口
        }
    }

    private bool ExtractWords()
    {
        HashSet<char> uniqueChars = new HashSet<char>();
        AddCharsToSet(uniqueChars, baseChars);
        AddCharsToSet(uniqueChars, uiTexts);

        if (Directory.Exists(sourceDir))
        {
            FileInfo[] files = new DirectoryInfo(sourceDir).GetFiles("*.json");
            foreach (var f in files)
            {
                AddCharsToSet(uniqueChars, File.ReadAllText(f.FullName));
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning($"关卡目录不存在: {sourceDir}，将只提取 UI 字符。");
        }

        List<char> charList = uniqueChars.ToList();
        charList.Sort();
        string finalString = new string(charList.ToArray());
        
        try
        {
            File.WriteAllText(wordsFilePath, finalString);
            UnityEngine.Debug.Log($"<color=#00FF00>✅ 字库提取完毕！共计提取了 {finalString.Length} 个不重复字符。</color>");
            return true;
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("写入失败", $"无法保存 words.txt 字典文件:\n{e.Message}", "确定");
            return false;
        }
    }

    private void AddCharsToSet(HashSet<char> set, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        foreach (char c in text)
        {
            if (!char.IsControl(c) && !char.IsWhiteSpace(c)) set.Add(c);
        }
    }

    private bool RunPythonSubsetter()
    {
        try
        {
            // 组装 Python 命令，使用 python -m fontTools.subset 能最大限度避免环境变量找不到的问题
            string commandArgs = $"-m fontTools.subset \"{inputFontPath}\" --text-file=\"{wordsFilePath}\" --output-file=\"{outputFontPath}\"";

            ProcessStartInfo psi = new ProcessStartInfo();
            // 在 Mac 上可能需要改成 "python3"
            psi.FileName = Application.platform == RuntimePlatform.OSXEditor ? "python3" : "python"; 
            psi.Arguments = commandArgs;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            Process process = Process.Start(psi);
            string errorOutput = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                UnityEngine.Debug.LogError($"Python 错误:\n{errorOutput}");
                EditorUtility.DisplayDialog("Python 裁剪失败", $"请检查控制台 Log。\n确保已安装 fonttools (pip install fonttools)。\n\n错误信息:\n{errorOutput}", "确定");
                return false;
            }

            return true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"执行 Python 进程时发生异常:\n{e.Message}");
            EditorUtility.DisplayDialog("环境错误", $"无法启动 Python 进程，请确保电脑已安装 Python 并加入环境变量。\n\n{e.Message}", "确定");
            return false;
        }
    }
}
#endif