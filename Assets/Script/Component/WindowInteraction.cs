using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;

public class WindowInteraction : MonoBehaviour
{
    [SerializeField] private WindowManager windowManager;

    [Header("弹性设置")]
    public bool enableElasticSnap = true;
    public float snapSmoothTime = 0.12f;

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern bool SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    private Coroutine snapCoroutine;

    // --- 外部调用接口：切换模式 ---
    public void SwitchMode(WindowManager.WindowMode newMode) {
        if (snapCoroutine != null) StopCoroutine(snapCoroutine);
        windowManager.SetWindowMode(newMode);
    }

    void Update() {
        // 只有在“器物”模式下才需要处理拖拽后的回弹判定
        // “器灵”模式下窗口已经全屏，不需要拖拽移动窗口位置
        if (windowManager.currentMode == WindowManager.WindowMode.Object) {
            if (Input.GetMouseButtonDown(0)) {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                StartCoroutine(DragRoutine());
            }
        }

        // 测试代码：按下空格键切换模式
        if (Input.GetKeyDown(KeyCode.Space)) {
            var nextMode = windowManager.currentMode == WindowManager.WindowMode.Object ? 
                           WindowManager.WindowMode.Spirit : WindowManager.WindowMode.Object;
            SwitchMode(nextMode);
        }
    }

    private IEnumerator DragRoutine() {
        if (snapCoroutine != null) StopCoroutine(snapCoroutine);
        #if !UNITY_EDITOR
        ReleaseCapture();
        SendMessage(windowManager.WindowHandle, 0xA1, 0x02, 0); 
        #endif
        Input.ResetInputAxes(); 

        if (enableElasticSnap && windowManager.currentMode == WindowManager.WindowMode.Object) {
            snapCoroutine = StartCoroutine(DoElasticSnap());
        }
        yield return null;
    }

    private IEnumerator DoElasticSnap() {
        Vector2Int currentPos = windowManager.GetWindowPosition();
        WindowManager.MONITORINFO mi = windowManager.GetCurrentMonitorInfo();
        WindowManager.RECT workArea = mi.rcWork; // 回弹依然参考工作区（避开任务栏）

        int targetX = Mathf.Clamp(currentPos.x, workArea.Left, workArea.Right - windowManager.objWidth);
        int targetY = Mathf.Clamp(currentPos.y, workArea.Top, workArea.Bottom - windowManager.objHeight);

        if (targetX == currentPos.x && targetY == currentPos.y) yield break;

        Vector2 targetVec = new Vector2(targetX, targetY);
        Vector2 currentFloatPos = new Vector2(currentPos.x, currentPos.y);
        Vector2 velocity = Vector2.zero;

        while (Vector2.Distance(currentFloatPos, targetVec) > 0.5f) {
            currentFloatPos = Vector2.SmoothDamp(currentFloatPos, targetVec, ref velocity, snapSmoothTime);
            windowManager.MoveWindow((int)currentFloatPos.x, (int)currentFloatPos.y);
            yield return null;
        }
        windowManager.MoveWindow(targetX, targetY);
    }
}