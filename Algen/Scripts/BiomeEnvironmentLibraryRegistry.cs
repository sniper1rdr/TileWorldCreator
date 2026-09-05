using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AglenRealms.WorldCore
{
    public static class BiomeEnvironmentLibraryRegistry
    {
        private static readonly Dictionary<string, BiomeEnvironmentLibraryDefinition> ByBiomeId = new();
        private static readonly Dictionary<string, List<BiomeEnvironmentLibraryDefinition>> AllByBiomeId = new();
        private static readonly List<BiomeEnvironmentLibraryDefinition> EmptyLibraryList = new();
        private static BiomeEnvironmentLibraryDefinition[] _all = System.Array.Empty<BiomeEnvironmentLibraryDefinition>();
        private static bool _initialized;

        public static IReadOnlyList<BiomeEnvironmentLibraryDefinition> All
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
            ByBiomeId.Clear();
            AllByBiomeId.Clear();
            _all = System.Array.Empty<BiomeEnvironmentLibraryDefinition>();
            Invalidated?.Invoke();
        }

        /// <summary>
        /// First-registered library for a biomeId (alphabetical by displayName — see <see cref="EnsureInitialized"/>).
        /// Kept as the "primary" reference; use <see cref="GetAllByBiomeId"/> to also see add-on libraries sharing
        /// the same biomeId.
        /// </summary>
        public static BiomeEnvironmentLibraryDefinition GetByBiomeId(string biomeId)
        {
            if (string.IsNullOrWhiteSpace(biomeId))
                return null;

            EnsureInitialized();
            ByBiomeId.TryGetValue(biomeId, out BiomeEnvironmentLibraryDefinition library);
            return library;
        }

        /// <summary>
        /// Every <see cref="BiomeEnvironmentLibraryDefinition"/> registered under <paramref name="biomeId"/> —
        /// the official biome library plus any separately shipped add-on pack (Buildings, VFX, ...) that targets
        /// the same biomeId. <see cref="BiomeEnvironmentLibrary.Resolve"/> merges these so an add-on pack is picked
        /// up on import without requiring a World Core update.
        /// </summary>
        public static IReadOnlyList<BiomeEnvironmentLibraryDefinition> GetAllByBiomeId(string biomeId)
        {
            if (string.IsNullOrWhiteSpace(biomeId))
                return EmptyLibraryList;

            EnsureInitialized();
            return AllByBiomeId.TryGetValue(biomeId, out List<BiomeEnvironmentLibraryDefinition> libraries)
                ? libraries
                : EmptyLibraryList;
        }

        public static BiomeEnvironmentLibraryDefinition ResolveForBiome(BiomeDefinition biome)
        {
            if (biome == null)
                return null;

            if (biome.environmentLibrary != null && biome.environmentLibrary.IsValid)
                return biome.environmentLibrary;

            return GetByBiomeId(biome.biomeId);
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            ByBiomeId.Clear();
            AllByBiomeId.Clear();

    #if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:BiomeEnvironmentLibraryDefinition");
            var discovered = new List<BiomeEnvironmentLibraryDefinition>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetDatabase.LoadAssetAtPath<BiomeEnvironmentLibraryDefinition>(path) is BiomeEnvironmentLibraryDefinition library &&
                    library.IsValid)
                {
                    discovered.Add(library);
                }
            }

            discovered.Sort((a, b) => string.CompareOrdinal(a.displayName, b.displayName));
            _all = discovered.ToArray();
    #else
            _all = Resources.LoadAll<BiomeEnvironmentLibraryDefinition>("EnvironmentLibraries");
    #endif

            for (int i = 0; i < _all.Length; i++)
            {
                BiomeEnvironmentLibraryDefinition library = _all[i];
                if (library == null || string.IsNullOrWhiteSpace(library.biomeId))
                    continue;

                if (!ByBiomeId.ContainsKey(library.biomeId))
                    ByBiomeId[library.biomeId] = library;

                if (!AllByBiomeId.TryGetValue(library.biomeId, out List<BiomeEnvironmentLibraryDefinition> libraries))
                {
                    libraries = new List<BiomeEnvironmentLibraryDefinition>();
                    AllByBiomeId[library.biomeId] = libraries;
                }

                libraries.Add(library);
            }
        }
    }

    #if UNITY_EDITOR
    [InitializeOnLoad]
    static class BiomeEnvironmentLibraryRegistryAssetWatcher
    {
        static bool _invalidateScheduled;

        static BiomeEnvironmentLibraryRegistryAssetWatcher()
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
            EditorApplication.delayCall += () =>
            {
                _invalidateScheduled = false;
                BiomeEnvironmentLibraryRegistry.Invalidate();
            };
        }
    }
    #endif
}
