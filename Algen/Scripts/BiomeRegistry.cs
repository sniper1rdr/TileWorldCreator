using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AglenRealms.WorldCore
{
    public static class BiomeRegistry
    {
        private static readonly Dictionary<string, BiomeDefinition> ById = new();
        private static readonly Dictionary<BrushBiome, BiomeDefinition> ByLegacyBiome = new();
        private static BiomeDefinition[] _all = System.Array.Empty<BiomeDefinition>();
        private static bool _initialized;

        public static IReadOnlyList<BiomeDefinition> All
        {
            get
            {
                EnsureInitialized();
                return _all;
            }
        }

        public static event Action Invalidated;

        public static void Invalidate()
        {
            _initialized = false;
            ById.Clear();
            ByLegacyBiome.Clear();
            _all = System.Array.Empty<BiomeDefinition>();
            Invalidated?.Invoke();
        }

        public static BiomeDefinition GetById(string biomeId)
        {
            if (string.IsNullOrWhiteSpace(biomeId))
                return null;

            EnsureInitialized();
            ById.TryGetValue(biomeId, out BiomeDefinition definition);
            return definition;
        }

        public static BiomeDefinition GetByLegacyBiome(BrushBiome biome)
        {
            BrushBiome normalized = BiomeTileLibrary.NormalizeBiome(biome);
            EnsureInitialized();
            ByLegacyBiome.TryGetValue(normalized, out BiomeDefinition definition);
            return definition;
        }

        public static string GetIdFromLegacyBiome(BrushBiome biome) =>
            TryGetLegacyId(biome, out string id) ? id : BiomeIds.Grasslands;

        public static bool TryGetLegacyBiome(string biomeId, out BrushBiome biome)
        {
            biome = BrushBiome.Grasslands;
            if (string.IsNullOrWhiteSpace(biomeId))
                return false;

            foreach (KeyValuePair<BrushBiome, string> pair in LegacyIdMap)
            {
                if (pair.Value == biomeId)
                {
                    biome = pair.Key;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetLegacyId(BrushBiome biome, out string biomeId)
        {
            biome = BiomeTileLibrary.NormalizeBiome(biome);
            return LegacyIdMap.TryGetValue(biome, out biomeId);
        }

        public static string ResolveBiomeId(string biomeId, BrushBiome legacyBiome)
        {
            if (!string.IsNullOrWhiteSpace(biomeId))
                return biomeId;

            return GetIdFromLegacyBiome(legacyBiome);
        }

        public static bool TryInferBiomeFromPrefab(GameObject prefab, out string biomeId)
        {
            biomeId = null;
            if (prefab == null)
                return false;

            EnsureInitialized();
            for (int i = 0; i < _all.Length; i++)
            {
                BiomeDefinition definition = _all[i];
                if (definition == null || !definition.IsValid)
                    continue;

                if (ContainsPrefab(definition.groundTiles, prefab) ||
                    ContainsPrefab(definition.liquidTiles, prefab))
                {
                    biomeId = definition.biomeId;
                    return true;
                }
            }

            return false;
        }

        private static readonly Dictionary<BrushBiome, string> LegacyIdMap = new()
        {
            { BrushBiome.Grasslands, BiomeIds.Grasslands },
            { BrushBiome.FrozenTundra, BiomeIds.FrozenTundra },
            { BrushBiome.GoldenDesert, BiomeIds.GoldenDesert },
            { BrushBiome.VolcanicAshlands, BiomeIds.VolcanicAshlands },
            { BrushBiome.RedForest, BiomeIds.RedForest },
            { BrushBiome.MistySwamp, BiomeIds.MistySwamp }
        };

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            ById.Clear();
            ByLegacyBiome.Clear();

    #if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:BiomeDefinition");
            var discovered = new List<BiomeDefinition>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetDatabase.LoadAssetAtPath<BiomeDefinition>(path) is BiomeDefinition definition &&
                    definition.IsValid)
                {
                    discovered.Add(definition);
                }
            }

            discovered.Sort((a, b) => string.CompareOrdinal(a.displayName, b.displayName));
            _all = discovered.ToArray();
    #else
            _all = Resources.LoadAll<BiomeDefinition>("Biomes");
    #endif

            for (int i = 0; i < _all.Length; i++)
            {
                BiomeDefinition definition = _all[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.biomeId))
                    continue;

                if (!ById.ContainsKey(definition.biomeId))
                    ById[definition.biomeId] = definition;

                if (TryGetLegacyBiome(definition.biomeId, out BrushBiome legacyBiome) &&
                    !ByLegacyBiome.ContainsKey(legacyBiome))
                {
                    ByLegacyBiome[legacyBiome] = definition;
                }
            }
        }

        private static bool ContainsPrefab(GameObject[] tiles, GameObject prefab)
        {
            if (tiles == null || prefab == null)
                return false;

            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == prefab)
                    return true;
            }

            return false;
        }
    }

    #if UNITY_EDITOR
    [InitializeOnLoad]
    static class BiomeRegistryAssetWatcher
    {
        static bool _invalidateScheduled;

        static BiomeRegistryAssetWatcher()
        {
            EditorApplication.projectChanged += ScheduleInvalidate;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            ScheduleInvalidate();
        }

        static void ScheduleInvalidate()
        {
            if (_invalidateScheduled)
                return;

            _invalidateScheduled = true;
            EditorApplication.delayCall += FlushInvalidate;
        }

        static void FlushInvalidate()
        {
            _invalidateScheduled = false;
            BiomeRegistry.Invalidate();
        }
    }
    #endif
}
