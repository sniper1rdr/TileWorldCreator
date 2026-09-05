using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal sealed class GroundScenePaintTool : IWorldCoreSceneTool
    {
        private bool isPainting;
        private bool isErasing;
        private LandscapeCellKey lastGridPos;
        private bool cursorRepaintLoopActive;
        private DualGrid3D cachedPaintTarget;
        private int activeStrokeUndoGroup = -1;
        private string activeStrokeUndoName;

        internal bool IsCtrlEraseModifierActive { get; private set; }

        public int Priority => 0;

        public bool CanActivate()
        {
            DualGrid3D target = WorldCoreEditorSession.PaintTarget;
            return target != null &&
                   WorldCoreEditorSession.ActiveEditorMode == WorldCoreEditorMode.Landscape &&
                   target.IsLevelPaintingActive;
        }

        public void OnActivate()
        {
            cachedPaintTarget = WorldCoreEditorSession.PaintTarget;
            EnsureCursorRepaintLoop();
        }

        public void OnDeactivate()
        {
            CancelActiveOperation();
        }

        public void CancelActiveOperation()
        {
            DualGrid3D target = cachedPaintTarget;
            if (target != null)
            {
                if (isPainting)
                    target.CancelPaintStroke();
                if (isErasing)
                    target.CancelEraseStroke();
            }

            FinalizeInterruptedStrokeUndo();
            isPainting = false;
            isErasing = false;
            StopCursorRepaintLoop();
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            DualGrid3D dualGrid = WorldCoreEditorSession.PaintTarget;
            if (dualGrid == null)
            {
                if (isPainting || isErasing || activeStrokeUndoGroup >= 0)
                    CancelActiveOperation();
                return;
            }

            // Level/layer or target identity changed mid-stroke: abort safely.
            if ((isPainting || isErasing) &&
                cachedPaintTarget != null &&
                !ReferenceEquals(cachedPaintTarget, dualGrid))
            {
                CancelActiveOperation();
            }

            cachedPaintTarget = dualGrid;
            EnsureCursorRepaintLoop();

            Event e = Event.current;
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
                sceneView.Repaint();

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            float planeY = dualGrid.ActiveLevelWorldY;
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));

            if (!groundPlane.Raycast(ray, out float distance))
                return;

            Vector3 worldPos = ray.GetPoint(distance);
            LandscapeCellKey gridPos = dualGrid.WorldPointToActiveLevelCell(worldPos);

            DrawCursor(dualGrid, gridPos, planeY);
            HandleInput(dualGrid, e, gridPos);
            UpdateCtrlEraseModifierState(e);
        }

        private static void DrawCursor(DualGrid3D dualGrid, LandscapeCellKey gridPos, float planeY)
        {
            bool eraseActive = dualGrid.IsEraserModeActive ||
                               (Event.current != null && Event.current.control);

            float pickPlaneHeight = dualGrid.GetBrushMaskPickPlaneHeight(eraseActive);
            Vector3 center = dualGrid.GetBrushMaskWorldCenter(gridPos, pickPlaneHeight);
            Vector3 size = new Vector3(1f, 0f, 1f);

            Handles.color = eraseActive ? Color.red : Color.green;
            Handles.DrawWireCube(center, size);

            if (dualGrid.levels == null || dualGrid.levels.Count == 0)
                return;

            int levelIndex = Mathf.Clamp(dualGrid.ActiveLevelIndex, 0, dualGrid.levels.Count - 1);
            int subIndex = dualGrid.ActiveSubLevelIndex;
            string levelName = dualGrid.levels[levelIndex].name;
            string layerName = subIndex < dualGrid.levels[levelIndex].subLevels.Count
                ? dualGrid.levels[levelIndex].subLevels[subIndex].name
                : "Layer";
            Handles.Label(
                center + Vector3.up * 0.25f,
                $"{levelName} / {layerName} / {dualGrid.GetSubLevelLayerType(levelIndex, subIndex)} (Y={gridPos.y})");
        }

        private void HandleInput(DualGrid3D dualGrid, Event e, LandscapeCellKey gridPos)
        {
            bool eraseActive = dualGrid.IsEraserModeActive || e.control;

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && !eraseActive)
            {
                isErasing = false;
                if (!dualGrid.BeginPaint())
                    return;

                activeStrokeUndoName = "Paint Tiles";
                activeStrokeUndoGroup = DualGridLandscapeUndo.BeginPaintContentStroke(dualGrid, activeStrokeUndoName);
                LandscapeLevelManagerWindow.RequestRepaintIfOpen();

                isPainting = true;
                LandscapeCellKey paintCell = dualGrid.ToActiveLevelCell(gridPos.x, gridPos.z);
                dualGrid.AddPaintCell(paintCell);
                lastGridPos = paintCell;
                e.Use();
            }

            if (e.type == EventType.MouseDrag && e.button == 0 && isPainting && !e.alt)
            {
                if (gridPos != lastGridPos)
                {
                    dualGrid.AddPaintCell(gridPos);
                    lastGridPos = gridPos;
                }

                e.Use();
            }

            if (e.type == EventType.MouseUp && e.button == 0 && isPainting)
            {
                isPainting = false;
                dualGrid.EndPaint();
                DualGridLandscapeUndo.EndPaintContentStroke(dualGrid, activeStrokeUndoGroup, activeStrokeUndoName);
                activeStrokeUndoGroup = -1;
                activeStrokeUndoName = null;
                e.Use();
            }

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && eraseActive)
            {
                if (isPainting)
                {
                    dualGrid.CancelPaintStroke();
                    FinalizeInterruptedStrokeUndo();
                    isPainting = false;
                }

                activeStrokeUndoName = "Erase Tiles";
                activeStrokeUndoGroup = DualGridLandscapeUndo.BeginPaintContentStroke(dualGrid, activeStrokeUndoName);
                isErasing = true;
                isPainting = false;
                dualGrid.BeginErase();
                LandscapeCellKey eraseCell = dualGrid.ToActiveLevelCell(gridPos.x, gridPos.z);
                dualGrid.AddEraseCell(eraseCell);
                lastGridPos = eraseCell;
                e.Use();
            }

            if (e.type == EventType.MouseDrag && e.button == 0 && isErasing && !e.alt)
            {
                if (gridPos != lastGridPos)
                {
                    dualGrid.AddEraseCell(gridPos);
                    lastGridPos = gridPos;
                }

                e.Use();
            }

            if (e.type == EventType.MouseUp && e.button == 0 && isErasing)
            {
                isErasing = false;
                dualGrid.EndErase();
                DualGridLandscapeUndo.EndPaintContentStroke(dualGrid, activeStrokeUndoGroup, activeStrokeUndoName);
                activeStrokeUndoGroup = -1;
                activeStrokeUndoName = null;
                e.Use();
            }
        }

        private void FinalizeInterruptedStrokeUndo()
        {
            if (activeStrokeUndoGroup < 0)
                return;

            DualGridLandscapeUndo.AbortPaintContentStroke(activeStrokeUndoGroup);
            activeStrokeUndoGroup = -1;
            activeStrokeUndoName = null;
        }

        private void UpdateCtrlEraseModifierState(Event e)
        {
            bool ctrlEraseActive = isErasing || (e != null && e.control && e.button == 0);
            SetCtrlEraseModifierActive(ctrlEraseActive);
        }

        private void SetCtrlEraseModifierActive(bool active)
        {
            if (IsCtrlEraseModifierActive == active)
                return;

            IsCtrlEraseModifierActive = active;
            LandscapeLevelManagerWindow.RequestRepaintIfOpen();
        }

        private void EnsureCursorRepaintLoop()
        {
            if (cursorRepaintLoopActive)
                return;

            cursorRepaintLoopActive = true;
            EditorApplication.update += OnCursorRepaintLoop;
        }

        private void StopCursorRepaintLoop()
        {
            if (!cursorRepaintLoopActive)
                return;

            cursorRepaintLoopActive = false;
            cachedPaintTarget = null;
            SetCtrlEraseModifierActive(false);
            EditorApplication.update -= OnCursorRepaintLoop;
        }

        private void OnCursorRepaintLoop()
        {
            if (Application.isPlaying)
            {
                StopCursorRepaintLoop();
                return;
            }

            DualGrid3D dualGrid = cachedPaintTarget ?? WorldCoreEditorSession.PaintTarget;
            if (dualGrid == null || !dualGrid.IsLevelPaintingActive)
            {
                if (isPainting || isErasing || activeStrokeUndoGroup >= 0)
                    CancelActiveOperation();
                else
                    StopCursorRepaintLoop();
                return;
            }

            SceneView.RepaintAll();
        }
    }
}
