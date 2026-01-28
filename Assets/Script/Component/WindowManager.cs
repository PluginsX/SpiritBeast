using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.Rendering.Universal;

public class WindowManager : MonoBehaviour
{
    [Header("样式设置")]
    public bool isBorderless = true;
    public bool isTransparent = true;
    public bool isTopmost = true;
    public bool hideFromTaskbar = true;

    [Header("尺寸设置")]
    public int targetWidth = 300;
    public int targetHeight = 300;

    [Header("相机设置")]
    public Color backgroundColor = new Color(0, 0, 0, 0);

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
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    void Awake() {
#if !UNITY_EDITOR
        SetProcessDPIAware();
        WindowHandle = GetActiveWindow();
#endif
        ApplySettings();
    }

    public void ApplySettings() {
#if UNITY_EDITOR
        InitCamera(); return;
#endif
        InitCamera();
        if (isBorderless) SetWindowLongPtr64(WindowHandle, -16, (IntPtr)(0x80000000 | 0x10000000));
        long exStyle = (long)GetWindowLongPtr64(WindowHandle, -20);
        if (isTransparent) exStyle |= 0x80000;
        if (hideFromTaskbar) exStyle |= 0x00000080;
        SetWindowLongPtr64(WindowHandle, -20, (IntPtr)exStyle);
        if (isTransparent) {
            MARGINS m = new MARGINS { leftWidth = -1 };
            DwmExtendFrameIntoClientArea(WindowHandle, ref m);
        }
        SetWindowPos(WindowHandle, isTopmost ? (IntPtr)(-1) : (IntPtr)(-2), 0, 0, targetWidth, targetHeight, 0x0002 | 0x0040);
    }

    private void InitCamera() {
        Camera cam = Camera.main;
        if (!cam) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        cam.allowHDR = false;
        var urpData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (urpData) { urpData.renderPostProcessing = false; urpData.volumeLayerMask = 0; }
    }

    public Vector2Int GetWindowPosition() { GetWindowRect(WindowHandle, out RECT r); return new Vector2Int(r.Left, r.Top); }
    public void MoveWindow(int x, int y) { SetWindowPos(WindowHandle, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010); }

    public RECT GetCurrentMonitorWorkArea() {
        IntPtr monitor = MonitorFromWindow(WindowHandle, 2);
        MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
        if (GetMonitorInfo(monitor, ref mi)) return mi.rcWork;
        return new RECT { Right = Screen.width, Bottom = Screen.height };
    }
}