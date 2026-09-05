using UnityEngine;

namespace TileWorldCreator
{
    /// <summary>
    /// One prefab pool per dual-grid display shape for a single tile type
    /// (Ground / Liquid / Decorative). All prefabs in a pool must be authored
    /// at 0 degree Y rotation matching the shape's canonical mask (see
    /// DualGridAutoTile) - the brush only ever rotates them in 90 degree
    /// steps, it never mirrors/flips them.
    /// </summary>
    [System.Serializable]
    public class TerrainAutoTileSet
    {
        [Tooltip("Угол: ровно 1 из 4 соседних клеток того же типа заполнена (выпуклый угол, торчащий в пустоту).")]
        public GameObject[] corner;

        [Tooltip("Край: 2 СОСЕДНИЕ из 4 клеток заполнены - прямая граница.")]
        public GameObject[] edge;

        [Tooltip("Три стороны: 3 из 4 клеток заполнены - прямая граница с вогнутой выемкой.")]
        public GameObject[] threeSided;

        [Tooltip("Диагональ: 2 ПРОТИВОПОЛОЖНЫЕ из 4 клеток заполнены - неоднозначный \"седловой\" случай.")]
        public GameObject[] diagonal;

        [Tooltip("Ровная земля: все 4 клетки заполнены - тайл полностью окружён, границы нет.")]
        public GameObject[] flat;

        public bool IsValid =>
            (corner != null && corner.Length > 0) ||
            (edge != null && edge.Length > 0) ||
            (threeSided != null && threeSided.Length > 0) ||
            (diagonal != null && diagonal.Length > 0) ||
            (flat != null && flat.Length > 0);
    }

    [CreateAssetMenu(menuName = "TileWorld/Biome Data", fileName = "TileBiomeData")]
    public class TileBiomeData : ScriptableObject
    {
        [Header("Identity")]
        public string biomeId;
        public string displayName;

        [Header("Ground Tiles (Auto Tile)")]
        public TerrainAutoTileSet groundTiles;

        [Header("Liquid Tiles (Auto Tile)")]
        public TerrainAutoTileSet liquidTiles;

        [Header("Decorative Tiles (Auto Tile)")]
        public TerrainAutoTileSet decorativeTiles;

        [Header("Environment - Rocks")]
        public GameObject[] rocks;

        [Header("Environment - Trees")]
        public GameObject[] trees;

        [Header("Environment - Vegetation")]
        public GameObject[] vegetation;

        [Header("Environment - Props")]
        public GameObject[] props;

        [Header("Materials")]
        public Material groundMaterial;
        public Material liquidMaterial;
        public Material decorativeMaterial;

        [Header("Settings")]
        public float tileHeight = 1f;
        public bool randomRotation = true;
        public Vector2 randomScaleRange = new Vector2(0.8f, 1.2f);

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(biomeId) &&
            groundTiles != null &&
            groundTiles.IsValid;

        private TerrainAutoTileSet GetTileSet(string tileType)
        {
            switch (tileType)
            {
                case "Ground":
                    return groundTiles;
                case "Liquid":
                    return liquidTiles;
                case "Decorative":
                    return decorativeTiles;
                default:
                    return groundTiles;
            }
        }

        public GameObject[] GetEnvironmentObjects(string category)
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

        public GameObject GetRandomEnvironmentObject(string category)
        {
            GameObject[] objects = GetEnvironmentObjects(category);
            if (objects == null || objects.Length == 0)
                return null;

            return objects[Random.Range(0, objects.Length)];
        }

        public bool HasEnvironmentCategory(string category)
        {
            GameObject[] objects = GetEnvironmentObjects(category);
            return objects != null && objects.Length > 0;
        }

        /// <summary>
        /// Picks a prefab for the given dual-grid display shape, based on
        /// this biome's auto tile pools for tileType. variantSeed is a
        /// deterministic index (wrapped to the pool length) rather than a
        /// random pick, so the same combination of neighbouring tiles always
        /// resolves to the same prefab until a tile's variant is cycled (see
        /// Tile.CycleVariant) - that's what lets clicking an already placed
        /// tile switch its look instead of re-rolling every refresh. Returns
        /// false (and a null prefab) if no prefab is authored for that role,
        /// so the display cell should stay empty rather than fake it with a
        /// different role. shape comes from DualGridAutoTile.TryGetShape.
        /// </summary>
        public bool TryGetDualTilePrefab(string tileType, DualTileShape shape, int variantSeed, out GameObject prefab)
        {
            prefab = null;

            TerrainAutoTileSet tileSet = GetTileSet(tileType);
            if (tileSet == null)
                return false;

            GameObject[] pool = GetRolePool(tileSet, shape);
            if (pool == null || pool.Length == 0)
                return false;

            int index = ((variantSeed % pool.Length) + pool.Length) % pool.Length;
            prefab = pool[index];
            return prefab != null;
        }

        private static GameObject[] GetRolePool(TerrainAutoTileSet tileSet, DualTileShape shape)
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
