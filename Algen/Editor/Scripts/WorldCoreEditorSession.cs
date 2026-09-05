using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal static class WorldCoreEditorSession
    {
        public static DualGrid3D LandscapePaintTarget { get; private set; }
        public static EnvironmentRoot EnvironmentPaintTarget { get; private set; }

        /// <summary>Legacy alias for landscape painting tools.</summary>
        public static DualGrid3D PaintTarget => LandscapePaintTarget;

        public static WorldCoreEditorMode ActiveEditorMode { get; private set; } = WorldCoreEditorMode.Landscape;

        public static void SetActiveEditorMode(WorldCoreEditorMode mode)
        {
            if (ActiveEditorMode == mode)
                return;

            WorldCoreEditorMode previousMode = ActiveEditorMode;
            LandscapeLevelManagerWindow.HandleLeavingEditorMode(previousMode);

            ActiveEditorMode = mode;
            RefreshPaintTarget();
            SceneView.RepaintAll();
        }

        public static void RefreshPaintTarget()
        {
            LandscapePaintTarget = ResolveLandscapePaintTarget();
            EnvironmentPaintTarget = ResolveEnvironmentPaintTarget();
        }

        private static DualGrid3D ResolveLandscapePaintTarget()
        {
            if (ActiveEditorMode != WorldCoreEditorMode.Landscape)
                return null;

            if (Selection.activeGameObject != null)
            {
                DualGrid3D selected = Selection.activeGameObject.GetComponentInParent<DualGrid3D>();
                if (selected != null)
                    return selected;
            }

            if (LandscapeLevelManagerWindow.TryGetLandscapeTarget(out DualGrid3D managerTarget))
                return managerTarget;

    #if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<DualGrid3D>();
    #else
            return Object.FindObjectOfType<DualGrid3D>();
    #endif
        }

        private static EnvironmentRoot ResolveEnvironmentPaintTarget()
        {
            if (ActiveEditorMode != WorldCoreEditorMode.Environment)
                return null;

            if (Selection.activeGameObject != null)
            {
                EnvironmentRoot selected = Selection.activeGameObject.GetComponentInParent<EnvironmentRoot>();
                if (selected != null)
                    return selected;
            }

            if (LandscapeLevelManagerWindow.TryGetEnvironmentTarget(out EnvironmentRoot managerTarget))
                return managerTarget;

            WorldRoot world = WorldRoot.FindInScene();
            if (world != null && world.TryGetEnvironment(out EnvironmentRoot environment))
                return environment;

            return null;
        }
    }
}
