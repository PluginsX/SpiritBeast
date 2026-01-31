#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WindowStyleManager))]
public class WindowStyleManagerEditor : Editor
{
    SerializedProperty cfg;

    void OnEnable()
    {
        cfg = serializedObject.FindProperty("windowConfig");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var isFullscreen = cfg.FindPropertyRelative("isFullscreen");
        EditorGUILayout.PropertyField(isFullscreen);

        if (!isFullscreen.boolValue)
        {
            EditorGUILayout.PropertyField(cfg.FindPropertyRelative("windowSize"));
            EditorGUILayout.PropertyField(cfg.FindPropertyRelative("hasBorder"));

            if (cfg.FindPropertyRelative("hasBorder").boolValue)
            {
                EditorGUILayout.PropertyField(cfg.FindPropertyRelative("resizable"));
            }

            EditorGUILayout.PropertyField(cfg.FindPropertyRelative("isTopmost"));
        }
        else
        {
            EditorGUILayout.PropertyField(cfg.FindPropertyRelative("asWallpaper"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("透明与交互", EditorStyles.boldLabel);

        var transparent = cfg.FindPropertyRelative("enableTransparent");
        EditorGUILayout.PropertyField(transparent);

        if (transparent.boolValue)
        {
            var click = cfg.FindPropertyRelative("enableClickThrough");
            EditorGUILayout.PropertyField(click);

            if (click.boolValue)
            {
                EditorGUILayout.PropertyField(cfg.FindPropertyRelative("interactionType"));
                EditorGUILayout.PropertyField(cfg.FindPropertyRelative("interactionLayerMask"));
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(cfg.FindPropertyRelative("enableDPIAware"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
