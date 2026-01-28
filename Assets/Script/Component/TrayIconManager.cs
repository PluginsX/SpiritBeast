using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;

public class TrayIconManager : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private WindowManager windowManager;
    [SerializeField] private WindowInteraction interaction;

    [Header("穿透检测")]
    public LayerMask hitLayer;
    public string tooltip = "桌面器灵助手";

    // ==================== Win32 API 导入 ====================
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct NOTIFYICONDATA {
        public int cbSize; public IntPtr hWnd; public int uID; public int uFlags; public int uCallbackMessage; public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)] private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);
    [DllImport("user32.dll")] private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);
    [DllImport("shell32.dll", CharSet = CharSet.Auto)] private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string lpModuleName);
    
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private NOTIFYICONDATA nid;
    private IntPtr oldWndProcPtr;
    private WndProcDelegate newWndProc;
    private bool isCurrentlyTransparent = false;

    void Start() {
        if (windowManager == null) windowManager = GetComponent<WindowManager>();
        if (interaction == null) interaction = GetComponent<WindowInteraction>();

#if !UNITY_EDITOR
        InitTray();
        newWndProc = WndProc;
        oldWndProcPtr = SetWindowLongPtr64(windowManager.WindowHandle, -4, Marshal.GetFunctionPointerForDelegate(newWndProc));
#endif
    }

    void Update() {
        bool shouldBeOpaque = CheckIfMouseOverContent();
        // 如果正在拖拽，强制不穿透，否则会丢失焦点
        bool isUserInteracting = Input.GetMouseButton(0) || interaction.isDragging;

        if ((shouldBeOpaque || isUserInteracting) && isCurrentlyTransparent) {
            windowManager.SetClickThrough(false);
            isCurrentlyTransparent = false;
        }
        else if (!shouldBeOpaque && !isUserInteracting && !isCurrentlyTransparent) {
            windowManager.SetClickThrough(true);
            isCurrentlyTransparent = true;
        }
    }

    private bool CheckIfMouseOverContent() {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, 100f, hitLayer);
    }

    private void InitTray() {
        // --- 核心修复：从 EXE 提取项目图标 ---
        string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        IntPtr hIcon = ExtractIcon(GetModuleHandle(null), exePath, 0);

        // 兜底：如果提取失败，加载默认图标
        if (hIcon == IntPtr.Zero) hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512);

        nid = new NOTIFYICONDATA {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = windowManager.WindowHandle,
            uID = 1,
            uFlags = 7, // NIF_MESSAGE | NIF_ICON | NIF_TIP
            uCallbackMessage = 0x0401,
            hIcon = hIcon,
            szTip = tooltip
        };
        Shell_NotifyIcon(0, ref nid); // NIM_ADD
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) {
        if (msg == 0x0401 && (int)lParam == 0x0205) ShowTrayMenu(); // TRAY_ICON_MSG + WM_RBUTTONUP
        return CallWindowProc(oldWndProcPtr, hWnd, msg, wParam, lParam);
    }

    private void ShowTrayMenu() {
        IntPtr hMenu = CreatePopupMenu();
        AppendMenu(hMenu, 0, 1, "退出程序");
        
        WindowManager.POINT pos;
        WindowManager.GetCursorPos(out pos);
        
        windowManager.SetClickThrough(false); // 弹出菜单时必须恢复实体，否则菜单无法交互
        uint cmd = TrackPopupMenu(hMenu, 0x0100, pos.x, pos.y, 0, windowManager.WindowHandle, IntPtr.Zero);
        
        if (cmd == 1) Application.Quit();
    }

    private void OnDestroy() {
#if !UNITY_EDITOR
        Shell_NotifyIcon(2, ref nid); // NIM_DELETE
        if (oldWndProcPtr != IntPtr.Zero) SetWindowLongPtr64(windowManager.WindowHandle, -4, oldWndProcPtr);
#endif
    }
}