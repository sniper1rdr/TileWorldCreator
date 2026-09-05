using UnityEngine;

namespace AglenRealms.WorldCore
{
    [CreateAssetMenu(menuName = "Aglen Realms/Prop Catalog", fileName = "PropCatalog")]
    public class PropCatalogDefinition : ScriptableObject, IContentModule
    {
        public string catalogId;
        public string displayName;

        [Tooltip("Optional filter tags, e.g. castle, medieval, forest")]
        public string[] tags;

        public GameObject[] props;
        public GameObject[] environmentProps;

        public string ModuleId => catalogId;
        public string ModuleDisplayName => displayName;
        public ContentModuleKind ModuleKind => ContentModuleKind.PropCatalog;
    }
}
