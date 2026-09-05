using UnityEngine;

namespace TileWorldCreator
{
    /// <summary>
    /// One prefab pool per dual-grid display shape for a single tile type.
    /// Supported tile types: Ground / Liquid.
    /// </summary>
    [System.Serializable]
    public class TerrainAutoTileSet
    {
        [Tooltip("Угол: ровно 1 из 4 соседних клеток того же типа заполнена.")]
        public GameObject[] corner;

        [Tooltip("Край: 2 СОСЕДНИЕ из 4 клеток заполнены.")]
        public GameObject[] edge;

        [Tooltip("Три стороны: 3 из 4 клеток заполнены.")]
        public GameObject[] threeSided;

        [Tooltip("Диагональ: 2 ПРОТИВОПОЛОЖНЫЕ из 4 клеток заполнены.")]
        public GameObject[] diagonal;

        [Tooltip("Ровная земля: все 4 клетки заполнены.")]
        public GameObject[] flat;

        public bool IsValid =>
            (corner != null && corner.Length > 0) ||
            (edge != null && edge.Length > 0) ||
            (threeSided != null && threeSided.Length > 0) ||
            (diagonal != null && diagonal.Length > 0) ||
            (flat != null && flat.Length > 0);
    }

    [CreateAssetMenu(
        menuName = "TileWorld/Biome Data",
        fileName = "TileBiomeData")]
    public class TileBiomeData : ScriptableObject
    {
        [Header("Identity")]
        public string biomeId;
        public string displayName;

        [Header("Ground Tiles (Auto Tile)")]
        public TerrainAutoTileSet groundTiles;

        [Header("Liquid Tiles (Auto Tile)")]
        public TerrainAutoTileSet liquidTiles;

        [Header("Environment - Rocks")]
        public GameObject[] rocks;

        [Header("Environment - Trees")]
        public GameObject[] trees;

        [Header("Environment - Vegetation")]
        public GameObject[] vegetation;

        [Header("Environment - Props")]
        public GameObject[] props;

[HideInInspector]
public float tileHeight = 1f;

[HideInInspector]
public bool randomRotation = true;

[HideInInspector]
public Vector2 randomScaleRange =
    new Vector2(0.8f, 1.2f);

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(biomeId) &&
            groundTiles != null &&
            groundTiles.IsValid;

        private TerrainAutoTileSet GetTileSet(
            string tileType)
        {
            switch (tileType)
            {
                case "Ground":
                    return groundTiles;

                case "Liquid":
                    return liquidTiles;

                default:
                    return null;
            }
        }

        public GameObject[] GetEnvironmentObjects(
            string category)
        {
            switch (category)
            {
                case "Rocks":
                    return rocks;

                case "Trees":
                    return trees;

                case "Vegetation":
                    return vegetation;

                case "Props":
                    return props;

                default:
                    return null;
            }
        }

        public GameObject GetRandomEnvironmentObject(
            string category)
        {
            GameObject[] objects =
                GetEnvironmentObjects(category);

            if (objects == null || objects.Length == 0)
                return null;

            return objects[
                Random.Range(0, objects.Length)
            ];
        }

        public bool HasEnvironmentCategory(
            string category)
        {
            GameObject[] objects =
                GetEnvironmentObjects(category);

            return objects != null &&
                   objects.Length > 0;
        }

        /// <summary>
        /// Picks a prefab for the given dual-grid display shape.
        /// The variantSeed deterministically selects an element
        /// from the corresponding prefab pool.
        /// </summary>
        public bool TryGetDualTilePrefab(
            string tileType,
            DualTileShape shape,
            int variantSeed,
            out GameObject prefab)
        {
            prefab = null;

            TerrainAutoTileSet tileSet =
                GetTileSet(tileType);

            if (tileSet == null)
                return false;

            GameObject[] pool =
                GetRolePool(
                    tileSet,
                    shape
                );

            if (pool == null || pool.Length == 0)
                return false;

            int index =
                ((variantSeed % pool.Length) +
                 pool.Length) %
                pool.Length;

            prefab = pool[index];

            return prefab != null;
        }

        private static GameObject[] GetRolePool(
            TerrainAutoTileSet tileSet,
            DualTileShape shape)
        {
            switch (shape)
            {
                case DualTileShape.Corner:
                    return tileSet.corner;

                case DualTileShape.Edge:
                    return tileSet.edge;

                case DualTileShape.ThreeSided:
                    return tileSet.threeSided;

                case DualTileShape.Diagonal:
                    return tileSet.diagonal;

                case DualTileShape.Flat:
                    return tileSet.flat;

                default:
                    return null;
            }
        }
    }
}