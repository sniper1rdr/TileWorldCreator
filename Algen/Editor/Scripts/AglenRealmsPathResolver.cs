#if UNITY_EDITOR
using UnityEditor;

namespace AglenRealms
{
    /// <summary>
    /// Resolves content paths during UPM migration (legacy Assets vs package Content).
    /// </summary>
    public static class AglenRealmsPathResolver
    {
        public static string GroundPrefabsRoot(string biomeId, string folderName) =>
            FirstValidFolder(
                AglenRealmsPaths.BiomeGroundPrefabsRoot(biomeId),
                $"{AglenRealmsPaths.LegacyPrefabsRoot}/Ground/{folderName}");

        public static string LiquidPrefabsRoot(string biomeId, string folderName) =>
            FirstValidFolder(
                AglenRealmsPaths.BiomeLiquidPrefabsRoot(biomeId),
                $"{AglenRealmsPaths.LegacyPrefabsRoot}/Liquid/{folderName}_Liquid");

        public static string BiomeDefinitionsRoot() =>
            FirstValidFolder(
                AglenRealmsPaths.StarterBiomesRoot,
                AglenRealmsPaths.LegacyBiomesRoot);

        public static string BiomeDefinitionAsset(string biomeId, string displayName)
        {
            string upmPath = AglenRealmsPaths.BiomeDefinitionAsset(biomeId, displayName);
            if (AssetExists(upmPath))
                return upmPath;

            return $"{AglenRealmsPaths.LegacyBiomesRoot}/{displayName}_BiomeDefinition.asset";
        }

        public static string BiomeMaterial(string biomeId, string biomeName) =>
            FirstValidAsset(
                $"{AglenRealmsPaths.BiomeMaterialsRoot(biomeId)}/{biomeName}_Material.mat",
                $"{AglenRealmsPaths.LegacyMaterialRoot}/{biomeName}_Material.mat");

        public static string LiquidPrefab(string biomeId, string folderName, int index)
        {
            string suffix = folderName.ToLowerInvariant();
            string fileName = $"liquid_{index}_{suffix}.prefab";
            string upmRoot = AglenRealmsPaths.BiomeLiquidPrefabsRoot(biomeId);
            string upmPath = $"{upmRoot}/{fileName}";
            if (AssetExists(upmPath))
                return upmPath;

            return $"{AglenRealmsPaths.LegacyPrefabsRoot}/Liquid/{folderName}_Liquid/{fileName}";
        }

        public static string GroundPrefab(string biomeId, string folderName, string fileName)
        {
            string upmPath = $"{AglenRealmsPaths.BiomeGroundPrefabsRoot(biomeId)}/{fileName}";
            if (AssetExists(upmPath))
                return upmPath;

            return $"{AglenRealmsPaths.LegacyPrefabsRoot}/Ground/{folderName}/{fileName}";
        }

        private static string FirstValidFolder(string primary, string fallback)
        {
            if (AssetDatabase.IsValidFolder(primary))
                return primary;

            return fallback;
        }

        private static string FirstValidAsset(string primary, string fallback) =>
            AssetExists(primary) ? primary : fallback;

        private static bool AssetExists(string assetPath) =>
            !string.IsNullOrEmpty(assetPath) && AssetDatabase.LoadMainAssetAtPath(assetPath) != null;
    }
}
#endif
