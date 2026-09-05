using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TileWorldCreator
{
    public enum LayerType
    {
        Ground,
        Liquid,
        Environment
    }

    public enum EnvironmentCategory
    {
        Rocks,
        Trees,
        Vegetation,
        Props
    }

    [AddComponentMenu("TileWorld/Core/Layer")]
    [ExecuteAlways]
    public class Layer : MonoBehaviour
    {
        [SerializeField] private string layerName = "Ground";
        [SerializeField] private LayerType layerType = LayerType.Ground;
        [SerializeField] private Grid grid;
        [SerializeField] private List<Tile> tiles = new List<Tile>();
        [SerializeField] private List<DualDisplayTile> displayTiles = new List<DualDisplayTile>();

        public string LayerName => layerName;
        public LayerType Type => layerType;
        public Grid Grid => grid;
        public List<Tile> Tiles => tiles;
        public List<DualDisplayTile> DisplayTiles => displayTiles;

        public bool IsGround => layerType == LayerType.Ground;
        public bool IsLiquid => layerType == LayerType.Liquid;
        public bool IsEnvironment => layerType == LayerType.Environment;

        public static readonly Vector3Int[] DualDisplayOffsets =
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(1, 0, 1)
        };

        // =========================================================
        // INITIALIZE
        // =========================================================
        public void Initialize(string name, LayerType type = LayerType.Ground)
        {
            layerName = name;
            layerType = type;
            gameObject.name = name;
            EnsureEnvironmentCategories();
        }

        public void SetGrid(Grid newGrid) => grid = newGrid;

        public void EnsureGrid()
        {
            if (grid != null) return;
            Level level = GetComponentInParent<Level>();
            if (level != null)
                grid = level.GetGrid();
        }

        // =========================================================
        // ENVIRONMENT
        // =========================================================
        public Transform GetEnvironmentCategory(EnvironmentCategory category)
        {
            if (!IsEnvironment) return null;
            EnsureEnvironmentCategories();
            return transform.Find(category.ToString());
        }

        public Transform GetEnvironmentCategory(string category)
        {
            if (!IsEnvironment || string.IsNullOrEmpty(category)) return null;
            EnsureEnvironmentCategories();
            return transform.Find(category);
        }

        private void EnsureEnvironmentCategories()
        {
            if (!IsEnvironment) return;
            CreateCategory("Rocks");
            CreateCategory("Trees");
            CreateCategory("Vegetation");
            CreateCategory("Props");
        }

        private Transform CreateCategory(string categoryName)
        {
            Transform existing = transform.Find(categoryName);
            if (existing != null) return existing;

            GameObject obj = new GameObject(categoryName);
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.RegisterCreatedObjectUndo(obj, "Create Environment Category");
#endif
            return obj.transform;
        }

      public Tile CreateTile(Vector3Int cellPosition, string tileType = "Default", GameObject prefab = null)
{
    EnsureGrid();
    if (grid == null)
    {
        Debug.LogError($"Layer '{layerName}' has no Grid.");
        return null;
    }

    if (IsCellOccupiedInThisLayer(cellPosition))
        return GetTileAt(cellPosition);

    Vector3 localPosition = GetCellCenterLocal(cellPosition);

    // 1. Всегда создаём корневой объект с компонентом Tile
    GameObject tileObject = new GameObject($"Tile_{cellPosition.x}_{cellPosition.y}_{cellPosition.z}");
    tileObject.transform.SetParent(transform, false);
    tileObject.transform.localPosition = localPosition;
    tileObject.transform.localRotation = Quaternion.identity;
    tileObject.transform.localScale = Vector3.one;

    Tile tile = tileObject.AddComponent<Tile>();
    tile.Initialize(cellPosition, tileType);

    // 2. Если есть префаб — ставим его ВНУТРЬ как ребёнка
    if (prefab != null)
    {
        GameObject visual;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, tileObject.transform);
        else
#endif
            visual = Object.Instantiate(prefab, tileObject.transform);

        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        visual.name = prefab.name;
    }

    if (!tiles.Contains(tile))
        tiles.Add(tile);

#if UNITY_EDITOR
    if (!Application.isPlaying)
        EditorUtility.SetDirty(this);
#endif

    return tile;
}

        // =========================================================
        // ENVIRONMENT OBJECT
        // =========================================================
        public GameObject CreateEnvironmentObject(Vector3 worldPosition, GameObject prefab, EnvironmentCategory category)
        {
            if (!IsEnvironment || prefab == null) return null;

            Transform parent = GetEnvironmentCategory(category) ?? transform;
            GameObject obj;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            else
#endif
                obj = Object.Instantiate(prefab, parent);

            obj.transform.position = worldPosition;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(this);
#endif
            return obj;
        }

        public GameObject CreateEnvironmentObject(Vector3 worldPosition, GameObject prefab, string category)
        {
            if (!System.Enum.TryParse(category, true, out EnvironmentCategory parsed))
                parsed = EnvironmentCategory.Props;

            return CreateEnvironmentObject(worldPosition, prefab, parsed);
        }

private Vector3 GetCellCenterLocal(Vector3Int cellPosition)
{
    EnsureGrid();
    if (grid == null) return Vector3.zero;

    Vector3 world = grid.GetCellCenterWorld(cellPosition);
    world.y = transform.position.y;          // высота от Layer
    return transform.InverseTransformPoint(world);
}

public Vector3 GetTileWorldPosition(Vector3Int cellPosition)
{
    EnsureGrid();
    if (grid == null) return transform.position;

    Vector3 world = grid.GetCellCenterWorld(cellPosition);
    world.y = transform.position.y;
    return world;
}

public Vector3 GetCellCenterWorld(Vector3Int cellPosition)
{
    EnsureGrid();
    if (grid == null) return transform.position;

    Vector3 world = grid.GetCellCenterWorld(cellPosition);
    world.y = transform.position.y;
    return world;
}
        // =========================================================
        // OCCUPANCY
        // =========================================================
        public bool IsCellOccupiedInThisLayer(Vector3Int cellPosition)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                Tile tile = tiles[i];
                if (tile != null && tile.CellPosition == cellPosition)
                    return true;
            }
            return false;
        }

        public bool IsCellOccupied(Vector3Int cellPosition)
        {
            if (IsCellOccupiedInThisLayer(cellPosition))
                return true;

            EnsureGrid();
            if (grid == null) return false;

            Vector3 center = grid.GetCellCenterWorld(cellPosition);
            Vector3 halfExtents = grid.cellSize * 0.45f;
            Collider[] colliders = Physics.OverlapBox(center, halfExtents);

            foreach (var collider in colliders)
            {
                if (collider != null && collider.GetComponentInParent<Tile>() != null)
                    return true;
            }
            return false;
        }

        // =========================================================
        // TILE ACCESS
        // =========================================================
        public Tile GetTileAt(Vector3Int cellPosition)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                Tile tile = tiles[i];
                if (tile != null && tile.CellPosition == cellPosition)
                    return tile;
            }
            return null;
        }

        public List<Tile> GetTilesByType(string tileType)
        {
            var result = new List<Tile>();
            foreach (var tile in tiles)
            {
                if (tile != null && tile.TileType == tileType)
                    result.Add(tile);
            }
            return result;
        }

        public bool HasTileOfType(Vector3Int cellPosition, string tileType)
        {
            Tile tile = GetTileAt(cellPosition);
            return tile != null && tile.TileType == tileType;
        }

        public Tile GetTileOfType(Vector3Int cellPosition, string tileType)
        {
            Tile tile = GetTileAt(cellPosition);
            return tile != null && tile.TileType == tileType ? tile : null;
        }

        // =========================================================
        // DESTROY / CLEAR
        // =========================================================
        public void DestroyTile(Tile tile)
        {
            if (tile == null) return;
            tiles.Remove(tile);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.DestroyObjectImmediate(tile.gameObject);
            else
#endif
                Destroy(tile.gameObject);
        }

        public void ClearAllTiles()
        {
            CleanupLists();

            for (int i = tiles.Count - 1; i >= 0; i--)
            {
                Tile tile = tiles[i];
                if (tile == null) continue;

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.DestroyObjectImmediate(tile.gameObject);
                else
#endif
                    Destroy(tile.gameObject);
            }
            tiles.Clear();

            for (int i = displayTiles.Count - 1; i >= 0; i--)
            {
                DualDisplayTile display = displayTiles[i];
                if (display == null) continue;

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.DestroyObjectImmediate(display.gameObject);
                else
#endif
                    Destroy(display.gameObject);
            }
            displayTiles.Clear();
        }

        private void CleanupLists()
        {
            tiles.RemoveAll(x => x == null);
            displayTiles.RemoveAll(x => x == null);
        }

private Vector3 GetDualDisplayLocalPosition(Vector3Int displayCellPosition)
{
    EnsureGrid();
    if (grid == null) return Vector3.zero;

    // Центр dual-клетки
    Vector3 world = grid.CellToWorld(displayCellPosition);
    
    // Смещение на половину клетки (dual-grid)
    Vector3 offset = new Vector3(
        grid.cellSize.x * 1f,
        0f,
        grid.cellSize.z * 1f
    );
    
    world += offset;
    
    // ===== ГЛАВНОЕ: высота от Layer, а не от Grid =====
    world.y = transform.position.y;
    // ==================================================
    
    return transform.InverseTransformPoint(world);
}

        public void RefreshDualDisplayCell(Vector3Int displayCellPosition, TileBiomeData biome, string tileType)
        {
            if (biome == null) return;

            Tile topLeft = GetTileOfType(displayCellPosition + new Vector3Int(0, 0, 1), tileType);
            Tile topRight = GetTileOfType(displayCellPosition + new Vector3Int(1, 0, 1), tileType);
            Tile bottomLeft = GetTileOfType(displayCellPosition, tileType);
            Tile bottomRight = GetTileOfType(displayCellPosition + new Vector3Int(1, 0, 0), tileType);

            RemoveDisplayTilesAt(displayCellPosition);

            // Liquid diagonals
            if (tileType == "Liquid")
            {
                bool diagonalA = topLeft != null && bottomRight != null && topRight == null && bottomLeft == null;
                bool diagonalB = topRight != null && bottomLeft != null && topLeft == null && bottomRight == null;

                if (diagonalA)
                {
                    CreateLiquidCorner(displayCellPosition, biome, 1);
                    CreateLiquidCorner(displayCellPosition, biome, 3);
                    return;
                }
                if (diagonalB)
                {
                    CreateLiquidCorner(displayCellPosition, biome, 2);
                    CreateLiquidCorner(displayCellPosition, biome, 0);
                    return;
                }
            }

            // Autotile
            if (!DualGridAutoTile.TryGetShape(
                    topLeft != null, topRight != null,
                    bottomLeft != null, bottomRight != null,
                    out DualTileShape shape, out int rotationSteps))
                return;

            int variantSeed = 0;
            if (topLeft != null) variantSeed += topLeft.VariantSeed;
            if (topRight != null) variantSeed += topRight.VariantSeed;
            if (bottomLeft != null) variantSeed += bottomLeft.VariantSeed;
            if (bottomRight != null) variantSeed += bottomRight.VariantSeed;

            if (!biome.TryGetDualTilePrefab(tileType, shape, variantSeed, out GameObject prefab))
                return;

            CreateDisplayObject(displayCellPosition, prefab, rotationSteps, shape, tileType);
        }

        private void CreateLiquidCorner(Vector3Int displayCellPosition, TileBiomeData biome, int rotationSteps)
        {
            if (!biome.TryGetDualTilePrefab("Liquid", DualTileShape.Corner, rotationSteps, out GameObject prefab))
                return;

            CreateDisplayObject(displayCellPosition, prefab, rotationSteps, DualTileShape.Corner, "Liquid");
        }

        private DualDisplayTile CreateDisplayObject(
            Vector3Int displayCellPosition,
            GameObject prefab,
            int rotationSteps,
            DualTileShape shape,
            string tileType)
        {
            if (prefab == null) return null;

            GameObject obj;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
            else
#endif
                obj = Object.Instantiate(prefab, transform);

            obj.transform.localPosition = GetDualDisplayLocalPosition(displayCellPosition);
            obj.transform.localRotation = Quaternion.Euler(0f, rotationSteps * 90f, 0f);

            DualDisplayTile display = obj.GetComponent<DualDisplayTile>() ?? obj.AddComponent<DualDisplayTile>();
            display.Initialize(displayCellPosition, tileType);
            displayTiles.Add(display);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(this);
#endif
            return display;
        }

        private void RemoveDisplayTilesAt(Vector3Int displayCellPosition)
        {
            for (int i = displayTiles.Count - 1; i >= 0; i--)
            {
                DualDisplayTile display = displayTiles[i];
                if (display == null)
                {
                    displayTiles.RemoveAt(i);
                    continue;
                }

                if (display.CellPosition != displayCellPosition) continue;

                displayTiles.RemoveAt(i);

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.DestroyObjectImmediate(display.gameObject);
                else
#endif
                    Destroy(display.gameObject);
            }
        }

        public void RefreshDualDisplayAround(Vector3Int cellPosition, TileBiomeData biome, string tileType)
        {
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                    RefreshDualDisplayCell(cellPosition + new Vector3Int(x, 0, z), biome, tileType);
        }

        // =========================================================
        // TRANSFORM
        // =========================================================
        public Vector3 GetLayerPosition() => transform.position;
        public void SetLayerPosition(Vector3 position) => transform.position = position;
    }
}