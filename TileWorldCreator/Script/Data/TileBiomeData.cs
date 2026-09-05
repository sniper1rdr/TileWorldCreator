using UnityEngine;

namespace TileWorldCreator
{
    /// <summary>
    /// One prefab pool per auto tile shape. All prefabs in a pool must be
    /// authored at 0 degrees Y rotation matching the shape's canonical mask
    /// (see AutoTileMask) - the brush only ever rotates them in 90 degree
    /// clockwise steps, it never mirrors or reorders neighbours for you.
    /// Leave a pool empty to fall back to a plain random tile for that shape.
    /// </summary>
    [System.Serializable]
    public class AutoTileRoleSet
    {
        [Tooltip("No connected same-type neighbours.")]
        public GameObject[] isolated;
        [Tooltip("Exactly 1 connected neighbour (dead end). Authored connecting North.")]
        public GameObject[] endCap;
        [Tooltip("2 connected opposite neighbours (straight run). Authored connecting North+South.")]
        public GameObject[] straight;
        [Tooltip("2 connected adjacent neighbours (L bend). Authored connecting North+East.")]
        public GameObject[] corner;
        [Tooltip("3 connected neighbours (T junction). Authored connecting North+East+South.")]
        public GameObject[] tJunction;
        [Tooltip("All 4 neighbours connected.")]
        public GameObject[] cross;
    }

    [CreateAssetMenu(menuName = "TileWorld/Biome Data", fileName = "TileBiomeData")]
    public class TileBiomeData : ScriptableObject
    {
        [Header("Identity")]
        public string biomeId;
        public string displayName;

        [Header("Tile Prefabs")]
        public GameObject[] groundTiles;
        public GameObject[] liquidTiles;
        public GameObject[] decorativeTiles;

        [Header("Auto Tiling")]
        [Tooltip("When enabled, the Level brush picks tile shape + rotation from the pools below based on same-type neighbours instead of a plain random tile.")]
        public bool useAutoTiling = false;
        public AutoTileRoleSet groundAutoTiles;
        public AutoTileRoleSet liquidAutoTiles;
        public AutoTileRoleSet decorativeAutoTiles;

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
            groundTiles.Length > 0;

        public GameObject[] GetTiles(string tileType)
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

        public GameObject GetRandomTile(string tileType)
        {
            GameObject[] tiles = GetTiles(tileType);
            if (tiles == null || tiles.Length == 0)
                return null;
            
            return tiles[Random.Range(0, tiles.Length)];
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
        /// Picks a tile prefab + Y rotation (in degrees) for the given neighbour
        /// mask, based on this biome's auto tile role pools. Falls back to a
        /// plain random tile (0 degrees rotation) when auto tiling is disabled
        /// or no prefab is authored for the resolved shape.
        /// </summary>
        public GameObject GetAutoTile(string tileType, TileSide neighborMask, out float rotationY)
        {
            rotationY = 0f;

            AutoTileRoleSet roleSet = GetAutoTileRoleSet(tileType);
            if (roleSet == null)
                return GetRandomTile(tileType);

            AutoTileShape shape = AutoTileMask.Classify(neighborMask, out int rotationSteps);
            GameObject[] pool = GetRolePool(roleSet, shape);

            if (pool == null || pool.Length == 0)
                return GetRandomTile(tileType);

            rotationY = rotationSteps * 90f;
            return pool[Random.Range(0, pool.Length)];
        }

        private AutoTileRoleSet GetAutoTileRoleSet(string tileType)
        {
            switch (tileType)
            {
                case "Ground":
                    return groundAutoTiles;
                case "Liquid":
                    return liquidAutoTiles;
                case "Decorative":
                    return decorativeAutoTiles;
                default:
                    return groundAutoTiles;
            }
        }

        private static GameObject[] GetRolePool(AutoTileRoleSet roleSet, AutoTileShape shape)
        {
            switch (shape)
            {
                case AutoTileShape.Isolated:
                    return roleSet.isolated;
                case AutoTileShape.EndCap:
                    return roleSet.endCap;
                case AutoTileShape.Straight:
                    return roleSet.straight;
                case AutoTileShape.Corner:
                    return roleSet.corner;
                case AutoTileShape.TJunction:
                    return roleSet.tJunction;
                case AutoTileShape.Cross:
                    return roleSet.cross;
                default:
                    return null;
            }
        }
    }
}