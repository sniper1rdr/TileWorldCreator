using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    [InitializeOnLoad]
    internal static class DualGridLandscapeUndo
    {
        internal const int MaxLandscapeUndoSteps = 30;

        private static readonly string[] PaintContentOperationNames =
        {
            "Paint Tiles",
            "Erase Tiles",
            "Delete Tile"
        };

        private static readonly string[] StructuralGridOperationNames =
        {
            "Clear Layer",
            "Delete Level",
            "Delete Sub Level",
            "Set Level Height",
            "Resync Layer Display",
            "Create World",
            "Apply Bake Static To Layer",
            "Clear Bake Static From Layer",
            "Add Level",
            "Add Sub Level"
        };

        /// <summary>Active SceneView paint/erase stroke group, or -1 when none.</summary>
        private static int activeStrokeGroup = -1;

        static DualGridLandscapeUndo()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        /// <summary>
        /// Sentinel coordinates embedded in an empty LandscapePaintContent snapshot so Unity 2022.3
        /// can reverse the first empty→non-empty List mutation (restore-to-empty List undo is broken).
        /// Stripped in DualGrid3D.EditorStripPaintUndoBaselineCells before logical grid load.
        /// </summary>
        internal static int PaintUndoBaselineCoord => DualGrid3D.EditorPaintUndoBaselineCoord;

        /// <summary>
        /// Paint/Erase/Delete Tile: register only LandscapePaintContent so session fields
        /// on DualGrid3D (active level, biome, brush) are not restored.
        /// </summary>
        internal static void RegisterPaintContentUndo(DualGrid3D grid, string operationName)
        {
            if (grid == null || Application.isPlaying)
                return;

            grid.EditorPersistGridStateForUndo();
            LandscapePaintContent content = grid.EditorGetPaintContent();
            if (content == null)
                return;

            // Unity cannot reliably undo List growth when the RegisterCompleteObjectUndo snapshot
            // had Count == 0. Seed a non-user sentinel so the snapshot is non-empty; clear it from
            // the live object so cancelled strokes stay empty; PersistLogicalGrid writes real cells;
            // Undo restores the sentinel; EditorStripPaintUndoBaselineCells removes it.
            bool seededBaseline = false;
            if (content.CellCount == 0)
            {
                content.Cells.Add(new LogicalCellData
                {
                    x = PaintUndoBaselineCoord,
                    y = PaintUndoBaselineCoord,
                    z = PaintUndoBaselineCoord,
                    layer = -1,
                    tileType = TileType.None
                });
                seededBaseline = true;
            }

            Undo.RegisterCompleteObjectUndo(content, operationName);

            if (seededBaseline)
                content.Cells.Clear();
        }

        /// <summary>
        /// Begins a caller-owned Paint/Erase undo group. Call before any content mutation.
        /// </summary>
        internal static int BeginPaintContentStroke(DualGrid3D grid, string operationName)
        {
            if (grid == null || Application.isPlaying)
                return -1;

            // Abandon a leaked stroke group so it cannot merge into this one.
            if (activeStrokeGroup >= 0)
                AbortPaintContentStroke(activeStrokeGroup);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(operationName);
            RegisterPaintContentUndo(grid, operationName);
            activeStrokeGroup = group;
            return group;
        }

        /// <summary>
        /// Completes a paint/erase stroke after EndPaint/EndErase/PersistLogicalGrid.
        /// </summary>
        internal static void EndPaintContentStroke(DualGrid3D grid, int group, string operationName)
        {
            if (group < 0)
                return;

            if (grid != null)
            {
                EditorUtility.SetDirty(grid);
                LandscapePaintContent content = grid.EditorGetPaintContent();
                if (content != null)
                    EditorUtility.SetDirty(content);
            }

            Undo.SetCurrentGroupName(operationName);
            Undo.CollapseUndoOperations(group);

            if (activeStrokeGroup == group)
                activeStrokeGroup = -1;
        }

        /// <summary>
        /// Cancels an in-progress stroke group without leaving a contaminating open group id.
        /// Content should already be reverted/cleared by CancelPaintStroke/CancelEraseStroke.
        /// </summary>
        internal static void AbortPaintContentStroke(int group)
        {
            if (group < 0)
            {
                activeStrokeGroup = -1;
                return;
            }

            Undo.SetCurrentGroupName(Undo.GetCurrentGroupName());
            Undo.CollapseUndoOperations(group);
            if (activeStrokeGroup == group)
                activeStrokeGroup = -1;
        }

        /// <summary>
        /// Structural ops that change levels and/or grid: snapshot DualGrid3D and paint content.
        /// </summary>
        internal static void RegisterGridStateUndo(DualGrid3D grid, string operationName)
        {
            if (grid == null || Application.isPlaying)
                return;

            grid.EditorPersistGridStateForUndo();

            Undo.SetCurrentGroupName(operationName);
            int group = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(grid, operationName);

            LandscapePaintContent content = grid.EditorGetPaintContent();
            if (content != null)
                Undo.RegisterCompleteObjectUndo(content, operationName);

            Undo.CollapseUndoOperations(group);
        }

        /// <summary>
        /// UI/command path for Add Level: one closed Undo group for DualGrid3D level data.
        /// Does not trailing-Increment: Paint/Erase always BeginPaintContentStroke with their own Increment.
        /// </summary>
        internal static void ExecuteAddLevel(DualGrid3D grid, string levelName = null)
        {
            if (grid == null || Application.isPlaying)
                return;

            // Caller-owned Undo group for DualGrid3D level data.
            // Do not call RegisterCreatedObjectUndo for the level root: on Unity 2022.3 that
            // nested registration splits the group into a top "Add Level" created-object step
            // which undoes as a visual no-op (destroy root → structural resync recreates it).
            // Roots are created/destroyed by EditorResyncDisplayAfterUndoRedo instead.
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Level");

            grid.EditorPersistGridStateForUndo();
            Undo.RegisterCompleteObjectUndo(grid, "Add Level");
            grid.AddLevel(levelName);

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Add Level");
            // No trailing IncrementCurrentGroup: the next Paint/Erase stroke begins with its own
            // Increment. A trailing Increment here left an empty group that became a no-op Undo
            // when Paint also Incremented at MouseDown.
        }

        /// <summary>
        /// UI/command path for Add Sub Level: one closed Undo group (DualGrid3D layer data).
        /// </summary>
        internal static void ExecuteAddSubLevel(DualGrid3D grid, int listIndex, LandscapeLayerType? layerTypeOverride = null)
        {
            if (grid == null || Application.isPlaying)
                return;

            if (grid.levels == null || listIndex < 0 || listIndex >= grid.levels.Count)
                return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Sub Level");

            grid.EditorPersistGridStateForUndo();
            Undo.RegisterCompleteObjectUndo(grid, "Add Sub Level");
            grid.AddSubLevel(listIndex, layerTypeOverride);

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Add Sub Level");
        }

        /// <summary>
        /// Test/helper: one owned paint-content stroke (Begin → mutate → End/Collapse).
        /// </summary>
        internal static void ExecutePaintContentStroke(DualGrid3D grid, string operationName, System.Action stroke)
        {
            if (grid == null || Application.isPlaying || stroke == null)
                return;

            int group = BeginPaintContentStroke(grid, operationName);
            try
            {
                stroke();
                EndPaintContentStroke(grid, group, operationName);
            }
            catch
            {
                AbortPaintContentStroke(group);
                throw;
            }
        }

        /// <summary>
        /// Scene-like stroke: BeginPaint → cells → EndPaint inside one owned undo group.
        /// </summary>
        internal static void ExecuteSceneLikePaintStroke(DualGrid3D grid, params LandscapeCellKey[] cells)
        {
            if (grid == null || Application.isPlaying || cells == null || cells.Length == 0)
                return;

            int group = BeginPaintContentStroke(grid, "Paint Tiles");
            try
            {
                if (!grid.BeginPaint())
                {
                    AbortPaintContentStroke(group);
                    return;
                }

                for (int i = 0; i < cells.Length; i++)
                    grid.AddPaintCell(cells[i]);

                grid.EndPaint();
                EndPaintContentStroke(grid, group, "Paint Tiles");
            }
            catch
            {
                grid.CancelPaintStroke();
                AbortPaintContentStroke(group);
                throw;
            }
        }

        /// <summary>
        /// Scene-like erase stroke inside one owned undo group.
        /// </summary>
        internal static void ExecuteSceneLikeEraseStroke(DualGrid3D grid, params LandscapeCellKey[] cells)
        {
            if (grid == null || Application.isPlaying || cells == null || cells.Length == 0)
                return;

            int group = BeginPaintContentStroke(grid, "Erase Tiles");
            try
            {
                grid.BeginErase();
                for (int i = 0; i < cells.Length; i++)
                    grid.AddEraseCell(cells[i]);
                grid.EndErase();
                EndPaintContentStroke(grid, group, "Erase Tiles");
            }
            catch
            {
                grid.CancelEraseStroke();
                AbortPaintContentStroke(group);
                throw;
            }
        }

        private static void OnUndoRedoPerformed()
        {
            if (Application.isPlaying)
                return;

            // Clear any in-flight stroke bookkeeping; undo/redo invalidates the active group.
            activeStrokeGroup = -1;

            // 1) Restore NonSerialized brush cache from current brushBiomeId (session — not from stroke).
            RefreshAllBrushTilesFromBrushId();

            bool paintContentUndo = IsNamedUndo(PaintContentOperationNames);
            bool structuralUndo = IsNamedUndo(StructuralGridOperationNames);

            if (!paintContentUndo && !structuralUndo)
            {
                LandscapeLevelManagerWindow.RequestRepaintIfOpen();
                SceneView.RepaintAll();
                return;
            }

            // 2) Rebuild display from restored painting-content data (per-cell biomeId).
            WorldCoreSceneToolController.CancelActiveOperations();
            ResyncAllLandscapeDisplays();
            LandscapeLevelManagerWindow.RequestRepaintIfOpen();
            SceneView.RepaintAll();
        }

        private static bool IsNamedUndo(string[] names)
        {
            string groupName = Undo.GetCurrentGroupName();
            if (string.IsNullOrEmpty(groupName))
                return false;

            for (int i = 0; i < names.Length; i++)
            {
                if (groupName == names[i])
                    return true;
            }

            return false;
        }

        private static void RefreshAllBrushTilesFromBrushId()
        {
            DualGrid3D[] grids = FindLandscapeGrids();
            for (int i = 0; i < grids.Length; i++)
            {
                if (grids[i] != null)
                    grids[i].ApplyActiveBrushTiles();
            }
        }

        private static void ResyncAllLandscapeDisplays()
        {
            DualGrid3D[] grids = FindLandscapeGrids();
            for (int i = 0; i < grids.Length; i++)
                grids[i].EditorResyncDisplayAfterUndoRedo();
        }

        private static DualGrid3D[] FindLandscapeGrids()
        {
    #if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<DualGrid3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    #else
            return Object.FindObjectsOfType<DualGrid3D>();
    #endif
        }
    }
}
