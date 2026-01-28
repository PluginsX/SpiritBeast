using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.Rendering.Universal;

public class WindowManager : MonoBehaviour
{
    public enum WindowMode { Object, Spirit }

    private const long WS_EX_TRANSPARENT = 0x20;
    
    [Header("模式配置")]
    public WindowMode currentMode = WindowMode.Object;
    public bool isTopmost = true;

    [Header("器物模式尺寸")]
    public int objWidth = 300;
    public int objHeight = 300;

    public IntPtr WindowHandle { get; private set; }

    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [Serializable][StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
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

    public void ApplyWindowSettings() {
#if UNITY_EDITOR
        InitCamera(); return;
#endif
        InitCamera();
        // 样式：无边框 + 透明 + 隐藏任务栏
        SetWindowLongPtr64(WindowHandle, -16, (IntPtr)(0x80000000 | 0x10000000));
        long exStyle = (long)GetWindowLongPtr64(WindowHandle, -20);
        exStyle |= 0x80000 | 0x00000080;
        SetWindowLongPtr64(WindowHandle, -20, (IntPtr)exStyle);
        
        MARGINS m = new MARGINS { leftWidth = -1 };
        DwmExtendFrameIntoClientArea(WindowHandle, ref m);

        SetWindowMode(currentMode);
    }

    public void SetWindowMode(WindowMode mode) {
        currentMode = mode;
        IntPtr zOrder = isTopmost ? (IntPtr)(-1) : (IntPtr)(-2);

        if (currentMode == WindowMode.Spirit) {
            // 器灵模式：覆盖整个显示器（包含任务栏，方便特效展示）
            MONITORINFO mi = GetCurrentMonitorInfo();
            int w = mi.rcMonitor.Right - mi.rcMonitor.Left;
            int h = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
            SetWindowPos(WindowHandle, zOrder, mi.rcMonitor.Left, mi.rcMonitor.Top, w, h, 0x0040);
        } else {
            // 器物模式：恢复初始大小，保持当前位置
            Vector2Int pos = GetWindowPosition();
            SetWindowPos(WindowHandle, zOrder, pos.x, pos.y, objWidth, objHeight, 0x0040);
        }
    }

    private void InitCamera() {
        Camera cam = Camera.main;
        if (!cam) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0,0,0,0);
        cam.allowHDR = false;
        var additionalData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (additionalData) { additionalData.renderPostProcessing = false; additionalData.volumeLayerMask = 0; }
    }

    public Vector2Int GetWindowPosition() { GetWindowRect(WindowHandle, out RECT r); return new Vector2Int(r.Left, r.Top); }
    public void MoveWindow(int x, int y) { SetWindowPos(WindowHandle, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010); }

    public MONITORINFO GetCurrentMonitorInfo()
    {
        IntPtr monitor = MonitorFromWindow(WindowHandle, 2);
        MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
        GetMonitorInfo(monitor, ref mi);
        return mi;
    }
    
    

    // 动态修改窗口是否“物理穿透”
    public void SetClickThrough(bool transparent)
    {
        #if !UNITY_EDITOR
        long exStyle = (long)GetWindowLongPtr64(WindowHandle, -20);
        if (transparent)
            exStyle |= WS_EX_TRANSPARENT; // 变为幽灵模式：点击穿透
        else
            exStyle &= ~WS_EX_TRANSPARENT; // 变为实体模式：响应点击
        
        SetWindowLongPtr64(WindowHandle, -20, (IntPtr)exStyle);
        #endif
    }
}