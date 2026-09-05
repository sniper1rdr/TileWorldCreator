using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AglenRealms.WorldCore.Editor
{
    internal static class EnvironmentPlacementGhost
    {
        private const float GhostAlpha = 0.42f;

        private static GameObject instance;
        private static GameObject sourcePrefab;
        private static Material fallbackGhostMaterial;
        private static readonly List<Material> createdMaterials = new();

        static EnvironmentPlacementGhost()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        public static void Draw(GameObject prefab, EnvironmentPlacementPose pose)
        {
            if (prefab == null)
            {
                Hide();
                return;
            }

            if (!EnsureInstance(prefab))
            {
                Hide();
                return;
            }

            EnvironmentPlacementUtility.ApplyPose(instance.transform, pose);

            if (!instance.activeSelf)
                instance.SetActive(true);
        }

        public static void Hide()
        {
            if (instance != null && instance.activeSelf)
                instance.SetActive(false);
        }

        public static void Dispose()
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
                instance = null;
            }

            sourcePrefab = null;

            for (int i = 0; i < createdMaterials.Count; i++)
            {
                if (createdMaterials[i] != null)
                    Object.DestroyImmediate(createdMaterials[i]);
            }

            createdMaterials.Clear();

            if (fallbackGhostMaterial != null)
            {
                Object.DestroyImmediate(fallbackGhostMaterial);
                fallbackGhostMaterial = null;
            }
        }

        private static bool EnsureInstance(GameObject prefab)
        {
            if (sourcePrefab == prefab && instance != null)
                return true;

            Dispose();

            GameObject asset = ResolvePrefabAsset(prefab);
            instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (instance == null)
                return false;

            sourcePrefab = prefab;
            instance.name = prefab.name + " (Preview)";
            instance.hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable;
            instance.transform.SetParent(null, true);

            DisableInteractionComponents(instance);
            ApplyGhostMaterials(instance);
            return true;
        }

        private static GameObject ResolvePrefabAsset(GameObject prefab)
        {
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(assetPath))
                return prefab;

            GameObject loaded = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            return loaded != null ? loaded : prefab;
        }

        private static void DisableInteractionComponents(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            Collider2D[] colliders2D = root.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders2D.Length; i++)
                colliders2D[i].enabled = false;

            Behaviour[] behaviours = root.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                behaviours[i].enabled = false;
        }

        private static void ApplyGhostMaterials(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                Material[] sourceMaterials = renderer.sharedMaterials;
                Material[] ghostMaterials = new Material[sourceMaterials.Length];
                for (int j = 0; j < sourceMaterials.Length; j++)
                {
                    Material ghostMaterial = CreateGhostMaterial(sourceMaterials[j]);
                    ghostMaterials[j] = ghostMaterial;
                    createdMaterials.Add(ghostMaterial);
                }

                renderer.sharedMaterials = ghostMaterials;
            }
        }

        private static Material CreateGhostMaterial(Material source)
        {
            if (source == null)
                return GetFallbackGhostMaterial();

            Material ghost = new Material(source);
            ghost.hideFlags = HideFlags.HideAndDontSave;
            ghost.name = source.name + " (Ghost)";

            if (ghost.HasProperty("_BaseColor"))
            {
                Color color = ghost.GetColor("_BaseColor");
                color.a = GhostAlpha;
                ghost.SetColor("_BaseColor", color);
            }
            else if (ghost.HasProperty("_Color"))
            {
                Color color = ghost.GetColor("_Color");
                color.a = GhostAlpha;
                ghost.SetColor("_Color", color);
            }

            MakeTransparent(ghost);
            return ghost;
        }

        private static Material GetFallbackGhostMaterial()
        {
            if (fallbackGhostMaterial != null)
                return fallbackGhostMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            fallbackGhostMaterial = new Material(shader);
            fallbackGhostMaterial.hideFlags = HideFlags.HideAndDontSave;
            fallbackGhostMaterial.color = new Color(0.55f, 0.9f, 0.65f, GhostAlpha);
            if (fallbackGhostMaterial.HasProperty("_BaseColor"))
                fallbackGhostMaterial.SetColor("_BaseColor", new Color(0.55f, 0.9f, 0.65f, GhostAlpha));

            MakeTransparent(fallbackGhostMaterial);
            return fallbackGhostMaterial;
        }

        private static void MakeTransparent(Material material)
        {
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);

            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);

            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
