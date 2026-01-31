using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WindowStyleManager))]
public class WindowStyleManagerEditor : Editor
{
    SerializedProperty targetCameraProp;
    SerializedProperty styleConfigsProp;
    SerializedProperty autoApplyStyleProp;
    SerializedProperty defaultStyleIndexProp;

    void OnEnable()
    {
        // 顶层属性
        targetCameraProp = serializedObject.FindProperty("targetCamera");
        styleConfigsProp = serializedObject.FindProperty("styleConfigs");
        autoApplyStyleProp = serializedObject.FindProperty("autoApplyStyle");
        defaultStyleIndexProp = serializedObject.FindProperty("defaultStyleIndex");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ===== Camera =====
        EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);
        if (targetCameraProp != null)
            EditorGUILayout.PropertyField(targetCameraProp);

        EditorGUILayout.Space(6);

        // ===== 样式配置 =====
        EditorGUILayout.LabelField("样式配置", EditorStyles.boldLabel);

        if (styleConfigsProp != null)
        {
            EditorGUILayout.PropertyField(styleConfigsProp);
        }

        EditorGUILayout.Space(6);

        // ===== 自动应用样式 =====
        EditorGUILayout.LabelField("自动应用样式", EditorStyles.boldLabel);

        if (autoApplyStyleProp != null)
        {
            EditorGUILayout.PropertyField(autoApplyStyleProp, new GUIContent("自动应用样式"));
            
            bool autoApply = autoApplyStyleProp.boolValue;
            
            if (autoApply)
            {
                EditorGUI.indentLevel++;
                
                if (styleConfigsProp != null && styleConfigsProp.arraySize > 0)
                {
                    if (defaultStyleIndexProp != null)
                    {
                        int maxIndex = styleConfigsProp.arraySize - 1;
                        defaultStyleIndexProp.intValue = EditorGUILayout.IntSlider(
                            new GUIContent("默认样式索引"),
                            defaultStyleIndexProp.intValue,
                            0,
                            maxIndex
                        );
                        
                        // 显示当前选中的样式名称（如果有）
                        if (styleConfigsProp.arraySize > defaultStyleIndexProp.intValue)
                        {
                            var config = styleConfigsProp.GetArrayElementAtIndex(defaultStyleIndexProp.intValue);
                            EditorGUILayout.LabelField("当前样式", $"索引: {defaultStyleIndexProp.intValue}");
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("请先添加至少一个样式配置。", MessageType.Warning);
                }
                
                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.Space(6);

        // ===== 使用说明 =====
        EditorGUILayout.LabelField("使用说明", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. 在样式配置列表中添加多个预设的窗口样式\n" +
            "2. 勾选'自动应用样式'以在游戏运行时自动应用默认样式\n" +
            "3. 使用 ChangeWindowStyleByIndex(index) API 在运行时切换样式\n" +
            "4. 索引从0开始，对应样式配置列表中的位置",
            MessageType.Info
        );

        serializedObject.ApplyModifiedProperties();
    }
}
