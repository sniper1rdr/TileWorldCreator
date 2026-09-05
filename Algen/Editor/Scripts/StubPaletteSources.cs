using System.Collections.Generic;

namespace AglenRealms.WorldCore.Editor
{
    internal sealed class FavoritesPaletteSource : IPaletteSource
    {
        public PaletteSource SourceKind => PaletteSource.Favorites;
        public string DisplayName => PaletteSource.Favorites.GetLabel();
        public bool IsAvailable => false;
        public string EmptyMessage => "Favorites will be available in a future update.";
        public IReadOnlyList<PaletteItem> GetItems() => System.Array.Empty<PaletteItem>();
    }

    internal sealed class RecentPaletteSource : IPaletteSource
    {
        public PaletteSource SourceKind => PaletteSource.Recent;
        public string DisplayName => PaletteSource.Recent.GetLabel();
        public bool IsAvailable => false;
        public string EmptyMessage => "Recent objects will be available in a future update.";
        public IReadOnlyList<PaletteItem> GetItems() => System.Array.Empty<PaletteItem>();
    }

    internal sealed class SearchPaletteSource : IPaletteSource
    {
        public PaletteSource SourceKind => PaletteSource.Search;
        public string DisplayName => PaletteSource.Search.GetLabel();
        public bool IsAvailable => false;
        public string EmptyMessage => "Search will be available in a future update.";
        public IReadOnlyList<PaletteItem> GetItems() => System.Array.Empty<PaletteItem>();
    }

    internal sealed class CustomCollectionPaletteSource : IPaletteSource
    {
        public PaletteSource SourceKind => PaletteSource.CustomCollection;
        public string DisplayName => PaletteSource.CustomCollection.GetLabel();
        public bool IsAvailable => false;
        public string EmptyMessage => "Custom collections will be available in a future update.";
        public IReadOnlyList<PaletteItem> GetItems() => System.Array.Empty<PaletteItem>();
    }
}
