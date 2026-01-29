using UnityEngine;
using UnityEngine.EventSystems; // 必须引入，用于UI检测
using System.Collections.Generic;

/// <summary>
/// 窗口交互检测器
/// 功能：同时检测鼠标是否悬停在 3D模型 或 UGUI元素 上
/// 并据此控制 WindowManager 的穿透状态
/// </summary>
[RequireComponent(typeof(WindowManager))]
public class WindowHitTester : MonoBehaviour
{
    [Header("核心引用")]
    public Camera targetCamera;
    public WindowManager windowManager;

    [Header("3D 检测设置")]
    [Tooltip("哪些层的 3D 物体会被视为不穿透")]
    public LayerMask physicsLayerMask = Physics.DefaultRaycastLayers;
    public float physicsMaxDistance = 100f;

    [Header("调试")]
    public bool showDebugColor = true;
    
    // 内部状态缓存，防止每帧重复调用 Windows API 造成闪烁或性能损耗
    private bool isInteractivePrev = true; 
    
    // UI 检测所需的缓存列表，避免每帧 GC
    private List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private PointerEventData pointerEventData;

    void Start()
    {
        if (!windowManager) windowManager = GetComponent<WindowManager>();
        if (!targetCamera) targetCamera = Camera.main;
        
        // 确保场景中有 EventSystem，否则 UGUI 检测无效
        if (EventSystem.current == null)
        {
            Debug.LogError("场景中缺少 EventSystem！UGUI 检测将无法工作。请在 Hierarchy 中右键 -> UI -> Event System 创建。");
        }
    }

    void Update()
    {
        // 如果在编辑器里，不做任何穿透处理，方便调试
#if UNITY_EDITOR
        return;
#endif

        bool hitSomething = CheckHit();

        // 状态过滤：只有当交互状态发生改变时，才调用底层 API
        if (hitSomething != isInteractivePrev)
        {
            // hitSomething = true (点到了东西) -> SetClickThrough(false) (不穿透)
            // hitSomething = false (没点到东西) -> SetClickThrough(true) (穿透)
            windowManager.SetClickThrough(!hitSomething);
            
            isInteractivePrev = hitSomething;

            if (showDebugColor && targetCamera)
            {
                // 调试：点到东西变红，穿透变绿 (仅修改背景色，实际项目中可移除)
                targetCamera.backgroundColor = hitSomething ? new Color(0.2f, 0, 0, 0) : new Color(0, 0.2f, 0, 0);
            }
        }
    }

    /// <summary>
    /// 核心检测逻辑：UI || 3D
    /// </summary>
    private bool CheckHit()
    {
        // 1. 优先检测 UI (UGUI)
        if (IsPointerOverUI())
        {
            return true;
        }

        // 2. 其次检测 3D Collider
        if (IsPointerOver3D())
        {
            return true;
        }

        // 3. 既没点到 UI 也没点到 3D -> 视为穿透区域
        return false;
    }

    /// <summary>
    /// UGUI 射线检测
    /// </summary>
    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        // 设置当前鼠标位置
        if (pointerEventData == null) pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = Input.mousePosition;

        // 发射 UI 射线
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerEventData, uiRaycastResults);

        // 过滤逻辑：
        // RaycastAll 会检测所有挂载了 Graphic (Image, Text, RawImage) 且勾选了 Raycast Target 的物体
        // 如果结果数量 > 0，说明鼠标下面有 UI
        foreach (var result in uiRaycastResults)
        {
            // 这里可以增加额外的过滤条件，比如排除某些特定的 Layer
            // 目前只要是 UI 就返回 true
            return true;
        }

        return false;
    }

    /// <summary>
    /// 3D 物理射线检测
    /// </summary>
    private bool IsPointerOver3D()
    {
        if (!targetCamera) return false;

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, physicsMaxDistance, physicsLayerMask);
    }
}