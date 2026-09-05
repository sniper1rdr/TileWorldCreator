using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal sealed class EnvironmentScenePaintTool : IWorldCoreSceneTool
    {
        public int Priority => 10;

        public bool CanActivate()
        {
            return WorldCoreEditorSession.EnvironmentPaintTarget != null &&
                   WorldCoreEditorSession.ActiveEditorMode == WorldCoreEditorMode.Environment &&
                   EnvironmentPainterState.HasActivePrefab;
        }

        public void OnActivate()
        {
        }

        public void OnDeactivate()
        {
            EnvironmentPlacementGhost.Dispose();
        }

        public void CancelActiveOperation()
        {
            EnvironmentPlacementGhost.Hide();
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            EnvironmentRoot environment = WorldCoreEditorSession.EnvironmentPaintTarget;
            if (environment == null)
                return;

            Event e = Event.current;
            if (e.type == EventType.MouseMove)
                sceneView.Repaint();

            GameObject prefab = EnvironmentPainterState.ActivePrefab;
            if (prefab == null)
            {
                EnvironmentPlacementGhost.Hide();
                return;
            }

            if (e.alt)
            {
                EnvironmentPlacementGhost.Hide();
                return;
            }

            EnvironmentBrushSettings brushSettings = EnvironmentPainterState.BrushSettings;
            float fallbackPlaneY = environment.GetActiveLayerWorldPlaneY();
            DualGrid3D alignTarget = environment.ResolveAlignLandscapeTarget();
            if (!EnvironmentPlacementUtility.TryGetPlacement(
                    e,
                    environment,
                    alignTarget,
                    brushSettings,
                    fallbackPlaneY,
                    out EnvironmentPlacementPose pose))
            {
                EnvironmentPlacementGhost.Hide();
                return;
            }

            EnvironmentPlacementGhost.Draw(prefab, pose);
            HandlePlacementInput(environment, e, pose);
        }

        private static void HandlePlacementInput(EnvironmentRoot environment, Event e, EnvironmentPlacementPose pose)
        {
            if (e.type != EventType.MouseDown || e.button != 0 || e.alt)
                return;

            GameObject prefab = EnvironmentPainterState.ActivePrefab;
            if (prefab == null)
                return;

            Transform parent = environment.GetActiveLayerRoot();
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
                return;

            EnvironmentPlacementUtility.ApplyPose(instance.transform, pose);

            Undo.RegisterCreatedObjectUndo(instance, "Place Environment Object");
            Selection.activeGameObject = instance;
            EditorUtility.SetDirty(environment);
            EnvironmentPainterState.RollPlacementRandoms();
            e.Use();
        }
    }
}
