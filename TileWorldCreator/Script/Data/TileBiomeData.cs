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
        [Tooltip("Ровная земля: базовый тайл на всю клетку, ставится всегда независимо от соседей.")]
        public GameObject[] flat;

        [Tooltip("Прямой край: маленькая накладка вдоль одной стороны клетки, ставится когда с этой стороны нет соседа того же типа (по умолчанию граница на West, соединения North+East+South).")]
        public GameObject[] straightEdge;

        [Tooltip("Внешний угол: маленькая накладка на четверть клетки в один угол, ставится когда обе смежные стороны этого угла открыты (по умолчанию North+East соединены, граница South+West).")]
        public GameObject[] outerCorner;

        [Tooltip("Внутренний угол: маленькая накладка на четверть клетки в один угол, ставится когда обе смежные стороны соединены, но диагональный сосед в этом углу отсутствует (по умолчанию вырез на NE).")]
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

        [Header("Auto Tile Overlay Placement")]
        [Tooltip("Насколько сильно (в долях размера клетки) сдвигать накладку Straight Edge к своей стороне клетки.")]
        [Range(0f, 1f)]
        public float edgeOverlayOffset = 0.5f;
        [Tooltip("Насколько сильно (в долях размера клетки, по обеим осям) сдвигать накладку Outer/Inner Corner к своему углу клетки.")]
        [Range(0f, 1f)]
        public float cornerOverlayOffset = 0.25f;

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
        /// Picks the tile prefab(s) for the given neighbour masks, based on
        /// this biome's auto tile pools. Always includes a Flat base piece
        /// (offset zero, covers the whole cell) plus a small Straight Edge /
        /// Outer Corner / Inner Corner overlay piece for every side/corner
        /// that needs one, each with its own Y rotation (degrees) and local
        /// XZ offset (in world units, already scaled by the cell size and the
        /// edgeOverlayOffset/cornerOverlayOffset settings above) so the
        /// overlay sits along the correct side or in the correct corner of
        /// the cell instead of the cell centre. orthoMask/cornerMask should
        /// come from Layer.GetNeighborMask / Layer.GetCornerMask for the cell
        /// being painted.
        /// </summary>
        public System.Collections.Generic.List<(GameObject prefab, float rotationY, Vector2 localOffset)> GetAutoTilePieces(string tileType, TileSide orthoMask, TileCorner cornerMask, Vector2 cellSize)
        {
            var result = new System.Collections.Generic.List<(GameObject prefab, float rotationY, Vector2 localOffset)>();

            TerrainAutoTileSet tileSet = GetTileSet(tileType);
            if (tileSet == null)
                return result;

            foreach (AutoTilePiece piece in AutoTileMask.BuildPieces(orthoMask, cornerMask))
            {
                GameObject[] pool = GetRolePool(tileSet, piece.shape);
                if (pool == null || pool.Length == 0)
                    continue; // no prefab authored for this role - skip this overlay, don't fake it with another role

                Vector2 localOffset;
                switch (piece.kind)
                {
                    case AutoTilePieceKind.Edge:
                        localOffset = Vector2.Scale(AutoTileMask.SideDirection(piece.edgeSide) * edgeOverlayOffset, cellSize);
                        break;
                    case AutoTilePieceKind.Corner:
                        localOffset = Vector2.Scale(AutoTileMask.CornerDirection(piece.cornerDir) * cornerOverlayOffset, cellSize);
                        break;
                    default:
                        localOffset = Vector2.zero;
                        break;
                }

                GameObject prefab = pool[Random.Range(0, pool.Length)];
                result.Add((prefab, piece.rotationSteps * 90f, localOffset));
            }

            return result;
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
