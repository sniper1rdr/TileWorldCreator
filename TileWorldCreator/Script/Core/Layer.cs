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
        [SerializeField]
        private string layerName = "Ground_01";

        [SerializeField]
        private Grid grid;

        [SerializeField]
        private List<GameObject> tiles =
            new List<GameObject>();


        public string LayerName => layerName;

        public Grid Grid => grid;

        public List<GameObject> Tiles => tiles;


        // =====================================================
        // INITIALIZATION
        // =====================================================

        public void Initialize(string name)
        {
            layerName = name;
            EnsureGrid();
        }


        public Grid EnsureGrid()
        {
            if (grid == null)
            {
                Level level =
                    GetComponentInParent<Level>();

                if (level != null)
                {
                    grid = level.GetGrid();
                }
                else
                {
                    WorldRoot worldRoot =
                        GetComponentInParent<WorldRoot>();

                    if (worldRoot != null)
                    {
                        grid =
                            worldRoot.EnsureGrid();
                    }
                }

                if (grid == null)
                {
                    grid =
                        FindObjectOfType<Grid>();
                }
            }

            return grid;
        }


        public void SetGrid(Grid newGrid)
        {
            grid = newGrid;
        }


        // =====================================================
        // CREATE TILE
        // =====================================================

        public GameObject CreateTile(
            Vector3Int cellPosition,
            string tileType = "Default",
            GameObject prefab = null)
        {
            if (grid == null)
            {
                EnsureGrid();

                if (grid == null)
                {
                    Debug.LogError(
                        $"Cannot create tile: No Grid found for Layer '{layerName}'"
                    );

                    return null;
                }
            }


            if (IsCellOccupiedInThisLayer(cellPosition))
            {
                return null;
            }


            Vector3 worldPos =
                GetTileWorldPosition(cellPosition);


            GameObject visual = null;


            // ==========================================
            // PREFAB TILE
            // ==========================================

            if (prefab != null)
            {
#if UNITY_EDITOR

                if (!Application.isPlaying)
                {
                    visual =
                        PrefabUtility.InstantiatePrefab(prefab)
                        as GameObject;
                }

                if (visual == null)
                {
                    visual =
                        Object.Instantiate(prefab);
                }

                if (!Application.isPlaying)
                {
                    Undo.RegisterCreatedObjectUndo(
                        visual,
                        "Instantiate Tile"
                    );
                }

#else

                visual =
                    Object.Instantiate(prefab);

#endif

                visual.transform.SetParent(
                    transform,
                    true
                );

                visual.transform.position =
                    worldPos;

                visual.name =
                    $"{prefab.name}_{cellPosition.x}_{cellPosition.z}";
            }


            // ==========================================
            // EMPTY TILE
            // ==========================================

            else
            {
                visual =
                    new GameObject(
                        $"Tile_{cellPosition.x}_{cellPosition.z}"
                    );

#if UNITY_EDITOR

                if (!Application.isPlaying)
                {
                    Undo.RegisterCreatedObjectUndo(
                        visual,
                        "Create Empty Tile"
                    );
                }

#endif

                visual.transform.SetParent(
                    transform,
                    true
                );

                visual.transform.position =
                    worldPos;
            }


            if (!tiles.Contains(visual))
            {
                tiles.Add(visual);
            }


            return visual;
        }


        // =====================================================
        // REPLACE TILE
        // =====================================================

        public GameObject ReplaceTile(
            Vector3Int cellPosition,
            GameObject newPrefab,
            float rotation = 0f)
        {
            if (newPrefab == null)
                return null;


            GameObject oldTile =
                GetTileAt(cellPosition);

            if (oldTile == null)
                return null;


            Vector3 worldPosition =
                GetTileWorldPosition(cellPosition);


            GameObject newTile = null;


#if UNITY_EDITOR

            if (!Application.isPlaying)
            {
                newTile =
                    PrefabUtility.InstantiatePrefab(newPrefab)
                    as GameObject;
            }

            if (newTile == null)
            {
                newTile =
                    Object.Instantiate(newPrefab);
            }

            if (!Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(
                    newTile,
                    "Replace Auto Tile"
                );
            }

#else

            newTile =
                Object.Instantiate(newPrefab);

#endif


            newTile.transform.SetParent(
                transform,
                true
            );

            newTile.transform.position =
                worldPosition;

            newTile.transform.rotation =
                Quaternion.Euler(
                    0f,
                    rotation,
                    0f
                );


            newTile.name =
                $"{newPrefab.name}_{cellPosition.x}_{cellPosition.z}";


            int index =
                tiles.IndexOf(oldTile);

            if (index >= 0)
            {
                tiles[index] = newTile;
            }
            else
            {
                tiles.Add(newTile);
            }


#if UNITY_EDITOR

            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(oldTile);
            }
            else
            {
                Destroy(oldTile);
            }

#else

            Destroy(oldTile);

#endif


            return newTile;
        }


        // =====================================================
        // REMOVE TILE
        // =====================================================

        public bool RemoveTile(
            Vector3Int cellPosition)
        {
            GameObject tile =
                GetTileAt(cellPosition);

            if (tile == null)
                return false;


            tiles.Remove(tile);


#if UNITY_EDITOR

            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(tile);
            }
            else
            {
                Destroy(tile);
            }

#else

            Destroy(tile);

#endif


            return true;
        }


        // =====================================================
        // GRID
        // =====================================================

        public Vector3Int WorldToCell(
            Vector3 worldPosition)
        {
            if (grid == null)
                EnsureGrid();


            if (grid != null)
            {
                Vector3Int cellPos =
                    grid.WorldToCell(worldPosition);

                cellPos.y = 0;

                return cellPos;
            }

            return Vector3Int.zero;
        }


        public Vector3 GetCellCenterWorld(
            Vector3Int cellPosition)
        {
            if (grid == null)
                EnsureGrid();


            if (grid != null)
            {
                Vector3 worldPos =
                    grid.GetCellCenterWorld(cellPosition);

                worldPos.y =
                    transform.position.y;

                return worldPos;
            }

            return Vector3.zero;
        }


        public Vector3 GetTileWorldPosition(
            Vector3Int cellPosition)
        {
            if (grid == null)
                EnsureGrid();


            if (grid == null)
                return Vector3.zero;


            Vector3 worldPos =
                grid.GetCellCenterWorld(cellPosition);

            worldPos.y =
                transform.position.y;

            return worldPos;
        }


        // =====================================================
        // CLEAR
        // =====================================================

        public void ClearAllTiles()
        {
            for (int i = tiles.Count - 1; i >= 0; i--)
            {
                GameObject tile = tiles[i];

                if (tile == null)
                    continue;


#if UNITY_EDITOR

                if (!Application.isPlaying)
                {
                    Undo.DestroyObjectImmediate(tile);
                }
                else
                {
                    Destroy(tile);
                }

#else

                Destroy(tile);

#endif
            }

            tiles.Clear();
        }


        // =====================================================
        // OCCUPIED CHECK
        // =====================================================

        public bool IsCellOccupiedInThisLayer(
            Vector3Int cellPosition)
        {
            return GetTileAt(cellPosition) != null;
        }


        public bool IsCellOccupied(
            Vector3Int cellPosition)
        {
            return GetTileAt(cellPosition) != null;
        }


        // =====================================================
        // GET TILE
        // =====================================================

        public GameObject GetTileAt(
            Vector3Int cellPosition)
        {
            if (grid == null)
                EnsureGrid();


            if (grid == null)
                return null;


            Vector3 worldPos =
                GetTileWorldPosition(cellPosition);


            float checkRadius =
                Mathf.Min(
                    grid.cellSize.x,
                    grid.cellSize.z
                ) * 0.35f;


            for (int i = tiles.Count - 1; i >= 0; i--)
            {
                GameObject tile = tiles[i];

                if (tile == null)
                {
                    tiles.RemoveAt(i);
                    continue;
                }


                Vector3 a = tile.transform.position;
                Vector3 b = worldPos;

                a.y = 0f;
                b.y = 0f;


                float distance =
                    Vector3.Distance(a, b);


                if (distance < checkRadius)
                {
                    return tile;
                }
            }


            return null;
        }


        // =====================================================
        // OTHER
        // =====================================================

        public List<GameObject> GetTilesByType(
            string tileType)
        {
            return new List<GameObject>(tiles);
        }


        public Vector3 GetLayerPosition()
        {
            return transform.position;
        }


        public void SetLayerPosition(
            Vector3 position)
        {
            transform.position = position;
        }
    }
}