using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System;

public class WindowInteraction : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private WindowManager windowManager;

    [Header("点击设置")]
    public float maxClickDuration = 0.2f;
    
    [Header("拖拽设置")]
    public float dragFrequency = 30f;
    public bool enableElasticSnap = true;
    public float snapSmoothTime = 0.12f;

    [Header("鼠标交互事件流")]
    public UnityEvent OnLeftClick;
    public UnityEvent OnRightClick;
    public UnityEvent OnDragStart;
    public UnityEvent OnDragging;
    public UnityEvent OnDragEnd;

    public bool isDragging { get; private set; } = false;
    private bool isMouseDown = false;
    private float mouseDownTime;
    private Vector2Int mouseDownCursorPos;
    private float lastDragEventTime;
    private Vector2Int dragOffset;
    private Coroutine snapCoroutine;

    void Start() {
        if (windowManager == null) windowManager = GetComponent<WindowManager>();
    }

    void Update() {
        HandleMouseInput();
        
        if (Input.GetKeyDown(KeyCode.Space)) {
            var nextMode = windowManager.currentMode == WindowManager.WindowMode.Object ? 
                           WindowManager.WindowMode.Spirit : WindowManager.WindowMode.Object;
            windowManager.SetWindowMode(nextMode);
        }
    }

    private void HandleMouseInput() {
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) {
            isMouseDown = true;
            mouseDownTime = Time.time;
            mouseDownCursorPos = windowManager.cursorPosition;
            dragOffset = windowManager.cursorPosition - windowManager.windowPosition;
        }

        if (isMouseDown && Input.GetMouseButton(0)) {
            float duration = Time.time - mouseDownTime;
            float dist = Vector2.Distance(mouseDownCursorPos, windowManager.cursorPosition);

            if (!isDragging && (duration > maxClickDuration || dist > 5f)) {
                if (windowManager.currentMode == WindowManager.WindowMode.Object) {
                    isDragging = true;
                    OnDragStart?.Invoke();
                    if (snapCoroutine != null) StopCoroutine(snapCoroutine);
                }
            }

            if (isDragging) {
                windowManager.MoveWindow(windowManager.cursorPosition.x - dragOffset.x, 
                                        windowManager.cursorPosition.y - dragOffset.y);
                if (Time.time - lastDragEventTime > 1f / dragFrequency) {
                    OnDragging?.Invoke();
                    lastDragEventTime = Time.time;
                }
            }
        }

        if (isMouseDown && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))) {
            if (isDragging) {
                isDragging = false;
                OnDragEnd?.Invoke();
                if (enableElasticSnap && windowManager.currentMode == WindowManager.WindowMode.Object)
                    snapCoroutine = StartCoroutine(DoElasticSnap());
            } else if (Time.time - mouseDownTime <= maxClickDuration) {
                if (Input.GetMouseButtonUp(0)) OnLeftClick?.Invoke();
                if (Input.GetMouseButtonUp(1)) OnRightClick?.Invoke();
            }
            isMouseDown = false;
        }
    }

    private IEnumerator DoElasticSnap() {
        Vector2Int currentPos = windowManager.windowPosition;
        WindowManager.RECT workArea = windowManager.GetCurrentMonitorInfo().rcWork;
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