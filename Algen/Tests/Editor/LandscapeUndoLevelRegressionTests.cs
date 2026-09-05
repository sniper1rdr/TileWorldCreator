using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor.Tests
{
    /// <summary>
    /// Edit Mode regression tests for Undo Add Level / Paint group boundaries.
    /// Uses the real Unity Undo API (PerformUndo / PerformRedo), not simulated state edits.
    /// </summary>
    public sealed class LandscapeUndoLevelRegressionTests
    {
        private readonly List<Object> _owned = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null)
                    Object.DestroyImmediate(_owned[i]);
            }

            _owned.Clear();
            Undo.ClearAll();
        }

        [Test]
        public void ColdLandscape_FirstPaint_FirstUndoRemovesPaintOnly()
        {
            LandscapeRoot landscape = CreateLandscape();
            LandscapePaintContent content = landscape.EditorGetPaintContent();
            Assert.AreEqual(0, content.CellCount);
            Assert.AreEqual(0, content.Variants.Count);

            DualGridLandscapeUndo.ExecuteAddLevel(landscape);
            Assert.AreEqual(2, landscape.levels.Count);
            int y2 = landscape.levels[1].heightUnits;
            Assert.IsNotNull(FindLevelRoot(landscape, y2));

            landscape.SetActiveLevel(1);
            landscape.SetActiveSubLevel(0);
            DualGridLandscapeUndo.ExecuteSceneLikePaintStroke(
                landscape,
                new LandscapeCellKey(5, y2, 5, 0));

            Assert.AreEqual(1, CountCellsAt(landscape, y2, layer: 0));
            Assert.Greater(CountMeshFiltersUnderLevel(landscape, y2), 0);

            Undo.PerformUndo();

            Assert.AreEqual(2, landscape.levels.Count, "First undo must keep Level 2");
            Assert.IsNotNull(FindLevelRoot(landscape, y2), "Level 2 root must remain");
            Assert.AreEqual(0, CountCellsAt(landscape, y2, layer: 0), "Paint stroke must be gone");
            Assert.AreEqual(0, CountMeshFiltersUnderLevel(landscape, y2), "No display tiles after undo paint");
        }

        [Test]
        public void ColdLandscape_FirstPaint_SecondUndoRemovesLevelOnly()
        {
            LandscapeRoot landscape = CreateLandscape();
            Assert.AreEqual(0, landscape.EditorGetPaintContent().CellCount);

            DualGridLandscapeUndo.ExecuteAddLevel(landscape);
            int y2 = landscape.levels[1].heightUnits;
            landscape.SetActiveLevel(1);
            landscape.SetActiveSubLevel(0);
            DualGridLandscapeUndo.ExecuteSceneLikePaintStroke(
                landscape,
                new LandscapeCellKey(5, y2, 5, 0));

            Undo.PerformUndo();
            Assert.AreEqual(2, landscape.levels.Count);
            Assert.AreEqual(0, CountCellsAt(landscape, y2, layer: 0));

            Undo.PerformUndo();

            Assert.AreEqual(1, landscape.levels.Count, "Second undo removes Add Level only");
            Assert.IsNull(FindLevelRoot(landscape, y2), "Level 2 root must be destroyed");
            Assert.AreEqual(1, landscape.levels.Count);
            Assert.IsNotNull(FindLevelRoot(landscape, 0), "Level 1 remains valid");
        }

        [Test]
        public void ColdLandscape_FirstPaint_RedoOrderIsStable()
        {
            LandscapeRoot landscape = CreateLandscape();
            Assert.AreEqual(0, landscape.EditorGetPaintContent().CellCount);

            DualGridLandscapeUndo.ExecuteAddLevel(landscape);
            int y2 = landscape.levels[1].heightUnits;
            landscape.SetActiveLevel(1);
            landscape.SetActiveSubLevel(0);
            DualGridLandscapeUndo.ExecuteSceneLikePaintStroke(
                landscape,
                new LandscapeCellKey(9, y2, 9, 0));

            Undo.PerformUndo();
            Undo.PerformUndo();
            Assert.AreEqual(1, landscape.levels.Count);

            Undo.PerformRedo();
            Assert.AreEqual(2, landscape.levels.Count);
            Assert.IsNotNull(FindLevelRoot(landscape, y2));
            Assert.AreEqual(0, CountCellsAt(landscape, y2, layer: 0), "Redo level restores hierarchy without paint");

            Undo.PerformRedo();
            Assert.AreEqual(2, landscape.levels.Count);
            Assert.IsNotNull(FindLevelRoot(landscape, y2));
            Assert.AreEqual(1, CountCellsAt(landscape, y2, layer: 0));
            Assert.IsTrue(HasCell(landscape, 9, y2, 9, 0));
            Assert.Greater(CountMeshFiltersUnderLevel(landscape, y2), 0);
        }

        [Test]
        public void ColdLandscape_FirstErase_DoesNotCreateNoOpUndo()
        {
            LandscapeRoot landscape = CreateLandscape();
            Assert.AreEqual(0, landscape.EditorGetPaintContent().CellCount);

            // Controlled setup: one cell via scene-like paint (not a "warm before Add Level" path).
            DualGridLandscapeUndo.ExecuteSceneLikePaintStroke(
                landscape,
                new LandscapeCellKey(3, 0, 3, 0));
            Assert.AreEqual(1, CountCellsAt(landscape, 0, layer: 0));

            DualGridLandscapeUndo.ExecuteSceneLikeEraseStroke(
                landscape,
                new LandscapeCellKey(3, 0, 3, 0));
            Assert.AreEqual(0, CountCellsAt(landscape, 0, layer: 0));

            Undo.PerformUndo();
            Assert.AreEqual(1, CountCellsAt(landscape, 0, layer: 0), "First erase undo restores the cell");
            Assert.IsTrue(HasCell(landscape, 3, 0, 3, 0));

            Undo.PerformUndo();
            Assert.AreEqual(0, CountCellsAt(landscape, 0, layer: 0), "Second undo removes the setup paint");
        }

        [Test]
        public void ContinuousStroke_IsOneUndoStep()
        {
            LandscapeRoot landscape = CreateLandscape();
            Assert.AreEqual(0, landscape.EditorGetPaintContent().CellCount);

            DualGridLandscapeUndo.ExecuteSceneLikePaintStroke(
                landscape,
                new LandscapeCellKey(1, 0, 1, 0),
                new LandscapeCellKey(2, 0, 1, 0),
                new LandscapeCellKey(3, 0, 1, 0));

            Assert.AreEqual(3, CountCellsAt(landscape, 0, layer: 0));

            Undo.PerformUndo();
            Assert.AreEqual(0, CountCellsAt(landscape, 0, layer: 0), "One undo removes the whole stroke");
        }

        [Test]
        public void WarmAndColdPaths_HaveIdenticalUndoOrder()
        {
            // Cold: never-painted → Add Level → Paint
            LandscapeRoot cold = CreateLandscape();
            Assert.AreEqual(0, cold.EditorGetPaintContent().CellCount);
            DualGridLandscapeUndo.ExecuteAddLevel(cold);
            int coldY2 = cold.levels[1].heightUnits;
            cold.SetActiveLevel(1);
            cold.SetActiveSubLevel(0);
            DualGridLandscapeUndo.ExecuteSceneLikePaintStroke(cold, new LandscapeCellKey(5, coldY2, 5, 0));

            Undo.PerformUndo();
            Assert.AreEqual(2, cold.levels.Count, "Cold: first undo keeps Level 2");
            Assert.AreEqual(0, CountCellsAt(cold, coldY2, layer: 0), "Cold: first undo removes paint");
            Undo.PerformUndo();
            Assert.AreEqual(1, cold.levels.Count, "Cold: second undo removes Add Level");
            Assert.IsNull(FindLevelRoot(cold, coldY2));

            // Warm: painted once → Add Level → Paint
            LandscapeRoot warm = CreateLandscape();
            DualGridLandscapeUndo.ExecuteSceneLikePaintStroke(warm, new LandscapeCellKey(0, 0, 0, 0));
            DualGridLandscapeUndo.ExecuteAddLevel(warm);
            int warmY2 = warm.levels[1].heightUnits;
            warm.SetActiveLevel(1);
            warm.SetActiveSubLevel(0);
            DualGridLandscapeUndo.ExecuteSceneLikePaintStroke(warm, new LandscapeCellKey(5, warmY2, 5, 0));

            Undo.PerformUndo();
            Assert.AreEqual(2, warm.levels.Count, "Warm: first undo keeps Level 2");
            Assert.AreEqual(0, CountCellsAt(warm, warmY2, layer: 0), "Warm: first undo removes paint");
            Assert.AreEqual(1, CountCellsAt(warm, 0, layer: 0), "Warm: Level 1 paint preserved");
            Undo.PerformUndo();
            Assert.AreEqual(1, warm.levels.Count, "Warm: second undo removes Add Level");
            Assert.IsNull(FindLevelRoot(warm, warmY2));
            Assert.AreEqual(1, CountCellsAt(warm, 0, layer: 0));
        }

        [Test]
        public void AddLevel_ThenPaint_FirstUndoRemovesPaintOnly()
        {
            LandscapeRoot landscape = CreateLandscape();
            DualGridLandscapeUndo.ExecuteSceneLikePaintStroke(landscape, new LandscapeCellKey(0, 0, 0, 0));
            int level1Cells = CountCellsAt(landscape, 0, layer: 0);

            DualGridLandscapeUndo.ExecuteAddLevel(landscape);
            int y2 = landscape.levels[1].heightUnits;
            landscape.SetActiveLevel(1);
            landscape.SetActiveSubLevel(0);
            DualGridLandscapeUndo.ExecuteSceneLikePaintStroke(landscape, new LandscapeCellKey(5, y2, 5, 0));

            Undo.PerformUndo();

            Assert.AreEqual(2, landscape.levels.Count);
            Assert.AreEqual(0, CountCellsAt(landscape, y2, layer: 0));
            Assert.IsNotNull(FindLevelRoot(landscape, y2));
            Assert.AreEqual(level1Cells, CountCellsAt(landscape, 0, layer: 0));
        }

        [Test]
        public void AddSubLevel_ThenPaint_HasTwoDistinctUndoSteps()
        {
            LandscapeRoot landscape = CreateLandscape();
            DualGridLandscapeUndo.ExecuteSceneLikePaintStroke(landscape, new LandscapeCellKey(0, 0, 0, 0));

            DualGridLandscapeUndo.ExecuteAddSubLevel(landscape, 0, LandscapeLayerType.Ground);
            Assert.AreEqual(2, landscape.levels[0].subLevels.Count);
            int layer1 = 1;
            Assert.AreEqual(1, CountLayerRootsWithIndex(FindLevelRoot(landscape, 0), layer1));

            landscape.SetActiveSubLevel(layer1);
            DualGridLandscapeUndo.ExecuteSceneLikePaintStroke(landscape, new LandscapeCellKey(4, 0, 4, layer1));

            Undo.PerformUndo();
            Assert.AreEqual(2, landscape.levels[0].subLevels.Count);
            Assert.AreEqual(0, CountCellsAt(landscape, 0, layer1));

            Undo.PerformUndo();
            Assert.AreEqual(1, landscape.levels[0].subLevels.Count);
            Assert.AreEqual(0, CountLayerRootsWithIndex(FindLevelRoot(landscape, 0), layer1));
            Assert.AreEqual(1, CountCellsAt(landscape, 0, layer: 0));
        }

        private LandscapeRoot CreateLandscape()
        {
            var go = new GameObject("TestLandscape");
            _owned.Add(go);
            var landscape = go.AddComponent<LandscapeRoot>();
            landscape.EnsurePaintContent();
            landscape.EnsureDefaultLevel();
            landscape.EnsureDefaultSubLevels();
            landscape.brushBiomeId = BiomeIds.Grasslands;
            landscape.brushBiome = BrushBiome.Grasslands;
            landscape.brushMode = LandscapeBrushMode.Ground;
            landscape.RebuildLevelRoots();
            return landscape;
        }

        private static int CountCellsAt(DualGrid3D landscape, int logicalY, int layer)
        {
            int count = 0;
            foreach (LogicalCellData cell in landscape.EditorGetPaintContent().Cells)
            {
                if (cell.y == logicalY && cell.layer == layer && cell.tileType != TileType.None)
                    count++;
            }

            return count;
        }

        private static bool HasCell(DualGrid3D landscape, int x, int y, int z, int layer)
        {
            foreach (LogicalCellData cell in landscape.EditorGetPaintContent().Cells)
            {
                if (cell.x == x && cell.y == y && cell.z == z && cell.layer == layer &&
                    cell.tileType != TileType.None)
                    return true;
            }

            return false;
        }

        private static Transform FindLevelRoot(DualGrid3D landscape, int logicalY)
        {
            string prefix = $"Level_{logicalY}_";
            foreach (Transform child in landscape.transform)
            {
                if (child.name.StartsWith(prefix) || child.name == $"Level_{logicalY}")
                    return child;
            }

            return null;
        }

        private static int CountMeshFiltersUnderLevel(DualGrid3D landscape, int logicalY)
        {
            Transform levelRoot = FindLevelRoot(landscape, logicalY);
            if (levelRoot == null)
                return 0;

            return levelRoot.GetComponentsInChildren<MeshFilter>(true).Length;
        }

        private static int CountLayerRootsWithIndex(Transform levelRoot, int layerIndex)
        {
            if (levelRoot == null)
                return 0;

            string prefix = $"Layer_{layerIndex:D2}_";
            int count = 0;
            foreach (Transform child in levelRoot)
            {
                if (child.name.StartsWith(prefix))
                    count++;
            }

            return count;
        }
    }
}
