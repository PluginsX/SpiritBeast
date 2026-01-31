using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.IO;

// 引入 Windows Forms 依赖
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
#endif

/// <summary>
/// 托盘应用管理器 (深色极简主题版)
/// </summary>
public class PalletProgramManager : MonoBehaviour
{
    [System.Serializable]
    public class TrayMenuItem
    {
        public string menuText = "菜单项";
        public UnityEvent onClickEvent;
    }

    [Header("核心设置")]
    public bool showInTray = true;
    
    [Tooltip("图标文件名 (Assets/StreamingAssets/ 下的文件名)")]
    public string iconFileName = "icon.ico";
    public string hoverTooltip = "我的应用";

    [Header("交互配置")]
    public UnityEvent onDoubleClickIcon;
    public List<TrayMenuItem> rightClickMenu = new List<TrayMenuItem>();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
    private NotifyIcon _notifyIcon;
    private ContextMenuStrip _contextMenu;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DestroyIcon(System.IntPtr hIcon);
#endif

    private void Start()
    {
        UnityMainThreadDispatcher.Instance();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (showInTray) InitializeTrayIcon();
#endif
    }

    private void OnApplicationQuit() => DisposeTrayIcon();
    private void OnDestroy() => DisposeTrayIcon();

    public void InitializeTrayIcon()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        try
        {
            if (_notifyIcon == null) _notifyIcon = new NotifyIcon();

            _notifyIcon.Icon = LoadIconFromStreamingAssets();
            _notifyIcon.Text = hoverTooltip;
            _notifyIcon.Visible = true;

            _notifyIcon.DoubleClick += (s, e) => 
                UnityMainThreadDispatcher.Instance().Enqueue(() => onDoubleClickIcon?.Invoke());

            BuildContextMenu();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PalletManager] 初始化失败: {e.Message}");
        }
#endif
    }

    private void BuildContextMenu()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        _contextMenu = new ContextMenuStrip();
        
        // --- 1. 应用深色主题渲染器 ---
        _contextMenu.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
        
        // --- 2. 样式微调 ---
        _contextMenu.ShowImageMargin = false; // 去除左侧图标栏，实现极简对齐
        _contextMenu.BackColor = System.Drawing.Color.FromArgb(45, 45, 48); // 背景深灰
        _contextMenu.ForeColor = System.Drawing.Color.White;             // 全局字体白色
        _contextMenu.ShowCheckMargin = false;             // 去除勾选栏
        
        // 字体设置 (可选，使用系统默认无衬线字体)
        _contextMenu.Font = new System.Drawing.Font("Segoe UI", 9F);

        foreach (var item in rightClickMenu)
        {
            if (string.IsNullOrEmpty(item.menuText)) continue;
            
            var menuItem = new ToolStripMenuItem(item.menuText);
            var evt = item.onClickEvent;

            // 确保每个 Item 继承字体颜色 (WinForms 有时会重置)
            menuItem.ForeColor = System.Drawing.Color.White; 
            
            menuItem.Click += (s, e) => 
                UnityMainThreadDispatcher.Instance().Enqueue(() => evt?.Invoke());
            
            _contextMenu.Items.Add(menuItem);
        }

        // 分割线样式
        ToolStripSeparator sep = new ToolStripSeparator();
        sep.ForeColor = System.Drawing.Color.FromArgb(80, 80, 80); // 深色分割线
        _contextMenu.Items.Add(sep);

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.ForeColor = System.Drawing.Color.White;
        exitItem.Click += (s, e) => UnityMainThreadDispatcher.Instance().Enqueue(() => {
            DisposeTrayIcon();
            UnityEngine.Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        });
        _contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = _contextMenu;
#endif
    }

    // ... (LoadIconFromStreamingAssets 代码保持不变，省略以节省篇幅，请保留之前的版本) ...
    private
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
    System.Drawing.Icon
#else
    object
#endif
    LoadIconFromStreamingAssets()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        string fullPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, iconFileName);
        if (!File.Exists(fullPath)) return GetDefaultIcon();
        try {
            if (fullPath.EndsWith(".ico", System.StringComparison.OrdinalIgnoreCase)) return new System.Drawing.Icon(fullPath);
            else {
                using (var bitmap = new Bitmap(fullPath)) {
                    System.IntPtr hIcon = bitmap.GetHicon();
                    var icon = System.Drawing.Icon.FromHandle(hIcon);
                    var safeIcon = (System.Drawing.Icon)icon.Clone();
                    DestroyIcon(hIcon);
                    return safeIcon;
                }
            }
        } catch { return GetDefaultIcon(); }
#else
        return null;
#endif
    }

    private
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
    System.Drawing.Icon
#else
    object
#endif
    GetDefaultIcon()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        try {
            if (UnityEngine.Application.isEditor) return System.Drawing.SystemIcons.Application;
            var process = System.Diagnostics.Process.GetCurrentProcess();
            return System.Drawing.Icon.ExtractAssociatedIcon(process.MainModule.FileName);
        } catch { return System.Drawing.SystemIcons.Application; }
#else
        return null;
#endif
    }

    public void DisposeTrayIcon()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (_notifyIcon != null) { _notifyIcon.Visible = false; _notifyIcon.Dispose(); _notifyIcon = null; }
        if (_contextMenu != null) { _contextMenu.Dispose(); _contextMenu = null; }
#endif
    }

    // ==========================================
    // 自定义深色主题渲染表
    // ==========================================
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
    public class DarkColorTable : ProfessionalColorTable
    {
        // 菜单背景色 (深灰)
        public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(45, 45, 48);

        // 鼠标悬停时的背景色 (稍亮一点的灰)
        public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.FromArgb(62, 62, 66);

        // 鼠标悬停时的边框色 (与背景同色，实现扁平化)
        public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.FromArgb(62, 62, 66);

        // 菜单边框色 (黑色)
        public override System.Drawing.Color MenuBorder => System.Drawing.Color.Black;
        
        // 鼠标按下时的背景色
        public override System.Drawing.Color MenuItemPressedGradientBegin => System.Drawing.Color.FromArgb(62, 62, 66);
        public override System.Drawing.Color MenuItemPressedGradientEnd => System.Drawing.Color.FromArgb(62, 62, 66);

        // 屏蔽掉左侧图标栏的渐变 (使其纯色)
        public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.FromArgb(45, 45, 48);
        public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.FromArgb(45, 45, 48);
        public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.FromArgb(45, 45, 48);
        
        // 分割线颜色
        public override System.Drawing.Color SeparatorDark => System.Drawing.Color.FromArgb(80, 80, 80);
        public override System.Drawing.Color SeparatorLight => System.Drawing.Color.FromArgb(45, 45, 48); // 隐藏高光
    }
#endif
}

// 线程调度器
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private static readonly Queue<System.Action> _executionQueue = new Queue<System.Action>();

    public static UnityMainThreadDispatcher Instance()
    {
        if (!_instance) {
            _instance = FindObjectOfType<UnityMainThreadDispatcher>();
            if (!_instance) {
                var obj = new GameObject("UnityMainThreadDispatcher");
                _instance = obj.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(obj);
            }
        }
        return _instance;
    }

    public void Enqueue(System.Action action) { lock (_executionQueue) { _executionQueue.Enqueue(action); } }

    void Update() {
        lock (_executionQueue) {
            while (_executionQueue.Count > 0) _executionQueue.Dequeue().Invoke();
        }
    }
}