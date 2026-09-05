namespace AglenRealms.WorldCore.Editor
{
    internal enum WorldCoreEditorMode
    {
        Landscape = 0,
        Environment = 1
    }

    internal static class WorldCoreEditorModeExtensions
    {
        public static readonly WorldCoreEditorMode[] All =
        {
            WorldCoreEditorMode.Landscape,
            WorldCoreEditorMode.Environment
        };

        public static string GetLabel(this WorldCoreEditorMode mode) =>
            mode switch
            {
                WorldCoreEditorMode.Landscape => "Landscape",
                WorldCoreEditorMode.Environment => "Environment",
                _ => mode.ToString()
            };
    }
}
