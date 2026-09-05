using UnityEngine;

namespace TileWorldCreator
{
    /// <summary>
    /// One prefab pool per auto tile shape for a single tile type (Ground /
    /// Liquid / Decorative). All prefabs in a pool must be authored at 0
    /// degrees Y rotation matching the shape's canonical mask (see
    /// AutoTileMask) - the brush only ever rotates them in 90 degree
    /// clockwise steps, it never mirrors/flips them.
    /// </summary>
    [System.Serializable]
    public class TerrainAutoTileSet
    {
        [Tooltip("Ровная земля: тайл окружён тем же типом со всех 4 сторон и по всем 4 диагоналям.")]
        public GameObject[] flat;

        [Tooltip("Прямой край: соединение с 3 сторон, граница с 4-й (по умолчанию граница на West, соединения North+East+South).")]
        public GameObject[] straightEdge;

        [Tooltip("Внешний угол: соединение с 2 соседних сторон (по умолчанию North+East), граница с двух других.")]
        public GameObject[] outerCorner;

        [Tooltip("Внутренний угол: все 4 стороны соединены, но одна диагональ \"вырезана\" (по умолчанию вырез на NE).")]
        public GameObject[] innerCorner;

        public bool IsValid =>
            (flat != null && flat.Length > 0) ||
            (straightEdge != null && straightEdge.Length > 0) ||
            (outerCorner != null && outerCorner.Length > 0) ||
            (innerCorner != null && innerCorner.Length > 0);
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
        /// Picks a tile prefab + Y rotation (in degrees) for the given
        /// neighbour masks, based on this biome's auto tile pools.
        /// orthoMask/cornerMask should come from Layer.GetNeighborMask /
        /// Layer.GetCornerMask for the cell being painted.
        /// </summary>
        public GameObject GetAutoTile(string tileType, TileSide orthoMask, TileCorner cornerMask, out float rotationY)
        {
            rotationY = 0f;

            TerrainAutoTileSet tileSet = GetTileSet(tileType);
            if (tileSet == null)
                return null;

            AutoTileShape shape = AutoTileMask.Classify(orthoMask, cornerMask, out int rotationSteps);
            GameObject[] pool = GetRolePool(tileSet, shape);

            // No dedicated prefab for the resolved shape - fall back to the Flat pool (no rotation).
            if ((pool == null || pool.Length == 0) && shape != AutoTileShape.Flat)
            {
                pool = tileSet.flat;
                rotationSteps = 0;
            }

            if (pool == null || pool.Length == 0)
                return null;

            rotationY = rotationSteps * 90f;
            return pool[Random.Range(0, pool.Length)];
        }

        private static GameObject[] GetRolePool(TerrainAutoTileSet tileSet, AutoTileShape shape)
        {
            switch (shape)
            {
                case AutoTileShape.Flat:
                    return tileSet.flat;
                case AutoTileShape.StraightEdge:
                    return tileSet.straightEdge;
                case AutoTileShape.OuterCorner:
                    return tileSet.outerCorner;
                case AutoTileShape.InnerCorner:
                    return tileSet.innerCorner;
                default:
                    return null;
            }
        }
    }
}
