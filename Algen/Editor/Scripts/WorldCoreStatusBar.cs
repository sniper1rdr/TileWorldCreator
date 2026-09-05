using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    /// <summary>
    /// Compact one-line status with full text in tooltip when truncated.
    /// </summary>
    internal static class WorldCoreStatusBar
    {
        public static float ReserveHeight => EditorGUIUtility.singleLineHeight + 2f;

        public static void Draw(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            EditorGUI.LabelField(rect, new GUIContent(text, text), EditorStyles.miniLabel);
        }
    }
}
