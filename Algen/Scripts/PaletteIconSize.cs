namespace AglenRealms.WorldCore
{
    public enum PaletteIconSize
    {
        Small = 48,
        Medium = 64,
        Large = 80
    }

    public static class PaletteIconSizeExtensions
    {
        public static readonly PaletteIconSize[] All =
        {
            PaletteIconSize.Small,
            PaletteIconSize.Medium,
            PaletteIconSize.Large
        };

        public static int ToPixels(this PaletteIconSize size) => (int)size;

        public static string GetLabel(this PaletteIconSize size) =>
            size switch
            {
                PaletteIconSize.Small => "Small",
                PaletteIconSize.Medium => "Medium",
                PaletteIconSize.Large => "Large",
                _ => size.ToString()
            };
    }
}
