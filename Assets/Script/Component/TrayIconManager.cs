using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;

[RequireComponent(typeof(WindowManager))]
public class TrayIconManager : MonoBehaviour
{
    [Header("托盘设置")]
    public string tooltip = "桌面器灵";

    [Header("命中检测 (精确穿透)")]
    public bool enablePixelPerfectClick = true;
    [Tooltip("必须给模型添加Collider！只有在此Layer上的物体才响应点击和拖拽")]
    public LayerMask hitLayer;

    // ==================== API ====================
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct NOTIFYICONDATA { public int cbSize; public IntPtr hWnd; public int uID; public int uFlags; public int uCallbackMessage; public IntPtr hIcon; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip; public int dwState; public int dwStateMask; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo; public int uVersion; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle; public int dwInfoFlags; }
    [DllImport("shell32.dll", CharSet = CharSet.Auto)] private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);
    [DllImport("user32.dll")] private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x; public int y; }

    private const int WM_NCHITTEST = 0x0084;
    private const int HTCAPTION = 0x02;   // 标题栏（可拖拽）
    private const int HTCLIENT = 0x01;    // 客户区（响应UI点击）
    private const int HTTRANSPARENT = -1; // 透明（穿透点击）
    private const int WM_ENTERSIZEMOVE = 0x0231; // 进入移动
    private const int WM_EXITSIZEMOVE = 0x0232;  // 退出移动

    private IntPtr windowHandle;
    private IntPtr oldWndProcPtr;
    private WndProcDelegate newWndProc;
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private NOTIFYICONDATA nid;

    private int currentHitType = HTCLIENT;
    private WindowInteraction interaction;

    void Start() {
        interaction = GetComponent<WindowInteraction>();
#if !UNITY_EDITOR
        windowHandle = GetComponent<WindowManager>().WindowHandle;
        InitTray();
        Subclass();
#endif
    }

    void Update() {
        if (!enablePixelPerfectClick) { currentHitType = HTCLIENT; return; }

        // 1. 检查是否在UI上
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
            currentHitType = HTCLIENT; // 允许UI点击
            return;
        }

        // 2. 检查鼠标下是否有模型
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, 100f, hitLayer)) {
            // 在模型上：设为 HTCAPTION 即可实现“按住模型就能拖动窗口”
            // 如果你希望只有特定逻辑才拖动，可以改回 HTCLIENT
            currentHitType = HTCAPTION; 
        } else {
            currentHitType = HTTRANSPARENT; // 穿透
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) {
        if (msg == WM_NCHITTEST) return (IntPtr)currentHitType;
        
        if (msg == WM_EXITSIZEMOVE) {
            // 当系统原生拖拽结束时，通知 Interaction 脚本尝试弹性回弹
            if (interaction != null) interaction.OnSystemDragEnd();
        }

        if (msg == (0x0400 + 1) && (int)lParam == 0x0205) ShowTrayMenu();
        return CallWindowProc(oldWndProcPtr, hWnd, msg, wParam, lParam);
    }

    // ... InitTray, Subclass, ShowTrayMenu, OnDestroy 保持原样 ...
#region Boilerplate
    private void InitTray() {
        nid = new NOTIFYICONDATA { cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)), hWnd = windowHandle, uID = 1, uFlags = 7, uCallbackMessage = 0x0400 + 1, hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512), szTip = tooltip };
        Shell_NotifyIcon(0, ref nid);
    }
    private void Subclass() {
        newWndProc = WndProc;
        oldWndProcPtr = SetWindowLongPtr64(windowHandle, -4, Marshal.GetFunctionPointerForDelegate(newWndProc));
    }
    private void ShowTrayMenu() {
        IntPtr hMenu = CreatePopupMenu();
        AppendMenu(hMenu, 0, 1, "退出程序");
        GetCursorPos(out POINT pos);
        uint cmd = TrackPopupMenu(hMenu, 0x0100, pos.x, pos.y, 0, windowHandle, IntPtr.Zero);
        if (cmd == 1) Application.Quit();
    }
    private void OnDestroy() {
#if !UNITY_EDITOR
        Shell_NotifyIcon(2, ref nid);
        if (oldWndProcPtr != IntPtr.Zero) SetWindowLongPtr64(windowHandle, -4, oldWndProcPtr);
#endif
    }
#endregion
}