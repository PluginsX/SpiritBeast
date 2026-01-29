using UnityEngine;

/// <summary>
/// 精确物理拖拽手柄 - 关节版 (Joint Based)
/// 适用于：布娃娃系统、长条物体、需要精确抓取点的交互。
/// 原理：在抓取点建立弹簧关节，通过移动隐形的锚点来拉动物体。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MouseDragPhysicsHandle : MonoBehaviour
{
    [Header("拖拽力度设置")]
    [Tooltip("弹簧的硬度。值越大，物体跟手越紧；值越小，越有橡皮筋的感觉。")]
    public float springForce = 1000.0f;

    [Tooltip("阻尼。防止弹簧过度震荡。")]
    public float damper = 50.0f;

    [Tooltip("拖拽时的最大拉伸距离。如果因为卡住导致距离过大，弹簧会自动断开（可选）。")]
    public float breakDistance = 20.0f;

    [Header("投掷设置")]
    [Tooltip("松手时的最大投掷速度")]
    public float maxThrowSpeed = 20.0f;

    [Header("引用")]
    public Camera targetCamera;

    // --- 内部状态 ---
    private bool isDragging = false;
    private Rigidbody targetRigidbody; // 被拖拽的具体刚体（可能是布娃娃的手臂）
    private SpringJoint currentJoint;  // 当前生成的关节
    private Rigidbody anchorRb;        // 鼠标控制的隐形刚体锚点
    private float zDistance;           // 深度

    // 备份物理属性
    private float originalDrag;
    private float originalAngularDrag;

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void Update()
    {
        if (!isDragging || anchorRb == null) return;

        // 1. 计算鼠标在世界空间的位置
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = zDistance;
        Vector3 mouseWorldPos = targetCamera.ScreenToWorldPoint(mouseScreenPos);

        // 2. 移动锚点 (MovePosition 配合 Kinematic 使用)
        anchorRb.MovePosition(mouseWorldPos);
    }

    // =========================================================
    // 事件响应 (需在 MouseEventResponder 的 Inspector 中绑定)
    // =========================================================

    /// <summary>
    /// 开始拖拽 - 需要 RaycastHit 参数
    /// </summary>
    public void OnStartDrag(RaycastHit hit)
    {
        if (targetCamera == null) return;

        // 1. 确定我们要拖拽哪个刚体
        // 对于布娃娃，Hit 到的可能是手臂的 Collider，我们需要那个肢体的 Rigidbody
        targetRigidbody = hit.rigidbody; 
        
        // 如果点到了 Collider 但没有刚体，尝试取自身的刚体
        if (targetRigidbody == null) targetRigidbody = GetComponent<Rigidbody>();
        if (targetRigidbody == null) return; // 实在没有刚体就没法物理拖拽

        isDragging = true;

        // 2. 记录深度
        Vector3 screenPos = targetCamera.WorldToScreenPoint(hit.point);
        zDistance = screenPos.z;

        // 3. 创建隐形锚点 (Anchor)
        GameObject anchorObj = new GameObject("DragAnchor_Temp");
        anchorObj.transform.position = hit.point; // 锚点直接生成在点击点
        anchorRb = anchorObj.AddComponent<Rigidbody>();
        anchorRb.isKinematic = true; // 锚点不受力，完全由鼠标控制

        // 4. 创建弹簧关节 (SpringJoint)
        currentJoint = anchorObj.AddComponent<SpringJoint>();
        currentJoint.autoConfigureConnectedAnchor = false;
        
        // 连接配置
        currentJoint.connectedBody = targetRigidbody;
        
        // 关键：锚点设为本地 (0,0,0)，也就是 anchorObj 的位置
        currentJoint.anchor = Vector3.zero; 
        
        // 关键：连接点设为物体上的点击点 (转换为局部坐标)
        Vector3 localHitPoint = targetRigidbody.transform.InverseTransformPoint(hit.point);
        currentJoint.connectedAnchor = localHitPoint;

        // 物理参数配置
        currentJoint.spring = springForce;
        currentJoint.damper = damper;
        currentJoint.maxDistance = 0; // 理想距离为0，即紧紧吸附
        currentJoint.breakForce = Mathf.Infinity;

        // 5. 临时增加目标刚体的阻尼，防止旋转过快
        originalDrag = targetRigidbody.drag;
        originalAngularDrag = targetRigidbody.angularDrag;
        targetRigidbody.drag = 5f;       // 稍微增加一点空气阻力
        targetRigidbody.angularDrag = 5f; // 增加旋转阻力稳定姿态
    }

    /// <summary>
    /// 结束拖拽
    /// </summary>
    public void OnStopDrag()
    {
        if (!isDragging) return;
        isDragging = false;

        // 1. 销毁关节和锚点
        if (currentJoint != null)
        {
            Destroy(currentJoint.gameObject); // 销毁锚点物体会连带销毁上面的 Joint 和 RB
        }

        // 2. 恢复物理属性
        if (targetRigidbody != null)
        {
            targetRigidbody.drag = originalDrag;
            targetRigidbody.angularDrag = originalAngularDrag;

            // 3. 处理投掷 (继承最后的速度)
            // 注意：因为我们是拉着走的，松手时物体本身就有物理速度，无需像上一版那样手动计算
            Vector3 velocity = targetRigidbody.velocity;
            if (velocity.magnitude > maxThrowSpeed)
            {
                targetRigidbody.velocity = velocity.normalized * maxThrowSpeed;
            }
        }

        targetRigidbody = null;
        anchorRb = null;
        currentJoint = null;
    }
}