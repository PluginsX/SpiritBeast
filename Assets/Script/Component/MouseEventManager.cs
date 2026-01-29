using UnityEngine;

/// <summary>
/// 全局鼠标事件管理器。
/// 负责发射射线，检测碰撞，并向 MouseEventResponder 分发事件。
/// 支持鼠标全局事件的调试输出。
/// </summary>
public class MouseEventManager : MonoBehaviour
{
    [Header("设置")]
    [Tooltip("发射射线的摄像机，为空则自动获取 MainCamera")]
    public Camera targetCamera;

    [Tooltip("检测层级：只检测这些层的物体")]
    public LayerMask detectionLayers = Physics.DefaultRaycastLayers;

    [Tooltip("射线最大检测距离")]
    public float maxDistance = 1000f;

    [Header("调试")]
    [Tooltip("是否在控制台输出鼠标的全局操作信息")]
    public bool showDebugInfo = false;

    // --- 内部状态记录 ---
    private MouseEventResponder currentHoveredResponder; 
    private MouseEventResponder pendingClickResponder;   

    // 用于检测鼠标是否移动的上一帧位置
    private Vector3 lastMousePosition;

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
        lastMousePosition = Input.mousePosition;
    }

    private void Update()
    {
        if (targetCamera == null) return;

        // --- 1. 调试信息：全局鼠标行为 ---
        if (showDebugInfo)
        {
            DebugGlobalMouseEvents();
        }

        // --- 2. 发射射线检测 ---
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        bool isHit = Physics.Raycast(ray, out hit, maxDistance, detectionLayers);

        MouseEventResponder hitResponder = null;

        if (isHit)
        {
            hit.collider.TryGetComponent(out hitResponder);
        }

        // --- 3. 处理悬停 (Enter / Exit) ---
        HandleHoverLogic(hitResponder);

        // --- 4. 处理点击 (Down / Up / Click) ---
        HandleClickLogic(hitResponder);
    }

    /// <summary>
    /// 输出全局鼠标调试信息
    /// </summary>
    private void DebugGlobalMouseEvents()
    {
        string prefix = "<color=#00FFFF>[MouseEventManager]</color> ";

        // 按下
        if (Input.GetMouseButtonDown(0)) Debug.Log(prefix + "Left Mouse Button DOWN");
        if (Input.GetMouseButtonDown(1)) Debug.Log(prefix + "Right Mouse Button DOWN");
        if (Input.GetMouseButtonDown(2)) Debug.Log(prefix + "Middle Mouse Button DOWN");

        // 松开
        if (Input.GetMouseButtonUp(0)) Debug.Log(prefix + "Left Mouse Button UP");
        if (Input.GetMouseButtonUp(1)) Debug.Log(prefix + "Right Mouse Button UP");
        if (Input.GetMouseButtonUp(2)) Debug.Log(prefix + "Middle Mouse Button UP");

        // 滚轮
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0) Debug.Log(prefix + "Mouse Scroll: " + scroll);

        // 移动 (只有位置改变才输出，防止每帧刷屏导致卡顿)
        if (Input.mousePosition != lastMousePosition)
        {
            Debug.Log(prefix + "Mouse Moved to: " + Input.mousePosition);
            lastMousePosition = Input.mousePosition;
        }
    }

    private void HandleHoverLogic(MouseEventResponder newResponder)
    {
        if (currentHoveredResponder != newResponder)
        {
            if (currentHoveredResponder != null)
            {
                currentHoveredResponder.TriggerExit();
            }

            if (newResponder != null)
            {
                newResponder.TriggerEnter();
            }

            currentHoveredResponder = newResponder;
        }
    }

    private void HandleClickLogic(MouseEventResponder hitResponder)
    {
        // 鼠标按下
        if (Input.GetMouseButtonDown(0)) 
        {
            if (hitResponder != null)
            {
                hitResponder.TriggerDown();
                pendingClickResponder = hitResponder;
            }
        }

        // 鼠标抬起
        if (Input.GetMouseButtonUp(0))
        {
            if (hitResponder != null)
            {
                hitResponder.TriggerUp();
            }

            if (pendingClickResponder != null && pendingClickResponder == hitResponder)
            {
                pendingClickResponder.TriggerClick();
            }

            pendingClickResponder = null;
        }
    }
}