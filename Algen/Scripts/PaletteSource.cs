namespace AglenRealms.WorldCore
{
    public enum PaletteSource
    {
        Biome = 0,
        Favorites = 1,
        Recent = 2,
        Search = 3,
        CustomCollection = 4
    }

    public static class PaletteSourceExtensions
    {
        public static string GetLabel(this PaletteSource source) =>
            source switch
            {
                PaletteSource.Biome => "Biome",
                PaletteSource.Favorites => "Favorites",
                PaletteSource.Recent => "Recent",
                PaletteSource.Search => "Search",
                PaletteSource.CustomCollection => "Custom",
                _ => source.ToString()
            };
    }
}
