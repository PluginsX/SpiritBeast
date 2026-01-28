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
    [Tooltip("开启后处理可能会导致透明区域变黑，需配合特定Shader使用")]
    public bool usePostProcessing = false;

    // 暴露句柄给交互脚本
    public IntPtr WindowHandle { get; private set; }

    // ==================== Windows API 导入 ====================
    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(int uiAction, int uiParam, ref RECT pvParam, int fWinIni);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS { public int leftWidth, rightWidth, topHeight, bottomHeight; }

    private void Awake()
    {
        #if !UNITY_EDITOR
        SetProcessDPIAware(); // 解决高分屏缩放变形
        WindowHandle = GetActiveWindow();
        #endif
        ApplySettings();
    }

    /// <summary>
    /// 应用窗口物理属性
    /// </summary>
    public void ApplySettings()
    {
        #if UNITY_EDITOR
        InitURPCamera();
        return;
        #endif

        if (WindowHandle == IntPtr.Zero) return;

        InitURPCamera();

        // 1. 无边框样式
        if (isBorderless)
        {
            const uint WS_POPUP = 0x80000000;
            const uint WS_VISIBLE = 0x10000000;
            SetWindowLongPtr64(WindowHandle, -16, (IntPtr)(WS_POPUP | WS_VISIBLE));
        }

        // 2. DWM Alpha 透明
        if (isTransparent)
        {
            long exStyle = (long)GetWindowLongPtr64(WindowHandle, -20);
            exStyle |= 0x80000; // WS_EX_LAYERED
            SetWindowLongPtr64(WindowHandle, -20, (IntPtr)exStyle);

            MARGINS margins = new MARGINS { leftWidth = -1 };
            DwmExtendFrameIntoClientArea(WindowHandle, ref margins);
        }

        // 3. 置顶与初始位置刷新
        IntPtr hWndInsertAfter = isTopmost ? (IntPtr)(-1) : (IntPtr)(-2);
        SetWindowPos(WindowHandle, hWndInsertAfter, 0, 0, targetWidth, targetHeight, 0x0002 | 0x0020 | 0x0040);
    }

    private void InitURPCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        cam.allowHDR = false;
        var urpData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (urpData != null)
        {
            urpData.renderPostProcessing = usePostProcessing;
            urpData.volumeLayerMask = 0;
        }
    }

    // --- 工具方法供交互脚本调用 ---

    public Vector2Int GetWindowPosition()
    {
        GetWindowRect(WindowHandle, out RECT rect);
        return new Vector2Int(rect.Left, rect.Top);
    }

    public void MoveWindow(int x, int y)
    {
        // NOSIZE | NOZORDER | NOACTIVATE
        SetWindowPos(WindowHandle, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010);
    }

    public RECT GetWorkArea()
    {
        RECT rect = new RECT();
        SystemParametersInfo(0x30, 0, ref rect, 0); // SPI_GETWORKAREA
        return rect;
    }
}