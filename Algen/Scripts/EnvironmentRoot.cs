using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AglenRealms.WorldCore
{
    [AddComponentMenu("Aglen Realms/World Core/Environment Root")]
    [ExecuteAlways]
    public class EnvironmentRoot : MonoBehaviour
    {
        [SerializeField] private List<EnvironmentLayerDefinition> layers = new();
        [SerializeField] private int activeLayerIndex;
        [SerializeField] private EnvironmentBrushSettings brushSettings = new();
        [SerializeField] private string environmentBiomeId = BiomeIds.Grasslands;
        [SerializeField] private DualGrid3D linkedLandscape;

        private readonly Dictionary<int, Transform> layerRoots = new();

        public List<EnvironmentLayerDefinition> Layers => layers;
        public EnvironmentBrushSettings BrushSettings => brushSettings;
        public string EnvironmentBiomeId
        {
            get => environmentBiomeId;
            set => environmentBiomeId = value;
        }

        public DualGrid3D LinkedLandscape
        {
            get => linkedLandscape;
            set => linkedLandscape = value;
        }

        public int ActiveLayerIndex =>
            Mathf.Clamp(activeLayerIndex, 0, Mathf.Max(0, GetLayerCount() - 1));

        public int GetLayerCount() => layers?.Count ?? 0;

        private void OnEnable()
        {
            EnsureDefaultLayer();
            RebuildLayerRoots();
        }

        public void EnsureDefaultLayer()
        {
            layers ??= new List<EnvironmentLayerDefinition>();
            if (layers.Count > 0)
                return;

            layers.Add(new EnvironmentLayerDefinition
            {
                name = "Layer_01",
                visible = true,
                height = 0f
            });
            activeLayerIndex = 0;
            Persist();
        }

        public void SetActiveLayer(int layerIndex)
        {
            EnsureDefaultLayer();
            activeLayerIndex = Mathf.Clamp(layerIndex, 0, layers.Count - 1);
            Persist();
        }

        public void AddLayer(string layerName = null)
        {
            EnsureDefaultLayer();

            int index = layers.Count;
            layers.Add(new EnvironmentLayerDefinition
            {
                name = string.IsNullOrWhiteSpace(layerName) ? $"Layer_{index + 1:D2}" : layerName.Trim(),
                visible = true,
                height = 0f
            });

            activeLayerIndex = index;
            GetOrCreateLayerRoot(index);
            Persist();
        }

        public void RemoveLayerAt(int index)
        {
            if (layers == null || layers.Count <= 1)
                return;

            if (index < 0 || index >= layers.Count)
                return;

            if (layerRoots.TryGetValue(index, out Transform layerRoot) && layerRoot != null)
            {
    #if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.DestroyObjectImmediate(layerRoot.gameObject);
                else
    #endif
                    DestroyUnityObject(layerRoot.gameObject);
            }

            layers.RemoveAt(index);
            layerRoots.Remove(index);

            if (activeLayerIndex >= layers.Count)
                activeLayerIndex = layers.Count - 1;
            else if (activeLayerIndex > index)
                activeLayerIndex--;

            RebuildLayerRoots();
            Persist();
        }

        public float GetLayerWorldPlaneY(int layerIndex)
        {
            EnsureDefaultLayer();
            layerIndex = Mathf.Clamp(layerIndex, 0, layers.Count - 1);
            float localHeight = layers[layerIndex].height;
            return transform.TransformPoint(new Vector3(0f, localHeight, 0f)).y;
        }

        public float GetActiveLayerWorldPlaneY() => GetLayerWorldPlaneY(ActiveLayerIndex);

        public Transform GetActiveLayerRoot() => GetOrCreateLayerRoot(ActiveLayerIndex);

        public Transform GetOrCreateLayerRoot(int layerIndex)
        {
            EnsureDefaultLayer();
            layerIndex = Mathf.Clamp(layerIndex, 0, layers.Count - 1);

            if (layerRoots.TryGetValue(layerIndex, out Transform cached) && cached != null)
            {
                SyncLayerRoot(layerIndex, cached);
                return cached;
            }

            EnvironmentLayerDefinition definition = layers[layerIndex];
            string rootName = SanitizeLayerRootName(definition.name);
            Transform existing = null;
            foreach (Transform child in transform)
            {
                if (child.name == rootName)
                {
                    existing = child;
                    break;
                }
            }

            if (existing == null)
            {
                GameObject layerObject = new GameObject(rootName);
                layerObject.transform.SetParent(transform, false);
                existing = layerObject.transform;
            }
            else if (existing.name != rootName)
            {
                existing.name = rootName;
            }

            layerRoots[layerIndex] = existing;
            SyncLayerRoot(layerIndex, existing);
            return existing;
        }

        public void RenameLayerRoot(int layerIndex)
        {
            if (layers == null || layerIndex < 0 || layerIndex >= layers.Count)
                return;

            if (!layerRoots.TryGetValue(layerIndex, out Transform layerRoot) || layerRoot == null)
                return;

            string rootName = SanitizeLayerRootName(layers[layerIndex].name);
            if (layerRoot.name != rootName)
                layerRoot.name = rootName;
        }

        public void RebuildLayerRoots()
        {
            layerRoots.Clear();
            EnsureDefaultLayer();

            for (int i = 0; i < layers.Count; i++)
                GetOrCreateLayerRoot(i);

            List<Transform> staleLayers = new();
            HashSet<Transform> validLayers = new(layerRoots.Values);
            foreach (Transform child in transform)
            {
                if (!validLayers.Contains(child))
                    staleLayers.Add(child);
            }

            for (int i = 0; i < staleLayers.Count; i++)
                DestroyUnityObject(staleLayers[i].gameObject);
        }

        public void ApplyLayerVisibility(int layerIndex)
        {
            if (layers == null || layerIndex < 0 || layerIndex >= layers.Count)
                return;

            if (!layerRoots.TryGetValue(layerIndex, out Transform layerRoot) || layerRoot == null)
                return;

            layerRoot.gameObject.SetActive(layers[layerIndex].visible);
        }

        public DualGrid3D ResolveAlignLandscapeTarget()
        {
            if (linkedLandscape != null)
                return linkedLandscape;

            WorldRoot world = GetComponentInParent<WorldRoot>();
            if (world != null && world.Landscape != null)
                return world.Landscape;

            return null;
        }

        public void TryAutoLinkLandscapeInWorld()
        {
            if (linkedLandscape != null)
                return;

            WorldRoot world = GetComponentInParent<WorldRoot>();
            if (world == null || world.Landscape == null)
                return;

            linkedLandscape = world.Landscape;
            Persist();
        }

        private void SyncLayerRoot(int layerIndex, Transform layerRoot)
        {
            EnvironmentLayerDefinition definition = layers[layerIndex];
            Vector3 expectedLocalPosition = new Vector3(0f, definition.height, 0f);
            if (layerRoot.localPosition != expectedLocalPosition)
                layerRoot.localPosition = expectedLocalPosition;

            ApplyLayerVisibility(layerIndex);
        }

        private static string SanitizeLayerRootName(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return "Layer";

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            string sanitized = layerName.Trim();
            for (int i = 0; i < invalid.Length; i++)
                sanitized = sanitized.Replace(invalid[i], '_');

            return sanitized.Replace(' ', '_');
        }

        private void Persist()
        {
    #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
    #endif
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

    #if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(target);
            else
    #endif
                UnityEngine.Object.Destroy(target);
        }
    }
}
