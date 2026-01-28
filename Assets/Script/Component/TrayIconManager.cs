using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;

[RequireComponent(typeof(WindowManager))]
public class TrayIconManager : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private WindowManager windowManager;

    [Header("穿透检测设置")]
    [Tooltip("请确保角色模型带有 Collider，并设置在正确的 Layer 上")]
    public LayerMask hitLayer;
    public string tooltip = "桌面器灵助手";

    // --- Win32 API 导入 ---
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("shell32.dll", CharSet = CharSet.Auto)] private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);
    [DllImport("user32.dll")] private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x; public int y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct NOTIFYICONDATA { public int cbSize; public IntPtr hWnd; public int uID; public int uFlags; public int uCallbackMessage; public IntPtr hIcon; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip; }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private NOTIFYICONDATA nid;
    private IntPtr oldWndProcPtr;
    private WndProcDelegate newWndProc;
    private bool isCurrentlyTransparent = false;

    void Start()
    {
        if (windowManager == null) windowManager = GetComponent<WindowManager>();

#if !UNITY_EDITOR
        InitTray();
        // 挂钩窗口过程，用于监听托盘右键菜单消息
        newWndProc = WndProc;
        oldWndProcPtr = SetWindowLongPtr64(windowManager.WindowHandle, -4, Marshal.GetFunctionPointerForDelegate(newWndProc));
#endif
    }

    void Update()
    {
        // 1. 获取全局鼠标坐标
        POINT screenPoint;
        GetCursorPos(out screenPoint);

        // 2. 判定鼠标下是否有可交互内容 (模型或UI)
        bool shouldBeOpaque = CheckIfMouseOverOpaque(screenPoint);

        // 3. 动态切换窗口物理状态
        // 逻辑：如果正在拖拽中，强制保持非穿透状态
        bool isDragging = Input.GetMouseButton(0); 

        if ((shouldBeOpaque || isDragging) && isCurrentlyTransparent)
        {
            // 鼠标回到模型上：关闭穿透
            windowManager.SetClickThrough(false);
            isCurrentlyTransparent = false;
        }
        else if (!shouldBeOpaque && !isDragging && !isCurrentlyTransparent)
        {
            // 鼠标离开模型且未操作：开启穿透
            windowManager.SetClickThrough(true);
            isCurrentlyTransparent = true;
        }
    }

    private bool CheckIfMouseOverOpaque(POINT screenPoint)
    {
        // 在 Unity 中，射线检测需要 Camera 空间。
        // 虽然窗口可能处于穿透状态，但 Input.mousePosition 在窗口激活时通常仍然有效。
        // 如果失效，可以使用 Camera.main.ScreenPointToRay(new Vector3(screenPoint.x, Screen.height - screenPoint.y, 0))
        
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // A. 判定是否在 UGUI 上
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return true;

        // B. 判定是否在模型 Collider 上
        if (Physics.Raycast(ray, 100f, hitLayer))
            return true;

        return false;
    }

    // --- 托盘消息处理 ---
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == 0x0401) // TRAY_ICON_MSG
        {
            if ((int)lParam == 0x0205) // WM_RBUTTONUP
            {
                ShowTrayMenu();
            }
        }
        return CallWindowProc(oldWndProcPtr, hWnd, msg, wParam, lParam);
    }

    private void InitTray()
    {
        nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = windowManager.WindowHandle,
            uID = 1,
            uFlags = 7, // NIF_MESSAGE | NIF_ICON | NIF_TIP
            uCallbackMessage = 0x0401,
            // 默认图标，会自动提取 PlayerSettings 中的图标
            hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512), 
            szTip = tooltip
        };
        Shell_NotifyIcon(0, ref nid); // NIM_ADD
    }

    private void ShowTrayMenu()
    {
        IntPtr hMenu = CreatePopupMenu();
        AppendMenu(hMenu, 0, 1, "退出程序");

        POINT pos;
        GetCursorPos(out pos);

        // 必须暂时置为非穿透，否则菜单可能无法响应点击
        windowManager.SetClickThrough(false);
        
        uint cmd = TrackPopupMenu(hMenu, 0x0100, pos.x, pos.y, 0, windowManager.WindowHandle, IntPtr.Zero);
        
        if (cmd == 1) Application.Quit();
        
        // 菜单关闭后，Update 会根据鼠标位置自动恢复穿透状态
    }

    private void OnDestroy()
    {
#if !UNITY_EDITOR
        Shell_NotifyIcon(2, ref nid); // NIM_DELETE
        if (oldWndProcPtr != IntPtr.Zero)
            SetWindowLongPtr64(windowManager.WindowHandle, -4, oldWndProcPtr);
#endif
    }
}