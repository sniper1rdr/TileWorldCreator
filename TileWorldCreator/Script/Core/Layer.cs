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
                    foreach (GameObject piece in tile.ExtraPieces)
                    {
                        if (piece == null) continue;
#if UNITY_EDITOR
                        if (!Application.isPlaying)
                            UnityEditor.Undo.DestroyObjectImmediate(piece);
                        else
#endif
                            Destroy(piece);
                    }

#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        UnityEditor.Undo.DestroyObjectImmediate(tile.gameObject);
                    else
#endif
                        Destroy(tile.gameObject);
                }
            }
            tiles.Clear();
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

        // ============ AUTO TILE ============

        private static readonly Vector3Int[] OrthogonalOffsets =
        {
            new Vector3Int(0, 0, 1),  // North (+Z)
            new Vector3Int(1, 0, 0),  // East  (+X)
            new Vector3Int(0, 0, -1), // South (-Z)
            new Vector3Int(-1, 0, 0), // West  (-X)
        };

        private static readonly TileSide[] OrthogonalSides =
        {
            TileSide.North, TileSide.East, TileSide.South, TileSide.West
        };

        private static readonly Vector3Int[] DiagonalOffsets =
        {
            new Vector3Int(1, 0, 1),   // NE
            new Vector3Int(1, 0, -1),  // SE
            new Vector3Int(-1, 0, -1), // SW
            new Vector3Int(-1, 0, 1),  // NW
        };

        private static readonly TileCorner[] DiagonalCorners =
        {
            TileCorner.NE, TileCorner.SE, TileCorner.SW, TileCorner.NW
        };

        /// <summary>Cells orthogonally adjacent to the given cell (N, E, S, W order).</summary>
        public Vector3Int[] GetOrthogonalNeighborCells(Vector3Int cellPosition)
        {
            Vector3Int[] result = new Vector3Int[OrthogonalOffsets.Length];
            for (int i = 0; i < OrthogonalOffsets.Length; i++)
                result[i] = cellPosition + OrthogonalOffsets[i];
            return result;
        }

        /// <summary>All 8 cells around the given cell (4 orthogonal + 4 diagonal).</summary>
        public Vector3Int[] GetAllNeighborCells(Vector3Int cellPosition)
        {
            Vector3Int[] result = new Vector3Int[OrthogonalOffsets.Length + DiagonalOffsets.Length];
            for (int i = 0; i < OrthogonalOffsets.Length; i++)
                result[i] = cellPosition + OrthogonalOffsets[i];
            for (int i = 0; i < DiagonalOffsets.Length; i++)
                result[OrthogonalOffsets.Length + i] = cellPosition + DiagonalOffsets[i];
            return result;
        }

        /// <summary>
        /// Computes which orthogonal sides of a cell have an existing tile of
        /// the same tileType in this layer, for use with auto tiling.
        /// </summary>
        public TileSide GetNeighborMask(Vector3Int cellPosition, string tileType)
        {
            TileSide mask = TileSide.None;

            for (int i = 0; i < OrthogonalOffsets.Length; i++)
            {
                Tile neighborTile = GetTileAt(cellPosition + OrthogonalOffsets[i]);
                if (neighborTile != null && neighborTile.TileType == tileType)
                {
                    mask |= OrthogonalSides[i];
                }
            }

            return mask;
        }

        /// <summary>
        /// Computes which diagonal neighbours of a cell have an existing tile
        /// of the same tileType in this layer, used to detect inner corners.
        /// </summary>
        public TileCorner GetCornerMask(Vector3Int cellPosition, string tileType)
        {
            TileCorner mask = TileCorner.None;

            for (int i = 0; i < DiagonalOffsets.Length; i++)
            {
                Tile neighborTile = GetTileAt(cellPosition + DiagonalOffsets[i]);
                if (neighborTile != null && neighborTile.TileType == tileType)
                {
                    mask |= DiagonalCorners[i];
                }
            }

            return mask;
        }

        /// <summary>
        /// Instantiates a prefab as an extra visual piece layered on top of the
        /// given cell (same position, its own absolute Y rotation). Used for
        /// composite auto tile pieces (see AutoTileMask.ClassifyComposite).
        /// </summary>
        public GameObject CreatePiecePrefabInstance(Vector3Int cellPosition, GameObject prefab, float rotationY, string name)
        {
            if (grid == null) EnsureGrid();
            if (grid == null || prefab == null) return null;

            Vector3 gridPosition = grid.GetCellCenterWorld(cellPosition);
            Vector3 localPos = new Vector3(gridPosition.x, 0f, gridPosition.z);
            localPos = transform.InverseTransformPoint(localPos);

            GameObject obj;
#if UNITY_EDITOR
            obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (obj == null)
                obj = Object.Instantiate(prefab);
            Undo.RegisterCreatedObjectUndo(obj, "Auto Tile Piece");
#else
            obj = Object.Instantiate(prefab);
#endif
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = new Vector3(localPos.x, 0f, localPos.z);
            obj.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            obj.name = name;
            return obj;
        }

        /// <summary>Destroys a tile's GameObject together with all of its extra composite pieces.</summary>
        public void DestroyTileVisual(Tile tile)
        {
            if (tile == null) return;

            foreach (GameObject piece in tile.ExtraPieces)
            {
                if (piece == null) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.DestroyObjectImmediate(piece);
                else
                    Destroy(piece);
#else
                Destroy(piece);
#endif
            }
            tile.ExtraPieces.Clear();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.DestroyObjectImmediate(tile.gameObject);
            else
                Destroy(tile.gameObject);
#else
            Destroy(tile.gameObject);
#endif
        }

        /// <summary>
        /// Replaces an existing tile's GameObject (and any extra composite
        /// pieces) with a new set of prefab instances at the given cell,
        /// keeping the same cell/tileType and its slot in the Tiles list. The
        /// first piece becomes the new primary tile object (holding the Tile
        /// component); any further pieces are layered on top as extra pieces.
        /// Used to re-evaluate neighbours after painting/erasing so their
        /// shape/rotation stays correct.
        /// </summary>
        public Tile ApplyAutoTileVisual(Tile tile, System.Collections.Generic.List<(GameObject prefab, float rotationY)> pieces)
        {
            if (tile == null || pieces == null || pieces.Count == 0 || grid == null) return tile;

            Vector3Int cellPosition = tile.CellPosition;
            string tileType = tile.TileType;
            GameObject oldObject = tile.gameObject;
            var oldExtraPieces = new System.Collections.Generic.List<GameObject>(tile.ExtraPieces);

            GameObject newObject = CreatePiecePrefabInstance(cellPosition, pieces[0].prefab, pieces[0].rotationY, $"Tile_{cellPosition.x}_{cellPosition.z}");

            Tile newTile = newObject.GetComponent<Tile>();
            if (newTile == null)
                newTile = newObject.AddComponent<Tile>();
            newTile.Initialize(cellPosition, tileType);

            for (int i = 1; i < pieces.Count; i++)
            {
                GameObject extra = CreatePiecePrefabInstance(cellPosition, pieces[i].prefab, pieces[i].rotationY, $"Tile_{cellPosition.x}_{cellPosition.z}_extra{i}");
                if (extra != null)
                    newTile.ExtraPieces.Add(extra);
            }

            int index = tiles.IndexOf(tile);
            if (index >= 0)
                tiles[index] = newTile;
            else
                tiles.Add(newTile);

            foreach (GameObject piece in oldExtraPieces)
            {
                if (piece == null) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.DestroyObjectImmediate(piece);
                else
                    Destroy(piece);
#else
                Destroy(piece);
#endif
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.DestroyObjectImmediate(oldObject);
            else
                Destroy(oldObject);
#else
            Destroy(oldObject);
#endif

            return newTile;
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
