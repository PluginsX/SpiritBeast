using UnityEngine;
using System.Collections;

public class WindowInteraction : MonoBehaviour
{
    [SerializeField] private WindowManager windowManager;
    public bool enableElasticSnap = true;
    public float snapSmoothTime = 0.12f;

    private Coroutine snapCoroutine;

    // 当用户从模型（HTCAPTION区域）松开鼠标时，WindowManager 会拦截到系统消息并调用此方法
    public void OnSystemDragEnd()
    {
        if (enableElasticSnap && gameObject.activeInHierarchy)
        {
            if (snapCoroutine != null) StopCoroutine(snapCoroutine);
            snapCoroutine = StartCoroutine(DoElasticSnap());
        }
    }

    private IEnumerator DoElasticSnap()
    {
        // 稍微等待一帧，确保位置已由系统最终确定
        yield return null;

        Vector2Int currentPos = windowManager.GetWindowPosition();
        WindowManager.RECT workArea = windowManager.GetCurrentMonitorWorkArea();

        int targetX = Mathf.Clamp(currentPos.x, workArea.Left, workArea.Right - windowManager.targetWidth);
        int targetY = Mathf.Clamp(currentPos.y, workArea.Top, workArea.Bottom - windowManager.targetHeight);

        if (targetX == currentPos.x && targetY == currentPos.y) yield break;

        Vector2 targetVec = new Vector2(targetX, targetY);
        Vector2 currentFloatPos = new Vector2(currentPos.x, currentPos.y);
        Vector2 velocity = Vector2.zero;

        while (Vector2.Distance(currentFloatPos, targetVec) > 0.5f)
        {
            currentFloatPos = Vector2.SmoothDamp(currentFloatPos, targetVec, ref velocity, snapSmoothTime);
            windowManager.MoveWindow((int)currentFloatPos.x, (int)currentFloatPos.y);
            yield return null;
        }
        windowManager.MoveWindow(targetX, targetY);
    }
}