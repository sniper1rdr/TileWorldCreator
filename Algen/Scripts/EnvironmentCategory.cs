namespace AglenRealms.WorldCore
{
    public enum EnvironmentCategory
    {
        Rocks = 0,
        Vegetation = 1,
        Props = 2,
        Buildings = 3,
        VFX = 4
    }

    public static class EnvironmentCategoryExtensions
    {
        public static readonly EnvironmentCategory[] All =
        {
            EnvironmentCategory.Rocks,
            EnvironmentCategory.Vegetation,
            EnvironmentCategory.Props,
            EnvironmentCategory.Buildings,
            EnvironmentCategory.VFX
        };

        public static string GetDisplayName(this EnvironmentCategory category) =>
            category switch
            {
                EnvironmentCategory.Rocks => "Rocks",
                EnvironmentCategory.Vegetation => "Vegetation",
                EnvironmentCategory.Props => "Props",
                EnvironmentCategory.Buildings => "Buildings",
                EnvironmentCategory.VFX => "VFX",
                _ => category.ToString()
            };

        public static string GetTabLabel(this EnvironmentCategory category) =>
            category switch
            {
                EnvironmentCategory.Rocks => "\U0001FAA8 Rocks",
                EnvironmentCategory.Vegetation => "\U0001F33F Vegetation",
                EnvironmentCategory.Props => "\U0001FAB5 Props",
                EnvironmentCategory.Buildings => "Buildings",
                EnvironmentCategory.VFX => "VFX",
                _ => category.GetDisplayName()
            };

        public static string GetFolderName(this EnvironmentCategory category) =>
            category.GetDisplayName();
    }
}
