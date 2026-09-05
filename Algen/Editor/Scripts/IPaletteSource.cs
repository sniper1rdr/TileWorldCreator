using System.Collections.Generic;

namespace AglenRealms.WorldCore.Editor
{
    internal interface IPaletteSource
    {
        PaletteSource SourceKind { get; }
        string DisplayName { get; }
        bool IsAvailable { get; }
        string EmptyMessage { get; }
        IReadOnlyList<PaletteItem> GetItems();
    }
}
