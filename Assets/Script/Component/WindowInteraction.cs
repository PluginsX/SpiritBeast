using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;

public class WindowInteraction : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private WindowManager windowManager;

    [Header("拖拽与弹性设置")]
    public bool allowDrag = true;
    public bool enableElasticSnap = true;
    [Tooltip("回弹动画的顺滑时间，越小越快")]
    public float snapSmoothTime = 0.12f;
    [Tooltip("如果点击在UI上，是否依然允许拖拽窗口")]
    public bool dragEvenOnUI = false;

    [Header("右键菜单设置")]
    public bool allowMenu = true;
    public GameObject menuObject;
    public Vector2 menuOffset = new Vector2(10, -10);

    // ==================== Windows API ====================
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern bool SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    private Coroutine snapCoroutine;
    private bool isDragging = false;

    void Start()
    {
        if (windowManager == null) windowManager = GetComponent<WindowManager>();
        if (menuObject != null) menuObject.SetActive(false);
    }

    void Update()
    {
        // 1. 处理左键拖拽
        if (allowDrag && Input.GetMouseButtonDown(0))
        {
            bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!isOverUI || dragEvenOnUI)
            {
                StartCoroutine(DragRoutine());
            }
        }

        // 2. 处理右键菜单
        if (allowMenu && Input.GetMouseButtonDown(1))
        {
            ShowMenu();
        }

        // 3. 点击空白处关闭菜单
        if (Input.GetMouseButtonDown(0) && menuObject != null && menuObject.activeSelf)
        {
            if (EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject())
                menuObject.SetActive(false);
        }
    }

    /// <summary>
    /// 核心拖拽协程：利用SendMessage的阻塞特性
    /// </summary>
    private IEnumerator DragRoutine()
    {
        isDragging = true;
        if (snapCoroutine != null) StopCoroutine(snapCoroutine); // 抓取时停止回弹

        #if !UNITY_EDITOR
        ReleaseCapture();
        // SendMessage会阻塞主线程直到鼠标松开
        SendMessage(windowManager.WindowHandle, 0xA1, 0x02, 0); 
        #endif

        // --- 鼠标在此处松开 ---
        isDragging = false;
        Input.ResetInputAxes(); // 关键修复：重置输入防止点击失效

        if (enableElasticSnap)
        {
            snapCoroutine = StartCoroutine(DoElasticSnap());
        }
        yield return null;
    }

    /// <summary>
    /// 弹性回弹逻辑：使用 SmoothDamp 算法
    /// </summary>
    private IEnumerator DoElasticSnap()
    {
        Vector2Int currentPos = windowManager.GetWindowPosition();
        WindowManager.RECT workArea = windowManager.GetWorkArea();

        // 计算目标安全位置
        int targetX = Mathf.Clamp(currentPos.x, workArea.Left, workArea.Right - windowManager.targetWidth);
        int targetY = Mathf.Clamp(currentPos.y, workArea.Top, workArea.Bottom - windowManager.targetHeight);

        // 如果已经在安全区内，直接退出
        if (targetX == currentPos.x && targetY == currentPos.y) yield break;

        Vector2 targetVec = new Vector2(targetX, targetY);
        Vector2 currentFloatPos = new Vector2(currentPos.x, currentPos.y);
        Vector2 velocity = Vector2.zero;

        // 执行平滑位移
        while (Vector2.Distance(currentFloatPos, targetVec) > 0.5f)
        {
            currentFloatPos = Vector2.SmoothDamp(currentFloatPos, targetVec, ref velocity, snapSmoothTime);
            windowManager.MoveWindow((int)currentFloatPos.x, (int)currentFloatPos.y);
            yield return null;
        }

        // 最终精准对齐
        windowManager.MoveWindow(targetX, targetY);
    }

    private void ShowMenu()
    {
        if (menuObject != null)
        {
            menuObject.SetActive(true);
            Vector3 mousePos = Input.mousePosition;
            menuObject.transform.position = new Vector3(mousePos.x + menuOffset.x, mousePos.y + menuOffset.y, 0);
        }
    }

    // 绑定至UI按钮的方法
    public void QuitApp() => Application.Quit();
}