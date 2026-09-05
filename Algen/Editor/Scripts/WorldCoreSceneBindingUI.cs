using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    /// <summary>
    /// Shared Scene Binding foldout chrome for World Core tabs.
    /// Stores only EditorPrefs UI expand state — no asset serialized fields.
    /// </summary>
    internal static class WorldCoreSceneBindingUI
    {
        public const string LandscapeExpandedPrefKey =
            "AglenRealms.WorldCore.Landscape.SceneBindingExpanded";

        public const string EnvironmentExpandedPrefKey =
            "AglenRealms.WorldCore.Environment.SceneBindingExpanded";

        /// <summary>
        /// Draws foldout header + summary when collapsed.
        /// Returns whether the body should be drawn this frame.
        /// </summary>
        public static bool DrawFoldout(string prefKey, string collapsedSummary, bool forceOpen)
        {
            bool storedExpanded = EditorPrefs.GetBool(prefKey, false);
            bool expanded = forceOpen || storedExpanded;

            bool newExpanded = EditorGUILayout.Foldout(expanded, "Scene Binding", true);

            if (!forceOpen && newExpanded != storedExpanded)
                EditorPrefs.SetBool(prefKey, newExpanded);

            expanded = forceOpen || EditorPrefs.GetBool(prefKey, false);

            if (!expanded && !string.IsNullOrEmpty(collapsedSummary))
                EditorGUILayout.LabelField(collapsedSummary, EditorStyles.miniLabel);

            return expanded;
        }

        public static string FormatName(Object obj, string missingLabel)
        {
            return obj != null ? obj.name : missingLabel;
        }
    }
}
