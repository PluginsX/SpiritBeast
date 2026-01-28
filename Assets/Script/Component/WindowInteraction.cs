using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;

public class WindowInteraction : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private WindowManager windowManager;

    [Header("拖拽与吸附")]
    public bool allowDrag = true;
    public bool enableElasticSnap = true;
    [Range(0.05f, 0.3f)] public float snapSmoothTime = 0.12f;
    public bool dragEvenOnUI = false;

    [Header("右键菜单")]
    public bool allowMenu = true;
    public GameObject menuObject;
    public Vector2 menuOffset = new Vector2(10, -10);

    // ==================== Windows API ====================
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern bool SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    private Coroutine snapCoroutine;

    void Start()
    {
        if (windowManager == null) windowManager = GetComponent<WindowManager>();
        if (menuObject != null) menuObject.SetActive(false);
    }

    void Update()
    {
        // 1. 处理左键按下：开始拖拽
        if (allowDrag && Input.GetMouseButtonDown(0))
        {
            // 注意：因为有 TrayIconManager 的穿透逻辑，透明处收不到点击
            // 只有点在模型/UI上时，才会执行到这里
            if (!dragEvenOnUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            StartCoroutine(DragRoutine());
        }

        // 2. 右键菜单
        if (allowMenu && Input.GetMouseButtonDown(1)) ShowMenu();

        // 3. 点击空白关闭菜单
        if (Input.GetMouseButtonDown(0) && menuObject != null && menuObject.activeSelf)
        {
            if (EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject())
                menuObject.SetActive(false);
        }
    }

    private IEnumerator DragRoutine()
    {
        if (snapCoroutine != null) StopCoroutine(snapCoroutine);

        #if !UNITY_EDITOR
        ReleaseCapture();
        // SendMessage 会阻塞 Unity 直到鼠标抬起，这保证了手感的同步
        SendMessage(windowManager.WindowHandle, 0xA1, 0x02, 0); 
        #endif

        // --- 鼠标在此刻已经抬起 ---
        Input.ResetInputAxes(); 

        if (enableElasticSnap)
        {
            snapCoroutine = StartCoroutine(DoElasticSnap());
        }
        yield return null;
    }

    private IEnumerator DoElasticSnap()
    {
        Vector2Int currentPos = windowManager.GetWindowPosition();
        
        // 获取工作区
        WindowManager.RECT workArea = windowManager.GetCurrentMonitorWorkArea();

        // 计算目标安全位置
        int targetX = Mathf.Clamp(currentPos.x, workArea.Left, workArea.Right - windowManager.targetWidth);
        int targetY = Mathf.Clamp(currentPos.y, workArea.Top, workArea.Bottom - windowManager.targetHeight);

        if (targetX == currentPos.x && targetY == currentPos.y) yield break;

        Vector2 targetVec = new Vector2(targetX, targetY);
        Vector2 currentFloatPos = new Vector2(currentPos.x, currentPos.y);
        Vector2 velocity = Vector2.zero;

        while (Vector2.Distance(currentFloatPos, targetVec) > 0.5f)
        {
            currentFloatPos = Vector2.SmoothDamp(currentFloatPos, targetVec, ref velocity, snapSmoothTime);
            windowManager.MoveWindow((int)currentFloatPos.x, (int)currentFloatPos.y);
            yield return null;
        }

        windowManager.MoveWindow(targetX, targetY);
    }

    private void ShowMenu()
    {
        if (menuObject != null)
        {
            menuObject.SetActive(true);
            Vector3 mPos = Input.mousePosition;
            menuObject.transform.position = new Vector3(mPos.x + menuOffset.x, mPos.y + menuOffset.y, 0);
        }
    }

    public void QuitApp() => Application.Quit();
}