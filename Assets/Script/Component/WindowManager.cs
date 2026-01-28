using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.Rendering.Universal;

public class WindowManager : MonoBehaviour
{
    [Header("窗口物理设置")]
    public bool isBorderless = true;    // 无边框
    public bool isTransparent = true;   // 物理背景透明
    public bool isTopmost = true;       // 窗口置顶
    public bool hideFromTaskbar = true; // 隐藏任务栏图标

    [Header("尺寸设置")]
    public int targetWidth = 300;
    public int targetHeight = 300;

    [Header("URP 相机设置")]
    public Color backgroundColor = new Color(0, 0, 0, 0);
    [Tooltip("开启后处理可能会导致透明区域变黑，建议在器灵项目中保持关闭")]
    public bool usePostProcessing = false;

    // 暴露给其他脚本（如 WindowInteraction, TrayIconManager）调用的窗口句柄
    public IntPtr WindowHandle { get; private set; }

    // ==================== Windows API 导入 ====================
    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    // 多显示器支持 API
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    // ==================== 常量与结构体 ====================
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const long WS_EX_LAYERED = 0x80000;
    private const long WS_EX_TOOLWINDOW = 0x00000080; // 隐藏任务栏图标的关键

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS { public int leftWidth, rightWidth, topHeight, bottomHeight; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    void Awake()
    {
        #if !UNITY_EDITOR
        // 1. 解决高分屏缩放导致的窗口变形和模糊
        SetProcessDPIAware();
        // 2. 获取当前活动窗口句柄
        WindowHandle = GetActiveWindow();
        #endif
        
        // 3. 应用配置
        ApplySettings();
    }

    /// <summary>
    /// 应用所有窗口物理属性设置
    /// </summary>
    public void ApplySettings()
    {
        #if UNITY_EDITOR
        InitURPCamera(); // 编辑器下仅初始化相机
        return;
        #endif

        if (WindowHandle == IntPtr.Zero) return;

        // 1. 初始化 URP 相机以支持 Alpha
        InitURPCamera();

        // 2. 设置普通样式 (无边框)
        if (isBorderless)
        {
            SetWindowLongPtr64(WindowHandle, GWL_STYLE, (IntPtr)(WS_POPUP | WS_VISIBLE));
        }

        // 3. 设置扩展样式 (透明 & 任务栏隐藏)
        long exStyle = (long)GetWindowLongPtr64(WindowHandle, GWL_EXSTYLE);
        
        if (isTransparent) exStyle |= WS_EX_LAYERED;
        if (hideFromTaskbar) exStyle |= WS_EX_TOOLWINDOW;
        
        SetWindowLongPtr64(WindowHandle, GWL_EXSTYLE, (IntPtr)exStyle);

        // 4. 实现基于 DWM 的 Alpha 通道透明混合
        if (isTransparent)
        {
            MARGINS margins = new MARGINS { leftWidth = -1 };
            DwmExtendFrameIntoClientArea(WindowHandle, ref margins);
        }

        // 5. 设置置顶、刷新样式并强制设定初始尺寸 (300x300)
        IntPtr hWndInsertAfter = isTopmost ? (IntPtr)(-1) : (IntPtr)(-2);
        SetWindowPos(WindowHandle, hWndInsertAfter, 0, 0, targetWidth, targetHeight, SWP_NOMOVE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
    }

    /// <summary>
    /// 配置 URP 相机清除标志和 Alpha 通道
    /// </summary>
    private void InitURPCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor; // A必须为0
        cam.allowHDR = false; // 必须关闭，否则透明会失效
        cam.allowMSAA = false; // 建议关闭以减少边缘白边

        UniversalAdditionalCameraData urpData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (urpData != null)
        {
            urpData.renderPostProcessing = usePostProcessing;
            urpData.volumeLayerMask = 0; // 禁用 Volume 干扰
            urpData.antialiasing = AntialiasingMode.None;
        }
    }

    // ==================== 工具方法 (供 Interaction 和 Tray 脚本调用) ====================

    /// <summary>
    /// 获取当前窗口在桌面上的物理坐标
    /// </summary>
    public Vector2Int GetWindowPosition()
    {
        GetWindowRect(WindowHandle, out RECT rect);
        return new Vector2Int(rect.Left, rect.Top);
    }

    /// <summary>
    /// 移动窗口位置 (不改变大小)
    /// </summary>
    public void MoveWindow(int x, int y)
    {
        // 0x0001: SWP_NOSIZE, 0x0004: SWP_NOZORDER, 0x0010: SWP_NOACTIVATE
        SetWindowPos(WindowHandle, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010);
    }

    /// <summary>
    /// 获取窗口当前所在显示器的工作区矩形 (处理多屏任务栏)
    /// </summary>
    public RECT GetCurrentMonitorWorkArea()
    {
        #if UNITY_EDITOR
        return new RECT { Right = Screen.width, Bottom = Screen.height };
        #endif

        IntPtr monitor = MonitorFromWindow(WindowHandle, MONITOR_DEFAULTTONEAREST);
        MONITORINFO mi = new MONITORINFO();
        mi.cbSize = Marshal.SizeOf(mi);

        if (GetMonitorInfo(monitor, ref mi))
        {
            return mi.rcWork;
        }
        return new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
    }
}