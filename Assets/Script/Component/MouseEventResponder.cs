using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 挂载在具体的 3D 物体上 (必须有 Collider)。
/// 接收鼠标事件并触发对应的 UnityEvent。
/// 支持调试信息输出，报告自身触发了什么事件。
/// </summary>
[RequireComponent(typeof(Collider))]
public class MouseEventResponder : MonoBehaviour
{
    [Header("调试")]
    [Tooltip("是否在控制台输出当前物体被触发的事件")]
    public bool showDebugInfo = false;

    [Header("鼠标悬停事件")]
    public UnityEvent onMouseEnter;
    public UnityEvent onMouseExit;

    [Header("鼠标点击事件")]
    public UnityEvent onMouseDown;
    public UnityEvent onMouseUp;
    public UnityEvent onMouseClick;

    // 内部状态
    private bool isHovered = false;

    // 调试输出辅助方法
    private void LogEvent(string eventName)
    {
        if (showDebugInfo)
        {
            Debug.Log($"<color=#00FF00>[MouseEventResponder]</color> <color=#FFFF00>[{gameObject.name}]</color> triggered: <b>{eventName}</b>");
        }
    }

    // --- 事件触发接口 ---

    public void TriggerEnter()
    {
        if (!isHovered)
        {
            isHovered = true;
            LogEvent("OnMouseEnter");
            onMouseEnter?.Invoke();
        }
    }

    public void TriggerExit()
    {
        if (isHovered)
        {
            isHovered = false;
            LogEvent("OnMouseExit");
            onMouseExit?.Invoke();
        }
    }

    public void TriggerDown()
    {
        LogEvent("OnMouseDown");
        onMouseDown?.Invoke();
    }

    public void TriggerUp()
    {
        LogEvent("OnMouseUp");
        onMouseUp?.Invoke();
    }

    public void TriggerClick()
    {
        LogEvent("OnMouseClick (Full Click)");
        onMouseClick?.Invoke();
    }
}