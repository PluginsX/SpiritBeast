using UnityEngine;

/// <summary>
/// 管理器 - 升级版
/// 负责将 RaycastHit 数据传递给 Responder
/// </summary>
public class MouseEventManager : MonoBehaviour
{
    [Header("设置")]
    public Camera targetCamera;
    public LayerMask detectionLayers = Physics.DefaultRaycastLayers;
    public float maxDistance = 100f;

    [Header("调试")]
    public bool showDebugInfo = false;

    private MouseEventResponder currentHoveredResponder; 
    private MouseEventResponder pendingClickResponder; 
    private Vector3 lastMousePosition;

    private void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        lastMousePosition = Input.mousePosition;
    }

    private void Update()
    {
        if (targetCamera == null) return;

        if (showDebugInfo) DebugGlobalMouseEvents();

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        // 射线检测
        bool isHit = Physics.Raycast(ray, out hit, maxDistance, detectionLayers);
        MouseEventResponder hitResponder = null;

        if (isHit)
        {
            // 尝试获取组件
            hit.collider.TryGetComponent(out hitResponder);
            
            // 如果物体没有直接挂载，尝试向父级查找（因为布娃娃的肢体Collider通常是子物体）
            if (hitResponder == null)
            {
                hitResponder = hit.collider.GetComponentInParent<MouseEventResponder>();
            }
        }

        HandleHoverLogic(hitResponder);
        
        // 传递 hit 信息
        HandleClickLogic(hitResponder, hit);
    }

    private void HandleHoverLogic(MouseEventResponder newResponder)
    {
        if (currentHoveredResponder != newResponder)
        {
            if (currentHoveredResponder != null) currentHoveredResponder.TriggerExit();
            if (newResponder != null) newResponder.TriggerEnter();
            currentHoveredResponder = newResponder;
        }
    }

    // --- 升级：接收 hit 参数 ---
    private void HandleClickLogic(MouseEventResponder hitResponder, RaycastHit hit)
    {
        // 按下
        if (Input.GetMouseButtonDown(0)) 
        {
            if (hitResponder != null)
            {
                // 关键修改：将 hit 传进去
                hitResponder.TriggerDown(hit);
                pendingClickResponder = hitResponder;
            }
        }

        // 松开
        if (Input.GetMouseButtonUp(0))
        {
            if (pendingClickResponder != null)
            {
                pendingClickResponder.TriggerUp();
            }

            if (pendingClickResponder != null && pendingClickResponder == hitResponder)
            {
                pendingClickResponder.TriggerClick();
            }
            pendingClickResponder = null;
        }
    }

    private void DebugGlobalMouseEvents()
    {
        // (保持原有的调试代码不变)
    }
}