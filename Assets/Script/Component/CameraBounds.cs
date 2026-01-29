using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 自动生成并实时更新包裹指定摄像机视口的碰撞墙壁。
/// 修复了图层设置不直观的问题，支持通过名称指定 Layer。
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

    // --- 修改点：使用字符串名称来配置图层，比 int 索引更直观、更不容易出错 ---
    [Header("图层与碰撞矩阵 (Layer Settings)")]
    
    [Tooltip("生成的墙壁 GameObject 所在的图层名称 (例如 Default, Ignore Raycast, Water)。\n请输入准确的图层名称。")]
    public string colliderLayer = "Default"; 

    [Space(10)]
    [Tooltip("【包含层】(Unity 2022+) 覆盖全局碰撞矩阵。\n碰撞体只与选中的层发生碰撞。如果不勾选任何层，则不会与任何物体碰撞。")]
    public LayerMask includeLayers = -1; // -1 代表 Everything (默认与所有层碰撞)

    [Tooltip("【排除层】(Unity 2022+) 覆盖全局碰撞矩阵。\n碰撞体将忽略选中层的碰撞。")]
    public LayerMask excludeLayers = 0; // 0 代表 Nothing

    [Header("调试与预览")]
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(0, 1, 0, 0.3f);

    // --- 内部变量 ---
    private Transform currentContainer;
    private Dictionary<string, BoxCollider> walls = new Dictionary<string, BoxCollider>();
    private Camera lastFrameCamera;

    // 缓存图层ID，避免每帧 String 转 Int
    private int cachedLayerID = 0;
    private string lastFrameLayerName = "";

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
        colliderLayer = "Default";
        // 默认 Include 为 Everything (-1)，这样默认行为才符合直觉（挡住所有东西）
        includeLayers = -1; 
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

        // 2. 检查图层名称变更 (缓存 ID 以提升性能)
        if (colliderLayer != lastFrameLayerName)
        {
            UpdateLayerID();
        }

        // 3. 容器管理
        if (currentContainer == null) CreateContainer();
        if (!currentContainer.gameObject.activeSelf) currentContainer.gameObject.SetActive(true);

        // 4. 更新碰撞体
        UpdateColliders();

        lastFrameCamera = targetCamera;
    }

    /// <summary>
    /// 将字符串图层名转换为 int ID，并处理无效名称
    /// </summary>
    private void UpdateLayerID()
    {
        int id = LayerMask.NameToLayer(colliderLayer);
        if (id == -1)
        {
            Debug.LogWarning($"[CameraBounds] 找不到名为 '{colliderLayer}' 的图层！已回退到 Default 层。请检查拼写。");
            id = 0; // Default
        }
        cachedLayerID = id;
        lastFrameLayerName = colliderLayer;
    }

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

            // 2. 图层设置 (使用缓存的ID)
            if (box.gameObject.layer != cachedLayerID)
            {
                box.gameObject.layer = cachedLayerID;
            }

            // 3. Layer Overrides (Unity 2022.2+ API)
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
        
        // 初始设置 Layer (使用缓存ID)
        go.layer = cachedLayerID;

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