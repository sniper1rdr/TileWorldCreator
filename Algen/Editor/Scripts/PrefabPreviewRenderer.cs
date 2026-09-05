using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal static class PrefabPreviewRenderer
    {
        private const int PreviewResolution = 128;
        private const int PreviewsPerTick = 3;
        private const string CacheVersion = "v2";
        private const float PreviewPadding = 1.18f;
        private const float PreviewOrbitPitch = 25f;
        private const float PreviewOrbitYaw = 125f;

        private static readonly Dictionary<string, Texture2D> Cache = new();
        private static readonly LinkedList<GameObject> RenderQueue = new();
        private static readonly Dictionary<string, LinkedListNode<GameObject>> QueueNodes = new();
        private static PreviewRenderUtility previewUtility;
        private static bool processingQueue;

        static PrefabPreviewRenderer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClearCache;
        }

        public static Texture GetPreview(GameObject prefab)
        {
            if (prefab == null)
                return null;

            string cacheKey = GetCacheKey(prefab);
            if (Cache.TryGetValue(cacheKey, out Texture2D cached) && cached != null)
                return cached;

            // Already queued (e.g. by biome warmup) — do not reorder on every repaint.
            if (QueueNodes.ContainsKey(cacheKey))
                return null;

            Enqueue(prefab, cacheKey, highPriority: true);
            return null;
        }

        /// <summary>
        /// Queues prefabs for background preview rendering.
        /// High-priority items are placed at the front of the queue in the given order.
        /// </summary>
        public static void Prefetch(IEnumerable<GameObject> prefabs, bool highPriority = false)
        {
            if (prefabs == null)
                return;

            LinkedListNode<GameObject> insertAfter = null;
            bool anyQueued = false;

            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null)
                    continue;

                string cacheKey = GetCacheKey(prefab);
                if (Cache.TryGetValue(cacheKey, out Texture2D cached) && cached != null)
                    continue;

                if (Enqueue(prefab, cacheKey, highPriority, ref insertAfter))
                    anyQueued = true;
            }

            if (anyQueued)
                ScheduleQueueProcessing();
        }

        public static void ClearCache()
        {
            foreach (KeyValuePair<string, Texture2D> entry in Cache)
            {
                if (entry.Value != null)
                    Object.DestroyImmediate(entry.Value);
            }

            Cache.Clear();
            RenderQueue.Clear();
            QueueNodes.Clear();
            CleanupPreviewUtility();
        }

        private static bool Enqueue(
            GameObject prefab,
            string cacheKey,
            bool highPriority)
        {
            LinkedListNode<GameObject> insertAfter = null;
            bool queued = Enqueue(prefab, cacheKey, highPriority, ref insertAfter);
            if (queued)
                ScheduleQueueProcessing();
            return queued;
        }

        private static bool Enqueue(
            GameObject prefab,
            string cacheKey,
            bool highPriority,
            ref LinkedListNode<GameObject> insertAfter)
        {
            if (QueueNodes.TryGetValue(cacheKey, out LinkedListNode<GameObject> existing))
            {
                if (!highPriority)
                    return false;

                if (existing.List != null)
                    RenderQueue.Remove(existing);

                if (insertAfter == null)
                    RenderQueue.AddFirst(existing);
                else
                    RenderQueue.AddAfter(insertAfter, existing);

                insertAfter = existing;
                return false;
            }

            LinkedListNode<GameObject> node;
            if (highPriority)
            {
                node = insertAfter == null
                    ? RenderQueue.AddFirst(prefab)
                    : RenderQueue.AddAfter(insertAfter, prefab);
                insertAfter = node;
            }
            else
            {
                node = RenderQueue.AddLast(prefab);
            }

            QueueNodes[cacheKey] = node;
            return true;
        }

        private static void ScheduleQueueProcessing()
        {
            if (processingQueue)
                return;

            processingQueue = true;
            EditorApplication.delayCall += ProcessRenderQueue;
        }

        private static void ProcessRenderQueue()
        {
            processingQueue = false;

            int processed = 0;
            while (RenderQueue.Count > 0 && processed < PreviewsPerTick)
            {
                LinkedListNode<GameObject> node = RenderQueue.First;
                RenderQueue.RemoveFirst();

                GameObject prefab = node.Value;
                string cacheKey = prefab != null ? GetCacheKey(prefab) : null;
                if (cacheKey != null)
                    QueueNodes.Remove(cacheKey);

                if (prefab == null)
                    continue;

                if (!Cache.ContainsKey(cacheKey))
                {
                    GameObject previewTarget = ResolvePrefabAsset(prefab);
                    Texture2D rendered = RenderColoredPreview(previewTarget);
                    if (rendered != null)
                        Cache[cacheKey] = rendered;
                }

                processed++;
            }

            if (processed > 0)
                LandscapeLevelManagerWindow.RequestRepaintIfOpen();

            if (RenderQueue.Count > 0)
                ScheduleQueueProcessing();
        }

        private static GameObject ResolvePrefabAsset(GameObject prefab)
        {
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(assetPath))
                return prefab;

            GameObject loaded = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            return loaded != null ? loaded : prefab;
        }

        private static string GetCacheKey(GameObject prefab)
        {
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            return string.IsNullOrEmpty(assetPath)
                ? prefab.GetInstanceID() + "|" + CacheVersion
                : assetPath + "|" + CacheVersion;
        }

        private static Texture2D RenderColoredPreview(GameObject prefab)
        {
            EnsurePreviewUtility();

            previewUtility.ambientColor = new Color(0.45f, 0.45f, 0.45f, 1f);
            previewUtility.cameraFieldOfView = 30f;
            previewUtility.camera.nearClipPlane = 0.01f;
            previewUtility.camera.farClipPlane = 200f;
            previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            previewUtility.camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0f);

            Rect previewRect = new Rect(0f, 0f, PreviewResolution, PreviewResolution);
            previewUtility.BeginStaticPreview(previewRect);

            GameObject instance = null;
            bool rendered = false;
            try
            {
                instance = previewUtility.InstantiatePrefabInScene(prefab);
                if (instance != null)
                {
                    FrameCameraOnInstance(instance);

                    if (previewUtility.lights != null && previewUtility.lights.Length > 0)
                    {
                        previewUtility.lights[0].intensity = 1.15f;
                        previewUtility.lights[0].transform.rotation = Quaternion.Euler(42f, 36f, 0f);

                        if (previewUtility.lights.Length > 1)
                        {
                            previewUtility.lights[1].intensity = 0.45f;
                            previewUtility.lights[1].transform.rotation = Quaternion.Euler(12f, 165f, 0f);
                        }
                    }

                    previewUtility.Render(allowScriptableRenderPipeline: true, updatefov: true);
                    rendered = true;
                }
            }
            finally
            {
                if (instance != null)
                    Object.DestroyImmediate(instance);
            }

            Texture2D copy = previewUtility.EndStaticPreview();
            if (!rendered)
            {
                if (copy != null)
                    Object.DestroyImmediate(copy);
                return null;
            }

            if (copy != null)
                copy.hideFlags = HideFlags.HideAndDontSave;

            return copy;
        }

        private static void EnsurePreviewUtility()
        {
            if (previewUtility != null)
                return;

            previewUtility = new PreviewRenderUtility();
            EditorApplication.quitting += CleanupPreviewUtility;
        }

        private static void CleanupPreviewUtility()
        {
            if (previewUtility == null)
                return;

            previewUtility.Cleanup();
            previewUtility = null;
        }

        private static void FrameCameraOnInstance(GameObject instance)
        {
            Bounds bounds = CalculateRenderableBounds(instance);
            instance.transform.position -= bounds.center;
            bounds.center = Vector3.zero;

            float boundingRadius = Mathf.Max(bounds.extents.magnitude, 0.15f);
            float halfFovRadians = previewUtility.camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float distance = boundingRadius / Mathf.Sin(halfFovRadians) * PreviewPadding;

            Quaternion orbit = Quaternion.Euler(PreviewOrbitPitch, PreviewOrbitYaw, 0f);
            Vector3 cameraPosition = orbit * new Vector3(0f, 0f, -distance);
            previewUtility.camera.transform.position = cameraPosition;
            previewUtility.camera.transform.rotation = Quaternion.LookRotation(-cameraPosition.normalized, Vector3.up);
        }

        private static Bounds CalculateRenderableBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.one * 0.5f);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }
    }
}
