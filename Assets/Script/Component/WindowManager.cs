using UnityEngine;
using UnityEngine.Events; // 必须引入
using System;
using System.Runtime.InteropServices;
using UnityEngine.Rendering.Universal;

public class WindowManager : MonoBehaviour
{
    public enum WindowMode { Object, Spirit }

    [Header("模式配置")]
    public WindowMode currentMode = WindowMode.Object;
    public bool isTopmost = true;

    [Header("器物模式尺寸")]
    public int objWidth = 300;
    public int objHeight = 300;

    [Header("窗口模式事件")]
    [Tooltip("窗口模式改变时触发：True为全屏(Spirit)，False为窗口(Object)")]
    public UnityEvent<bool> OnWindowModeChanged;
    [Tooltip("仅在切换到全屏模式(Spirit)时触发")]
    public UnityEvent OnSwitchedToSpirit;
    [Tooltip("仅在切换到窗口模式(Object)时触发")]
    public UnityEvent OnSwitchedToObject;

    [Header("实时参数 (只读)")]
    public Vector2Int windowPosition;
    public Vector2Int windowSize;
    public Vector2Int cursorPosition;

    private Vector2Int savedObjectPos = new Vector2Int(100, 100);
    public IntPtr WindowHandle { get; private set; }

    // ==================== Win32 API 导入 ====================
    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT lpPoint);

    [Serializable][StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x; public int y; }
    [StructLayout(LayoutKind.Sequential)] public struct MARGINS { public int leftWidth, rightWidth, topHeight, bottomHeight; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] 
    public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    void Awake() {
#if !UNITY_EDITOR
        SetProcessDPIAware();
        WindowHandle = GetActiveWindow();
#endif
        ApplyWindowSettings();
    }

    void Update() {
#if !UNITY_EDITOR
        GetWindowRect(WindowHandle, out RECT rect);
        windowPosition = new Vector2Int(rect.Left, rect.Top);
        windowSize = new Vector2Int(rect.Right - rect.Left, rect.Bottom - rect.Top);
        GetCursorPos(out POINT p);
        cursorPosition = new Vector2Int(p.x, p.y);
#endif
    }

    /// <summary>
    /// 核心方法：设置并切换窗口模式
    /// </summary>
    public void SetWindowMode(WindowMode mode) {
        IntPtr zOrder = isTopmost ? (IntPtr)(-1) : (IntPtr)(-2);
        
        if (mode == WindowMode.Spirit) {
            // 记忆当前位置
            savedObjectPos = windowPosition;
            
            MONITORINFO mi = GetCurrentMonitorInfo();
            SetWindowPos(WindowHandle, zOrder, mi.rcMonitor.Left, mi.rcMonitor.Top, 
                         mi.rcMonitor.Right - mi.rcMonitor.Left, mi.rcMonitor.Bottom - mi.rcMonitor.Top, 0x0040);
            
            // 触发事件
            OnSwitchedToSpirit?.Invoke();
            OnWindowModeChanged?.Invoke(true);
        } else {
            // 还原记忆的位置
            SetWindowPos(WindowHandle, zOrder, savedObjectPos.x, savedObjectPos.y, objWidth, objHeight, 0x0040);
            
            // 触发事件
            OnSwitchedToObject?.Invoke();
            OnWindowModeChanged?.Invoke(false);
        }
        currentMode = mode;
    }

    public void ApplyWindowSettings() {
#if UNITY_EDITOR
        InitCamera(); return;
#endif
        InitCamera();
        SetWindowLongPtr64(WindowHandle, -16, (IntPtr)(0x80000000 | 0x10000000));
        long exStyle = (long)GetWindowLongPtr64(WindowHandle, -20);
        exStyle |= 0x80000 | 0x00000080;
        SetWindowLongPtr64(WindowHandle, -20, (IntPtr)exStyle);
        MARGINS m = new MARGINS { leftWidth = -1 };
        DwmExtendFrameIntoClientArea(WindowHandle, ref m);
        
        // 初始化模式
        SetWindowMode(currentMode);
    }

    private void InitCamera() {
        Camera cam = Camera.main;
        if (!cam) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0,0,0,0);
        cam.allowHDR = false;
        var ad = cam.GetComponent<UniversalAdditionalCameraData>();
        if (ad) { ad.renderPostProcessing = false; ad.volumeLayerMask = 0; }
    }

    public void MoveWindow(int x, int y) {
        SetWindowPos(WindowHandle, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010 | 0x0400);
    }

    public MONITORINFO GetCurrentMonitorInfo() {
        IntPtr monitor = MonitorFromWindow(WindowHandle, 2);
        MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
        GetMonitorInfo(monitor, ref mi);
        return mi;
    }

    public void SetClickThrough(bool transparent) {
#if !UNITY_EDITOR
        long exStyle = (long)GetWindowLongPtr64(WindowHandle, -20);
        if (transparent) exStyle |= 0x20; else exStyle &= ~0x20;
        SetWindowLongPtr64(WindowHandle, -20, (IntPtr)exStyle);
#endif
    }
}