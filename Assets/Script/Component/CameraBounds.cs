using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 自动生成并实时更新包裹指定摄像机视口的碰撞墙壁。
/// 支持编辑器实时预览、运行时物理生成、以及 Unity 2022+ 的 Layer Overrides 设置。
/// </summary>
[ExecuteAlways]
public class CameraBounds : MonoBehaviour
{
    [Header("核心设置")]
    [Tooltip("要跟随的目标摄像机")]
    public Camera targetCamera;

    [Header("边界生成开关")]
    public bool enableTop = true;
    public bool enableBottom = true;
    public bool enableLeft = true;
    public bool enableRight = true;
    public bool enableNear = false;
    public bool enableFar = false;

    [Header("物理参数")]
    [Tooltip("墙壁厚度")]
    public float wallThickness = 1.0f;
    [Tooltip("物理材质 (3D)")]
    public PhysicMaterial physicsMaterial;

    // --- 新增：层级设置 ---
    [Header("层级与碰撞覆盖 (Layer Overrides)")]
    
    [Tooltip("生成的墙壁 GameObject 本身所在的层级 ID (0=Default)。\n如果你希望射线能穿透墙壁，可以将墙壁设为 Ignore Raycast 层。")]
    public int objectLayerIndex = 0; 

    [Tooltip("【包含层】碰撞体只与选中的层发生碰撞。\nUnity 默认逻辑为 Everything，此处根据需求默认为 Default。")]
    public LayerMask includeLayers = 1; // 1 = Default Layer (Bitmask)

    [Tooltip("【排除层】碰撞体将忽略选中层的碰撞。\n默认为 Nothing。")]
    public LayerMask excludeLayers = 0; // 0 = Nothing

    [Header("调试与预览")]
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(0, 1, 0, 0.3f);

    // --- 内部变量 ---
    private Transform currentContainer;
    private Dictionary<string, BoxCollider> walls = new Dictionary<string, BoxCollider>();
    private Camera lastFrameCamera;

    // 数据结构
    private struct WallData
    {
        public Vector3 localPos;
        public Vector3 size;
        public bool isEnabled;
    }

    private void Reset()
    {
        targetCamera = GetComponent<Camera>();
        // Reset 时将 includeLayers 设为 Default (mask = 1)
        includeLayers = 1;
        excludeLayers = 0;
    }

    private void OnDisable()
    {
        if (Application.isPlaying && currentContainer != null)
        {
            currentContainer.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        // 编辑器模式下仅允许 Inspector 刷新，不执行生成逻辑
        if (!Application.isPlaying) return;

        // 1. 摄像机变更检查
        if (targetCamera != lastFrameCamera)
        {
            HandleCameraChange();
        }

        // 2. 容器管理
        if (currentContainer == null) CreateContainer();
        if (!currentContainer.gameObject.activeSelf) currentContainer.gameObject.SetActive(true);

        // 3. 更新碰撞体
        UpdateColliders();

        lastFrameCamera = targetCamera;
    }

    /// <summary>
    /// 纯数学计算：算出墙壁位置和大小
    /// </summary>
    private Dictionary<string, WallData> CalculateWallData()
    {
        var data = new Dictionary<string, WallData>();
        if (targetCamera == null || !targetCamera.orthographic) return data;

        float height = targetCamera.orthographicSize * 2f;
        float width = height * targetCamera.aspect;
        float near = targetCamera.nearClipPlane;
        float far = targetCamera.farClipPlane;
        float depth = far - near;
        float zCenter = near + depth / 2f;

        void Add(string key, bool enable, Vector3 pos, Vector3 size)
        {
            data[key] = new WallData { localPos = pos, size = size, isEnabled = enable };
        }

        Add("Top", enableTop,
            new Vector3(0, height / 2f + wallThickness / 2f, zCenter),
            new Vector3(width + wallThickness * 2, wallThickness, depth));

        Add("Bottom", enableBottom,
            new Vector3(0, -(height / 2f + wallThickness / 2f), zCenter),
            new Vector3(width + wallThickness * 2, wallThickness, depth));

        Add("Left", enableLeft,
            new Vector3(-(width / 2f + wallThickness / 2f), 0, zCenter),
            new Vector3(wallThickness, height, depth));

        Add("Right", enableRight,
            new Vector3(width / 2f + wallThickness / 2f, 0, zCenter),
            new Vector3(wallThickness, height, depth));

        Add("Near", enableNear,
            new Vector3(0, 0, near - wallThickness / 2f),
            new Vector3(width + wallThickness * 2, height + wallThickness * 2, wallThickness));

        Add("Far", enableFar,
            new Vector3(0, 0, far + wallThickness / 2f),
            new Vector3(width + wallThickness * 2, height + wallThickness * 2, wallThickness));

        return data;
    }

    /// <summary>
    /// 运行时逻辑：同步 Collider 参数
    /// </summary>
    private void UpdateColliders()
    {
        var dataMap = CalculateWallData();

        foreach (var kvp in dataMap)
        {
            string key = kvp.Key;
            WallData data = kvp.Value;

            bool exists = walls.TryGetValue(key, out BoxCollider box);

            // 禁用逻辑
            if (!data.isEnabled)
            {
                if (exists && box != null && box.gameObject.activeSelf) box.gameObject.SetActive(false);
                continue;
            }

            // 创建逻辑
            if (!exists || box == null)
            {
                box = CreateWallObj(key);
                walls[key] = box;
            }

            // 激活
            if (!box.gameObject.activeSelf) box.gameObject.SetActive(true);

            // --- 核心更新 ---
            // 1. 位置尺寸
            box.size = data.size;
            box.transform.localPosition = data.localPos;
            box.transform.localRotation = Quaternion.identity;
            box.transform.localScale = Vector3.one;

            // 2. 层级设置 (实时更新，允许运行时调整)
            if (box.gameObject.layer != objectLayerIndex)
            {
                box.gameObject.layer = objectLayerIndex;
            }

            // 3. Layer Overrides (Unity 2022.2+ API)
            // 只有当值发生变化时才赋值，减少 overhead
            if (box.includeLayers != includeLayers) box.includeLayers = includeLayers;
            if (box.excludeLayers != excludeLayers) box.excludeLayers = excludeLayers;
        }
    }

    private void HandleCameraChange()
    {
        if (currentContainer != null)
        {
            currentContainer.SetParent(targetCamera.transform);
            currentContainer.localPosition = Vector3.zero;
            currentContainer.localRotation = Quaternion.identity;
            currentContainer.localScale = Vector3.one;
        }
    }

    private void CreateContainer()
    {
        Transform existing = targetCamera.transform.Find("[Generated_BoundsContainer]");
        if (existing != null)
        {
            currentContainer = existing;
        }
        else
        {
            GameObject go = new GameObject("[Generated_BoundsContainer]");
            currentContainer = go.transform;
            currentContainer.SetParent(targetCamera.transform);
            currentContainer.localPosition = Vector3.zero;
            currentContainer.localRotation = Quaternion.identity;
            currentContainer.localScale = Vector3.one;
        }
        
        walls.Clear();
        foreach (Transform child in currentContainer)
        {
            BoxCollider box = child.GetComponent<BoxCollider>();
            if (box != null)
            {
                string key = child.name.Replace("Wall_", "");
                walls[key] = box;
            }
        }
    }

    private BoxCollider CreateWallObj(string name)
    {
        GameObject go = new GameObject("Wall_" + name);
        go.transform.SetParent(currentContainer);
        
        // 初始设置 Layer
        go.layer = objectLayerIndex;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.material = physicsMaterial;
        
        // 初始设置 Overrides
        box.includeLayers = includeLayers;
        box.excludeLayers = excludeLayers;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        return box;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || targetCamera == null) return;

        var dataMap = CalculateWallData();
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Color oldColor = Gizmos.color;

        Gizmos.matrix = targetCamera.transform.localToWorldMatrix;
        Gizmos.color = gizmoColor;

        foreach (var kvp in dataMap)
        {
            WallData data = kvp.Value;
            if (data.isEnabled)
            {
                Gizmos.DrawCube(data.localPos, data.size);
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
                Gizmos.DrawWireCube(data.localPos, data.size);
                Gizmos.color = gizmoColor;
            }
        }

        Gizmos.matrix = oldMatrix;
        Gizmos.color = oldColor;
    }
}