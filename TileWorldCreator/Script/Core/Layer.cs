using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TileWorldCreator
{
    [AddComponentMenu("TileWorld/Core/Layer")]
    [ExecuteAlways]
    public class Layer : MonoBehaviour
    {
        [SerializeField] private string layerName = "Ground_01";
        [SerializeField] private Grid grid;
        [SerializeField] private List<Tile> tiles = new List<Tile>();
        [SerializeField] private List<DualDisplayTile> displayTiles = new List<DualDisplayTile>();

        public string LayerName => layerName;
        public Grid Grid => grid;
        public List<Tile> Tiles => tiles;

        public void Initialize(string name)
        {
            layerName = name;
            EnsureGrid();
        }

        public Grid EnsureGrid()
        {
            if (grid == null)
            {
                Level level = GetComponentInParent<Level>();
                if (level != null)
                {
                    grid = level.GetGrid();
                }
                else
                {
                    WorldRoot worldRoot = GetComponentInParent<WorldRoot>();
                    if (worldRoot != null)
                    {
                        grid = worldRoot.EnsureGrid();
                    }
                }

                if (grid == null)
                {
                    grid = FindObjectOfType<Grid>();
                }
            }

            return grid;
        }

        public void SetGrid(Grid newGrid)
        {
            grid = newGrid;
        }

        // Теперь CreateTile поддерживает передачу префаба, чтобы избегать лишних создания/удаления объектов
        public Tile CreateTile(Vector3Int cellPosition, string tileType = "Default", GameObject prefab = null)
        {
            if (grid == null)
            {
                EnsureGrid();
                if (grid == null)
                {
                    Debug.LogError($"Cannot create tile: No Grid found for Layer '{layerName}'");
                    return null;
                }
            }

            // Проверяем занята ли ячейка ТОЛЬКО В ЭТОМ СЛОЕ
            if (IsCellOccupiedInThisLayer(cellPosition))
            {
                Debug.Log($"Cell {cellPosition.x}, {cellPosition.z} is already occupied in layer '{layerName}'!");
                return null;
            }

            // Получаем позицию от Grid
            Vector3 gridPosition = grid.GetCellCenterWorld(cellPosition);

            // Берем ТОЛЬКО X и Z, Y устанавливаем в позицию слоя
            Vector3 localPos = new Vector3(gridPosition.x, 0f, gridPosition.z);
            localPos = transform.InverseTransformPoint(localPos);

            GameObject tileObject = null;

            if (prefab != null)
            {
#if UNITY_EDITOR
                // Стараться использовать PrefabUtility в редакторе для сохранения связей
                tileObject = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (tileObject == null)
                    tileObject = Object.Instantiate(prefab);
                Undo.RegisterCreatedObjectUndo(tileObject, "Create Tile");
#else
                tileObject = Object.Instantiate(prefab);
#endif
                tileObject.transform.SetParent(transform, false);

                // Устанавливаем локальную позицию согласно ячейке
                Vector3 worldPos = new Vector3(gridPosition.x, transform.position.y, gridPosition.z);
                Vector3 local = transform.InverseTransformPoint(worldPos);
                tileObject.transform.localPosition = new Vector3(local.x, 0f, local.z);
                tileObject.name = $"Tile_{cellPosition.x}_{cellPosition.z}";
            }
            else
            {
                // Создаем GameObject для тайла-заглушки
                tileObject = new GameObject($"Tile_{cellPosition.x}_{cellPosition.z}");
                tileObject.transform.SetParent(transform, false);
                tileObject.transform.localPosition = new Vector3(localPos.x, 0f, localPos.z);

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.RegisterCreatedObjectUndo(tileObject, "Create Tile");
#endif
            }

            Tile tile = tileObject.GetComponent<Tile>();
            if (tile == null)
                tile = tileObject.AddComponent<Tile>();

            tile.Initialize(cellPosition, tileType);

            tiles.Add(tile);

            return tile;
        }

        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            if (grid == null) EnsureGrid();

            if (grid != null)
            {
                Vector3Int cellPos = grid.WorldToCell(worldPosition);
                cellPos.y = 0;
                return cellPos;
            }
            return Vector3Int.zero;
        }

        public Vector3 GetCellCenterWorld(Vector3Int cellPosition)
        {
            if (grid == null) EnsureGrid();

            if (grid != null)
            {
                Vector3 worldPos = grid.GetCellCenterWorld(cellPosition);
                return new Vector3(worldPos.x, 0f, worldPos.z);
            }
            return Vector3.zero;
        }

        public Vector3 GetTileWorldPosition(Vector3Int cellPosition)
        {
            Vector3 localPos = GetCellCenterWorld(cellPosition);
            Vector3 worldPos = transform.TransformPoint(localPos);
            worldPos.y = transform.position.y;
            return worldPos;
        }

        public void ClearAllTiles()
        {
            foreach (Tile tile in tiles)
            {
                if (tile != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        UnityEditor.Undo.DestroyObjectImmediate(tile.gameObject);
                    else
#endif
                        Destroy(tile.gameObject);
                }
            }
            tiles.Clear();

            foreach (DualDisplayTile displayTile in displayTiles)
            {
                if (displayTile != null)
                    DestroyDisplayObject(displayTile.gameObject);
            }
            displayTiles.Clear();
        }

        /// <summary>Destroys a logical tile's GameObject (its Undo-aware, matches CreateTile).</summary>
        public void DestroyTile(Tile tile)
        {
            if (tile == null) return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.DestroyObjectImmediate(tile.gameObject);
            else
                Destroy(tile.gameObject);
#else
            Destroy(tile.gameObject);
#endif
        }

        // НОВЫЙ МЕТОД - проверяет только в этом слое
        public bool IsCellOccupiedInThisLayer(Vector3Int cellPosition)
        {
            if (grid == null) EnsureGrid();
            if (grid == null) return false;

            // Проверяем по списку тайлов в этом слое
            foreach (Tile tile in tiles)
            {
                if (tile != null)
                {
                    if (tile.CellPosition.x == cellPosition.x && tile.CellPosition.z == cellPosition.z)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // СТАРЫЙ МЕТОД - проверяет все слои (для совместимости)
        public bool IsCellOccupied(Vector3Int cellPosition)
        {
            if (grid == null) EnsureGrid();
            if (grid == null) return false;

            // Проверяем по списку тайлов в этом слое
            foreach (Tile tile in tiles)
            {
                if (tile != null)
                {
                    if (tile.CellPosition.x == cellPosition.x && tile.CellPosition.z == cellPosition.z)
                    {
                        return true;
                    }
                }
            }

            // Дополнительная проверка через физику (только тайлы в этом слое)
            Vector3 worldPos = GetTileWorldPosition(cellPosition);
            Vector3 halfExt = grid.cellSize * 0.4f;
            Collider[] colliders = Physics.OverlapBox(worldPos, halfExt);

            foreach (Collider collider in colliders)
            {
                if (collider.gameObject != null && !collider.isTrigger)
                {
                    Tile tile = collider.GetComponent<Tile>();
                    if (tile != null && tile.CellPosition.x == cellPosition.x && tile.CellPosition.z == cellPosition.z)
                    {
                        // Проверяем что тайл принадлежит этому слою
                        if (tile.transform.parent == transform)
                        {
                            if (!tiles.Contains(tile))
                            {
                                tiles.Add(tile);
                            }
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public Tile GetTileAt(Vector3Int cellPosition)
        {
            return tiles.Find(t => t.CellPosition.x == cellPosition.x && t.CellPosition.z == cellPosition.z);
        }

        // ============ DUAL GRID AUTO TILE ============
        //
        // The VISUAL grid is offset by 1 cell from this LOGICAL (painted)
        // grid: a display tile at index D is centred exactly on the corner
        // shared by the 4 logical cells D, D-West, D-South, D-West-South (=
        // grid.CellToWorld(D), the cell's own min/SW corner). That means a
        // display tile never needs an offset or a stack of extra pieces to
        // close a border - the mesh IS the border, based on which of its 4
        // sampled logical cells are filled. See DualGridAutoTile.

        private static readonly Vector3Int[] DualDisplayOffsets =
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(1, 0, 1),
        };

        /// <summary>True if this layer has a painted logical tile of the given type at cellPosition.</summary>
        public bool HasTileOfType(Vector3Int cellPosition, string tileType)
        {
            return tiles.Exists(t => t != null && t.TileType == tileType && t.CellPosition.x == cellPosition.x && t.CellPosition.z == cellPosition.z);
        }

        private DualDisplayTile FindDisplayTile(Vector3Int displayCellPosition, string tileType)
        {
            return displayTiles.Find(d => d != null && d.TileType == tileType && d.CellPosition.x == displayCellPosition.x && d.CellPosition.z == displayCellPosition.z);
        }

        /// <summary>
        /// World-space (X/Z only) position of the corner shared by the 4
        /// logical cells a display tile at displayCellPosition straddles.
        /// </summary>
        public Vector3 GetDualDisplayLocalPosition(Vector3Int displayCellPosition)
        {
            if (grid == null) EnsureGrid();
            if (grid == null) return Vector3.zero;

            Vector3 corner = grid.CellToWorld(displayCellPosition);
            return new Vector3(corner.x, 0f, corner.z);
        }

        /// <summary>
        /// Recomputes and applies the correct dual-grid visual (shape +
        /// rotation, or none at all) for a single display cell of the given
        /// tile type, using biome to pick the prefab.
        /// </summary>
        public void RefreshDualDisplayCell(Vector3Int displayCellPosition, string tileType, TileBiomeData biome)
        {
            bool topLeft = HasTileOfType(displayCellPosition + new Vector3Int(-1, 0, 0), tileType);
            bool topRight = HasTileOfType(displayCellPosition, tileType);
            bool botLeft = HasTileOfType(displayCellPosition + new Vector3Int(-1, 0, -1), tileType);
            bool botRight = HasTileOfType(displayCellPosition + new Vector3Int(0, 0, -1), tileType);

            GameObject prefab = null;
            int rotationSteps = 0;

            if (biome != null && DualGridAutoTile.TryGetShape(topLeft, topRight, botLeft, botRight, out DualTileShape shape, out rotationSteps))
                biome.TryGetDualTilePrefab(tileType, shape, out prefab);

            DualDisplayTile existing = FindDisplayTile(displayCellPosition, tileType);
            if (existing != null)
            {
                displayTiles.Remove(existing);
                DestroyDisplayObject(existing.gameObject);
            }

            if (prefab == null)
                return;

            if (grid == null) EnsureGrid();
            if (grid == null) return;

            Vector3 localPos = transform.InverseTransformPoint(GetDualDisplayLocalPosition(displayCellPosition));

            GameObject obj;
#if UNITY_EDITOR
            obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (obj == null)
                obj = Object.Instantiate(prefab);
            Undo.RegisterCreatedObjectUndo(obj, "Auto Tile Display");
#else
            obj = Object.Instantiate(prefab);
#endif
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = new Vector3(localPos.x, 0f, localPos.z);
            obj.transform.localRotation = Quaternion.Euler(0f, rotationSteps * 90f, 0f);
            obj.name = $"Display_{tileType}_{displayCellPosition.x}_{displayCellPosition.z}";

            DualDisplayTile displayTile = obj.GetComponent<DualDisplayTile>();
            if (displayTile == null)
                displayTile = obj.AddComponent<DualDisplayTile>();
            displayTile.Initialize(displayCellPosition, tileType);
            displayTiles.Add(displayTile);
        }

        private static void DestroyDisplayObject(GameObject obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.DestroyObjectImmediate(obj);
            else
                Destroy(obj);
#else
            Destroy(obj);
#endif
        }

        /// <summary>
        /// Refreshes all 4 dual-grid display cells that sample the given
        /// logical cell - call this right after painting or erasing a
        /// logical tile so its visual border updates on both sides of the
        /// change.
        /// </summary>
        public void RefreshDualDisplayAround(Vector3Int logicalCellPosition, string tileType, TileBiomeData biome)
        {
            foreach (Vector3Int offset in DualDisplayOffsets)
                RefreshDualDisplayCell(logicalCellPosition + offset, tileType, biome);
        }

        public List<Tile> GetTilesByType(string tileType)
        {
            return tiles.FindAll(t => t.TileType == tileType);
        }

        public Vector3 GetLayerPosition()
        {
            return transform.position;
        }

        public void SetLayerPosition(Vector3 position)
        {
            transform.position = position;
        }
    }
}
