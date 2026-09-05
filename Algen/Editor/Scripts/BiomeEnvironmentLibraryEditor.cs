using System.Collections.Generic;
using AglenRealms;
using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    public static class BiomeEnvironmentLibraryEditor
    {
        public static BiomeEnvironmentLibraryDefinition ResolveLibrary(string biomeId)
        {
            BiomeEnvironmentLibraryDefinition library = BiomeEnvironmentLibrary.Resolve(biomeId);
            if (library != null)
                return library;

            return DiscoverLibraryFromFolders(biomeId);
        }

        public static GameObject[] GetPrefabs(string biomeId, EnvironmentCategory category)
        {
            BiomeEnvironmentLibraryDefinition library = BiomeEnvironmentLibrary.Resolve(biomeId);
            if (library != null)
            {
                GameObject[] fromLibrary = library.GetPrefabsCopy(category);
                if (fromLibrary.Length > 0)
                    return fromLibrary;
            }

            // Folder scan already allocates a new array — not an asset-owned reference.
            return GetPrefabsFromFolderScan(biomeId, category);
        }

        public static BiomeEnvironmentLibraryDefinition BuildLibraryForBiome(string biomeId, string displayName, string assetToken)
        {
            BiomeEnvironmentCategoryEntry[] categories = BuildCategoryEntries(biomeId);
            string assetPath = AglenRealmsPaths.BiomeEnvironmentLibraryAsset(biomeId, assetToken);

            BiomeEnvironmentLibraryDefinition library =
                AssetDatabase.LoadAssetAtPath<BiomeEnvironmentLibraryDefinition>(assetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<BiomeEnvironmentLibraryDefinition>();
                AssetDatabase.CreateAsset(library, assetPath);
            }

            library.biomeId = biomeId;
            library.displayName = displayName;
            library.categories = categories;
            EditorUtility.SetDirty(library);
            return library;
        }

        public static GameObject[] LoadPrefabsInFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return System.Array.Empty<GameObject>();

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            var loaded = new List<GameObject>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) is GameObject prefab)
                    loaded.Add(prefab);
            }

            loaded.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return loaded.ToArray();
        }

        public static GameObject[] LoadPrefabsDirectlyInFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return System.Array.Empty<GameObject>();

            string normalizedFolder = folderPath.Replace('\\', '/').TrimEnd('/');
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { normalizedFolder });
            var loaded = new List<GameObject>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string parentFolder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (parentFolder != normalizedFolder)
                    continue;

                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) is GameObject prefab)
                    loaded.Add(prefab);
            }

            loaded.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return loaded.ToArray();
        }

        public static EnvironmentCategory ClassifyEnvironmentPrefab(string prefabName)
        {
            string normalized = prefabName.ToLowerInvariant();
            if (normalized.Contains("rock") || normalized.Contains("stone"))
                return EnvironmentCategory.Rocks;

            if (normalized.Contains("tree") ||
                normalized.Contains("bush") ||
                normalized.Contains("grass") ||
                normalized.Contains("pine") ||
                normalized.Contains("fern") ||
                normalized.Contains("moss") ||
                normalized.Contains("flower") ||
                normalized.Contains("reed") ||
                normalized.Contains("cactus") ||
                normalized.Contains("shrub") ||
                normalized.Contains("stump") ||
                normalized.Contains("log"))
                return EnvironmentCategory.Vegetation;

            return EnvironmentCategory.Props;
        }

        private static GameObject[] GetPrefabsFromFolderScan(string biomeId, EnvironmentCategory category)
        {
            BiomeEnvironmentCategoryEntry[] entries = BuildCategoryEntries(biomeId);
            for (int i = 0; i < entries.Length; i++)
            {
                BiomeEnvironmentCategoryEntry entry = entries[i];
                if (entry != null && entry.category == category && entry.prefabs != null)
                    return entry.prefabs;
            }

            return System.Array.Empty<GameObject>();
        }

        private static BiomeEnvironmentLibraryDefinition DiscoverLibraryFromFolders(string biomeId)
        {
            BiomeEnvironmentCategoryEntry[] categories = BuildCategoryEntries(biomeId);
            if (categories.Length == 0)
                return null;

            BiomeDefinition biome = BiomeRegistry.GetById(biomeId);
            string displayName = biome != null ? biome.displayName : biomeId;

            var library = ScriptableObject.CreateInstance<BiomeEnvironmentLibraryDefinition>();
            library.biomeId = biomeId;
            library.displayName = displayName;
            library.categories = categories;
            return library;
        }

        private static BiomeEnvironmentCategoryEntry[] BuildCategoryEntries(string biomeId)
        {
            var buckets = CreateEmptyBuckets();

            for (int i = 0; i < EnvironmentCategoryExtensions.All.Length; i++)
            {
                EnvironmentCategory category = EnvironmentCategoryExtensions.All[i];
                AddPrefabsToBucket(
                    buckets,
                    category,
                    LoadPrefabsInFolder(AglenRealmsPaths.BiomeEnvironmentCategoryPrefabsRoot(biomeId, category)));
            }

            AddClassifiedPrefabs(
                buckets,
                LoadPrefabsDirectlyInFolder(AglenRealmsPaths.BiomeEnvironmentPrefabsRoot(biomeId)));

            return BuildCategoryEntries(buckets);
        }

        private static Dictionary<EnvironmentCategory, List<GameObject>> CreateEmptyBuckets()
        {
            var buckets = new Dictionary<EnvironmentCategory, List<GameObject>>(EnvironmentCategoryExtensions.All.Length);
            for (int i = 0; i < EnvironmentCategoryExtensions.All.Length; i++)
                buckets[EnvironmentCategoryExtensions.All[i]] = new List<GameObject>();
            return buckets;
        }

        private static void AddPrefabsToBucket(
            Dictionary<EnvironmentCategory, List<GameObject>> buckets,
            EnvironmentCategory category,
            GameObject[] prefabs)
        {
            List<GameObject> bucket = buckets[category];
            for (int i = 0; i < prefabs.Length; i++)
                TryAddUnique(bucket, prefabs[i]);
        }

        private static BiomeEnvironmentCategoryEntry[] BuildCategoryEntries(
            Dictionary<EnvironmentCategory, List<GameObject>> buckets)
        {
            var categories = new List<BiomeEnvironmentCategoryEntry>(EnvironmentCategoryExtensions.All.Length);
            for (int i = 0; i < EnvironmentCategoryExtensions.All.Length; i++)
            {
                EnvironmentCategory category = EnvironmentCategoryExtensions.All[i];
                List<GameObject> prefabs = buckets[category];
                if (prefabs.Count == 0)
                    continue;

                prefabs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                categories.Add(new BiomeEnvironmentCategoryEntry
                {
                    category = category,
                    prefabs = prefabs.ToArray()
                });
            }

            return categories.ToArray();
        }

        private static void AddClassifiedPrefabs(
            Dictionary<EnvironmentCategory, List<GameObject>> buckets,
            GameObject[] prefabs,
            bool forceProps = false)
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                GameObject prefab = prefabs[i];
                if (prefab == null)
                    continue;

                EnvironmentCategory category = forceProps
                    ? EnvironmentCategory.Props
                    : ClassifyEnvironmentPrefab(prefab.name);
                TryAddUnique(buckets[category], prefab);
            }
        }

        private static void TryAddUnique(List<GameObject> bucket, GameObject prefab)
        {
            if (prefab == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(prefab);
            for (int i = 0; i < bucket.Count; i++)
            {
                if (AssetDatabase.GetAssetPath(bucket[i]) == assetPath)
                    return;
            }

            bucket.Add(prefab);
        }
    }
}
