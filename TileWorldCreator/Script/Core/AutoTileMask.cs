using UnityEngine;

namespace TileWorldCreator
{
    /// <summary>
    /// Bit flags describing which orthogonal sides of a tile connect to a
    /// same-type neighbour tile. North/East/South/West map to the local
    /// +Z/+X/-Z/-X grid directions.
    ///
    /// IMPORTANT: this order (N, E, S, W) is chosen to match Unity's rotation
    /// convention: a positive rotation around Y is clockwise when viewed from
    /// above (Quaternion.Euler(0, 90, 0) turns +Z (North) into +X (East)).
    /// Role prefabs must be authored at 0 degrees rotation matching the
    /// "*Base" masks below - the auto tile system only ever rotates them in
    /// 90 degree clockwise steps, it never mirrors/flips them.
    /// </summary>
    [System.Flags]
    public enum TileSide
    {
        None = 0,
        North = 1,
        East = 2,
        South = 4,
        West = 8,
        All = North | East | South | West
    }

    /// <summary>
    /// Bit flags describing which DIAGONAL neighbours connect to a same-type
    /// tile. Only used to detect an inner corner (a notch cut into an
    /// otherwise fully-surrounded tile). Cyclic clockwise order NE-SE-SW-NW
    /// matches the TileSide clockwise order (NE sits between North and East).
    /// </summary>
    [System.Flags]
    public enum TileCorner
    {
        None = 0,
        NE = 1,
        SE = 2,
        SW = 4,
        NW = 8,
        All = NE | SE | SW | NW
    }

    public enum AutoTileShape
    {
        Flat,         // fully surrounded on all 4 sides + all 4 diagonals
        StraightEdge, // connected on 3 sides, border on the 4th
        OuterCorner,  // connected on 2 adjacent sides, border on the other 2
        InnerCorner   // connected on all 4 sides but a single diagonal is missing
    }

    /// <summary>One visual piece (role + rotation) making up a tile's composite look.</summary>
    public struct AutoTilePiece
    {
        public AutoTileShape shape;
        public int rotationSteps;

        public AutoTilePiece(AutoTileShape shape, int rotationSteps)
        {
            this.shape = shape;
            this.rotationSteps = rotationSteps;
        }
    }

    public static class AutoTileMask
    {
        // Canonical (0 degree) masks each role prefab is authored against.
        public const TileSide StraightEdgeBase = TileSide.North | TileSide.East | TileSide.South; // border on West
        public const TileSide OuterCornerBase = TileSide.North | TileSide.East;                    // border on South+West
        public const TileCorner InnerCornerBase = TileCorner.SE | TileCorner.SW | TileCorner.NW;   // notch on NE

        /// <summary>Rotates a side mask 90 degrees clockwise (as seen from above).</summary>
        public static TileSide RotateClockwise(TileSide mask)
        {
            TileSide result = TileSide.None;
            if ((mask & TileSide.West) != 0) result |= TileSide.North;
            if ((mask & TileSide.North) != 0) result |= TileSide.East;
            if ((mask & TileSide.East) != 0) result |= TileSide.South;
            if ((mask & TileSide.South) != 0) result |= TileSide.West;
            return result;
        }

        /// <summary>Rotates a corner mask 90 degrees clockwise (as seen from above).</summary>
        public static TileCorner RotateClockwise(TileCorner mask)
        {
            TileCorner result = TileCorner.None;
            if ((mask & TileCorner.NW) != 0) result |= TileCorner.NE;
            if ((mask & TileCorner.NE) != 0) result |= TileCorner.SE;
            if ((mask & TileCorner.SE) != 0) result |= TileCorner.SW;
            if ((mask & TileCorner.SW) != 0) result |= TileCorner.NW;
            return result;
        }

        public static int CountSides(TileSide mask)
        {
            int count = 0;
            if ((mask & TileSide.North) != 0) count++;
            if ((mask & TileSide.East) != 0) count++;
            if ((mask & TileSide.South) != 0) count++;
            if ((mask & TileSide.West) != 0) count++;
            return count;
        }

        private static bool IsAdjacentPair(TileSide mask)
        {
            return mask == (TileSide.North | TileSide.East) ||
                   mask == (TileSide.East | TileSide.South) ||
                   mask == (TileSide.South | TileSide.West) ||
                   mask == (TileSide.West | TileSide.North);
        }

        /// <summary>
        /// Classifies a tile into one of the 4 rotation-only shapes and returns
        /// how many 90 degree clockwise steps must be applied to a 0-degree
        /// authored role prefab so its connections line up with the actual
        /// neighbours.
        ///
        /// orthoMask must be computed from the 4 orthogonal same-type
        /// neighbours, cornerMask from the 4 diagonal same-type neighbours
        /// (cornerMask is only consulted when all 4 orthogonal sides connect).
        ///
        /// Note: a lone tile, a tile with a single connection, or a 1-wide
        /// strip (2 opposite sides connected) cannot be represented by these 4
        /// whole-tile shapes - they fall back to Flat since there is no other
        /// pool to draw from.
        /// </summary>
        public static AutoTileShape Classify(TileSide orthoMask, TileCorner cornerMask, out int rotationSteps)
        {
            int sideCount = CountSides(orthoMask);

            if (sideCount == 4)
            {
                if (cornerMask == TileCorner.All)
                {
                    rotationSteps = 0;
                    return AutoTileShape.Flat;
                }

                rotationSteps = FindCornerRotation(InnerCornerBase, cornerMask);
                return AutoTileShape.InnerCorner;
            }

            if (sideCount == 3)
            {
                rotationSteps = FindSideRotation(StraightEdgeBase, orthoMask);
                return AutoTileShape.StraightEdge;
            }

            if (sideCount == 2 && IsAdjacentPair(orthoMask))
            {
                rotationSteps = FindSideRotation(OuterCornerBase, orthoMask);
                return AutoTileShape.OuterCorner;
            }

            // sideCount == 0, 1, or 2-opposite: no dedicated shape, fall back to Flat.
            rotationSteps = 0;
            return AutoTileShape.Flat;
        }

        /// <summary>
        /// Like <see cref="Classify"/>, but never falls back to a plain Flat
        /// piece for the 3 under-represented connectivity cases (isolated
        /// tile, single connection, 1-wide strip). Instead it composes 1 or 2
        /// existing role pieces (StraightEdge/OuterCorner) so every open side
        /// that has no real neighbour still shows a border - a placed tile
        /// should never look like it just floats with an unfinished edge.
        /// Returns 1 piece for the shapes that already have an exact match,
        /// 2 pieces for the composite fallback cases. The caller instantiates
        /// each returned piece and layers them on top of each other in the
        /// same cell.
        /// </summary>
        public static AutoTilePiece[] ClassifyComposite(TileSide orthoMask, TileCorner cornerMask)
        {
            int sideCount = CountSides(orthoMask);

            if (sideCount == 4)
            {
                if (cornerMask == TileCorner.All)
                    return new[] { new AutoTilePiece(AutoTileShape.Flat, 0) };

                return new[] { new AutoTilePiece(AutoTileShape.InnerCorner, FindCornerRotation(InnerCornerBase, cornerMask)) };
            }

            if (sideCount == 3)
                return new[] { new AutoTilePiece(AutoTileShape.StraightEdge, FindSideRotation(StraightEdgeBase, orthoMask)) };

            if (sideCount == 2 && IsAdjacentPair(orthoMask))
                return new[] { new AutoTilePiece(AutoTileShape.OuterCorner, FindSideRotation(OuterCornerBase, orthoMask)) };

            if (sideCount == 2)
            {
                // Opposite pair connected (1-wide strip) - the 2 open sides are also
                // opposite each other. OuterCorner pieces only ever cover an ADJACENT
                // pair, so they can't close 2 opposite sides without also incorrectly
                // walling off one of the real connections. Use 2 StraightEdge pieces
                // instead - each closes exactly 1 side, leaving the 2 connected sides
                // untouched.
                TileSide openA = (orthoMask == (TileSide.North | TileSide.South)) ? TileSide.East : TileSide.North;
                TileSide openB = (orthoMask == (TileSide.North | TileSide.South)) ? TileSide.West : TileSide.South;

                int stepA = FindSideRotation(StraightEdgeBase, TileSide.All & ~openA);
                int stepB = FindSideRotation(StraightEdgeBase, TileSide.All & ~openB);
                return new[]
                {
                    new AutoTilePiece(AutoTileShape.StraightEdge, stepA),
                    new AutoTilePiece(AutoTileShape.StraightEdge, stepB)
                };
            }

            if (sideCount == 1)
            {
                // Single connection - 3 open sides. Use the 2 OuterCorner rotations
                // that do NOT touch the connected side; together their adjacent pairs
                // cover exactly the 3 open sides with no false wall on the real neighbour.
                var pieces = new System.Collections.Generic.List<AutoTilePiece>(2);
                TileSide current = OuterCornerBase;
                for (int step = 0; step < 4; step++)
                {
                    if ((current & orthoMask) == 0)
                        pieces.Add(new AutoTilePiece(AutoTileShape.OuterCorner, step));
                    current = RotateClockwise(current);
                }
                return pieces.ToArray();
            }

            // sideCount == 0: fully isolated tile, all 4 sides open. 2 OuterCorner
            // pieces rotated 180 degrees apart (steps 0 and 2) close N+E and S+W -
            // exactly all 4 sides, no gaps, no overlap.
            return new[]
            {
                new AutoTilePiece(AutoTileShape.OuterCorner, 0),
                new AutoTilePiece(AutoTileShape.OuterCorner, 2)
            };
        }

        private static int FindSideRotation(TileSide baseMask, TileSide targetMask)
        {
            TileSide current = baseMask;
            for (int step = 0; step < 4; step++)
            {
                if (current == targetMask)
                    return step;
                current = RotateClockwise(current);
            }

            Debug.LogWarning($"AutoTileMask: could not match side mask {targetMask} against base {baseMask}");
            return 0;
        }

        private static int FindCornerRotation(TileCorner baseMask, TileCorner targetMask)
        {
            TileCorner current = baseMask;
            for (int step = 0; step < 4; step++)
            {
                if (current == targetMask)
                    return step;
                current = RotateClockwise(current);
            }

            Debug.LogWarning($"AutoTileMask: could not match corner mask {targetMask} against base {baseMask}");
            return 0;
        }
    }
}
