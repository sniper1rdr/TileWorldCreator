using System;
using UnityEngine;

namespace AglenRealms.WorldCore
{
    [Serializable]
    public struct LandscapeCellKey : IEquatable<LandscapeCellKey>
    {
        public int x;
        public int y;
        public int z;
        public int layer;

        public LandscapeCellKey(int x, int y, int z, int layer = 0)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.layer = layer;
        }

        public LandscapeCellKey Offset(Vector3Int delta) =>
            new LandscapeCellKey(x + delta.x, y + delta.y, z + delta.z, layer);

        public bool Equals(LandscapeCellKey other) =>
            x == other.x && y == other.y && z == other.z && layer == other.layer;

        public override bool Equals(object obj) => obj is LandscapeCellKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(x, y, z, layer);

        public static bool operator ==(LandscapeCellKey left, LandscapeCellKey right) => left.Equals(right);

        public static bool operator !=(LandscapeCellKey left, LandscapeCellKey right) => !left.Equals(right);
    }
}
