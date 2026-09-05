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

        // CreateTile теперь создаёт контейнер (Tile metadata) и, при наличии префаба, инстанцирует префаб как дочерний объект
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

            // Вычисляем мировую позицию для уровня (y = height of layer)
            Vector3 worldPos = new Vector3(gridPosition.x, transform.position.y, gridPosition.z);
            Vector3 localPos = transform.InverseTransformPoint(worldPos);

            // Создаём контейнер для тайла
            GameObject container = new GameObject($"Tile_{cellPosition.x}_{cellPosition.z}");
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.RegisterCreatedObjectUndo(container, "Create Tile Container");
#endif
            container.transform.SetParent(transform, false);
            container.transform.localPosition = new Vector3(localPos.x, 0f, localPos.z);

            Tile tile = container.AddComponent<Tile>();
            tile.Initialize(cellPosition, tileType);

            // Если передан префаб — инстанцируем его как дочерний визуал
            if (prefab != null)
            {
                GameObject visual = null;
#if UNITY_EDITOR
                // В редакторе сохраняем связь с исходным префабом
                visual = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (visual == null)
                    visual = Object.Instantiate(prefab);
                Undo.RegisterCreatedObjectUndo(visual, "Instantiate Tile Visual");
#else
                visual = Object.Instantiate(prefab);
#endif
                visual.transform.SetParent(container.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.name = prefab.name;

                tile.SetPrefabReference(prefab);
                tile.SetVisualInstance(visual);
            }

            tiles.Add(tile);

            // Удаляем Tile компонент в игре (только хранить в редакторе для отладки)
#if !UNITY_EDITOR
            if (Application.isPlaying)
            {
                Object.DestroyImmediate(tile);
            }
#endif

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
                    Tile tile = collider.GetComponentInParent<Tile>();
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
