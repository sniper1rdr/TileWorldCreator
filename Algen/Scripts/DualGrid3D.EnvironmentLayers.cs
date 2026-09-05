using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AglenRealms.WorldCore
{
    public partial class DualGrid3D
    {
        private Transform environmentLayersRoot;
        private readonly Dictionary<int, Transform> environmentLayerRoots = new();

        [HideInInspector] public List<EnvironmentLayerDefinition> environmentLayers = new();
        [HideInInspector] public int activeEnvironmentLayerIndex;

        private EnvironmentRoot ResolveEnvironmentRoot()
        {
            Transform environmentChild = transform.Find(WorldRoot.EnvironmentObjectName);
            if (environmentChild != null && environmentChild.TryGetComponent(out EnvironmentRoot root))
                return root;

            return null;
        }

        private bool UsesExternalEnvironmentRoot => ResolveEnvironmentRoot() != null;

        public int ActiveEnvironmentLayerIndex
        {
            get
            {
                EnvironmentRoot root = ResolveEnvironmentRoot();
                if (root != null)
                    return root.ActiveLayerIndex;

                return Mathf.Clamp(activeEnvironmentLayerIndex, 0, Mathf.Max(0, GetEnvironmentLayerCount() - 1));
            }
        }

        public int GetEnvironmentLayerCount()
        {
            EnvironmentRoot root = ResolveEnvironmentRoot();
            if (root != null)
                return root.GetLayerCount();

            return environmentLayers?.Count ?? 0;
        }

        public void EnsureDefaultEnvironmentLayer()
        {
            EnvironmentRoot root = ResolveEnvironmentRoot();
            if (root != null)
            {
                root.EnsureDefaultLayer();
                return;
            }

            EnsureDefaultEnvironmentLayerLegacy();
        }

        public void SetActiveEnvironmentLayer(int layerIndex)
        {
            EnvironmentRoot root = ResolveEnvironmentRoot();
            if (root != null)
            {
                root.SetActiveLayer(layerIndex);
                return;
            }

            EnsureDefaultEnvironmentLayerLegacy();
            activeEnvironmentLayerIndex = Mathf.Clamp(layerIndex, 0, environmentLayers.Count - 1);
            PersistEnvironmentLayers();
        }

        public void AddEnvironmentLayer(string layerName = null)
        {
            EnvironmentRoot root = ResolveEnvironmentRoot();
            if (root != null)
            {
                root.AddLayer(layerName);
                return;
            }

            AddEnvironmentLayerLegacy(layerName);
        }

        public void RemoveEnvironmentLayerAt(int index)
        {
            EnvironmentRoot root = ResolveEnvironmentRoot();
            if (root != null)
            {
                root.RemoveLayerAt(index);
                return;
            }

            RemoveEnvironmentLayerAtLegacy(index);
        }

        public float GetEnvironmentLayerWorldPlaneY(int layerIndex)
        {
            EnvironmentRoot root = ResolveEnvironmentRoot();
            if (root != null)
                return root.GetLayerWorldPlaneY(layerIndex);

            EnsureDefaultEnvironmentLayerLegacy();
            layerIndex = Mathf.Clamp(layerIndex, 0, environmentLayers.Count - 1);
            float localHeight = environmentLayers[layerIndex].height;
            return transform.TransformPoint(new Vector3(0f, localHeight, 0f)).y;
        }

        public Transform GetOrCreateEnvironmentLayerRoot(int layerIndex)
        {
            EnvironmentRoot root = ResolveEnvironmentRoot();
            if (root != null)
                return root.GetOrCreateLayerRoot(layerIndex);

            return GetOrCreateEnvironmentLayerRootLegacy(layerIndex);
        }

        public void RenameEnvironmentLayerRoot(int layerIndex)
        {
            EnvironmentRoot root = ResolveEnvironmentRoot();
            if (root != null)
            {
                root.RenameLayerRoot(layerIndex);
                return;
            }

            RenameEnvironmentLayerRootLegacy(layerIndex);
        }

        public void RebuildEnvironmentLayerRoots()
        {
            EnvironmentRoot root = ResolveEnvironmentRoot();
            if (root != null)
            {
                root.RebuildLayerRoots();
                return;
            }

            RebuildEnvironmentLayerRootsLegacy();
        }

        public void ApplyEnvironmentLayerVisibility(int layerIndex)
        {
            EnvironmentRoot root = ResolveEnvironmentRoot();
            if (root != null)
            {
                root.ApplyLayerVisibility(layerIndex);
                return;
            }

            ApplyEnvironmentLayerVisibilityLegacy(layerIndex);
        }

        public void MigrateLegacyEnvironmentHierarchy()
        {
            if (UsesExternalEnvironmentRoot)
                return;

            MigrateLegacyEnvironmentHierarchyLegacy();
        }

        private void EnsureDefaultEnvironmentLayerLegacy()
        {
            environmentLayers ??= new List<EnvironmentLayerDefinition>();
            if (environmentLayers.Count > 0)
                return;

            environmentLayers.Add(new EnvironmentLayerDefinition
            {
                name = "Layer_01",
                visible = true,
                height = 0f
            });
            activeEnvironmentLayerIndex = 0;
            PersistEnvironmentLayers();
        }

        private void AddEnvironmentLayerLegacy(string layerName)
        {
            EnsureDefaultEnvironmentLayerLegacy();

            int index = environmentLayers.Count;
            environmentLayers.Add(new EnvironmentLayerDefinition
            {
                name = string.IsNullOrWhiteSpace(layerName) ? $"Layer_{index + 1:D2}" : layerName.Trim(),
                visible = true,
                height = 0f
            });

            activeEnvironmentLayerIndex = index;
            GetOrCreateEnvironmentLayerRootLegacy(index);
            PersistEnvironmentLayers();
        }

        private void RemoveEnvironmentLayerAtLegacy(int index)
        {
            if (environmentLayers == null || environmentLayers.Count <= 1)
                return;

            if (index < 0 || index >= environmentLayers.Count)
                return;

            if (environmentLayerRoots.TryGetValue(index, out Transform layerRoot) && layerRoot != null)
            {
    #if UNITY_EDITOR
                Undo.DestroyObjectImmediate(layerRoot.gameObject);
    #else
                Destroy(layerRoot.gameObject);
    #endif
            }

            environmentLayers.RemoveAt(index);
            environmentLayerRoots.Remove(index);

            if (activeEnvironmentLayerIndex >= environmentLayers.Count)
                activeEnvironmentLayerIndex = environmentLayers.Count - 1;
            else if (activeEnvironmentLayerIndex > index)
                activeEnvironmentLayerIndex--;

            RebuildEnvironmentLayerRootsLegacy();
            PersistEnvironmentLayers();
        }

        private Transform GetOrCreateEnvironmentLayerRootLegacy(int layerIndex)
        {
            EnsureDefaultEnvironmentLayerLegacy();
            layerIndex = Mathf.Clamp(layerIndex, 0, environmentLayers.Count - 1);

            if (environmentLayerRoots.TryGetValue(layerIndex, out Transform cached) && cached != null)
            {
                SyncEnvironmentLayerRootLegacy(layerIndex, cached);
                return cached;
            }

            EnvironmentLayerDefinition definition = environmentLayers[layerIndex];
            Transform layersRoot = GetOrCreateEnvironmentLayersRootLegacy();
            string rootName = SanitizeEnvironmentLayerRootName(definition.name);
            Transform existing = layersRoot.Find(rootName);
            if (existing == null)
            {
                foreach (Transform child in layersRoot)
                {
                    if (child.name == rootName)
                    {
                        existing = child;
                        break;
                    }
                }
            }

            if (existing == null)
            {
                GameObject layerObject = new GameObject(rootName);
                layerObject.transform.SetParent(layersRoot, false);
                existing = layerObject.transform;
            }
            else if (existing.name != rootName)
            {
                existing.name = rootName;
            }

            environmentLayerRoots[layerIndex] = existing;
            SyncEnvironmentLayerRootLegacy(layerIndex, existing);
            return existing;
        }

        private void RenameEnvironmentLayerRootLegacy(int layerIndex)
        {
            if (environmentLayers == null || layerIndex < 0 || layerIndex >= environmentLayers.Count)
                return;

            if (!environmentLayerRoots.TryGetValue(layerIndex, out Transform layerRoot) || layerRoot == null)
                return;

            string rootName = SanitizeEnvironmentLayerRootName(environmentLayers[layerIndex].name);
            if (layerRoot.name != rootName)
                layerRoot.name = rootName;
        }

        private void RebuildEnvironmentLayerRootsLegacy()
        {
            environmentLayerRoots.Clear();
            EnsureDefaultEnvironmentLayerLegacy();
            MigrateLegacyEnvironmentHierarchyLegacy();

            Transform layersRoot = GetOrCreateEnvironmentLayersRootLegacy();
            for (int i = 0; i < environmentLayers.Count; i++)
                GetOrCreateEnvironmentLayerRootLegacy(i);

            List<Transform> staleLayers = new();
            HashSet<Transform> validLayers = new(environmentLayerRoots.Values);
            foreach (Transform child in layersRoot)
            {
                if (!validLayers.Contains(child))
                    staleLayers.Add(child);
            }

            for (int i = 0; i < staleLayers.Count; i++)
                DestroyObjectImmediateOrRuntime(staleLayers[i].gameObject);
        }

        private void ApplyEnvironmentLayerVisibilityLegacy(int layerIndex)
        {
            if (environmentLayers == null || layerIndex < 0 || layerIndex >= environmentLayers.Count)
                return;

            if (!environmentLayerRoots.TryGetValue(layerIndex, out Transform layerRoot) || layerRoot == null)
                return;

            layerRoot.gameObject.SetActive(environmentLayers[layerIndex].visible);
        }

        private Transform GetOrCreateEnvironmentLayersRootLegacy()
        {
            if (environmentLayersRoot != null)
                return environmentLayersRoot;

            Transform existing = transform.Find(WorldRoot.EnvironmentObjectName);
            if (existing != null)
            {
                environmentLayersRoot = existing;
                return existing;
            }

            GameObject rootObject = new GameObject(WorldRoot.EnvironmentObjectName);
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            environmentLayersRoot = rootObject.transform;
            return environmentLayersRoot;
        }

        private void SyncEnvironmentLayerRootLegacy(int layerIndex, Transform layerRoot)
        {
            EnvironmentLayerDefinition definition = environmentLayers[layerIndex];
            Vector3 expectedLocalPosition = new Vector3(0f, definition.height, 0f);
            if (layerRoot.localPosition != expectedLocalPosition)
                layerRoot.localPosition = expectedLocalPosition;

            ApplyEnvironmentLayerVisibilityLegacy(layerIndex);
        }

        private static string SanitizeEnvironmentLayerRootName(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return "Layer";

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            string sanitized = layerName.Trim();
            for (int i = 0; i < invalid.Length; i++)
                sanitized = sanitized.Replace(invalid[i], '_');

            return sanitized.Replace(' ', '_');
        }

        private void PersistEnvironmentLayers()
        {
    #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
    #endif
        }

        private void MigrateLegacyEnvironmentHierarchyLegacy()
        {
            EnsureDefaultEnvironmentLayerLegacy();
            Transform targetLayerRoot = GetOrCreateEnvironmentLayerRootLegacy(ActiveEnvironmentLayerIndex);

            List<Transform> legacyEnvironmentRoots = new();
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Level_"))
                    continue;

                Transform legacyEnvironment = child.Find(WorldRoot.EnvironmentObjectName);
                if (legacyEnvironment != null)
                    legacyEnvironmentRoots.Add(legacyEnvironment);
            }

            for (int i = 0; i < legacyEnvironmentRoots.Count; i++)
                MoveLegacyEnvironmentChildren(legacyEnvironmentRoots[i], targetLayerRoot);
        }

        private void MoveLegacyEnvironmentChildren(Transform legacyEnvironmentRoot, Transform targetLayerRoot)
        {
            if (legacyEnvironmentRoot == null || targetLayerRoot == null)
                return;

            var children = new List<Transform>();
            foreach (Transform child in legacyEnvironmentRoot)
                children.Add(child);

            for (int i = 0; i < children.Count; i++)
            {
                Transform child = children[i];
                if (child == null)
                    continue;

                child.SetParent(targetLayerRoot, true);
    #if UNITY_EDITOR
                EditorUtility.SetDirty(child.gameObject);
    #endif
            }

            if (legacyEnvironmentRoot.childCount == 0)
                DestroyObjectImmediateOrRuntime(legacyEnvironmentRoot.gameObject);
        }
    }
}
