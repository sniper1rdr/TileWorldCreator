using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal static class EnvironmentLayersPanel
    {
        private const int MaxVisibleLayerRows = 7;

        private static Vector2 layersScrollPosition;
        private static int lastAutoScrollActiveIndex = -1;

        private static float LayerRowHeight => EditorGUIUtility.singleLineHeight + 2f;

        public static void Draw(EnvironmentRoot target)
        {
            if (target == null)
                return;

            target.EnsureDefaultLayer();

            int layerCount = target.Layers.Count;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Layers ({layerCount})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUIContent addLayerContent = EditorGUIUtility.TrTextContentWithIcon(
                " Add Layer",
                "Add an environment layer",
                "Toolbar Plus");
            if (GUILayout.Button(addLayerContent, GUILayout.Width(110f)))
                AddLayer(target);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2f);

            int activeIndex = target.ActiveLayerIndex;
            bool canDelete = target.Layers.Count > 1;
            LayerRowOptions options = LayerRowDrawer.EnvironmentDefaults(canDelete);

            float maxHeight = MaxVisibleLayerRows * LayerRowHeight;
            bool useScroll = layerCount > MaxVisibleLayerRows;

            if (!useScroll)
                layersScrollPosition = Vector2.zero;

            if (activeIndex != lastAutoScrollActiveIndex)
            {
                EnsureActiveLayerVisible(activeIndex, layerCount, maxHeight);
                lastAutoScrollActiveIndex = activeIndex;
            }

            if (useScroll)
            {
                layersScrollPosition = EditorGUILayout.BeginScrollView(
                    layersScrollPosition,
                    GUILayout.Height(maxHeight));
            }

            for (int i = 0; i < target.Layers.Count; i++)
                DrawLayerRow(target, i, activeIndex, options);

            if (useScroll)
                EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
        }

        private static void EnsureActiveLayerVisible(int activeIndex, int layerCount, float viewHeight)
        {
            if (layerCount <= MaxVisibleLayerRows || activeIndex < 0 || activeIndex >= layerCount)
                return;

            float rowHeight = LayerRowHeight;
            float activeTop = activeIndex * rowHeight;
            float activeBottom = activeTop + rowHeight;

            if (activeTop < layersScrollPosition.y)
                layersScrollPosition.y = activeTop;
            else if (activeBottom > layersScrollPosition.y + viewHeight)
                layersScrollPosition.y = Mathf.Max(0f, activeBottom - viewHeight);
        }

        private static void DrawLayerRow(
            EnvironmentRoot target,
            int layerIndex,
            int activeIndex,
            LayerRowOptions options)
        {
            EnvironmentLayerDefinition layer = target.Layers[layerIndex];

            LayerRowOutput output = LayerRowDrawer.Draw(
                new LayerRowInput
                {
                    name = layer.name,
                    enabled = true,
                    visible = layer.visible,
                    height = layer.height,
                    isActive = layerIndex == activeIndex
                },
                options);

            if (output.selectRow)
                SetActiveLayer(target, layerIndex);

            if (output.nameChanged)
            {
                Undo.RecordObject(target, "Rename Environment Layer");
                layer.name = output.name;
                target.RenameLayerRoot(layerIndex);
                EditorUtility.SetDirty(target);
            }

            if (output.visibilityToggled)
            {
                Undo.RecordObject(target, "Toggle Environment Layer Visibility");
                layer.visible = !layer.visible;
                target.ApplyLayerVisibility(layerIndex);
                EditorUtility.SetDirty(target);
                SceneView.RepaintAll();
            }

            if (output.heightChanged)
            {
                Undo.RecordObject(target, "Set Environment Layer Height");
                layer.height = output.height;
                target.RebuildLayerRoots();
                EditorUtility.SetDirty(target);
                SceneView.RepaintAll();
            }

            if (output.deleteClicked)
            {
                RemoveLayer(target, layerIndex);
                GUIUtility.ExitGUI();
            }
        }

        private static void SetActiveLayer(EnvironmentRoot target, int layerIndex)
        {
            if (target.ActiveLayerIndex == layerIndex)
            {
                if (EnvironmentPainterState.TryDeactivatePainting())
                {
                    SceneView.RepaintAll();
                    LandscapeLevelManagerWindow.RequestRepaintIfOpen();
                }

                return;
            }

            // Navigation only — no Undo entry.
            target.SetActiveLayer(layerIndex);
            EnvironmentPainterState.SetActiveLayer(layerIndex);
            lastAutoScrollActiveIndex = -1;
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }

        private static void AddLayer(EnvironmentRoot target)
        {
            Undo.RecordObject(target, "Add Environment Layer");
            target.AddLayer();
            EnvironmentPainterState.SetActiveLayer(target.ActiveLayerIndex);
            lastAutoScrollActiveIndex = -1;
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }

        private static void RemoveLayer(EnvironmentRoot target, int layerIndex)
        {
            Undo.RecordObject(target, "Remove Environment Layer");
            target.RemoveLayerAt(layerIndex);
            EnvironmentPainterState.SetActiveLayer(target.ActiveLayerIndex);
            lastAutoScrollActiveIndex = -1;
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }
    }
}
