using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Runtime.InteropServices;

public class WindowStyleManager : MonoBehaviour
{
    // ================= 配置结构 =================

    [Serializable]
    public class WindowsStyleConfig
    {
        [Header("模式")]
        public bool isFullscreen = false;

        [Header("窗口模式")]
        public Vector2Int windowSize = new Vector2Int(800, 600);
        public bool hasBorder = true;
        public bool resizable = true;
        public bool isTopmost = true;

        [Header("全屏模式")]
        [Tooltip("是否作为桌面壁纸层（WorkerW），false = 全屏置顶")]
        public bool asWallpaper = false;

        [Header("透明与交互")]
        public bool enableTransparent = true;
        public bool enableClickThrough = false;
        public InteractionType interactionType = InteractionType.Collider;
        public LayerMask interactionLayerMask = Physics.DefaultRaycastLayers;

        [Header("系统")]
        public bool enableDPIAware = true;

        public enum InteractionType
        {
            Collider,
            UGUI,
            Both
        }
    }

    public Camera targetCamera;
    public WindowsStyleConfig windowConfig = new WindowsStyleConfig();

    private IntPtr hWnd;
    private bool initialized;
    private bool clickThroughCached;

    // ================= Win32 =================

    const int GWL_STYLE   = -16;
    const int GWL_EXSTYLE = -20;

    const long WS_OVERLAPPED = 0x00000000;
    const long WS_POPUP      = 0x80000000;
    const long WS_VISIBLE    = 0x10000000;
    const long WS_CAPTION    = 0x00C00000;
    const long WS_SYSMENU    = 0x00080000;
    const long WS_THICKFRAME = 0x00040000;

    const long WS_EX_LAYERED     = 0x00080000;
    const long WS_EX_TOOLWINDOW  = 0x00000080;
    const long WS_EX_TRANSPARENT = 0x00000020;

    const uint SWP_NOSIZE        = 0x0001;
    const uint SWP_NOMOVE        = 0x0002;
    const uint SWP_NOZORDER      = 0x0004;
    const uint SWP_FRAMECHANGED  = 0x0020;
    const uint SWP_SHOWWINDOW    = 0x0040;

    [DllImport("user32.dll")]
    static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy,
        uint uFlags
    );

    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    [DllImport("dwmapi.dll")]
    static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS m);

    [StructLayout(LayoutKind.Sequential)]
    struct MARGINS
    {
        public int left, right, top, bottom;
    }

    // ================= Unity 生命周期 =================

    void Awake()
    {
#if !UNITY_EDITOR
        if (windowConfig.enableDPIAware)
            SetProcessDPIAware();

        hWnd = GetActiveWindow();
        initialized = true;
#endif
        if (!targetCamera)
            targetCamera = Camera.main;

        Apply();
    }

    void Update()
    {
        if (!initialized) return;

        if (windowConfig.enableTransparent && windowConfig.enableClickThrough)
            UpdateClickThrough();
    }

    // ================= 核心逻辑 =================

    public void Apply()
    {
#if UNITY_EDITOR
        InitCamera();
        return;
#endif
        if (!initialized) return;

        ApplyWindowStyle();
        ApplyWindowZOrder();
        ApplyWindowRect();
        InitCamera();
    }

    void ApplyWindowStyle()
    {
        long style;
        long exStyle = WS_EX_TOOLWINDOW;

        if (windowConfig.isFullscreen)
        {
            style = WS_POPUP | WS_VISIBLE;
        }
        else
        {
            if (windowConfig.hasBorder)
            {
                style = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_VISIBLE;
                if (windowConfig.resizable)
                    style |= WS_THICKFRAME;
            }
            else
            {
                style = WS_POPUP | WS_VISIBLE;
            }
        }

        if (windowConfig.enableTransparent)
            exStyle |= WS_EX_LAYERED;

        SetWindowLongPtr64(hWnd, GWL_STYLE, (IntPtr)style);
        SetWindowLongPtr64(hWnd, GWL_EXSTYLE, (IntPtr)exStyle);

        if (windowConfig.enableTransparent)
        {
            MARGINS m = new MARGINS { left = -1 };
            DwmExtendFrameIntoClientArea(hWnd, ref m);
        }
    }

    void ApplyWindowRect()
    {
        if (windowConfig.isFullscreen)
        {
            var r = Screen.currentResolution;
            SetWindowPos(
                hWnd, IntPtr.Zero,
                0, 0, r.width, r.height,
                SWP_FRAMECHANGED | SWP_SHOWWINDOW
            );
        }
        else
        {
            SetWindowPos(
                hWnd, IntPtr.Zero,
                100, 100,
                windowConfig.windowSize.x,
                windowConfig.windowSize.y,
                SWP_FRAMECHANGED | SWP_SHOWWINDOW
            );
        }
    }

    void ApplyWindowZOrder()
    {
        IntPtr z;

        if (windowConfig.isFullscreen)
        {
            // asWallpaper 结构预留（WorkerW 挂载应在此扩展）
            z = windowConfig.asWallpaper ? (IntPtr)1 : (IntPtr)(-1);
        }
        else
        {
            z = windowConfig.isTopmost ? (IntPtr)(-1) : (IntPtr)(-2);
        }

        SetWindowPos(
            hWnd, z,
            0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE
        );
    }

    void InitCamera()
    {
        if (!targetCamera) return;

        targetCamera.clearFlags = CameraClearFlags.SolidColor;
        targetCamera.backgroundColor =
            new Color(0, 0, 0, windowConfig.enableTransparent ? 0 : 1);
        targetCamera.allowHDR = false;
    }

    // ================= 点击穿透 =================

    void UpdateClickThrough()
    {
        bool hit = false;

        if (windowConfig.interactionType != WindowsStyleConfig.InteractionType.Collider)
        {
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
                hit = true;
        }

        if (windowConfig.interactionType != WindowsStyleConfig.InteractionType.UGUI &&
            targetCamera != null)
        {
            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, 100f, windowConfig.interactionLayerMask))
                hit = true;
        }

        if (hit != clickThroughCached)
        {
            long ex = (long)GetWindowLongPtr64(hWnd, GWL_EXSTYLE);
            if (!hit) ex |= WS_EX_TRANSPARENT;
            else ex &= ~WS_EX_TRANSPARENT;

            SetWindowLongPtr64(hWnd, GWL_EXSTYLE, (IntPtr)ex);
            clickThroughCached = hit;
        }
    }
}
