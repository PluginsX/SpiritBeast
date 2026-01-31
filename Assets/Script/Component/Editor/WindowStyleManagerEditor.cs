using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WindowStyleManager))]
public class WindowStyleManagerEditor : Editor
{
    SerializedProperty targetCameraProp;
    SerializedProperty windowConfigProp;

    // windowConfig 内部字段
    SerializedProperty isFullscreenProp;
    SerializedProperty windowSizeProp;
    SerializedProperty hasBorderProp;
    SerializedProperty resizableProp;
    SerializedProperty enableTransparentProp;
    SerializedProperty enableClickThroughProp;
    SerializedProperty interactionTypeProp;
    SerializedProperty interactionLayerMaskProp;

    void OnEnable()
    {
        // 顶层
        targetCameraProp = serializedObject.FindProperty("targetCamera");
        windowConfigProp = serializedObject.FindProperty("windowConfig");

        if (windowConfigProp == null)
            return;

        // windowConfig 子字段（统一从 Relative 获取，避免路径错误）
        isFullscreenProp        = windowConfigProp.FindPropertyRelative("isFullscreen");
        windowSizeProp          = windowConfigProp.FindPropertyRelative("windowSize");
        hasBorderProp           = windowConfigProp.FindPropertyRelative("hasBorder");
        resizableProp           = windowConfigProp.FindPropertyRelative("resizable");
        enableTransparentProp   = windowConfigProp.FindPropertyRelative("enableTransparent");
        enableClickThroughProp  = windowConfigProp.FindPropertyRelative("enableClickThrough");
        interactionTypeProp     = windowConfigProp.FindPropertyRelative("interactionType");
        interactionLayerMaskProp= windowConfigProp.FindPropertyRelative("interactionLayerMask");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ===== Camera =====
        EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);
        if (targetCameraProp != null)
            EditorGUILayout.PropertyField(targetCameraProp);

        EditorGUILayout.Space(6);

        // ===== Window Config =====
        EditorGUILayout.LabelField("Window Style", EditorStyles.boldLabel);

        if (windowConfigProp == null)
        {
            EditorGUILayout.HelpBox(
                "windowConfig is missing or failed to serialize.",
                MessageType.Error
            );
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // --- Fullscreen ---
        if (isFullscreenProp != null)
            EditorGUILayout.PropertyField(isFullscreenProp, new GUIContent("Fullscreen"));

        bool isFullscreen = isFullscreenProp != null && isFullscreenProp.boolValue;

        // --- Windowed-only options ---
        if (!isFullscreen)
        {
            EditorGUI.indentLevel++;

            if (windowSizeProp != null)
                EditorGUILayout.PropertyField(windowSizeProp, new GUIContent("Window Size"));

            if (hasBorderProp != null)
                EditorGUILayout.PropertyField(hasBorderProp, new GUIContent("Has Border"));

            bool hasBorder = hasBorderProp != null && hasBorderProp.boolValue;

            if (hasBorder && resizableProp != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(resizableProp, new GUIContent("Resizable"));
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6);

        // ===== Transparency =====
        EditorGUILayout.LabelField("Transparency", EditorStyles.boldLabel);

        // 判断透明选项是否应该显示
        // 透明选项只在以下前提至少有一个成立时才出现：
        // 1. 全屏模式（此时默认就是无边框的）
        // 2. 窗口模式，且无边框时
        bool shouldShowTransparentOptions = isFullscreen || (!isFullscreen && hasBorderProp != null && !hasBorderProp.boolValue);

        if (shouldShowTransparentOptions)
        {
            if (enableTransparentProp != null)
                EditorGUILayout.PropertyField(enableTransparentProp, new GUIContent("Enable Transparent"));

            bool isTransparentEnabled = enableTransparentProp != null && enableTransparentProp.boolValue;

            if (isTransparentEnabled)
            {
                EditorGUI.indentLevel++;

                if (enableClickThroughProp != null)
                    EditorGUILayout.PropertyField(enableClickThroughProp, new GUIContent("Click Through"));

                if (interactionTypeProp != null)
                    EditorGUILayout.PropertyField(interactionTypeProp, new GUIContent("Interaction Type"));

                if (interactionLayerMaskProp != null)
                    EditorGUILayout.PropertyField(interactionLayerMaskProp, new GUIContent("Interaction Layer Mask"));

                EditorGUI.indentLevel--;
            }
        }
        else
        {
            EditorGUILayout.HelpBox("透明选项仅在全屏模式或窗口模式且无边框时可用。", MessageType.Info);
        }

        EditorGUILayout.Space(6);

        // ===== Window Topmost =====
        EditorGUILayout.LabelField("Window Topmost", EditorStyles.boldLabel);

        // 获取透明选项的值，用于置顶选项的判断
        bool transparent = enableTransparentProp != null && enableTransparentProp.boolValue;

        // 置顶选项只在以下两种情况下显示：
        // 1. 全屏模式 + 透明（此时本质是窗口）
        // 2. 窗口模式（无论是否透明）
        bool shouldShowAlwaysOnTop = (!isFullscreen) || (isFullscreen && transparent);

        if (shouldShowAlwaysOnTop)
        {
            if (windowConfigProp != null)
            {
                SerializedProperty isAlwaysOnTopProp = windowConfigProp.FindPropertyRelative("isAlwaysOnTop");
                if (isAlwaysOnTopProp != null)
                    EditorGUILayout.PropertyField(isAlwaysOnTopProp, new GUIContent("Always On Top"));
            }
        }
        else
        {
            EditorGUILayout.HelpBox("置顶选项仅在窗口模式或透明全屏模式下可用。", MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
