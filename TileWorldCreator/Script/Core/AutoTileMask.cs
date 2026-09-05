using System.Collections.Generic;
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
        Flat,         // full-cell ground base, always placed regardless of neighbours
        StraightEdge, // small edge trim overlay, placed along one side with no same-type neighbour
        OuterCorner,  // small convex corner overlay, placed where 2 adjacent open sides meet
        InnerCorner   // small concave corner overlay, placed where a single diagonal neighbour is missing
    }

    /// <summary>What role a composite piece plays and how it should be positioned within the cell.</summary>
    public enum AutoTilePieceKind
    {
        Base,   // covers the whole cell, no offset
        Edge,   // offset toward one side (edgeSide)
        Corner  // offset toward one diagonal corner (cornerDir)
    }

    /// <summary>
    /// One visual piece (role + rotation + placement) making up a tile's
    /// composite look. The Flat base always covers the whole cell; Edge and
    /// Corner pieces are small overlays offset toward the side/corner they
    /// border, layered on top of the base.
    /// </summary>
    public struct AutoTilePiece
    {
        public AutoTileShape shape;
        public int rotationSteps;
        public AutoTilePieceKind kind;
        public TileSide edgeSide;     // meaningful when kind == Edge
        public TileCorner cornerDir;  // meaningful when kind == Corner
    }

    public static class AutoTileMask
    {
        // Canonical (0 degree) masks each role prefab is authored against.
        public const TileSide StraightEdgeBase = TileSide.North | TileSide.East | TileSide.South; // border on West
        public const TileSide OuterCornerBase = TileSide.North | TileSide.East;                    // border on South+West
        public const TileCorner InnerCornerBase = TileCorner.SE | TileCorner.SW | TileCorner.NW;   // notch on NE

        // Which 2 orthogonal sides meet at each diagonal corner.
        private static readonly (TileCorner corner, TileSide sideA, TileSide sideB)[] CornerDefs =
        {
            (TileCorner.NE, TileSide.North, TileSide.East),
            (TileCorner.SE, TileSide.East, TileSide.South),
            (TileCorner.SW, TileSide.South, TileSide.West),
            (TileCorner.NW, TileSide.West, TileSide.North),
        };

        private static readonly TileSide[] AllSides =
        {
            TileSide.North, TileSide.East, TileSide.South, TileSide.West
        };

        /// <summary>Local XZ direction (unit-ish, sign only) a given side points toward. X=East, Y=North.</summary>
        public static Vector2 SideDirection(TileSide side)
        {
            switch (side)
            {
                case TileSide.North: return new Vector2(0f, 1f);
                case TileSide.East: return new Vector2(1f, 0f);
                case TileSide.South: return new Vector2(0f, -1f);
                case TileSide.West: return new Vector2(-1f, 0f);
                default: return Vector2.zero;
            }
        }

        /// <summary>Local XZ direction (sign only, both axes) a given diagonal corner points toward.</summary>
        public static Vector2 CornerDirection(TileCorner corner)
        {
            switch (corner)
            {
                case TileCorner.NE: return new Vector2(1f, 1f);
                case TileCorner.SE: return new Vector2(1f, -1f);
                case TileCorner.SW: return new Vector2(-1f, -1f);
                case TileCorner.NW: return new Vector2(-1f, 1f);
                default: return Vector2.zero;
            }
        }

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

        /// <summary>
        /// Builds the full set of composite pieces for a cell: a Flat base
        /// (always, covers the whole cell) plus a small Straight Edge overlay
        /// for every orthogonal side with no same-type neighbour, plus a small
        /// corner overlay for every diagonal corner that needs one - Outer
        /// Corner where its 2 adjacent sides are both open (a convex corner),
        /// or Inner Corner where both adjacent sides connect but the diagonal
        /// neighbour itself is missing (a concave notch). An edge is skipped
        /// whenever BOTH corners at its 2 ends already get an Outer Corner
        /// piece, since those already close off that whole side and a
        /// separate edge there would just duplicate/overlap them. This
        /// naturally covers every possible neighbour configuration with no
        /// gaps: an isolated tile (no neighbours at all) gets only its 4
        /// Outer Corner pieces (no edges - each side's both corners trigger,
        /// so every edge is redundant) layered on top of its Flat base.
        ///
        /// orthoMask must be computed from the 4 orthogonal same-type
        /// neighbours, cornerMask from the 4 diagonal same-type neighbours.
        /// </summary>
        public static AutoTilePiece[] BuildPieces(TileSide orthoMask, TileCorner cornerMask)
        {
            var pieces = new List<AutoTilePiece>();

            pieces.Add(new AutoTilePiece
            {
                shape = AutoTileShape.Flat,
                rotationSteps = 0,
                kind = AutoTilePieceKind.Base
            });

            // Figure out which diagonal corners will get an Outer Corner piece
            // first (both adjacent sides open), so edges that would be fully
            // covered by 2 such corners at both their ends can be skipped -
            // otherwise an isolated tile ends up with both 4 edges AND 4
            // corners stacked on top of each other (redundant, looks wrong).
            var outerCornerAt = new Dictionary<TileCorner, bool>();
            foreach (var def in CornerDefs)
            {
                bool sideAOpen = (orthoMask & def.sideA) == 0;
                bool sideBOpen = (orthoMask & def.sideB) == 0;
                outerCornerAt[def.corner] = sideAOpen && sideBOpen;
            }

            foreach (TileSide side in AllSides)
            {
                if ((orthoMask & side) != 0) continue; // has a neighbour there - no edge needed

                var flanks = FlankingCorners(side);
                if (outerCornerAt[flanks.a] && outerCornerAt[flanks.b])
                    continue; // both ends already covered by an Outer Corner piece - edge would be redundant

                int steps = FindSideRotation(StraightEdgeBase, TileSide.All & ~side);
                pieces.Add(new AutoTilePiece
                {
                    shape = AutoTileShape.StraightEdge,
                    rotationSteps = steps,
                    kind = AutoTilePieceKind.Edge,
                    edgeSide = side
                });
            }

            foreach (var def in CornerDefs)
            {
                bool sideAOpen = (orthoMask & def.sideA) == 0;
                bool sideBOpen = (orthoMask & def.sideB) == 0;

                if (outerCornerAt[def.corner])
                {
                    int steps = FindSideRotation(OuterCornerBase, TileSide.All & ~(def.sideA | def.sideB));
                    pieces.Add(new AutoTilePiece
                    {
                        shape = AutoTileShape.OuterCorner,
                        rotationSteps = steps,
                        kind = AutoTilePieceKind.Corner,
                        cornerDir = def.corner
                    });
                }
                else if (!sideAOpen && !sideBOpen && (cornerMask & def.corner) == 0)
                {
                    int steps = FindCornerRotation(InnerCornerBase, TileCorner.All & ~def.corner);
                    pieces.Add(new AutoTilePiece
                    {
                        shape = AutoTileShape.InnerCorner,
                        rotationSteps = steps,
                        kind = AutoTilePieceKind.Corner,
                        cornerDir = def.corner
                    });
                }
            }

            return pieces.ToArray();
        }

        /// <summary>The 2 diagonal corners that sit at the 2 ends of a given orthogonal side.</summary>
        private static (TileCorner a, TileCorner b) FlankingCorners(TileSide side)
        {
            switch (side)
            {
                case TileSide.North: return (TileCorner.NW, TileCorner.NE);
                case TileSide.East: return (TileCorner.NE, TileCorner.SE);
                case TileSide.South: return (TileCorner.SE, TileCorner.SW);
                case TileSide.West: return (TileCorner.SW, TileCorner.NW);
                default: return (TileCorner.None, TileCorner.None);
            }
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
