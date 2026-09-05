using System.Collections.Generic;
using AglenRealms.WorldCore;

namespace AglenRealms
{
    /// <summary>
    /// Canonical content paths for the Aglen Realms UPM ecosystem.
    /// Dev tools and exporters should reference these constants instead of hardcoded strings.
    /// </summary>
    public static class AglenRealmsPaths
    {
        public const string WorldCorePackage = "Packages/com.aglenrealms.world-core";
        public const string BiomesRoot = "Packages/com.aglenrealms.biome-";

        public const string SharedMeshesRoot = WorldCorePackage + "/Content/SharedMeshes";
        public const string SharedTilesRoot = SharedMeshesRoot + "/Tiles";
        public const string SharedLiquidRoot = SharedMeshesRoot + "/Liquid";
        public const string SharedPropsRoot = SharedMeshesRoot + "/Props";
        public const string SharedEnvironmentRoot = SharedMeshesRoot + "/Environment";

        public const string ShadersRoot = WorldCorePackage + "/Content/Shaders";
        public const string StarterBiomesRoot = WorldCorePackage + "/Content/StarterBiomes";
        public const string RuntimeScriptsRoot = WorldCorePackage + "/Runtime/Scripts";
        public const string EditorScriptsRoot = WorldCorePackage + "/Editor/Scripts";

        private static readonly Dictionary<string, string> StarterBiomeFolders = new()
        {
            [BiomeIds.Grasslands] = "Grasslands",
            [BiomeIds.FrozenTundra] = "FrozenTundra",
            [BiomeIds.GoldenDesert] = "GoldenDesert",
        };

        public static bool IsStarterBiome(string biomeId) =>
            StarterBiomeFolders.ContainsKey(biomeId);

        public static string StarterBiomeFolder(string biomeId) =>
            StarterBiomeFolders.TryGetValue(biomeId, out string folder) ? folder : null;

        public static string BiomePackage(string biomeId) => "com.aglenrealms.biome-" + biomeId;

        public static string BiomeContentRoot(string biomeId)
        {
            if (StarterBiomeFolders.TryGetValue(biomeId, out string folder))
                return $"{StarterBiomesRoot}/{folder}";

            return "Packages/" + BiomePackage(biomeId) + "/Content";
        }

        public static string BiomePrefabsRoot(string biomeId) =>
            BiomeContentRoot(biomeId) + "/Prefabs";

        public static string BiomeGroundPrefabsRoot(string biomeId) =>
            BiomePrefabsRoot(biomeId) + "/Ground";

        public static string BiomeLiquidPrefabsRoot(string biomeId) =>
            BiomePrefabsRoot(biomeId) + "/Liquid";

        public static string BiomeEnvironmentPrefabsRoot(string biomeId) =>
            BiomePrefabsRoot(biomeId) + "/Environment";

        public static string BiomeEnvironmentCategoryPrefabsRoot(string biomeId, EnvironmentCategory category) =>
            BiomeEnvironmentPrefabsRoot(biomeId) + "/" + category.GetFolderName();

        public static string BiomePropsPrefabsRoot(string biomeId) =>
            BiomeEnvironmentCategoryPrefabsRoot(biomeId, EnvironmentCategory.Props);

        public static string BiomeEnvironmentLibraryAsset(string biomeId, string assetToken) =>
            BiomeContentRoot(biomeId) + "/" + assetToken + "_EnvironmentLibrary.asset";

        public static string BiomeMaterialsRoot(string biomeId) =>
            BiomeContentRoot(biomeId) + "/Materials";

        public static string BiomeTexturesRoot(string biomeId) =>
            BiomeContentRoot(biomeId) + "/Textures";

        public static string BiomeUniqueMeshesRoot(string biomeId) =>
            BiomeContentRoot(biomeId) + "/UniqueMeshes";

        public static string BiomeDefinitionAsset(string biomeId, string displayName) =>
            BiomeContentRoot(biomeId) + "/" + displayName + "_BiomeDefinition.asset";

        public static string BiomeScenesRoot(string biomeId) =>
            BiomeContentRoot(biomeId) + "/Scenes";

        public static string BiomeShowcaseScenePath(string biomeId) =>
            BiomeScenesRoot(biomeId) + "/Showcase.unity";

        public static string BiomeDemoScenePath(string biomeId) =>
            BiomeScenesRoot(biomeId) + "/Demo.unity";

        // Legacy paths — used during migration from Assets/AglenRealmsTool.
        public const string LegacyRoot = "Assets/AglenRealmsTool";
        public const string LegacyFbxRoot = LegacyRoot + "/FBX";
        public const string LegacyPrefabsRoot = LegacyRoot + "/Prefabs";
        public const string LegacyMaterialRoot = LegacyRoot + "/Material";
        public const string LegacyTexturesRoot = LegacyRoot + "/Textures";
        public const string LegacyBiomesRoot = LegacyRoot + "/Biomes";
    }
}
