using UnityEngine;

/// <summary>
/// 全局鼠标事件管理器。
/// 修复了 MouseUp 逻辑，确保按下物体的对象一定能收到松开事件。
/// </summary>
public class MouseEventManager : MonoBehaviour
{
    [Header("设置")]
    [Tooltip("发射射线的摄像机，为空则自动获取 MainCamera")]
    public Camera targetCamera;

    [Tooltip("检测层级：只检测这些层的物体")]
    public LayerMask detectionLayers = Physics.DefaultRaycastLayers;

    [Tooltip("射线最大检测距离")]
    public float maxDistance = 100f;

    [Header("调试")]
    [Tooltip("是否在控制台输出鼠标的全局操作信息")]
    public bool showDebugInfo = false;

    // --- 内部状态记录 ---
    private MouseEventResponder currentHoveredResponder; 
    private MouseEventResponder pendingClickResponder; // 记录按下时的对象

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

        // --- 1. 调试信息 ---
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

    // --- 核心修改区域 ---
    private void HandleClickLogic(MouseEventResponder hitResponder)
    {
        // --- 鼠标按下 ---
        if (Input.GetMouseButtonDown(0)) 
        {
            if (hitResponder != null)
            {
                hitResponder.TriggerDown();
                // 关键点：记录谁被按下了
                pendingClickResponder = hitResponder;
            }
        }

        // --- 鼠标松开 ---
        if (Input.GetMouseButtonUp(0))
        {
            // 逻辑修正：
            // 只要之前有物体被按下，无论现在鼠标在哪，都必须通知该物体“松开”了。
            // 这样才能闭环，保证拖拽等逻辑能正确结束。
            if (pendingClickResponder != null)
            {
                pendingClickResponder.TriggerUp();
            }

            // 判断完整的点击 (Click)：
            // 只有当“当初按下的物体” == “现在鼠标悬停的物体”时，才算点击
            if (pendingClickResponder != null && pendingClickResponder == hitResponder)
            {
                pendingClickResponder.TriggerClick();
            }

            // 重置状态
            pendingClickResponder = null;
        }
    }

    private void DebugGlobalMouseEvents()
    {
        string prefix = "<color=#00FFFF>[MouseEventManager]</color> ";

        if (Input.GetMouseButtonDown(0)) Debug.Log(prefix + "Left Mouse Button DOWN");
        if (Input.GetMouseButtonUp(0)) Debug.Log(prefix + "Left Mouse Button UP");
        
        // 只有位置改变才输出
        if (Vector3.Distance(Input.mousePosition, lastMousePosition) > 1f)
        {
            // Debug.Log(prefix + "Mouse Moved"); // 减少刷屏，注释掉
            lastMousePosition = Input.mousePosition;
        }
    }
}