using UnityEngine;

/// <summary>
/// 物理拖拽手柄 - 终极版
/// 特性：
/// 1. 0延迟跟手（在无障碍时）。
/// 2. 完美的物理碰撞（不会穿墙）。
/// 3. 自动防止穿出屏幕边界（配合 CameraBounds）。
/// 4. 丝滑的 1:1 控制与拉力模式切换。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MouseDragPhysicsHandle : MonoBehaviour
{
    [Header("拖拽参数")]
    [Tooltip("最大拖拽速度 (m/s)。\n决定了'拉力模式'的强度，也防止物体因速度过快穿透墙壁。\n建议值：20 ~ 50")]
    public float maxDragSpeed = 40.0f;

    [Tooltip("拖拽时的旋转阻尼。\n设大一点(如 5-10)可以防止拖拽时物体疯狂自旋。")]
    public float dragAngularDrag = 10.0f;

    [Tooltip("是否在拖拽时冻结旋转？\n对于某些物体，拖拽时保持角度可能手感更好。")]
    public bool freezeRotationOnDrag = false;

    [Tooltip("松手时的投掷速度限制")]
    public float maxThrowSpeed = 15.0f;

    [Header("引用")]
    [Tooltip("用于坐标转换的摄像机")]
    public Camera targetCamera;

    // --- 内部变量 ---
    private Rigidbody rb;
    private bool isDragging = false;
    
    // 坐标相关
    private Vector3 offsetVector; 
    private float zDistance;      

    // 物理状态备份（用于松手恢复）
    private float originalAngularDrag;
    private bool originalUseGravity;
    private CollisionDetectionMode originalCollisionMode;
    private RigidbodyConstraints originalConstraints;

    // 速度计算（用于投掷）
    private Vector3 currentVelocityForThrow; 

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void Update()
    {
        // 我们只在 Update 里处理输入坐标转换，物理移动全部放 FixedUpdate
        if (isDragging && targetCamera != null)
        {
            UpdateDragTarget();
        }
    }

    private void FixedUpdate()
    {
        // 物理核心逻辑
        if (isDragging)
        {
            ApplyVelocityControl();
        }
    }

    // 目标世界坐标
    private Vector3 targetWorldPos;

    /// <summary>
    /// 计算鼠标对应的目标位置
    /// </summary>
    private void UpdateDragTarget()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = zDistance;
        Vector3 mouseWorldPos = targetCamera.ScreenToWorldPoint(mouseScreenPos);
        targetWorldPos = mouseWorldPos + offsetVector;
    }

    /// <summary>
    /// 核心物理控制逻辑
    /// </summary>
    private void ApplyVelocityControl()
    {
        // 1. 计算从当前位置到目标位置的向量
        Vector3 directionToTarget = targetWorldPos - rb.position;
        
        // 2. 计算这一帧需要多快的速度才能正好到达目标点
        // 速度 = 距离 / 时间
        Vector3 desiredVelocity = directionToTarget / Time.fixedDeltaTime;

        // 3. 【防穿模关键点】限制最大速度
        // 如果需要的速度太快（说明鼠标移动太快，或者被墙挡住了导致距离拉大），
        // 我们就截断速度。这就自然形成了“拉力模式”。
        // 如果需要的速度很小（说明鼠标就在边上），我们就不截断，这就形成了“位置模式”。
        Vector3 finalVelocity = Vector3.ClampMagnitude(desiredVelocity, maxDragSpeed);

        // 4. 应用速度
        // 直接修改 Velocity 是物理引擎中最接近“上帝之手”又保留碰撞的方法
        rb.velocity = finalVelocity;

        // 记录速度用于 Update 中的逻辑或 Debug
        currentVelocityForThrow = rb.velocity;
    }

    // =========================================================
    // 事件响应
    // =========================================================

    public void OnStartDrag()
    {
        if (targetCamera == null) return;
        isDragging = true;

        // --- 1. 备份原始物理状态 ---
        originalUseGravity = rb.useGravity;
        originalAngularDrag = rb.angularDrag;
        originalCollisionMode = rb.collisionDetectionMode;
        originalConstraints = rb.constraints;

        // --- 2. 设置适合拖拽的物理状态 ---
        
        // 关闭重力，否则拿起来会往下掉
        rb.useGravity = false; 
        
        // 【关键】保持 isKinematic = false
        // 只有非运动学刚体才会和墙壁发生物理碰撞。如果开了 Kinematic，它就会穿墙。
        rb.isKinematic = false; 

        // 增加旋转阻尼，防止拿起来像陀螺一样转
        rb.angularDrag = dragAngularDrag;

        // 【关键】开启连续碰撞检测
        // 防止在快速拖拽（拉力模式）时因为速度太快穿透墙壁（Tunneling）
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (freezeRotationOnDrag)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        // --- 3. 计算抓取偏移 ---
        Vector3 screenPos = targetCamera.WorldToScreenPoint(transform.position);
        zDistance = screenPos.z;
        Vector3 mouseWorldPos = targetCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, zDistance));
        offsetVector = transform.position - mouseWorldPos;

        // 初始化目标位置，防止第一帧跳变
        targetWorldPos = transform.position;
    }

    public void OnStopDrag()
    {
        if (!isDragging) return;
        isDragging = false;

        // --- 1. 恢复原始物理状态 ---
        rb.useGravity = originalUseGravity;
        rb.angularDrag = originalAngularDrag;
        rb.collisionDetectionMode = originalCollisionMode;
        rb.constraints = originalConstraints;

        // --- 2. 处理投掷 ---
        // 限制最大投掷速度
        Vector3 throwVelocity = Vector3.ClampMagnitude(rb.velocity, maxThrowSpeed);
        
        // 细微体验优化：如果速度极小，直接归零，防止物体在桌面上缓慢滑动
        if (throwVelocity.magnitude < 0.1f)
        {
            rb.velocity = Vector3.zero;
        }
        else
        {
            rb.velocity = throwVelocity;
        }
    }
}