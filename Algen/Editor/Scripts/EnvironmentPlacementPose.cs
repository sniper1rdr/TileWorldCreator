using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal readonly struct EnvironmentPlacementPose
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }

        public EnvironmentPlacementPose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }
}
