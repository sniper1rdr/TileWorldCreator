using UnityEngine;

namespace AglenRealms.WorldCore
{
    public readonly struct PaletteItem
    {
        public readonly GameObject Prefab;
        public readonly string Label;

        public PaletteItem(GameObject prefab)
        {
            Prefab = prefab;
            Label = prefab != null ? prefab.name : string.Empty;
        }

        public bool IsValid => Prefab != null;
    }
}
