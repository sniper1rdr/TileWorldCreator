using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    /// <summary>
    /// Shared Help / Shortcuts foldout. Default collapsed. EditorPrefs only.
    /// Callers that pin this block to the window bottom must reserve
    /// <see cref="GetReserveHeight"/> so expanded content does not clip past the window.
    /// </summary>
    internal static class WorldCoreHelpFoldout
    {
        public const string LandscapeExpandedPrefKey =
            "AglenRealms.WorldCore.Landscape.HelpExpanded";

        public const string EnvironmentExpandedPrefKey =
            "AglenRealms.WorldCore.Environment.HelpExpanded";

        // Padding / safety must be generous: under-estimating clips the last Help lines
        // (e.g. "Ctrl + LMB — erase") past the window bottom.
        private const float HelpBoxVerticalPadding = 12f;
        private const float ModuleSectionSpacing = 4f;
        private const float FoldoutBottomGap = 4f;
        private const float ExpandedHeightSafety = 8f;

        public static float HeaderReserveHeight => EditorGUIUtility.singleLineHeight + FoldoutBottomGap;

        public static bool IsExpanded(string prefKey) =>
            EditorPrefs.GetBool(prefKey, false);

        /// <summary>
        /// Vertical space to keep free below the main scroll/palette for this foldout
        /// (header only when collapsed; header + help box when expanded).
        /// </summary>
        public static float GetReserveHeight(string prefKey, string[] sharedLines, string[] moduleLines)
        {
            float reserve = HeaderReserveHeight;
            if (!IsExpanded(prefKey))
                return reserve;

            return reserve + EstimateExpandedContentHeight(sharedLines, moduleLines);
        }

        public static void Draw(string prefKey, string[] sharedLines, string[] moduleLines)
        {
            bool storedExpanded = EditorPrefs.GetBool(prefKey, false);
            bool expanded = EditorGUILayout.Foldout(storedExpanded, "Help / Shortcuts", true);
            if (expanded != storedExpanded)
            {
                EditorPrefs.SetBool(prefKey, expanded);
                // Force a layout pass so callers recompute bottom reserve on the next Repaint.
                GUI.changed = true;
            }

            if (!expanded)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawLines(sharedLines);
            if (moduleLines != null && moduleLines.Length > 0)
            {
                EditorGUILayout.Space(ModuleSectionSpacing);
                DrawLines(moduleLines);
            }

            EditorGUILayout.EndVertical();
        }

        private static float EstimateExpandedContentHeight(string[] sharedLines, string[] moduleLines)
        {
            int lineCount = CountLines(sharedLines) + CountLines(moduleLines);
            if (lineCount <= 0)
                return HelpBoxVerticalPadding;

            // LabelField(miniLabel) is typically >= singleLineHeight; CalcHeight alone under-counts.
            float lineHeight = Mathf.Max(
                EditorGUIUtility.singleLineHeight,
                EditorStyles.miniLabel.CalcHeight(new GUIContent("• X"), 100f));

            float height = HelpBoxVerticalPadding + lineCount * lineHeight + ExpandedHeightSafety;
            if (CountLines(moduleLines) > 0 && CountLines(sharedLines) > 0)
                height += ModuleSectionSpacing;

            return height;
        }

        private static int CountLines(string[] lines)
        {
            if (lines == null)
                return 0;

            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrEmpty(lines[i]))
                    count++;
            }

            return count;
        }

        private static void DrawLines(string[] lines)
        {
            if (lines == null)
                return;

            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrEmpty(lines[i]))
                    EditorGUILayout.LabelField("• " + lines[i], EditorStyles.miniLabel);
            }
        }
    }
}
