using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    [InitializeOnLoad]
    internal static class WorldCoreSceneToolController
    {
        private static IWorldCoreSceneTool activeTool;
        private static bool globalEscapeHandlerRegistered;

        internal static bool IsGroundCtrlEraseModifierActive =>
            WorldCoreToolRegistry.FindGroundTool()?.IsCtrlEraseModifierActive ?? false;

        static WorldCoreSceneToolController()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.playModeStateChanged += _ => CancelActiveOperations();
            RegisterGlobalPaintingEscapeHandler();
        }

        private static void RegisterGlobalPaintingEscapeHandler()
        {
            if (globalEscapeHandlerRegistered)
                return;

            FieldInfo field = typeof(EditorApplication).GetField(
                "globalEventHandler",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                return;

            var handler = (EditorApplication.CallbackFunction)HandleGlobalPaintingEscape;
            if (field.GetValue(null) is EditorApplication.CallbackFunction existing)
                field.SetValue(null, existing + handler);
            else
                field.SetValue(null, handler);

            globalEscapeHandlerRegistered = true;
        }

        private static void HandleGlobalPaintingEscape()
        {
            if (Application.isPlaying)
                return;

            if (!LandscapeLevelManagerWindow.TryHandleGlobalPaintingEscape())
                return;

            ApplyPaintingEscapeSideEffects();
        }

        internal static void ApplyPaintingEscapeSideEffects()
        {
            WorldCoreEditorSession.RefreshPaintTarget();
            DeactivateCurrentTool();
        }

        internal static void CancelActiveOperations()
        {
            DeactivateCurrentTool();
            IReadOnlyList<IWorldCoreSceneTool> tools = WorldCoreToolRegistry.All;
            for (int i = 0; i < tools.Count; i++)
                tools[i].CancelActiveOperation();
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (Application.isPlaying)
            {
                CancelActiveOperations();
                return;
            }

            if (TryHandleScenePaintingEscape())
            {
                ApplyPaintingEscapeSideEffects();
                return;
            }

            WorldCoreEditorSession.RefreshPaintTarget();
            IWorldCoreSceneTool nextTool = ResolveActiveTool();

            if (nextTool != activeTool)
            {
                DeactivateCurrentTool();
                activeTool = nextTool;
                activeTool?.OnActivate();
            }

            if (activeTool == null)
                return;

            activeTool.OnSceneGUI(sceneView);
        }

        private static bool TryHandleScenePaintingEscape()
        {
            if (LandscapeLevelManagerWindow.TryHandleGlobalPaintingEscape())
                return true;

            return false;
        }

        private static IWorldCoreSceneTool ResolveActiveTool()
        {
            IReadOnlyList<IWorldCoreSceneTool> tools = WorldCoreToolRegistry.All;
            for (int i = 0; i < tools.Count; i++)
            {
                IWorldCoreSceneTool tool = tools[i];
                if (tool.CanActivate())
                    return tool;
            }

            return null;
        }

        private static void DeactivateCurrentTool()
        {
            if (activeTool == null)
                return;

            activeTool.CancelActiveOperation();
            activeTool.OnDeactivate();
            activeTool = null;
        }
    }
}
