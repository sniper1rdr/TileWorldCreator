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

    public enum AutoTileShape
    {
        Isolated,   // 0 connected sides
        EndCap,     // 1 connected side (dead end)
        Straight,   // 2 connected opposite sides
        Corner,     // 2 connected adjacent sides
        TJunction,  // 3 connected sides
        Cross       // 4 connected sides
    }

    public static class AutoTileMask
    {
        // Canonical (0 degree) masks each role prefab is authored against.
        public const TileSide EndCapBase = TileSide.North;
        public const TileSide StraightBase = TileSide.North | TileSide.South;
        public const TileSide CornerBase = TileSide.North | TileSide.East;
        public const TileSide TJunctionBase = TileSide.North | TileSide.East | TileSide.South;

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

        public static int CountSides(TileSide mask)
        {
            int count = 0;
            if ((mask & TileSide.North) != 0) count++;
            if ((mask & TileSide.East) != 0) count++;
            if ((mask & TileSide.South) != 0) count++;
            if ((mask & TileSide.West) != 0) count++;
            return count;
        }

        private static bool IsOpposite(TileSide mask)
        {
            return mask == (TileSide.North | TileSide.South) || mask == (TileSide.East | TileSide.West);
        }

        /// <summary>
        /// Classifies a neighbour mask into a rotation-invariant shape and returns
        /// how many 90 degree clockwise steps must be applied to a 0-degree
        /// authored role prefab so its connections line up with the actual mask.
        /// </summary>
        public static AutoTileShape Classify(TileSide mask, out int rotationSteps)
        {
            int count = CountSides(mask);

            switch (count)
            {
                case 0:
                    rotationSteps = 0;
                    return AutoTileShape.Isolated;

                case 1:
                    rotationSteps = FindRotation(EndCapBase, mask);
                    return AutoTileShape.EndCap;

                case 2:
                    if (IsOpposite(mask))
                    {
                        rotationSteps = FindRotation(StraightBase, mask);
                        return AutoTileShape.Straight;
                    }
                    rotationSteps = FindRotation(CornerBase, mask);
                    return AutoTileShape.Corner;

                case 3:
                    rotationSteps = FindRotation(TJunctionBase, mask);
                    return AutoTileShape.TJunction;

                default:
                    rotationSteps = 0;
                    return AutoTileShape.Cross;
            }
        }

        private static int FindRotation(TileSide baseMask, TileSide targetMask)
        {
            TileSide current = baseMask;
            for (int step = 0; step < 4; step++)
            {
                if (current == targetMask)
                    return step;
                current = RotateClockwise(current);
            }

            // Should never happen for a mask with the same side-count as baseMask.
            Debug.LogWarning($"AutoTileMask: could not match mask {targetMask} against base {baseMask}");
            return 0;
        }
    }
}
