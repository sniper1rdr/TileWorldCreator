using System;
using UnityEngine;

namespace AglenRealms.WorldCore
{
    [CreateAssetMenu(menuName = "Aglen Realms/Biome Environment Library", fileName = "BiomeEnvironmentLibrary")]
    public class BiomeEnvironmentLibraryDefinition : ScriptableObject, IContentModule
    {
        public string biomeId;
        public string displayName;
        public BiomeEnvironmentCategoryEntry[] categories;

        public string ModuleId => biomeId;
        public string ModuleDisplayName => displayName;
        public ContentModuleKind ModuleKind => ContentModuleKind.Biome;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(biomeId) &&
            categories != null &&
            categories.Length > 0;

        /// <summary>
        /// Asset-owned array for editor tooling that edits the library in place.
        /// Runtime/UI callers should use <see cref="GetPrefabsCopy"/>.
        /// </summary>
        public GameObject[] GetPrefabs(EnvironmentCategory category)
        {
            if (categories == null)
                return Array.Empty<GameObject>();

            for (int i = 0; i < categories.Length; i++)
            {
                BiomeEnvironmentCategoryEntry entry = categories[i];
                if (entry != null && entry.category == category && entry.prefabs != null)
                    return entry.prefabs;
            }

            return Array.Empty<GameObject>();
        }

        /// <summary>
        /// Detached copy safe to hand to scene/editor consumers.
        /// </summary>
        public GameObject[] GetPrefabsCopy(EnvironmentCategory category)
        {
            GameObject[] source = GetPrefabs(category);
            if (source == null || source.Length == 0)
                return Array.Empty<GameObject>();

            if (ReferenceEquals(source, Array.Empty<GameObject>()))
                return source;

            return (GameObject[])source.Clone();
        }
    }
}
