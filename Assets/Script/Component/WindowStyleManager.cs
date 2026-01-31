using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;
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

        [Header("窗口交互（仅窗口模式有效）")]
        public bool movable = true;
        public bool snapToEdges = true;

        public enum InteractionType
        {
            Collider,
            UGUI,
            Both
        }
    }

    // ==================== Inspector ====================
    public Camera targetCamera;
    
    [Header("样式配置")]
    public List<WindowsStyleConfig> styleConfigs = new List<WindowsStyleConfig>();
    public bool autoApplyStyle = false;
    public int defaultStyleIndex = 0;

    // ==================== 状态 ====================
    private IntPtr windowHandle;
    private bool isInitialized;
    private bool isQuitting;

    private PointerEventData pointerEventData;
    private readonly List<RaycastResult> uiRaycastResults = new();
    
    // 当前激活的窗口配置
    private WindowsStyleConfig currentConfig;

    // 保存的窗口位置（用于从全屏切换回窗口时恢复）
    private Vector2Int savedWindowPosition = new Vector2Int(100, 100);

    // ==================== 拖拽状态 ====================
    private bool isDragging = false;
    private bool isMouseDown = false;
    private float mouseDownTime;
    private Vector2Int mouseDownCursorPos;
    private float lastDragEventTime;
    private Vector2Int dragOffset;
    private Coroutine snapCoroutine;

    // ==================== 拖拽设置 ====================
    private float maxClickDuration = 0.2f;
    private float dragFrequency = 30f;
    private float snapSmoothTime = 0.12f;

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

    // ==================== 拖拽相关 API ====================
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

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

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    // ==================== WndProc Hook ====================
    private IntPtr oldWndProc;
    private WndProcDelegate newWndProc;

    // ==================== 窗口位置和光标位置 ====================
    private Vector2Int GetWindowPosition()
    {
        if (windowHandle == IntPtr.Zero) return Vector2Int.zero;
        GetWindowRect(windowHandle, out RECT rect);
        return new Vector2Int(rect.Left, rect.Top);
    }

    private Vector2Int GetCursorPosition()
    {
        GetCursorPos(out POINT p);
        return new Vector2Int(p.x, p.y);
    }

    private void MoveWindow(int x, int y)
    {
        if (windowHandle == IntPtr.Zero) return;
        SetWindowPos(windowHandle, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010 | 0x0400);
    }

    private MONITORINFO GetCurrentMonitorInfo()
    {
        if (windowHandle == IntPtr.Zero) return new MONITORINFO();
        IntPtr monitor = MonitorFromWindow(windowHandle, 2);
        MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
        GetMonitorInfo(monitor, ref mi);
        return mi;
    }

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
        // 自动应用样式
        if (autoApplyStyle && styleConfigs.Count > 0)
        {
            // 确保默认索引在有效范围内
            defaultStyleIndex = Mathf.Clamp(defaultStyleIndex, 0, styleConfigs.Count - 1);
            ChangeWindowStyleByIndex(defaultStyleIndex);
        }
        else
        {
            ApplyWindowStyle();
        }
    }

    void Update()
    {
        if (!isInitialized || isQuitting || windowHandle == IntPtr.Zero)
            return;

        HandleClickThrough();
        HandleMouseInput();
    }

    private void HandleMouseInput()
    {
        // 仅在窗口模式且可移动时处理拖拽
        if (currentConfig == null || currentConfig.isFullscreen || !currentConfig.movable)
            return;

        // 检查鼠标是否在可交互区域
        bool canInteract = CheckInteraction();

        // 鼠标按下
        if (Input.GetMouseButtonDown(0))
        {
            if (canInteract)
            {
                isMouseDown = true;
                mouseDownTime = Time.time;
                mouseDownCursorPos = GetCursorPosition();
                dragOffset = GetCursorPosition() - GetWindowPosition();
            }
        }

        // 鼠标按下并移动
        if (isMouseDown && Input.GetMouseButton(0))
        {
            float duration = Time.time - mouseDownTime;
            float dist = Vector2.Distance(mouseDownCursorPos, GetCursorPosition());

            // 触发拖拽
            if (!isDragging && (duration > maxClickDuration || dist > 5f))
            {
                isDragging = true;
                if (snapCoroutine != null) StopCoroutine(snapCoroutine);
            }

            // 正在拖拽
            if (isDragging)
            {
                Vector2Int newPos = GetCursorPosition() - dragOffset;
                MoveWindow(newPos.x, newPos.y);
                if (Time.time - lastDragEventTime > 1f / dragFrequency)
                {
                    lastDragEventTime = Time.time;
                }
            }
        }

        // 鼠标释放
        if (isMouseDown && Input.GetMouseButtonUp(0))
        {
            if (isDragging)
            {
                isDragging = false;
                if (currentConfig.snapToEdges)
                    snapCoroutine = StartCoroutine(DoElasticSnap());
            }
            isMouseDown = false;
        }
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private IEnumerator DoElasticSnap()
    {
        if (currentConfig == null || currentConfig.isFullscreen || !currentConfig.snapToEdges)
            yield break;

        Vector2Int currentPos = GetWindowPosition();
        MONITORINFO monitorInfo = GetCurrentMonitorInfo();
        
        // 计算窗口大小
        int windowWidth = currentConfig.windowSize.x;
        int windowHeight = currentConfig.windowSize.y;
        
        // 计算目标位置（吸附到屏幕边缘）
        int targetX = Mathf.Clamp(currentPos.x, monitorInfo.rcWork.Left, monitorInfo.rcWork.Right - windowWidth);
        int targetY = Mathf.Clamp(currentPos.y, monitorInfo.rcWork.Top, monitorInfo.rcWork.Bottom - windowHeight);
        
        // 如果位置没有变化，直接返回
        if (targetX == currentPos.x && targetY == currentPos.y)
            yield break;

        // 使用平滑阻尼算法实现弹性效果
        Vector2 targetVec = new Vector2(targetX, targetY);
        Vector2 currentFloatPos = new Vector2(currentPos.x, currentPos.y);
        Vector2 velocity = Vector2.zero;
        
        while (Vector2.Distance(currentFloatPos, targetVec) > 0.5f)
        {
            currentFloatPos = Vector2.SmoothDamp(currentFloatPos, targetVec, ref velocity, snapSmoothTime);
            MoveWindow((int)currentFloatPos.x, (int)currentFloatPos.y);
            yield return null;
        }
        
        // 确保最终位置准确
        MoveWindow(targetX, targetY);
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
        currentConfig = new WindowsStyleConfig
        {
            isFullscreen = newConfig.isFullscreen,
            windowSize = newConfig.windowSize,
            hasBorder = newConfig.hasBorder,
            resizable = newConfig.resizable,
            enableTransparent = newConfig.enableTransparent,
            enableClickThrough = newConfig.enableClickThrough,
            interactionType = newConfig.interactionType,
            interactionLayerMask = newConfig.interactionLayerMask,
            isAlwaysOnTop = newConfig.isAlwaysOnTop,
            movable = newConfig.movable,
            snapToEdges = newConfig.snapToEdges
        };

#if !UNITY_EDITOR
        ApplyWindowStyle();
#endif
    }

    /// <summary>
    /// 通过索引切换窗口样式
    /// </summary>
    /// <param name="index">样式配置在列表中的索引</param>
    public void ChangeWindowStyleByIndex(int index)
    {
        if (index < 0 || index >= styleConfigs.Count)
        {
            Debug.LogWarning($"ChangeWindowStyleByIndex failed: index {index} is out of range. Valid range: 0-{styleConfigs.Count - 1}");
            return;
        }

        if (!isInitialized || isQuitting)
            return;

        // 深拷贝，防止外部修改引用
        currentConfig = new WindowsStyleConfig
        {
            isFullscreen = styleConfigs[index].isFullscreen,
            windowSize = styleConfigs[index].windowSize,
            hasBorder = styleConfigs[index].hasBorder,
            resizable = styleConfigs[index].resizable,
            enableTransparent = styleConfigs[index].enableTransparent,
            enableClickThrough = styleConfigs[index].enableClickThrough,
            interactionType = styleConfigs[index].interactionType,
            interactionLayerMask = styleConfigs[index].interactionLayerMask,
            isAlwaysOnTop = styleConfigs[index].isAlwaysOnTop,
            movable = styleConfigs[index].movable,
            snapToEdges = styleConfigs[index].snapToEdges
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
        if (currentConfig.isFullscreen && currentConfig.enableTransparent)
        {
            currentConfig.hasBorder = false;
        }

        // 窗口模式且有边框时，自动禁用透明（因为透明选项被隐藏了）
        if (!currentConfig.isFullscreen && currentConfig.hasBorder)
        {
            currentConfig.enableTransparent = false;
        }

        // 判断是否为独占全屏模式
        bool isExclusiveFullscreen = currentConfig.isFullscreen && !currentConfig.enableTransparent;

        if (isExclusiveFullscreen)
        {
            // 独占全屏模式：真正的全屏，不支持透明
            style = WS_POPUP | WS_VISIBLE;
        }
        else
        {
            // 窗口模式（包括透明全屏模拟）
            if (currentConfig.hasBorder)
            {
                style = 
                    WS_OVERLAPPED |
                    WS_CAPTION |
                    WS_SYSMENU |
                    WS_MINIMIZEBOX |
                    WS_MAXIMIZEBOX |
                    WS_VISIBLE;

                if (currentConfig.resizable)
                    style |= WS_THICKFRAME;
            }
            else
            {
                style = WS_POPUP | WS_VISIBLE;
            }
        }

        SetWindowLongPtr64(windowHandle, GWL_STYLE, (IntPtr)style);

        long exStyle = (long)GetWindowLongPtr64(windowHandle, GWL_EXSTYLE);

        if (currentConfig.enableTransparent)
            exStyle |= WS_EX_LAYERED;
        else
            exStyle &= ~WS_EX_LAYERED;

        SetWindowLongPtr64(windowHandle, GWL_EXSTYLE, (IntPtr)exStyle);

        // 如果是窗口模式，初始化保存的窗口位置
        if (!currentConfig.isFullscreen)
        {
            // 获取当前窗口位置
            Vector2Int currentPos = GetWindowPosition();
            // 如果当前位置不是默认值，使用当前位置作为保存的位置
            if (currentPos != Vector2Int.zero)
            {
                savedWindowPosition = currentPos;
            }
        }

        // 设置窗口位置和大小
        SetWindowPosition();

        ConfigureCamera();

        // 立即应用透明穿透设置
        if (currentConfig.enableTransparent && currentConfig.enableClickThrough)
        {
            // 延迟一帧应用穿透设置，确保窗口样式已生效
            Invoke(nameof(ApplyClickThrough), 0.1f);
        }
    }

    void ApplyClickThrough()
    {
        if (!currentConfig.enableTransparent || !currentConfig.enableClickThrough)
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

        if (currentConfig.isFullscreen)
        {
            // 保存当前窗口位置
            savedWindowPosition = GetWindowPosition();
            
            // 获取当前窗口所在显示器的信息
            MONITORINFO monitorInfo = GetCurrentMonitorInfo();
            
            // 根据置顶选项决定窗口层级
            IntPtr hWndInsertAfter = currentConfig.isAlwaysOnTop ? 
                (IntPtr)HWND_TOPMOST : 
                (IntPtr)HWND_NOTOPMOST;
                
            // 全屏模式：设置为当前显示器大小
            SetWindowPos(
                windowHandle,
                hWndInsertAfter,
                monitorInfo.rcMonitor.Left, monitorInfo.rcMonitor.Top, // X, Y (当前显示器的左上角)
                monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left, // 宽度
                monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top, // 高度
                SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_NOACTIVATE
            );
        }
        else
        {
            // 窗口模式：设置指定大小和保存的位置
            IntPtr hWndInsertAfter = currentConfig.isAlwaysOnTop ? 
                (IntPtr)HWND_TOPMOST : 
                (IntPtr)HWND_NOTOPMOST;
                
            SetWindowPos(
                windowHandle,
                hWndInsertAfter,
                savedWindowPosition.x, savedWindowPosition.y, // X, Y (使用保存的位置)
                currentConfig.windowSize.x, currentConfig.windowSize.y, // 宽度, 高度
                SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_NOACTIVATE
            );
        }
    }

    // ==================== 相机 ====================
    void ConfigureCamera()
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (!targetCamera) return;

        if (currentConfig.enableTransparent)
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
        if (!currentConfig.enableTransparent || !currentConfig.enableClickThrough)
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
        if (currentConfig.interactionType == WindowsStyleConfig.InteractionType.UGUI ||
            currentConfig.interactionType == WindowsStyleConfig.InteractionType.Both)
        {
            if (IsPointerOverUI())
                return true;
        }

        if (currentConfig.interactionType == WindowsStyleConfig.InteractionType.Collider ||
            currentConfig.interactionType == WindowsStyleConfig.InteractionType.Both)
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
            currentConfig.interactionLayerMask
        );
    }
}
