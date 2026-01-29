using UnityEngine;

namespace Museum.Debug
{
    public static class Log
    {
        public static void Print(string category, string level, string message)
        {
            #if UNITY_EDITOR||UNITY_DEVELOPMENT_BUILD
            // 根据等级输出不同类型的日志
            string logMessage = $"[{category}] {message}";
            switch (level.ToLower())
            {
                case "debug":
                    UnityEngine.Debug.Log(logMessage);
                    break;
                case "warning":
                    UnityEngine.Debug.LogWarning(logMessage);
                    break;
                case "error":
                    UnityEngine.Debug.LogError(logMessage);
                    break;
                default:
                    UnityEngine.Debug.Log(logMessage);
                    break;
            }
            #endif
        }
    }
}
