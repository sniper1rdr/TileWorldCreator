using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal static class EnvironmentPainterState
    {
        public static string ActiveBiomeId { get; private set; }
        public static EnvironmentCategory ActiveCategory { get; private set; } = EnvironmentCategory.Rocks;
        public static GameObject ActivePrefab { get; private set; }
        public static string SearchQuery { get; set; } = string.Empty;

        private static EnvironmentRoot boundEnvironment;
        private static readonly EnvironmentBrushSettings fallbackBrushSettings = new EnvironmentBrushSettings();

        public static EnvironmentBrushSettings BrushSettings =>
            boundEnvironment != null ? boundEnvironment.BrushSettings : fallbackBrushSettings;

        public static bool HasActivePrefab => ActivePrefab != null;

        public static void BindEnvironment(EnvironmentRoot environment) => boundEnvironment = environment;

        public static void SetBiome(string biomeId)
        {
            if (ActiveBiomeId == biomeId)
                return;

            ActiveBiomeId = biomeId;
            ActivePrefab = null;
        }

        public static void SetCategory(EnvironmentCategory category)
        {
            if (ActiveCategory == category)
                return;

            ActiveCategory = category;
            ActivePrefab = null;
        }

        public static void SetSearchQuery(string query)
        {
            SearchQuery = query ?? string.Empty;
        }

        public static void SetPrefab(GameObject prefab)
        {
            if (ActivePrefab == prefab)
            {
                ActivePrefab = null;
                return;
            }

            ActivePrefab = prefab;
            RollPlacementRandoms();
        }

        public static void ClearPrefab()
        {
            ActivePrefab = null;
        }

        public static bool TryDeactivatePainting()
        {
            if (!HasActivePrefab)
                return false;

            ActivePrefab = null;
            return true;
        }

        public static float PlacementRandomYaw { get; private set; }
        public static float PlacementRandomUniformScale { get; private set; } = 1f;

        public static void RollPlacementRandoms()
        {
            EnvironmentBrushSettings settings = BrushSettings;
            PlacementRandomYaw = settings.randomRotation ? Random.Range(0f, 360f) : 0f;

            if (settings.randomScale)
            {
                float min = Mathf.Min(settings.randomScaleRange.x, settings.randomScaleRange.y);
                float max = Mathf.Max(settings.randomScaleRange.x, settings.randomScaleRange.y);
                PlacementRandomUniformScale = Random.Range(min, max);
            }
            else
            {
                PlacementRandomUniformScale = 1f;
            }
        }

        public static void SyncBiomeFromEnvironment(string biomeId)
        {
            if (string.IsNullOrWhiteSpace(biomeId) || ActiveBiomeId == biomeId)
                return;

            ActiveBiomeId = biomeId;
            ActivePrefab = null;
        }

        public static void EnsureDefaults(string fallbackBiomeId)
        {
            if (string.IsNullOrWhiteSpace(ActiveBiomeId))
                ActiveBiomeId = fallbackBiomeId;
        }

        public static void SyncLayersFromTarget(EnvironmentRoot target)
        {
            if (target == null)
                return;

            target.EnsureDefaultLayer();
        }

        public static void SetActiveLayer(int layerIndex)
        {
            ActiveEnvironmentLayerIndex = layerIndex;
        }

        public static int ActiveEnvironmentLayerIndex { get; private set; }
    }
}
