using System.Collections.Generic;

namespace TileWorldCreator
{
    /// <summary>
    /// The 5 unique mesh roles needed by a dual-grid auto tile system.
    /// Visual grid is offset by one cell from the logical (painted) grid,
    /// so every visual cell sits on a corner shared by 4 logical cells.
    /// 16 combinations are covered by only 5 meshes + rotations.
    /// </summary>
    public enum DualTileShape
    {
        Corner,     // exactly 1 of 4 corners filled
        Edge,       // 2 adjacent corners filled
        ThreeSided, // 3 corners filled
        Diagonal,   // 2 opposite corners filled
        Flat        // all 4 corners filled
    }

    /// <summary>
    /// Looks up which DualTileShape and Y-rotation a visual cell needs
    /// based on which of its 4 logical corner cells are filled.
    /// </summary>
    public static class DualGridAutoTile
    {
        /// <summary>
        /// topLeft/topRight/botLeft/botRight = the 4 logical cells a visual cell straddles.
        /// "top" = +Z, "bot" = -Z, "left" = -X, "right" = +X.
        /// Returns false when all 4 are empty.
        /// </summary>
        public static bool TryGetShape(
            bool topLeft, bool topRight, bool botLeft, bool botRight,
            out DualTileShape shape, out int rotationSteps)
        {
            if (Table.TryGetValue((topLeft, topRight, botLeft, botRight), out var entry))
            {
                shape = entry.shape;
                rotationSteps = entry.rotationSteps;
                return true;
            }

            shape = default;
            rotationSteps = 0;
            return false;
        }

        // rotationSteps = 90° clockwise steps (from above)
        private static readonly Dictionary<(bool, bool, bool, bool), (DualTileShape shape, int rotationSteps)> Table = new()
        {
            // 1 corner
            { (false, false, false, true),  (DualTileShape.Corner, 3) }, // botRight
            { (false, false, true,  false), (DualTileShape.Corner, 0) }, // botLeft
            { (false, true,  false, false), (DualTileShape.Corner, 2) }, // topRight
            { (true,  false, false, false), (DualTileShape.Corner, 1) }, // topLeft

            // 2 adjacent
            { (false, true,  false, true),  (DualTileShape.Edge, 0) }, // right
            { (true,  false, true,  false), (DualTileShape.Edge, 2) }, // left
            { (false, false, true,  true),  (DualTileShape.Edge, 1) }, // bottom
            { (true,  true,  false, false), (DualTileShape.Edge, 3) }, // top

            // 3 corners
            { (false, true,  true,  true),  (DualTileShape.ThreeSided, 3) }, // empty topLeft
            { (true,  false, true,  true),  (DualTileShape.ThreeSided, 0) }, // empty topRight
            { (true,  true,  false, true),  (DualTileShape.ThreeSided, 2) }, // empty botLeft
            { (true,  true,  true,  false), (DualTileShape.ThreeSided, 1) }, // empty botRight

            // 2 opposite
            { (false, true,  true,  false), (DualTileShape.Diagonal, 1) }, // topRight + botLeft
            { (true,  false, false, true),  (DualTileShape.Diagonal, 0) }, // topLeft + botRight

            // all 4
            { (true,  true,  true,  true),  (DualTileShape.Flat, 0) },
        };
    }
}