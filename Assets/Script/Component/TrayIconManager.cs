using UnityEngine;
using System;
using System.Runtime.InteropServices;

public class TrayIconManager : MonoBehaviour
{
    [Header("托盘设置")]
    public string tooltip = "我的桌面器灵";
    
    // ==================== Win32 API 导入 ====================
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x; public int y; }

    // 窗口过程回调相关
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int NIM_ADD = 0x00;
    private const int NIM_DELETE = 0x02;
    private const int NIF_MESSAGE = 0x01;
    private const int NIF_ICON = 0x02;
    private const int NIF_TIP = 0x04;
    private const int WM_USER = 0x0400;
    private const int TRAY_ICON_MSG = WM_USER + 1;
    private const int WM_RBUTTONUP = 0x0205;

    private NOTIFYICONDATA nid;
    private IntPtr windowHandle;
    private IntPtr oldWndProcPtr;
    private WndProcDelegate newWndProc;

    void Start()
    {
        #if !UNITY_EDITOR
        windowHandle = GetComponent<WindowManager>().WindowHandle;
        InitTrayIcon();
        SubclassWindow();
        #endif
    }

    private void InitTrayIcon()
    {
        nid = new NOTIFYICONDATA();
        nid.cbSize = Marshal.SizeOf(nid);
        nid.hWnd = windowHandle;
        nid.uID = 1;
        nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        nid.uCallbackMessage = TRAY_ICON_MSG;
        // 使用程序默认图标
        nid.hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
        nid.szTip = tooltip;

        Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    private void SubclassWindow()
    {
        // 挂钩 Unity 的窗口过程，监听托盘消息
        newWndProc = new WndProcDelegate(WndProc);
        IntPtr newWndProcPtr = Marshal.GetFunctionPointerForDelegate(newWndProc);
        oldWndProcPtr = SetWindowLongPtr(windowHandle, -4, newWndProcPtr); // GWLP_WNDPROC
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == TRAY_ICON_MSG)
        {
            if ((int)lParam == WM_RBUTTONUP) // 右键点击图标
            {
                ShowTrayMenu();
            }
        }
        return CallWindowProc(oldWndProcPtr, hWnd, msg, wParam, lParam);
    }

    private void ShowTrayMenu()
    {
        IntPtr hMenu = CreatePopupMenu();
        AppendMenu(hMenu, 0, 1, "退出程序");

        POINT pos;
        GetCursorPos(out pos);

        // 设置窗口焦点以便点击菜单外可以自动关闭
        SetWindowLongPtr(windowHandle, -4, oldWndProcPtr); // 临时恢复以便处理菜单
        uint command = TrackPopupMenu(hMenu, 0x0100, pos.x, pos.y, 0, windowHandle, IntPtr.Zero);
        SetWindowLongPtr(windowHandle, -4, Marshal.GetFunctionPointerForDelegate(newWndProc)); // 重新挂钩

        if (command == 1)
        {
            Application.Quit();
        }
    }

    private void OnDestroy()
    {
        #if !UNITY_EDITOR
        Shell_NotifyIcon(NIM_DELETE, ref nid);
        if (oldWndProcPtr != IntPtr.Zero)
        {
            SetWindowLongPtr(windowHandle, -4, oldWndProcPtr);
        }
        #endif
    }
}