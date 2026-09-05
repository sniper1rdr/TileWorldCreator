namespace AglenRealms.WorldCore.Editor
{
    internal static class PaletteSourceFactory
    {
        public static IPaletteSource Create(PaletteSource source) =>
            source switch
            {
                PaletteSource.Biome => new BiomePaletteSource(),
                PaletteSource.Favorites => new FavoritesPaletteSource(),
                PaletteSource.Recent => new RecentPaletteSource(),
                PaletteSource.Search => new SearchPaletteSource(),
                PaletteSource.CustomCollection => new CustomCollectionPaletteSource(),
                _ => new BiomePaletteSource()
            };

        public static IPaletteSource CreateBiomeSource() => new BiomePaletteSource();
    }
}
