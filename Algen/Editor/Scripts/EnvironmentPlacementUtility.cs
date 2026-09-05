using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal static class EnvironmentPlacementUtility
    {
        public static bool TryGetPlacement(
            Event e,
            EnvironmentRoot environment,
            DualGrid3D alignTarget,
            EnvironmentBrushSettings settings,
            float fallbackPlaneWorldY,
            out EnvironmentPlacementPose pose)
        {
            pose = default;
            if (environment == null || e == null)
                return false;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (settings.alignToSurface && TryRaycastSurface(ray, alignTarget, environment, settings, out RaycastHit hit))
            {
                pose = BuildPose(hit.point, settings);
                return true;
            }

            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, fallbackPlaneWorldY, 0f));
            if (!groundPlane.Raycast(ray, out float distance))
                return false;

            pose = BuildPose(ray.GetPoint(distance), settings);
            return true;
        }

        public static void ApplyPose(Transform transform, EnvironmentPlacementPose pose)
        {
            transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            transform.localScale = pose.Scale;
        }

        private static EnvironmentPlacementPose BuildPose(Vector3 position, EnvironmentBrushSettings settings)
        {
            return new EnvironmentPlacementPose(position, BuildRotation(settings), BuildScale(settings));
        }

        private static Quaternion BuildRotation(EnvironmentBrushSettings settings)
        {
            if (!settings.randomRotation)
                return Quaternion.identity;

            return Quaternion.Euler(0f, EnvironmentPainterState.PlacementRandomYaw, 0f);
        }

        private static Vector3 BuildScale(EnvironmentBrushSettings settings)
        {
            if (!settings.randomScale)
                return Vector3.one;

            return Vector3.one * EnvironmentPainterState.PlacementRandomUniformScale;
        }

        private static bool TryRaycastSurface(
            Ray ray,
            DualGrid3D alignTarget,
            EnvironmentRoot environment,
            EnvironmentBrushSettings settings,
            out RaycastHit bestHit)
        {
            bestHit = default;
            RaycastHit[] hits = Physics.RaycastAll(ray, 5000f);
            if (hits.Length == 0)
                return false;

            bool found = false;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                    continue;

                Transform hitTransform = hit.collider.transform;
                if (!IsAllowedAlignTransform(hitTransform, alignTarget, environment, settings))
                    continue;

                if (hit.distance >= bestDistance)
                    continue;

                bestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }

            return found;
        }

        private static bool IsAllowedAlignTransform(
            Transform hitTransform,
            DualGrid3D alignTarget,
            EnvironmentRoot environment,
            EnvironmentBrushSettings settings)
        {
            switch (settings.alignMode)
            {
                case EnvironmentAlignMode.Landscape:
                    return IsUnderTransform(hitTransform, alignTarget != null ? alignTarget.transform : null);

                case EnvironmentAlignMode.Environment:
                    return environment != null &&
                           IsUnderTransform(hitTransform, environment.transform);

                case EnvironmentAlignMode.All:
                    if ((settings.alignLayerMask.value & (1 << hitTransform.gameObject.layer)) == 0)
                        return false;

                    return true;

                default:
                    return false;
            }
        }

        private static bool IsUnderTransform(Transform hitTransform, Transform root)
        {
            if (root == null)
                return false;

            return hitTransform == root || hitTransform.IsChildOf(root);
        }
    }
}
