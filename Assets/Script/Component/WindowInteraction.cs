using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System;

public class WindowInteraction : MonoBehaviour
{
    [SerializeField] private WindowManager windowManager;

    [Header("点击设置")]
    public float maxClickDuration = 0.2f;
    
    [Header("拖拽设置")]
    public float dragFrequency = 30f;
    public bool enableElasticSnap = true;
    public float snapSmoothTime = 0.12f;

    [Header("鼠标事件流")]
    public UnityEvent OnLeftClick;
    public UnityEvent OnRightClick;
    public UnityEvent OnDragStart;
    public UnityEvent OnDragging; // 按照频率触发
    public UnityEvent OnDragEnd;

    // 内部状态
    private bool isMouseDown = false;
    private float mouseDownTime;
    private Vector2Int mouseDownCursorPos;
    private bool isDragging = false;
    private float lastDragEventTime;
    
    private Vector2Int dragOffset; // 鼠标相对窗口左上角的偏移

    private Coroutine snapCoroutine;

    void Update()
    {
        HandleMouseInput();
        
        // 测试切换
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var nextMode = windowManager.currentMode == WindowManager.WindowMode.Object ? 
                           WindowManager.WindowMode.Spirit : WindowManager.WindowMode.Object;
            windowManager.SetWindowMode(nextMode);
        }
    }

    private void HandleMouseInput()
    {
        // 1. 按下瞬间 (注意：由于穿透逻辑，透明处不会触发此处)
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            isMouseDown = true;
            mouseDownTime = Time.time;
            mouseDownCursorPos = windowManager.cursorPosition;
            
            // 计算拖拽偏移 (用于手动拖拽)
            dragOffset = windowManager.cursorPosition - windowManager.windowPosition;
        }

        // 2. 持续按住 (处理拖拽判定与执行)
        if (isMouseDown && Input.GetMouseButton(0))
        {
            float duration = Time.time - mouseDownTime;
            float dist = Vector2.Distance(mouseDownCursorPos, windowManager.cursorPosition);

            // 如果按下时间超过阈值或移动距离较大，判定为拖拽
            if (!isDragging && (duration > maxClickDuration || dist > 5f))
            {
                if (windowManager.currentMode == WindowManager.WindowMode.Object)
                {
                    isDragging = true;
                    OnDragStart?.Invoke();
                    if (snapCoroutine != null) StopCoroutine(snapCoroutine);
                }
            }

            if (isDragging)
            {
                // 执行手动同步拖拽
                windowManager.MoveWindow(windowManager.cursorPosition.x - dragOffset.x, 
                                        windowManager.cursorPosition.y - dragOffset.y);

                // 频率触发拖拽中事件
                if (Time.time - lastDragEventTime > 1f / dragFrequency)
                {
                    OnDragging?.Invoke();
                    lastDragEventTime = Time.time;
                }
            }
        }

        // 3. 抬起瞬间
        if (isMouseDown && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)))
        {
            float duration = Time.time - mouseDownTime;

            if (isDragging)
            {
                isDragging = false;
                OnDragEnd?.Invoke();
                if (enableElasticSnap && windowManager.currentMode == WindowManager.WindowMode.Object)
                {
                    snapCoroutine = StartCoroutine(DoElasticSnap());
                }
            }
            else if (duration <= maxClickDuration)
            {
                // 触发单击事件
                if (Input.GetMouseButtonUp(0)) OnLeftClick?.Invoke();
                if (Input.GetMouseButtonUp(1)) OnRightClick?.Invoke();
            }

            isMouseDown = false;
        }
    }

    private IEnumerator DoElasticSnap()
    {
        Vector2Int currentPos = windowManager.windowPosition;
        WindowManager.RECT workArea = windowManager.GetCurrentMonitorInfo().rcWork;

        int targetX = Mathf.Clamp(currentPos.x, workArea.Left, workArea.Right - windowManager.objWidth);
        int targetY = Mathf.Clamp(currentPos.y, workArea.Top, workArea.Bottom - windowManager.objHeight);

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
}