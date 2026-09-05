using UnityEngine;

namespace AglenRealms.WorldCore
{
    [CreateAssetMenu(menuName = "Aglen Realms/Biome Definition", fileName = "BiomeDefinition")]
    public class BiomeDefinition : ScriptableObject, IContentModule
    {
        [Header("Identity")]
        public string biomeId;
        public string displayName;
        public Sprite icon;

        [Header("Paint Tiles")]
        public GameObject[] groundTiles;
        public GameObject[] liquidTiles;

        [Header("Optional Content")]
        public BiomeEnvironmentLibraryDefinition environmentLibrary;
        public GameObject[] environmentProps;
        public GameObject[] props;
        public Material groundMaterial;
        public Material liquidMaterial;

        public string ModuleId => biomeId;
        public string ModuleDisplayName => displayName;
        public ContentModuleKind ModuleKind => ContentModuleKind.Biome;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(biomeId) &&
            groundTiles != null && groundTiles.Length > 0;

        public GameObject[] GetTiles(LandscapeBrushMode brushMode) =>
            brushMode == LandscapeBrushMode.Liquid ? liquidTiles : groundTiles;
    }
}
