using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;

public class TrayIconManager : MonoBehaviour
{
    [Header("穿透检测")]
    public LayerMask hitLayer;
    public string tooltip = "我的器灵";

    [DllImport("shell32.dll", CharSet = CharSet.Auto)] private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);
    [DllImport("user32.dll")] private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct NOTIFYICONDATA { public int cbSize; public IntPtr hWnd; public int uID; public int uFlags; public int uCallbackMessage; public IntPtr hIcon; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip; public int dwState; public int dwStateMask; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo; public int uVersion; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle; public int dwInfoFlags; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x; public int y; }

    private IntPtr windowHandle;
    private IntPtr oldWndProcPtr;
    private WndProcDelegate newWndProc;
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private NOTIFYICONDATA nid;
    private bool isMouseOverOpaque = true;

    void Start() {
#if !UNITY_EDITOR
        windowHandle = GetComponent<WindowManager>().WindowHandle;
        InitTray();
        newWndProc = WndProc;
        oldWndProcPtr = SetWindowLongPtr64(windowHandle, -4, Marshal.GetFunctionPointerForDelegate(newWndProc));
#endif
    }

    void Update() {
        // 判定鼠标下是否有东西
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
            isMouseOverOpaque = true;
            return;
        }
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        isMouseOverOpaque = Physics.Raycast(ray, 100f, hitLayer);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) {
        if (msg == 0x0084) { // WM_NCHITTEST
            // 只有当鼠标在物体上时才返回 HTCLIENT(1)，否则返回 HTTRANSPARENT(-1)
            return isMouseOverOpaque ? (IntPtr)1 : (IntPtr)(-1);
        }
        if (msg == 0x0401 && (int)lParam == 0x0205) ShowMenu(); // 右键托盘
        return CallWindowProc(oldWndProcPtr, hWnd, msg, wParam, lParam);
    }

    private void InitTray() {
        nid = new NOTIFYICONDATA { cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)), hWnd = windowHandle, uID = 1, uFlags = 7, uCallbackMessage = 0x0401, hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512), szTip = tooltip };
        Shell_NotifyIcon(0, ref nid);
    }

    private void ShowMenu() {
        IntPtr hMenu = CreatePopupMenu();
        AppendMenu(hMenu, 0, 1, "退出程序");
        GetCursorPos(out POINT p);
        uint cmd = TrackPopupMenu(hMenu, 0x0100, p.x, p.y, 0, windowHandle, IntPtr.Zero);
        if (cmd == 1) Application.Quit();
    }

    private void OnDestroy() {
#if !UNITY_EDITOR
        Shell_NotifyIcon(2, ref nid);
        if (oldWndProcPtr != IntPtr.Zero) SetWindowLongPtr64(windowHandle, -4, oldWndProcPtr);
#endif
    }
}