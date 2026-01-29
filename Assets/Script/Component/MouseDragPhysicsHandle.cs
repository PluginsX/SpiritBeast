using UnityEngine;

/// <summary>
/// 实现鼠标拖拽刚体对象的物理手柄。
/// 特性：零延迟跟手，松手时继承加速度进行投掷。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MouseDragPhysicsHandle : MonoBehaviour
{
    [Header("设置")]
    [Tooltip("松手时的最大投掷速度限制 (m/s)")]
    public float maxThrowSpeed = 10.0f; // 稍微调大一点，因为瞬时移动产生的速度通常较快

    [Tooltip("用于坐标转换的摄像机，为空自动获取主摄像机")]
    public Camera targetCamera;

    // --- 内部变量 ---
    private Rigidbody rb;
    private bool isDragging = false;
    
    // 坐标转换相关
    private Vector3 offsetVector; // 记录抓取点相对于物体中心的偏移
    private float zDistance;      // 物体距离摄像机的深度平面
    
    // 速度计算相关
    private Vector3 previousPosition;
    private Vector3 currentVelocity; // 实时计算的拖拽速度

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void Update()
    {
        // 只有在拖拽状态下才执行跟随逻辑
        if (isDragging && targetCamera != null)
        {
            HandleDragInstant();
        }
    }

    /// <summary>
    /// 核心逻辑：瞬时跟随鼠标
    /// </summary>
    private void HandleDragInstant()
    {
        // 1. 获取当前鼠标位置并还原深度
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = zDistance;

        // 2. 转换为世界坐标
        Vector3 mouseWorldPos = targetCamera.ScreenToWorldPoint(mouseScreenPos);

        // 3. 计算目标位置 (鼠标位置 + 抓取时的偏移量)
        Vector3 targetPos = mouseWorldPos + offsetVector;

        // 4. 【关键点】瞬时移动，绝不迟疑
        // 直接修改 Transform，忽略物理引擎的移动限制，实现"上帝之手"般的控制感
        transform.position = targetPos;

        // 5. 【后台计算】虽然位置是瞬移的，但我们必须计算速度用于投掷
        // 速度 = (当前位置 - 上一帧位置) / 时间差
        if (Time.deltaTime > 0)
        {
            Vector3 instantaneousVelocity = (targetPos - previousPosition) / Time.deltaTime;
            
            // 使用一点点平滑来计算“投掷意图”，避免因为鼠标回报率导致的数值剧烈抖动
            // 0.2f 的系数意味着我们更看重当下的瞬时速度，但也保留了一点历史趋势
            currentVelocity = Vector3.Lerp(currentVelocity, instantaneousVelocity, 0.2f);
        }

        // 更新上一帧位置用于下一次计算
        previousPosition = targetPos;
    }

    // =========================================================
    // 对外提供的事件响应函数
    // =========================================================

    public void OnStartDrag()
    {
        if (targetCamera == null) return;

        isDragging = true;
        
        // 1. 开启 Kinematic：完全接管物理，不再受重力和碰撞反弹影响
        rb.isKinematic = true; 
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 2. 锁定深度 zDistance
        Vector3 screenPos = targetCamera.WorldToScreenPoint(transform.position);
        zDistance = screenPos.z;

        // 3. 锁定偏移 Offset
        // 这样你抓哪里，物体就固定在哪里，不会中心跳变
        Vector3 mouseWorldPos = targetCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, zDistance));
        offsetVector = transform.position - mouseWorldPos;
        
        // 初始化速度计算变量
        previousPosition = transform.position;
        currentVelocity = Vector3.zero;
    }

    public void OnStopDrag()
    {
        if (!isDragging) return;

        isDragging = false;

        // 1. 恢复物理模拟
        rb.isKinematic = false;

        // 2. 限制最大速度 (防止穿模或飞出宇宙)
        if (currentVelocity.magnitude > maxThrowSpeed)
        {
            currentVelocity = currentVelocity.normalized * maxThrowSpeed;
        }

        // 3. 应用刚才在后台计算好的速度
        // 这里有一个小技巧：如果速度非常小，可能是用户只是想轻轻放下
        if (currentVelocity.magnitude < 0.1f)
        {
            rb.velocity = Vector3.zero;
        }
        else
        {
            rb.velocity = currentVelocity;
        }
    }
}