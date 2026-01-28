using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.Rendering.Universal;

public class WindowManager : MonoBehaviour
{
    [Header("窗口物理设置")]
    public bool isBorderless = true;
    public bool isTransparent = true;
    public bool isTopmost = true;

    [Header("尺寸设置")]
    public int targetWidth = 300;
    public int targetHeight = 300;

    [Header("URP 相机设置")]
    public Color backgroundColor = new Color(0, 0, 0, 0);
    [Tooltip("开启后处理可能会导致透明区域变黑")]
    public bool usePostProcessing = false;

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
        public RECT rcMonitor; // 整个显示器
        public RECT rcWork;    // 工作区 (不含任务栏)
        public uint dwFlags;
    }

    void Awake()
    {
        #if !UNITY_EDITOR
        SetProcessDPIAware(); // 必须在获取句柄前调用
        WindowHandle = GetActiveWindow();
        #endif
        ApplySettings();
    }

    public void ApplySettings()
    {
        #if UNITY_EDITOR
        InitURPCamera(); return;
        #endif

        if (WindowHandle == IntPtr.Zero) return;

        InitURPCamera();

        // 1. 无边框样式
        if (isBorderless)
            SetWindowLongPtr64(WindowHandle, -16, (IntPtr)(0x80000000 | 0x10000000));

        // 2. 透明设置
        if (isTransparent)
        {
            long exStyle = (long)GetWindowLongPtr64(WindowHandle, -20);
            exStyle |= 0x80000;      // WS_EX_LAYERED (透明支持)
            exStyle |= 0x00000080;   // WS_EX_TOOLWINDOW (隐藏任务栏标签)
            SetWindowLongPtr64(WindowHandle, -20, (IntPtr)exStyle);
            MARGINS margins = new MARGINS { leftWidth = -1 };
            DwmExtendFrameIntoClientArea(WindowHandle, ref margins);
        }

        // 3. 置顶与尺寸
        IntPtr hWndInsertAfter = isTopmost ? (IntPtr)(-1) : (IntPtr)(-2);
        SetWindowPos(WindowHandle, hWndInsertAfter, 0, 0, targetWidth, targetHeight, 0x0002 | 0x0020 | 0x0040);
    }

    private void InitURPCamera()
    {
        Camera cam = Camera.main;
        if (!cam) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        cam.allowHDR = false;
        var urpData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (urpData) { urpData.renderPostProcessing = usePostProcessing; urpData.volumeLayerMask = 0; }
    }

    // 获取当前窗口所在的显示器工作区 (支持多屏)
    public RECT GetCurrentWorkArea()
    {
        #if UNITY_EDITOR
        return new RECT { Right = Screen.width, Bottom = Screen.height };
        #endif
        IntPtr monitor = MonitorFromWindow(WindowHandle, MONITOR_DEFAULTTONEAREST);
        MONITORINFO mi = new MONITORINFO();
        mi.cbSize = Marshal.SizeOf(mi);
        if (GetMonitorInfo(monitor, ref mi)) return mi.rcWork;
        return new RECT { Right = 1920, Bottom = 1080 }; // 兜底返回
    }

    public Vector2Int GetWindowPosition()
    {
        GetWindowRect(WindowHandle, out RECT rect);
        return new Vector2Int(rect.Left, rect.Top);
    }

    public void MoveWindow(int x, int y)
    {
        SetWindowPos(WindowHandle, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010);
    }
}