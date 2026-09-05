using System;
using UnityEngine;

namespace AglenRealms.WorldCore
{
    public static class BiomeTileLibrary
    {
        public static GameObject[] Load(string biomeId, LandscapeBrushMode brushMode = LandscapeBrushMode.Ground)
        {
    #if UNITY_EDITOR
            BiomeDefinition definition = BiomeRegistry.GetById(biomeId);
            if (definition != null)
                return CopyTilesFromDefinition(definition, brushMode);

            if (BiomeRegistry.TryGetLegacyBiome(biomeId, out BrushBiome legacyBiome))
                return Load(legacyBiome, brushMode);

            Debug.LogWarning($"Landscape setup: biome definition not found for id '{biomeId}'");
            return Array.Empty<GameObject>();
    #else
            return Array.Empty<GameObject>();
    #endif
        }

        public static GameObject[] Load(BrushBiome biome, LandscapeBrushMode brushMode = LandscapeBrushMode.Ground)
        {
    #if UNITY_EDITOR
            BrushBiome normalizedBiome = NormalizeBiome(biome);
            BiomeDefinition definition = BiomeRegistry.GetByLegacyBiome(normalizedBiome);
            if (definition != null)
                return CopyTilesFromDefinition(definition, brushMode);

            Debug.LogWarning($"Landscape setup: biome definition not found for legacy biome '{normalizedBiome}'");
            return Array.Empty<GameObject>();
    #else
            return Array.Empty<GameObject>();
    #endif
        }

        public static BrushBiome NormalizeBiome(BrushBiome biome) =>
            biome == BrushBiome.Liquid ? BrushBiome.Grasslands : biome;

        public static string NormalizeBiomeId(string biomeId, BrushBiome legacyFallback)
        {
            if (!string.IsNullOrWhiteSpace(biomeId))
                return biomeId;

            return BiomeRegistry.GetIdFromLegacyBiome(legacyFallback);
        }

    #if UNITY_EDITOR
        /// <summary>
        /// Dev-time check: resolved brush tiles must never alias a BiomeDefinition array.
        /// </summary>
        public static void AssertDetachedFromBiomeAssets(GameObject[] resolvedTiles, string biomeId)
        {
            if (resolvedTiles == null || resolvedTiles.Length == 0)
                return;

            BiomeDefinition definition = BiomeRegistry.GetById(biomeId);
            if (definition == null)
                return;

            Debug.Assert(
                !ReferenceEquals(resolvedTiles, definition.groundTiles),
                "Resolved tiles must not reference BiomeDefinition.groundTiles directly.");
            Debug.Assert(
                !ReferenceEquals(resolvedTiles, definition.liquidTiles),
                "Resolved tiles must not reference BiomeDefinition.liquidTiles directly.");
        }
    #endif

        private static GameObject[] CopyTilesFromDefinition(BiomeDefinition definition, LandscapeBrushMode brushMode)
        {
            GameObject[] source = definition.GetTiles(brushMode);
            if (source == null || source.Length == 0)
            {
    #if UNITY_EDITOR
                Debug.LogWarning(
                    $"Landscape setup: {definition.displayName} ({definition.biomeId}) has no {brushMode} tiles assigned.");
    #endif
                return Array.Empty<GameObject>();
            }

            // Never return the asset-owned array. Undo/serialization of a scene component
            // that aliases groundTiles/liquidTiles mutates the BiomeDefinition in memory.
            return (GameObject[])source.Clone();
        }
    }
}
