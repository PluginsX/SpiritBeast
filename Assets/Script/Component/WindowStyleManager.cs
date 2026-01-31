using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class WindowStyleManager : MonoBehaviour
{
    // ==================== 数据结构 ====================
    [Serializable]
    public class WindowsStyleConfig
    {
        [Header("窗口模式")]
        public bool isFullscreen = false;
        public Vector2Int windowSize = new Vector2Int(1024, 576);

        [Header("窗口边框（仅窗口模式有效）")]
        public bool hasBorder = true;
        public bool resizable = true;

        [Header("透明与穿透")]
        public bool enableTransparent = false;
        public bool enableClickThrough = false;

        public InteractionType interactionType = InteractionType.Collider;
        public LayerMask interactionLayerMask = Physics.DefaultRaycastLayers;

        [Header("窗口置顶（仅窗口模式有效）")]
        public bool isAlwaysOnTop = false;

        public enum InteractionType
        {
            Collider,
            UGUI,
            Both
        }
    }

    // ==================== Inspector ====================
    public Camera targetCamera;
    public WindowsStyleConfig windowConfig = new WindowsStyleConfig();

    // ==================== 状态 ====================
    private IntPtr windowHandle;
    private bool isInitialized;
    private bool isQuitting;

    private PointerEventData pointerEventData;
    private readonly List<RaycastResult> uiRaycastResults = new();

    // ==================== Win32 常量 ====================
    const int GWL_STYLE = -16;
    const int GWL_EXSTYLE = -20;
    const int GWL_WNDPROC = -4;

    const int WM_CLOSE = 0x0010;

    const long WS_VISIBLE = 0x10000000;
    const long WS_OVERLAPPED = 0x00000000;
    const long WS_CAPTION = 0x00C00000;
    const long WS_SYSMENU = 0x00080000;
    const long WS_THICKFRAME = 0x00040000;
    const long WS_MINIMIZEBOX = 0x00020000;
    const long WS_MAXIMIZEBOX = 0x00010000;
    const long WS_POPUP = 0x80000000;

    const long WS_EX_LAYERED = 0x00080000;
    const long WS_EX_TRANSPARENT = 0x00000020;

    // ==================== Win32 API ====================
    [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();

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
    static extern IntPtr CallWindowProc(
        IntPtr lpPrevWndFunc,
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam
    );

    [DllImport("dwmapi.dll")]
    static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_FRAMECHANGED = 0x0020;
    const uint SWP_SHOWWINDOW = 0x0040;
    
    // 窗口置顶相关常量
    const int HWND_TOPMOST = -1;
    const int HWND_NOTOPMOST = -2;
    const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    struct MARGINS
    {
        public int left;
        public int right;
        public int top;
        public int bottom;
    }

    // ==================== WndProc Hook ====================
    private IntPtr oldWndProc;
    private WndProcDelegate newWndProc;

    private delegate IntPtr WndProcDelegate(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam
    );

    // ==================== Unity 生命周期 ====================
    void Awake()
    {
#if !UNITY_EDITOR
        windowHandle = GetActiveWindow();
        HookWindowClose();
#endif
        isInitialized = true;
    }

    void Start()
    {
        ApplyWindowStyle();
    }

    void Update()
    {
        if (!isInitialized || isQuitting || windowHandle == IntPtr.Zero)
            return;

        HandleClickThrough();
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
    }

    // ==================== Public API ====================

    /// <summary>
    /// 运行时设置窗口样式（立即生效）
    /// </summary>
    public void SetWindowStyle(WindowsStyleConfig newConfig)
    {
        if (newConfig == null)
        {
            Debug.LogWarning("SetWindowStyle failed: config is null");
            return;
        }

        if (!isInitialized || isQuitting)
            return;

        // 深拷贝，防止外部修改引用
        windowConfig = new WindowsStyleConfig
        {
            isFullscreen = newConfig.isFullscreen,
            windowSize = newConfig.windowSize,
            hasBorder = newConfig.hasBorder,
            resizable = newConfig.resizable,
            enableTransparent = newConfig.enableTransparent,
            enableClickThrough = newConfig.enableClickThrough,
            interactionType = newConfig.interactionType,
            interactionLayerMask = newConfig.interactionLayerMask
        };

#if !UNITY_EDITOR
        ApplyWindowStyle();
#endif
    }

    // ==================== 核心：拦截系统关闭 ====================
    void HookWindowClose()
    {
        newWndProc = CustomWndProc;
        oldWndProc = SetWindowLongPtr64(
            windowHandle,
            GWL_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(newWndProc)
        );
    }

    IntPtr CustomWndProc(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (msg == WM_CLOSE)
        {
            if (!isQuitting)
            {
                isQuitting = true;
                Application.Quit();
            }
            return IntPtr.Zero; // 阻止系统直接销毁窗口
        }

        return CallWindowProc(oldWndProc, hWnd, msg, wParam, lParam);
    }

    // ==================== 窗口样式 ====================
    void ApplyWindowStyle()
    {
#if UNITY_EDITOR
        return;
#endif
        long style;

        // 全屏模式且启用透明时，自动设置为无边框
        if (windowConfig.isFullscreen && windowConfig.enableTransparent)
        {
            windowConfig.hasBorder = false;
        }

        // 窗口模式且有边框时，自动禁用透明（因为透明选项被隐藏了）
        if (!windowConfig.isFullscreen && windowConfig.hasBorder)
        {
            windowConfig.enableTransparent = false;
        }

        // 判断是否为独占全屏模式
        bool isExclusiveFullscreen = windowConfig.isFullscreen && !windowConfig.enableTransparent;

        if (isExclusiveFullscreen)
        {
            // 独占全屏模式：真正的全屏，不支持透明
            style = WS_POPUP | WS_VISIBLE;
        }
        else
        {
            // 窗口模式（包括透明全屏模拟）
            if (windowConfig.hasBorder)
            {
                style =
                    WS_OVERLAPPED |
                    WS_CAPTION |
                    WS_SYSMENU |
                    WS_MINIMIZEBOX |
                    WS_MAXIMIZEBOX |
                    WS_VISIBLE;

                if (windowConfig.resizable)
                    style |= WS_THICKFRAME;
            }
            else
            {
                style = WS_POPUP | WS_VISIBLE;
            }
        }

        SetWindowLongPtr64(windowHandle, GWL_STYLE, (IntPtr)style);

        long exStyle = (long)GetWindowLongPtr64(windowHandle, GWL_EXSTYLE);

        if (windowConfig.enableTransparent)
            exStyle |= WS_EX_LAYERED;
        else
            exStyle &= ~WS_EX_LAYERED;

        SetWindowLongPtr64(windowHandle, GWL_EXSTYLE, (IntPtr)exStyle);

        // 设置窗口位置和大小
        SetWindowPosition();

        ConfigureCamera();

        // 立即应用透明穿透设置
        if (windowConfig.enableTransparent && windowConfig.enableClickThrough)
        {
            // 延迟一帧应用穿透设置，确保窗口样式已生效
            Invoke(nameof(ApplyClickThrough), 0.1f);
        }
    }

    void ApplyClickThrough()
    {
        if (!windowConfig.enableTransparent || !windowConfig.enableClickThrough)
            return;

        bool hit = CheckInteraction();

        long exStyle = (long)GetWindowLongPtr64(windowHandle, GWL_EXSTYLE);

        if (hit)
            exStyle &= ~WS_EX_TRANSPARENT;
        else
            exStyle |= WS_EX_TRANSPARENT;

        SetWindowLongPtr64(windowHandle, GWL_EXSTYLE, (IntPtr)exStyle);
    }

    void SetWindowPosition()
    {
        if (windowHandle == IntPtr.Zero) return;

        if (windowConfig.isFullscreen)
        {
            // 全屏模式：设置为屏幕大小
            var screen = Screen.currentResolution;
            
            // 根据置顶选项决定窗口层级
            IntPtr hWndInsertAfter = windowConfig.isAlwaysOnTop ? 
                (IntPtr)HWND_TOPMOST : 
                (IntPtr)HWND_NOTOPMOST;
                
            SetWindowPos(
                windowHandle,
                hWndInsertAfter,
                0, 0, // X, Y
                screen.width, screen.height, // 宽度, 高度
                SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_NOACTIVATE
            );
        }
        else
        {
            // 窗口模式：设置指定大小
            IntPtr hWndInsertAfter = windowConfig.isAlwaysOnTop ? 
                (IntPtr)HWND_TOPMOST : 
                (IntPtr)HWND_NOTOPMOST;
                
            SetWindowPos(
                windowHandle,
                hWndInsertAfter,
                100, 100, // X, Y
                windowConfig.windowSize.x, windowConfig.windowSize.y, // 宽度, 高度
                SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_NOACTIVATE
            );
        }
    }

    // ==================== 相机 ====================
    void ConfigureCamera()
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (!targetCamera) return;

        if (windowConfig.enableTransparent)
        {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = new Color(0, 0, 0, 0);
            targetCamera.allowHDR = false;
            
            // 启用DWM透明效果
            EnableDWMTransparency();
        }
    }

    void EnableDWMTransparency()
    {
        if (windowHandle == IntPtr.Zero) return;

        // 设置边距为-1，表示扩展整个客户区
        MARGINS margins = new MARGINS
        {
            left = -1,
            right = -1,
            top = -1,
            bottom = -1
        };

        // 调用DWM API启用透明效果
        DwmExtendFrameIntoClientArea(windowHandle, ref margins);
    }

    // ==================== 穿透 ====================
    void HandleClickThrough()
    {
        if (!windowConfig.enableTransparent || !windowConfig.enableClickThrough)
            return;

        bool hit = CheckInteraction();

        long exStyle = (long)GetWindowLongPtr64(windowHandle, GWL_EXSTYLE);

        if (hit)
            exStyle &= ~WS_EX_TRANSPARENT;
        else
            exStyle |= WS_EX_TRANSPARENT;

        SetWindowLongPtr64(windowHandle, GWL_EXSTYLE, (IntPtr)exStyle);
    }

    bool CheckInteraction()
    {
        if (windowConfig.interactionType == WindowsStyleConfig.InteractionType.UGUI ||
            windowConfig.interactionType == WindowsStyleConfig.InteractionType.Both)
        {
            if (IsPointerOverUI())
                return true;
        }

        if (windowConfig.interactionType == WindowsStyleConfig.InteractionType.Collider ||
            windowConfig.interactionType == WindowsStyleConfig.InteractionType.Both)
        {
            if (IsPointerOver3D())
                return true;
        }

        return false;
    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        pointerEventData ??= new PointerEventData(EventSystem.current);
        pointerEventData.position = Input.mousePosition;

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerEventData, uiRaycastResults);

        return uiRaycastResults.Count > 0;
    }

    bool IsPointerOver3D()
    {
        if (!targetCamera) return false;

        return Physics.Raycast(
            targetCamera.ScreenPointToRay(Input.mousePosition),
            100f,
            windowConfig.interactionLayerMask
        );
    }
}
