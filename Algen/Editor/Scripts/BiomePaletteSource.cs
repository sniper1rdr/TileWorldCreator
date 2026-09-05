using System.Collections.Generic;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal sealed class BiomePaletteSource : IPaletteSource
    {
        public PaletteSource SourceKind => PaletteSource.Biome;
        public string DisplayName => PaletteSource.Biome.GetLabel();
        public bool IsAvailable => true;

        public string EmptyMessage
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(EnvironmentPainterState.SearchQuery))
                    return "No objects match the current search.";

                EnvironmentCategory category = EnvironmentPainterState.ActiveCategory;
                return $"No objects in {category.GetDisplayName()} for this biome.";
            }
        }

        public IReadOnlyList<PaletteItem> GetItems()
        {
            string biomeId = EnvironmentPainterState.ActiveBiomeId;
            EnvironmentCategory category = EnvironmentPainterState.ActiveCategory;
            GameObject[] prefabs = BiomeEnvironmentLibraryEditor.GetPrefabs(biomeId, category);
            string searchQuery = EnvironmentPainterState.SearchQuery?.Trim() ?? string.Empty;

            var items = new List<PaletteItem>(prefabs.Length);
            for (int i = 0; i < prefabs.Length; i++)
            {
                GameObject prefab = prefabs[i];
                if (prefab == null)
                    continue;

                PaletteItem item = new PaletteItem(prefab);
                if (searchQuery.Length > 0 &&
                    item.Label.IndexOf(searchQuery, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                items.Add(item);
            }

            return items;
        }
    }
}
