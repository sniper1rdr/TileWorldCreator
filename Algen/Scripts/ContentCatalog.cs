using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AglenRealms.WorldCore
{
    public static class ContentCatalog
    {
        private static PropCatalogDefinition[] _propCatalogs = System.Array.Empty<PropCatalogDefinition>();
        private static bool _initialized;

        public static IReadOnlyList<PropCatalogDefinition> PropCatalogs
        {
            get
            {
                EnsureInitialized();
                return _propCatalogs;
            }
        }

        public static void Invalidate()
        {
            _initialized = false;
            _propCatalogs = System.Array.Empty<PropCatalogDefinition>();
        }

        public static PropCatalogDefinition GetPropCatalog(string catalogId)
        {
            if (string.IsNullOrWhiteSpace(catalogId))
                return null;

            EnsureInitialized();
            for (int i = 0; i < _propCatalogs.Length; i++)
            {
                if (_propCatalogs[i].catalogId == catalogId)
                    return _propCatalogs[i];
            }

            return null;
        }

        public static IEnumerable<GameObject> GetAllProps(string tagFilter = null)
        {
            EnsureInitialized();
            for (int i = 0; i < _propCatalogs.Length; i++)
            {
                PropCatalogDefinition catalog = _propCatalogs[i];
                if (catalog == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(tagFilter) && !HasTag(catalog.tags, tagFilter))
                    continue;

                if (catalog.props != null)
                {
                    for (int j = 0; j < catalog.props.Length; j++)
                    {
                        if (catalog.props[j] != null)
                            yield return catalog.props[j];
                    }
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;

    #if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:PropCatalogDefinition");
            var discovered = new List<PropCatalogDefinition>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetDatabase.LoadAssetAtPath<PropCatalogDefinition>(path) is PropCatalogDefinition catalog)
                    discovered.Add(catalog);
            }

            _propCatalogs = discovered.ToArray();
    #else
            _propCatalogs = Resources.LoadAll<PropCatalogDefinition>("PropCatalogs");
    #endif
        }

        private static bool HasTag(string[] tags, string tagFilter)
        {
            if (tags == null)
                return false;

            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i] == tagFilter)
                    return true;
            }

            return false;
        }
    }

    #if UNITY_EDITOR
    [InitializeOnLoad]
    static class ContentCatalogAssetWatcher
    {
        static bool _invalidateScheduled;

        static ContentCatalogAssetWatcher()
        {
            EditorApplication.projectChanged += ScheduleInvalidate;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            ScheduleInvalidate();
        }

        static void ScheduleInvalidate()
        {
            if (_invalidateScheduled)
                return;

            _invalidateScheduled = true;
            EditorApplication.delayCall += FlushInvalidate;
        }

        static void FlushInvalidate()
        {
            _invalidateScheduled = false;
            ContentCatalog.Invalidate();
        }
    }
    #endif
}
