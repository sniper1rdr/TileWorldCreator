using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TileWorldCreator
{
    public class TileBrush
    {
        public string paintMode = "Level";
        public TileBiomeData currentBiome;
        public string currentTileType = "Ground";
        public TileBiomeData environmentBiome;
        public string environmentCategory = "Rocks";

        public Color highlightColor = new Color(0.2f, 0.8f, 0.2f, 0.25f);
        public Color outlineColor = new Color(0f, 0f, 0f, 0.5f);
        public Color validColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
        public Color invalidColor = new Color(0.8f, 0.2f, 0.2f, 0.5f);

        public bool paintOnDrag = true;
        public float paintInterval = 0.05f;

        private bool isActive;
        public Layer targetLayer;

        public bool IsActive
        {
            get => isActive;
            set => isActive = value;
        }

        private Vector3Int lastHighlightedCell = new Vector3Int(-999, -999, -999);
        private Vector3Int lastPaintedCell = new Vector3Int(-999, -999, -999);
        private float lastPaintTime;
        private readonly HashSet<Vector3Int> paintedCells = new HashSet<Vector3Int>();

        private static readonly Vector3Int[] NeighborDirections =
        {
            new Vector3Int(-1, 0, 1),
            new Vector3Int(0, 0, 1),
            new Vector3Int(1, 0, 1),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, -1),
            new Vector3Int(0, 0, -1),
            new Vector3Int(1, 0, -1)
        };

#if UNITY_EDITOR
        public void OnSceneGUI(SceneView sceneView)
        {
            if (!isActive || targetLayer == null) return;

            Grid grid = targetLayer.EnsureGrid();
            if (grid == null) return;

            Event e = Event.current;

            if (e.alt || e.control || e.command)
                return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane plane = new Plane(Vector3.up, new Vector3(0f, targetLayer.transform.position.y, 0f));

            if (!plane.Raycast(ray, out float distance))
                return;

            Vector3 hitPoint = ray.GetPoint(distance);
            Vector3Int cell = grid.WorldToCell(hitPoint);
            cell.y = 0;

            bool valid = IsCellValid(cell);

            lastHighlightedCell = cell;
            DrawHighlight(cell, valid);

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                paintedCells.Clear();

                if (valid)
                {
                    PaintTile(cell);
                    lastPaintedCell = cell;
                    paintedCells.Add(cell);
                    lastPaintTime = Time.realtimeSinceStartup;
                }

                e.Use();
            }

            if (e.type == EventType.MouseDrag && e.button == 0 && paintOnDrag)
            {
                if (valid &&
                    cell != lastPaintedCell &&
                    !paintedCells.Contains(cell) &&
                    Time.realtimeSinceStartup - lastPaintTime >= paintInterval)
                {
                    PaintTile(cell);
                    lastPaintedCell = cell;
                    paintedCells.Add(cell);
                    lastPaintTime = Time.realtimeSinceStartup;
                }

                e.Use();
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                paintedCells.Clear();
                e.Use();
            }

            sceneView.Repaint();
        }

        private void DrawHighlight(Vector3Int cell, bool valid)
        {
            Grid grid = targetLayer.Grid;
            if (grid == null) return;

            Vector3 center = targetLayer.GetCellCenterWorld(cell);
            center.y += 0.01f;

            Vector3 size = grid.cellSize;
            float hx = size.x * 0.5f;
            float hz = size.z * 0.5f;

            Vector3[] points =
            {
                new Vector3(center.x - hx, center.y, center.z - hz),
                new Vector3(center.x - hx, center.y, center.z + hz),
                new Vector3(center.x + hx, center.y, center.z + hz),
                new Vector3(center.x + hx, center.y, center.z - hz)
            };

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.DrawSolidRectangleWithOutline(
                points,
                valid ? validColor : invalidColor,
                outlineColor
            );
        }
#endif

        public void ClearAll()
        {
            lastHighlightedCell = new Vector3Int(-999, -999, -999);
            lastPaintedCell = new Vector3Int(-999, -999, -999);
            paintedCells.Clear();
        }

        public void SetPaintOnDrag(bool value) => paintOnDrag = value;
        public void SetPaintInterval(float value) => paintInterval = Mathf.Max(0.001f, value);
        public void SetTargetLayer(Layer layer) => targetLayer = layer;
        public void SetBiome(TileBiomeData biome) => currentBiome = biome;
        public void SetTileType(string type) => currentTileType = type;
        public void SetEnvironmentBiome(TileBiomeData biome) => environmentBiome = biome;
        public void SetEnvironmentCategory(string category) => environmentCategory = category;
        public void SetPaintMode(string mode) => paintMode = mode;

        public void SetActive(bool active)
        {
            isActive = active;
            if (!active) ClearAll();
        }

        public void SetHighlightColor(Color color)
        {
            highlightColor = color;
            validColor = new Color(color.r, color.g, color.b, 0.5f);
        }

        public void LoadBiomes()
        {
        }

        private bool IsCellValid(Vector3Int cell)
        {
            if (targetLayer == null) return false;

            if (paintMode == "Level")
                return !targetLayer.IsCellOccupiedInThisLayer(cell);

            return !IsEnvironmentObjectAtCell(cell);
        }

        private void PaintTile(Vector3Int cell)
        {
            if (targetLayer == null) return;

            if (paintMode == "Level")
                PaintLevelTile(cell);
            else if (paintMode == "Environment")
                PaintEnvironment(cell);
        }

        private void PaintLevelTile(Vector3Int cell)
        {
            if (currentBiome == null || currentBiome.tileTop == null)
            {
                Debug.LogWarning("Tile Biome or Tile Top is missing!");
                return;
            }

            if (targetLayer.IsCellOccupiedInThisLayer(cell))
                return;

            GameObject tile = targetLayer.CreateTile(
                cell,
                currentTileType,
                currentBiome.tileTop
            );

            if (tile != null && currentBiome.useAutoTiling)
                RefreshAutoTilesAround(cell);
        }

        private void RefreshAutoTilesAround(Vector3Int center)
        {
            RefreshAutoTile(center);

            foreach (Vector3Int dir in NeighborDirections)
            {
                Vector3Int cell = center + dir;

                if (targetLayer.IsCellOccupiedInThisLayer(cell))
                    RefreshAutoTile(cell);
            }
        }

        private void RefreshAutoTile(Vector3Int cell)
        {
            if (targetLayer == null ||
                currentBiome == null ||
                !currentBiome.useAutoTiling)
                return;

            GameObject existing = targetLayer.GetTileAt(cell);
            if (existing == null) return;

            bool n = IsTileOccupied(cell + new Vector3Int(0, 0, 1));
            bool e = IsTileOccupied(cell + new Vector3Int(1, 0, 0));
            bool s = IsTileOccupied(cell + new Vector3Int(0, 0, -1));
            bool w = IsTileOccupied(cell + new Vector3Int(-1, 0, 0));

            bool ne = IsTileOccupied(cell + new Vector3Int(1, 0, 1));
            bool nw = IsTileOccupied(cell + new Vector3Int(-1, 0, 1));
            bool se = IsTileOccupied(cell + new Vector3Int(1, 0, -1));
            bool sw = IsTileOccupied(cell + new Vector3Int(-1, 0, -1));

            GetAutoTile(
                n, e, s, w,
                ne, nw, se, sw,
                out GameObject prefab,
                out float rotation
            );

            if (prefab == null) return;

            if (IsSamePrefab(existing, prefab))
            {
                existing.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
                return;
            }

            targetLayer.ReplaceTile(cell, prefab, rotation);
        }

        private bool IsTileOccupied(Vector3Int cell)
        {
            return targetLayer != null &&
                   targetLayer.IsCellOccupiedInThisLayer(cell);
        }

private void GetAutoTile(
    bool n, bool e, bool s, bool w,
    bool ne, bool nw, bool se, bool sw,
    out GameObject prefab,
    out float rotation)
{
    prefab = currentBiome.tileTop;
    rotation = 0f;

    int openSides = 0;

    if (!n) openSides++;
    if (!e) openSides++;
    if (!s) openSides++;
    if (!w) openSides++;

    // =====================================================
    // 0 ОТКРЫТЫХ СТОРОН
    // Полностью окружён
    // =====================================================

    if (openSides == 0)
    {
        // Внутренние углы
        if (!ne && currentBiome.tileInnerCorner != null)
        {
            prefab = currentBiome.tileInnerCorner;
            rotation = 180f; // NE открыт, смотрим на SW
            return;
        }

        if (!nw && currentBiome.tileInnerCorner != null)
        {
            prefab = currentBiome.tileInnerCorner;
            rotation = 270f; // NW открыт, смотрим на SE
            return;
        }

        if (!se && currentBiome.tileInnerCorner != null)
        {
            prefab = currentBiome.tileInnerCorner;
            rotation = 90f; // SE открыт, смотрим на NW
            return;
        }

        if (!sw && currentBiome.tileInnerCorner != null)
        {
            prefab = currentBiome.tileInnerCorner;
            rotation = 0f; // SW открыт, смотрим на NE
            return;
        }

        prefab = currentBiome.tileTop;
        return;
    }

    // =====================================================
    // 1 ОТКРЫТАЯ СТОРОНА
    // Прямая стена (базовое направление +Z = North)
    // =====================================================

    if (openSides == 1)
    {
        prefab = currentBiome.tileStraightWall;

        if (!n) rotation = 0f;      // Свободно сверху (+Z)
        else if (!e) rotation = 90f;  // Свободно справа (+X)
        else if (!s) rotation = 180f; // Свободно снизу (-Z)
        else if (!w) rotation = 270f; // Свободно слева (-X)

        return;
    }

    // =====================================================
    // 2 ОТКРЫТЫЕ СОСЕДНИЕ СТОРОНЫ
    // Внешний угол
    // =====================================================
// =====================================================
// 2 ОТКРЫТЫЕ СОСЕДНИЕ СТОРОНЫ
// Внешний угол
// =====================================================

if (openSides == 2)
{
    prefab = currentBiome.tileOuterCorner;
    
    // Северо-западный угол (открыты N и W)
    if (!n && !w)
    {
        rotation = 0f;    // ← СЮДА ВПИШИТЕ ПРАВИЛЬНЫЙ ПОВОРОТ
        return;
    }

    // Северо-восточный угол (открыты N и E)
    if (!n && !e)
    {
        rotation = 90f;   // ← СЮДА ВПИШИТЕ ПРАВИЛЬНЫЙ ПОВОРОТ
        return;
    }

    // Юго-восточный угол (открыты S и E)
    if (!s && !e)
    {
        rotation = 180f;  // ← СЮДА ВПИШИТЕ ПРАВИЛЬНЫЙ ПОВОРОТ
        return;
    }

    // Юго-западный угол (открыты S и W)
    if (!s && !w)
    {
        rotation = 270f;  // ← СЮДА ВПИШИТЕ ПРАВИЛЬНЫЙ ПОВОРОТ
        return;
    }
}
    // =====================================================
    // СЛОЖНЫЕ СЛУЧАИ
    // 2 противоположные стороны / 3 / 4 открытые
    // Пока используем Top
    // =====================================================

    prefab = currentBiome.tileTop;
    rotation = 0f;
}

        private bool IsSamePrefab(GameObject instance, GameObject prefab)
        {
            if (instance == null || prefab == null)
                return false;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                GameObject source =
                    PrefabUtility.GetCorrespondingObjectFromSource(instance);

                if (source == prefab)
                    return true;
            }
#endif

            return instance.name.StartsWith(prefab.name);
        }

        public bool RemoveTile(Vector3Int cell)
        {
            if (targetLayer == null) return false;

            bool removed = targetLayer.RemoveTile(cell);

            if (removed && currentBiome != null && currentBiome.useAutoTiling)
            {
                foreach (Vector3Int dir in NeighborDirections)
                {
                    Vector3Int neighbor = cell + dir;

                    if (targetLayer.IsCellOccupiedInThisLayer(neighbor))
                        RefreshAutoTile(neighbor);
                }
            }

            return removed;
        }

        private void PaintEnvironment(Vector3Int cell)
        {
            if (targetLayer == null ||
                environmentBiome == null ||
                IsEnvironmentObjectAtCell(cell))
                return;

            GameObject prefab =
                environmentBiome.GetRandomEnvironmentObject(environmentCategory);

            if (prefab == null)
            {
                Debug.LogWarning($"No objects in category '{environmentCategory}'!");
                return;
            }

            WorldRoot worldRoot = targetLayer.GetComponentInParent<WorldRoot>();

            if (worldRoot == null)
            {
                Debug.LogWarning("WorldRoot not found!");
                return;
            }

            EnvironmentRoot envRoot = worldRoot.Environment;

            if (envRoot == null)
            {
                GameObject root = new GameObject("Environment");
                root.transform.SetParent(worldRoot.transform);
                envRoot = root.AddComponent<EnvironmentRoot>();
            }

            Vector3 worldPos = targetLayer.GetTileWorldPosition(cell);
            GameObject obj;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            else
                obj = Object.Instantiate(prefab);

            if (obj == null)
                obj = Object.Instantiate(prefab);

            if (!Application.isPlaying)
                Undo.RegisterCreatedObjectUndo(obj, $"Place {environmentCategory}");
#else
            obj = Object.Instantiate(prefab);
#endif

            obj.transform.position = worldPos;
            obj.transform.SetParent(envRoot.transform, true);
            obj.name = $"{prefab.name}_{envRoot.EnvironmentObjects.Count}";

            if (environmentBiome.randomRotation)
                obj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            float scale = Random.Range(
                environmentBiome.randomScaleRange.x,
                environmentBiome.randomScaleRange.y
            );

            obj.transform.localScale = Vector3.one * scale;
            envRoot.EnvironmentObjects.Add(obj);
        }

        private bool IsEnvironmentObjectAtCell(Vector3Int cell)
        {
            if (targetLayer == null) return false;

            WorldRoot worldRoot = targetLayer.GetComponentInParent<WorldRoot>();
            if (worldRoot == null || worldRoot.Environment == null) return false;

            Vector3 worldPos = targetLayer.GetTileWorldPosition(cell);
            float radius = targetLayer.Grid.cellSize.x * 0.3f;

            foreach (GameObject obj in worldRoot.Environment.EnvironmentObjects)
            {
                if (obj == null) continue;

                Vector3 a = obj.transform.position;
                Vector3 b = worldPos;

                a.y = 0f;
                b.y = 0f;

                if (Vector3.Distance(a, b) < radius)
                    return true;
            }

            return false;
        }
    }
}