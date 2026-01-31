using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

// 引入 Windows Forms 依赖
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System.Drawing;
using System.Windows.Forms;
#endif

/// <summary>
/// 托盘与任务栏管理器 (集成深色菜单、流式资源加载、任务栏控制)
/// </summary>
public class PalletProgramManager : MonoBehaviour
{
    [System.Serializable]
    public class TrayMenuItem
    {
        public string menuText = "菜单项";
        public UnityEvent onClickEvent;
    }

    // ========================================================================
    // 配置区域
    // ========================================================================
    [Header("1. 托盘设置 (Tray)")]
    [Tooltip("是否在右下角托盘显示图标")]
    public bool showInTray = true;
    [Tooltip("托盘图标文件名 (StreamingAssets下)")]
    public string trayIconFileName = "icon.ico";
    public string hoverTooltip = "我的应用";

    [Header("2. 任务栏设置 (Taskbar)")]
    [Tooltip("是否在下方任务栏显示标签")]
    public bool showInTaskbar = true;
    [Tooltip("任务栏图标文件名 (StreamingAssets下)。\n留空则与托盘图标一致。")]
    public string taskbarIconFileName = ""; 

    [Header("3. 交互配置")]
    public UnityEvent onDoubleClickIcon;
    public List<TrayMenuItem> rightClickMenu = new List<TrayMenuItem>();

    // ========================================================================
    // 内部变量
    // ========================================================================
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
    private NotifyIcon _notifyIcon;
    private ContextMenuStrip _contextMenu;
    private System.Drawing.Icon _taskbarIconRef; // 保持引用防止GC回收

    // --- Windows API 声明 ---
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DestroyIcon(System.IntPtr hIcon);

    [DllImport("user32.dll")]
    static extern System.IntPtr GetActiveWindow();

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    static extern int SetWindowLong32(System.IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    static extern System.IntPtr SetWindowLongPtr64(System.IntPtr hWnd, int nIndex, System.IntPtr dwNewLong);

    [DllImport("user32.dll")]
    static extern System.IntPtr GetWindowLong(System.IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    static extern System.IntPtr SendMessage(System.IntPtr hWnd, int Msg, System.IntPtr wParam, System.IntPtr lParam);

    // 常量定义
    const int GWL_EXSTYLE = -20;
    const int WS_EX_APPWINDOW = 0x00040000;  // 强制显示在任务栏
    const int WS_EX_TOOLWINDOW = 0x00000080; // 工具窗口(不显示在任务栏)
    
    const int WM_SETICON = 0x0080;
    const int ICON_SMALL = 0;
    const int ICON_BIG = 1;
#endif

    private void Start()
    {
        UnityMainThreadDispatcher.Instance();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        // 1. 初始化托盘
        if (showInTray) InitializeTrayIcon();

        // 2. 初始化任务栏配置 (图标 + 显隐)
        ApplyTaskbarSettings();
#endif
    }

    private void OnApplicationQuit() => DisposeTrayIcon();
    private void OnDestroy() => DisposeTrayIcon();

    // ========================================================================
    // 任务栏管理 (Taskbar Logic)
    // ========================================================================
    public void ApplyTaskbarSettings()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // 编辑器模式下修改窗口样式会导致编辑器异常，仅在打包后执行
        System.IntPtr hwnd = GetActiveWindow();
        if (hwnd == System.IntPtr.Zero) return;

        // --- A. 设置任务栏图标 ---
        string iconName = string.IsNullOrEmpty(taskbarIconFileName) ? trayIconFileName : taskbarIconFileName;
        _taskbarIconRef = LoadIconFromStreamingAssets(iconName);
        
        if (_taskbarIconRef != null)
        {
            // 同时设置大图标(Alt+Tab)和小图标(任务栏)
            SendMessage(hwnd, WM_SETICON, (System.IntPtr)ICON_SMALL, _taskbarIconRef.Handle);
            SendMessage(hwnd, WM_SETICON, (System.IntPtr)ICON_BIG, _taskbarIconRef.Handle);
        }

        // --- B. 设置任务栏显隐 ---
        // 获取当前扩展样式
        // 注意：兼容 x86 和 x64
        long style = GetWindowLong(hwnd, GWL_EXSTYLE).ToInt64();

        if (showInTaskbar)
        {
            // 显示：移除工具窗口属性，添加APP窗口属性
            style &= ~WS_EX_TOOLWINDOW;
            style |= WS_EX_APPWINDOW;
        }
        else
        {
            // 隐藏：添加工具窗口属性，移除APP窗口属性
            style |= WS_EX_TOOLWINDOW;
            style &= ~WS_EX_APPWINDOW;
        }

        // 应用样式
        if (System.IntPtr.Size == 8)
            SetWindowLongPtr64(hwnd, GWL_EXSTYLE, (System.IntPtr)style);
        else
            SetWindowLong32(hwnd, GWL_EXSTYLE, (int)style);
#endif
    }

    // ========================================================================
    // 托盘管理 (Tray Logic)
    // ========================================================================
    public void InitializeTrayIcon()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        try
        {
            if (_notifyIcon == null) _notifyIcon = new NotifyIcon();

            _notifyIcon.Icon = LoadIconFromStreamingAssets(trayIconFileName);
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
        
        // 深色主题渲染
        _contextMenu.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
        
        // 样式微调
        _contextMenu.ShowImageMargin = false; 
        _contextMenu.BackColor = System.Drawing.Color.FromArgb(45, 45, 48); 
        _contextMenu.ForeColor = System.Drawing.Color.White;             
        _contextMenu.ShowCheckMargin = false;
        _contextMenu.Font = new System.Drawing.Font("Segoe UI", 9F);

        foreach (var item in rightClickMenu)
        {
            if (string.IsNullOrEmpty(item.menuText)) continue;
            
            var menuItem = new ToolStripMenuItem(item.menuText);
            var evt = item.onClickEvent;

            menuItem.ForeColor = System.Drawing.Color.White; 
            menuItem.Click += (s, e) => 
                UnityMainThreadDispatcher.Instance().Enqueue(() => evt?.Invoke());
            
            _contextMenu.Items.Add(menuItem);
        }

        ToolStripSeparator sep = new ToolStripSeparator();
        sep.ForeColor = System.Drawing.Color.FromArgb(80, 80, 80);
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

    // ========================================================================
    // 资源加载 (StreamingAssets)
    // ========================================================================
    private
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
    System.Drawing.Icon
#else
    object
#endif
    LoadIconFromStreamingAssets(string fileName)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        string fullPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, fileName);
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
        if (_taskbarIconRef != null) { _taskbarIconRef.Dispose(); _taskbarIconRef = null; }
#endif
    }

    // ==========================================
    // 自定义深色主题渲染表 (Color 冲突已修复)
    // ==========================================
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
    public class DarkColorTable : ProfessionalColorTable
    {
        public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(45, 45, 48);
        public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.FromArgb(62, 62, 66);
        public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.FromArgb(62, 62, 66);
        public override System.Drawing.Color MenuBorder => System.Drawing.Color.Black;
        public override System.Drawing.Color MenuItemPressedGradientBegin => System.Drawing.Color.FromArgb(62, 62, 66);
        public override System.Drawing.Color MenuItemPressedGradientEnd => System.Drawing.Color.FromArgb(62, 62, 66);
        public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.FromArgb(45, 45, 48);
        public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.FromArgb(45, 45, 48);
        public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.FromArgb(45, 45, 48);
        public override System.Drawing.Color SeparatorDark => System.Drawing.Color.FromArgb(80, 80, 80);
        public override System.Drawing.Color SeparatorLight => System.Drawing.Color.FromArgb(45, 45, 48);
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