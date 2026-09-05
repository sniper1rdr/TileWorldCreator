using System.Collections.Generic;

namespace TileWorldCreator
{
    /// <summary>
    /// The 5 unique mesh roles needed by a dual-grid auto tile system. The
    /// VISUAL grid is offset by one cell from the LOGICAL (painted) grid, so
    /// every visual cell sits exactly on a corner shared by 4 logical cells
    /// instead of covering just 1. That means each visual tile only ever has
    /// to represent "which of my 4 corner cells are filled" (2^4 = 16
    /// combinations), which only takes 5 unique meshes (each used with up to
    /// 4 rotations, see DualGridAutoTile.TryGetShape) to cover completely -
    /// no separate base+overlay pieces, no positional offsets, no stacking,
    /// and never any open/unbordered side.
    /// </summary>
    public enum DualTileShape
    {
        Corner,     // exactly 1 of the 4 corner cells filled - a convex corner poking into empty space
        Edge,       // 2 ADJACENT corner cells filled - a straight border
        ThreeSided, // 3 of the 4 corner cells filled - a straight border with a concave notch cut into it
        Diagonal,   // 2 OPPOSITE corner cells filled - the ambiguous "saddle" case, resolved to one diagonal
        Flat        // all 4 corner cells filled - fully surrounded, no border at all
    }

    /// <summary>
    /// Looks up which of the 5 DualTileShape roles (and Y rotation) a visual
    /// cell needs, given which of its 4 sampled logical corner cells are
    /// filled with the same tile type currently being painted.
    /// </summary>
    public static class DualGridAutoTile
    {
        /// <summary>
        /// topLeft/topRight/botLeft/botRight name the 4 logical cells a
        /// visual cell straddles - "top" = +Z (North), "bot" = -Z (South),
        /// "left" = -X (West), "right" = +X (East), matching this project's
        /// existing North/East/South/West convention. Returns false (no
        /// shape) when all 4 are empty - nothing should be drawn there.
        /// </summary>
        public static bool TryGetShape(bool topLeft, bool topRight, bool botLeft, bool botRight, out DualTileShape shape, out int rotationSteps)
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

        // rotationSteps is in 90 degree clockwise steps (as seen from above).
        private static readonly Dictionary<(bool topLeft, bool topRight, bool botLeft, bool botRight), (DualTileShape shape, int rotationSteps)> Table = new()
        {
            // 1 corner filled - Corner piece, rotated so it points at that corner.
            { (false, false, false, true), (DualTileShape.Corner, 3) }, // botRight only
            { (false, false, true, false), (DualTileShape.Corner, 0) }, // botLeft only
            { (false, true, false, false), (DualTileShape.Corner, 2) }, // topRight only
            { (true, false, false, false), (DualTileShape.Corner, 1) }, // topLeft only

            // 2 adjacent corners filled - Edge piece (straight border).
            { (false, true, false, true), (DualTileShape.Edge, 0) }, // right side (topRight + botRight)
            { (true, false, true, false), (DualTileShape.Edge, 2) }, // left side (topLeft + botLeft)
            { (false, false, true, true), (DualTileShape.Edge, 1) }, // bottom side (botLeft + botRight)
            { (true, true, false, false), (DualTileShape.Edge, 3) }, // top side (topLeft + topRight)

            // 3 corners filled - Three-sided piece (concave notch at the 1 empty corner).
            { (false, true, true, true), (DualTileShape.ThreeSided, 3) }, // empty topLeft
            { (true, false, true, true), (DualTileShape.ThreeSided, 0) }, // empty topRight
            { (true, true, false, true), (DualTileShape.ThreeSided, 2) }, // empty botLeft
            { (true, true, true, false), (DualTileShape.ThreeSided, 1) }, // empty botRight

            // 2 opposite corners filled - Diagonal piece (ambiguous saddle case).
            { (false, true, true, false), (DualTileShape.Diagonal, 1) }, // topRight + botLeft
            { (true, false, false, true), (DualTileShape.Diagonal, 0) }, // topLeft + botRight

            // all 4 filled - Flat, fully surrounded.
            { (true, true, true, true), (DualTileShape.Flat, 0) },
        };
    }
}
