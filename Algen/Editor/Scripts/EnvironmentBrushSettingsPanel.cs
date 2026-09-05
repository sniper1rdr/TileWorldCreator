using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal static class EnvironmentBrushSettingsPanel
    {
        private const string ExpandedPrefKey =
            "AglenRealms.WorldCore.Environment.PlacementSettingsExpanded";

        public static void Draw(EnvironmentBrushSettings settings, Object undoTarget = null)
        {
            bool storedExpanded = EditorPrefs.GetBool(ExpandedPrefKey, false);
            bool expanded = EditorGUILayout.Foldout(storedExpanded, "Placement Settings", true);
            if (expanded != storedExpanded)
                EditorPrefs.SetBool(ExpandedPrefKey, expanded);

            if (!expanded)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();

            settings.randomRotation = EditorGUILayout.Toggle("Random Rotation", settings.randomRotation);

            settings.randomScale = EditorGUILayout.Toggle("Random Scale", settings.randomScale);
            EditorGUI.BeginDisabledGroup(!settings.randomScale);
            settings.randomScaleRange = EditorGUILayout.Vector2Field("Range", settings.randomScaleRange);
            EditorGUI.EndDisabledGroup();

            settings.alignToSurface = EditorGUILayout.Toggle("Align To Surface", settings.alignToSurface);

            EditorGUI.BeginDisabledGroup(!settings.alignToSurface);
            settings.alignMode = (EnvironmentAlignMode)EditorGUILayout.EnumPopup("Align Mode", settings.alignMode);
            if (settings.alignMode == EnvironmentAlignMode.All)
                settings.alignLayerMask = LayerMaskField("Align Layers", settings.alignLayerMask);
            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
            {
                if (undoTarget != null)
                    EditorUtility.SetDirty(undoTarget);

                if (EnvironmentPainterState.HasActivePrefab)
                    EnvironmentPainterState.RollPlacementRandoms();

                SceneView.RepaintAll();
            }

            EditorGUILayout.EndVertical();
        }

        private static LayerMask LayerMaskField(string label, LayerMask layerMask)
        {
            int mask = EditorGUILayout.MaskField(label, layerMask.value, InternalEditorUtility.layers);
            layerMask.value = mask;
            return layerMask;
        }
    }
}
