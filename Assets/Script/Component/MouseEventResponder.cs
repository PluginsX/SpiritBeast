using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 响应器 - 升级版
/// 支持传递 RaycastHit 信息，用于精确交互
/// </summary>
[RequireComponent(typeof(Collider))]
public class MouseEventResponder : MonoBehaviour
{
    // 定义一个可以携带 RaycastHit 参数的事件类型
    [System.Serializable]
    public class RaycastHitEvent : UnityEvent<RaycastHit> { }

    [Header("调试")]
    public bool showDebugInfo = false;

    [Header("鼠标悬停事件")]
    public UnityEvent onMouseEnter;
    public UnityEvent onMouseExit;

    [Header("鼠标点击事件 (升级)")]
    // 注意：这里改用了自定义的 RaycastHitEvent
    [Tooltip("按下时触发，携带碰撞信息")]
    public RaycastHitEvent onMouseDown; 
    
    [Tooltip("松开时触发")]
    public UnityEvent onMouseUp; // 松开通常不需要碰撞点信息，只需要信号
    
    public UnityEvent onMouseClick;

    private bool isHovered = false;

    private void LogEvent(string eventName)
    {
        if (showDebugInfo)
            Debug.Log($"<color=#00FF00>[Responder]</color> <color=#FFFF00>[{gameObject.name}]</color>: {eventName}");
    }

    public void TriggerEnter()
    {
        if (!isHovered)
        {
            isHovered = true;
            LogEvent("Enter");
            onMouseEnter?.Invoke();
        }
    }

    public void TriggerExit()
    {
        if (isHovered)
        {
            isHovered = false;
            LogEvent("Exit");
            onMouseExit?.Invoke();
        }
    }

    // --- 升级：接收 hit 参数 ---
    public void TriggerDown(RaycastHit hit)
    {
        LogEvent($"Down at {hit.point}");
        // 将 hit 信息传给监听者 (即 MouseDragPhysicsHandle)
        onMouseDown?.Invoke(hit);
    }

    public void TriggerUp()
    {
        LogEvent("Up");
        onMouseUp?.Invoke();
    }

    public void TriggerClick()
    {
        LogEvent("Click");
        onMouseClick?.Invoke();
    }
}