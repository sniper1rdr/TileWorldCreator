using System;
using System.Collections.Generic;
using UnityEngine;

namespace AglenRealms.WorldCore
{
    public static class BiomeEnvironmentLibrary
    {
        // Synthetic, never-saved libraries for biomeIds backed by more than one BiomeEnvironmentLibraryDefinition
        // asset (official biome pack + a separately shipped add-on pack targeting the same biomeId). Rebuilt
        // whenever BiomeEnvironmentLibraryRegistry.Invalidated fires (project/package changes).
        private static readonly Dictionary<string, BiomeEnvironmentLibraryDefinition> MergedCache = new();
        private static bool _subscribedToInvalidation;

        public static BiomeEnvironmentLibraryDefinition Resolve(string biomeId)
        {
            if (string.IsNullOrWhiteSpace(biomeId))
                return null;

            EnsureSubscribedToInvalidation();

            BiomeDefinition biome = BiomeRegistry.GetById(biomeId);
            BiomeEnvironmentLibraryDefinition primary = biome != null
                ? BiomeEnvironmentLibraryRegistry.ResolveForBiome(biome)
                : null;

            if (primary == null)
                primary = BiomeEnvironmentLibraryRegistry.GetByBiomeId(biomeId);

            if (primary == null)
                return null;

            return ResolveMerged(biomeId, primary);
        }

        public static GameObject[] GetPrefabs(string biomeId, EnvironmentCategory category)
        {
            BiomeEnvironmentLibraryDefinition library = Resolve(biomeId);
            if (library == null)
                return Array.Empty<GameObject>();

            // Detached copy — never expose asset-owned prefab arrays to callers.
            return library.GetPrefabsCopy(category);
        }

        public static IReadOnlyList<BiomeEnvironmentCategoryEntry> GetCategories(string biomeId)
        {
            BiomeEnvironmentLibraryDefinition library = Resolve(biomeId);
            if (library?.categories == null)
                return Array.Empty<BiomeEnvironmentCategoryEntry>();

            var result = new List<BiomeEnvironmentCategoryEntry>(library.categories.Length);
            for (int i = 0; i < library.categories.Length; i++)
            {
                BiomeEnvironmentCategoryEntry entry = library.categories[i];
                if (entry != null && entry.HasPrefabs)
                    result.Add(entry);
            }

            return result;
        }

        /// <summary>
        /// Combines every library registered under <paramref name="biomeId"/> into one category set, so an add-on
        /// pack (Buildings, VFX, ...) shipping its own <see cref="BiomeEnvironmentLibraryDefinition"/> with a matching
        /// biomeId is picked up on import — no World Core code change or update needed per add-on.
        /// </summary>
        private static BiomeEnvironmentLibraryDefinition ResolveMerged(string biomeId, BiomeEnvironmentLibraryDefinition primary)
        {
            IReadOnlyList<BiomeEnvironmentLibraryDefinition> all = BiomeEnvironmentLibraryRegistry.GetAllByBiomeId(biomeId);
            if (all.Count <= 1)
                return primary; // Single-library biome (today's official packs) — unchanged behavior, no allocation.

            if (MergedCache.TryGetValue(biomeId, out BiomeEnvironmentLibraryDefinition cached) && cached != null)
                return cached;

            BiomeEnvironmentLibraryDefinition merged = BuildMergedLibrary(biomeId, primary, all);
            MergedCache[biomeId] = merged;
            return merged;
        }

        private static BiomeEnvironmentLibraryDefinition BuildMergedLibrary(
            string biomeId,
            BiomeEnvironmentLibraryDefinition primary,
            IReadOnlyList<BiomeEnvironmentLibraryDefinition> discovered)
        {
            var sources = new List<BiomeEnvironmentLibraryDefinition>();
            if (primary != null)
                sources.Add(primary);

            for (int i = 0; i < discovered.Count; i++)
            {
                BiomeEnvironmentLibraryDefinition candidate = discovered[i];
                if (candidate != null && !sources.Contains(candidate))
                    sources.Add(candidate);
            }

            var byCategory = new Dictionary<EnvironmentCategory, List<GameObject>>();
            for (int i = 0; i < sources.Count; i++)
            {
                BiomeEnvironmentCategoryEntry[] categories = sources[i].categories;
                if (categories == null)
                    continue;

                for (int c = 0; c < categories.Length; c++)
                {
                    BiomeEnvironmentCategoryEntry entry = categories[c];
                    if (entry?.prefabs == null)
                        continue;

                    if (!byCategory.TryGetValue(entry.category, out List<GameObject> bucket))
                    {
                        bucket = new List<GameObject>();
                        byCategory[entry.category] = bucket;
                    }

                    for (int p = 0; p < entry.prefabs.Length; p++)
                    {
                        GameObject prefab = entry.prefabs[p];
                        if (prefab != null && !bucket.Contains(prefab))
                            bucket.Add(prefab);
                    }
                }
            }

            var mergedCategories = new List<BiomeEnvironmentCategoryEntry>(byCategory.Count);
            foreach (KeyValuePair<EnvironmentCategory, List<GameObject>> pair in byCategory)
            {
                if (pair.Value.Count == 0)
                    continue;

                mergedCategories.Add(new BiomeEnvironmentCategoryEntry
                {
                    category = pair.Key,
                    prefabs = pair.Value.ToArray()
                });
            }

            BiomeEnvironmentLibraryDefinition merged = ScriptableObject.CreateInstance<BiomeEnvironmentLibraryDefinition>();
            merged.name = $"{biomeId}_MergedEnvironmentLibrary";
            merged.hideFlags = HideFlags.DontSave;
            merged.biomeId = biomeId;
            merged.displayName = primary != null ? primary.displayName : biomeId;
            merged.categories = mergedCategories.ToArray();
            return merged;
        }

        private static void EnsureSubscribedToInvalidation()
        {
            if (_subscribedToInvalidation)
                return;

            _subscribedToInvalidation = true;
            BiomeEnvironmentLibraryRegistry.Invalidated += OnRegistryInvalidated;
        }

        private static void OnRegistryInvalidated()
        {
            foreach (BiomeEnvironmentLibraryDefinition merged in MergedCache.Values)
                DestroyMergedInstance(merged);

            MergedCache.Clear();
        }

        private static void DestroyMergedInstance(UnityEngine.Object merged)
        {
            if (merged == null)
                return;

    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(merged);
                return;
            }
    #endif
            UnityEngine.Object.Destroy(merged);
        }
    }
}
