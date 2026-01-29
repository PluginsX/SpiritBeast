using UnityEngine;

/// <summary>
/// 绘制正交摄像机的视口范围（考虑近裁剪面和远裁剪面）
/// </summary>
public class DrawCameraViewPort : MonoBehaviour
{
    [Header("目标摄像机")]
    [Tooltip("要绘制视口的摄像机。如果为空，则不绘制。")]
    public Camera targetCamera;

    [Header("显示设置")]
    [Tooltip("Gizmo 线框的颜色")]
    public Color gizmoColor = Color.green;

    [Tooltip("是否始终显示？如果为false，则只在选中该物体时显示")]
    public bool alwaysShow = true;

    // Reset 是 Unity 编辑器的一个回调函数
    // 当组件第一次被添加到 GameObject，或者用户在组件菜单点击 "Reset" 时调用
    private void Reset()
    {
        // 自动尝试获取当前物体上的 Camera 组件
        targetCamera = GetComponent<Camera>();
    }

    private void OnDrawGizmos()
    {
        if (alwaysShow)
        {
            DrawBox();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!alwaysShow)
        {
            DrawBox();
        }
    }

    private void DrawBox()
    {
        // 1. 如果没有指定摄像机，直接返回，不绘制
        if (targetCamera == null)
        {
            return;
        }

        // 2. 确保是正交摄像机
        if (!targetCamera.orthographic)
        {
            return;
        }

        // 保存原本的 Gizmos 颜色和矩阵，绘制完后恢复，保持良好的编码习惯
        Color oldColor = Gizmos.color;
        Matrix4x4 oldMatrix = Gizmos.matrix;

        // 3. 设置 Gizmos 颜色
        Gizmos.color = gizmoColor;

        // 4. 设置 Gizmos 的矩阵为“目标摄像机”的局部坐标系
        // 这一点很重要：即使本脚本挂在物体A，如果targetCamera是物体B，
        // 我们也应该基于物体B的坐标系来绘制
        Gizmos.matrix = targetCamera.transform.localToWorldMatrix;

        // 5. 获取计算参数
        float height = targetCamera.orthographicSize * 2f;
        float width = height * targetCamera.aspect;
        
        float near = targetCamera.nearClipPlane;
        float far = targetCamera.farClipPlane;

        // 6. 计算 Box 的尺寸和中心
        // 长度 = 远裁剪面 - 近裁剪面
        float length = far - near;

        // 如果远裁剪面比近裁剪面还近，或者距离为0，就没有绘制的必要了
        if (length <= 0) return;

        // Box 的大小
        Vector3 size = new Vector3(width, height, length);

        // Box 的中心点 (局部坐标系)
        // 在局部坐标系中，摄像机看向 +Z 方向 (Unity View Matrix 处理了翻转，但在 Transform 层面 forward 是 +Z)
        // Box 应该从 Z = near 开始，延伸到 Z = far
        // 所以中心点 Z = near + (长度的一半)
        float centerZ = near + (length / 2f);
        Vector3 center = new Vector3(0f, 0f, centerZ);

        // 7. 绘制
        Gizmos.DrawWireCube(center, size);

        // 8. 恢复 Gizmos 状态
        Gizmos.matrix = oldMatrix;
        Gizmos.color = oldColor;
    }
}