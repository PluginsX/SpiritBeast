using UnityEngine;
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

    [Header("实时参数 (只读)")]
    public Vector2Int windowPosition;
    public Vector2Int windowSize;
    public Vector2Int cursorPosition;

    private Vector2Int savedObjectPos = new Vector2Int(100, 100); // 记忆位置
    public IntPtr WindowHandle { get; private set; }

    // ==================== Win32 API ====================
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
        UpdateRuntimeParameters();
    }

    private void UpdateRuntimeParameters() {
        // 1. 更新窗口位置与尺寸 (Win32 坐标系)
        GetWindowRect(WindowHandle, out RECT rect);
        windowPosition = new Vector2Int(rect.Left, rect.Top);
        windowSize = new Vector2Int(rect.Right - rect.Left, rect.Bottom - rect.Top);

        // 2. 更新全局光标位置
        GetCursorPos(out POINT p);
        cursorPosition = new Vector2Int(p.x, p.y);
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

        SetWindowMode(currentMode);
    }

    public void SetWindowMode(WindowMode mode) {
        IntPtr zOrder = isTopmost ? (IntPtr)(-1) : (IntPtr)(-2);

        if (mode == WindowMode.Spirit) {
            // 保存进入全屏前的位置
            savedObjectPos = windowPosition;
            
            MONITORINFO mi = GetCurrentMonitorInfo();
            int w = mi.rcMonitor.Right - mi.rcMonitor.Left;
            int h = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
            SetWindowPos(WindowHandle, zOrder, mi.rcMonitor.Left, mi.rcMonitor.Top, w, h, 0x0040);
        } else {
            // 回到器物模式，还原保存的位置
            SetWindowPos(WindowHandle, zOrder, savedObjectPos.x, savedObjectPos.y, objWidth, objHeight, 0x0040);
        }
        currentMode = mode;
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

    public void MoveWindow(int x, int y) {
        // 0x0001: NOSIZE, 0x0004: NOZORDER, 0x0010: NOACTIVATE, 0x0400: NOSENDCHANGING (高性能)
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