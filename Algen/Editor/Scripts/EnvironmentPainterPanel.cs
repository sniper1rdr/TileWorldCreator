using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal static class EnvironmentPainterPanel
    {
        private const float PaletteMinHeight = 120f;
        private const float WindowBottomPadding = 8f;

        private static readonly string[] EnvironmentHelpLines =
        {
            "Alt — orbit camera (blocks placement)",
            "Esc — stop / deselect active prefab",
            "Click layer row — select active layer",
            "Click layer again — clear layer selection (module rules)",
            "LMB — place prefab when Placement Active",
            "Pick thumbnail — select asset (activates placement)",
            "Esc — Placement: Idle",
        };

        private static Vector2 paletteScrollPosition;
        private static string previewWarmupBiomeId;
        private static EnvironmentCategory previewWarmupCategory;

        public static void DrawTabContent(EnvironmentRoot environment, LandscapeLevelManagerWindow hostWindow)
        {
            if (environment == null)
            {
                EditorGUILayout.HelpBox("No Environment module found in the selected world.", MessageType.Warning);
                return;
            }

            if (BiomeRegistry.All.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No biome packs found. Import a biome pack, or create your own via Create → Aglen Realms → Biome Definition. See Documentation~/CUSTOM_BIOME_GUIDE.md in the World Core package.",
                    MessageType.Info);
                return;
            }

            environment.TryAutoLinkLandscapeInWorld();

            string biomeId = BiomeTileLibrary.NormalizeBiomeId(environment.EnvironmentBiomeId, BrushBiome.Grasslands);
            EnvironmentPainterState.BindEnvironment(environment);
            EnvironmentPainterState.EnsureDefaults(biomeId);
            EnvironmentPainterState.SyncBiomeFromEnvironment(biomeId);
            EnvironmentPainterState.SyncLayersFromTarget(environment);

            DrawEnvironmentBiomeSelector(environment);

            biomeId = BiomeTileLibrary.NormalizeBiomeId(environment.EnvironmentBiomeId, BrushBiome.Grasslands);
            BiomeEnvironmentLibraryDefinition library = BiomeEnvironmentLibrary.Resolve(biomeId);
            if (library == null)
            {
                EditorGUILayout.HelpBox("No Environment Library is available for this biome.", MessageType.Info);
                DrawEnvironmentHelp();
                return;
            }

            List<EnvironmentCategory> visibleCategories = GetVisibleCategories(library);
            if (visibleCategories.Count == 0)
            {
                EditorGUILayout.HelpBox("No environment prefabs are available for this biome.", MessageType.Info);
                DrawEnvironmentHelp();
                return;
            }

            if (!visibleCategories.Contains(EnvironmentPainterState.ActiveCategory))
                EnvironmentPainterState.SetCategory(visibleCategories[0]);

            EnsurePreviewWarmup(biomeId, EnvironmentPainterState.ActiveCategory, visibleCategories);

            DrawCategoryToolbar(visibleCategories);
            DrawPlacementStatus(environment);

            EnvironmentLayersPanel.Draw(environment);
            EnvironmentBrushSettingsPanel.Draw(environment.BrushSettings, environment);
            EditorGUILayout.Space(2f);
            DrawSearchField();
            hostWindow.RecordEnvironmentHeaderBottom(GUILayoutUtility.GetLastRect().yMax);

            float paletteHeight = CalculatePaletteBlockHeight(hostWindow);
            Rect paletteRect = EditorGUILayout.GetControlRect(false, paletteHeight, GUILayout.ExpandWidth(true));

            IPaletteSource source = new BiomePaletteSource();
            PaletteView.DrawInRect(
                paletteRect,
                source,
                EnvironmentPainterState.ActivePrefab,
                prefab =>
                {
                    EnvironmentPainterState.SetPrefab(prefab);
                    SceneView.RepaintAll();
                },
                ref paletteScrollPosition);

            DrawEnvironmentHelp();
        }

        private static void DrawEnvironmentHelp()
        {
            WorldCoreHelpFoldout.Draw(
                WorldCoreHelpFoldout.EnvironmentExpandedPrefKey,
                EnvironmentHelpLines,
                null);
        }

        private static float CalculatePaletteBlockHeight(LandscapeLevelManagerWindow hostWindow)
        {
            if (hostWindow == null)
                return PaletteMinHeight;

            float helpReserve = WorldCoreHelpFoldout.GetReserveHeight(
                WorldCoreHelpFoldout.EnvironmentExpandedPrefKey,
                EnvironmentHelpLines,
                null);
            float headerBottom = Mathf.Max(0f, hostWindow.EnvironmentHeaderBottom);
            float available = hostWindow.position.height
                - headerBottom
                - helpReserve
                - WindowBottomPadding;

            // Clamp: never negative / never below min (prevents GUILayout exceptions on tiny windows).
            if (float.IsNaN(available) || float.IsInfinity(available))
                return PaletteMinHeight;

            return Mathf.Max(PaletteMinHeight, available);
        }

        private static void DrawEnvironmentBiomeSelector(EnvironmentRoot environment)
        {
            IReadOnlyList<BiomeDefinition> biomes = BiomeRegistry.All;
            if (biomes.Count == 0)
                return;

            string[] labels = new string[biomes.Count];
            int selectedIndex = 0;
            string currentBiomeId = BiomeTileLibrary.NormalizeBiomeId(environment.EnvironmentBiomeId, BrushBiome.Grasslands);

            for (int i = 0; i < biomes.Count; i++)
            {
                labels[i] = biomes[i].displayName;
                if (biomes[i].biomeId == currentBiomeId)
                    selectedIndex = i;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(
                new GUIContent("Biome", "Biome library source for environment prefabs"),
                selectedIndex,
                labels);
            if (EditorGUI.EndChangeCheck())
            {
                // Active environment biome is tool session state — no Undo entry.
                environment.EnvironmentBiomeId = biomes[newIndex].biomeId;
                EnvironmentPainterState.SetBiome(environment.EnvironmentBiomeId);
                paletteScrollPosition = Vector2.zero;
                EditorUtility.SetDirty(environment);
                SceneView.RepaintAll();
            }
        }

        private static void DrawPlacementStatus(EnvironmentRoot environment)
        {
            string biomeName = ResolveBiomeDisplayName(
                BiomeTileLibrary.NormalizeBiomeId(environment.EnvironmentBiomeId, BrushBiome.Grasslands));
            string category = EnvironmentPainterState.ActiveCategory.GetTabLabel();
            string layerName = ResolveActiveLayerName(environment);

            string status;
            if (EnvironmentPainterState.HasActivePrefab)
            {
                status =
                    $"Placement: Active · {biomeName} · {category} · {layerName} · {EnvironmentPainterState.ActivePrefab.name}";
            }
            else
            {
                status = $"Placement: Idle · {biomeName} · {category} · {layerName}";
            }

            WorldCoreStatusBar.Draw(status);
        }

        private static string ResolveActiveLayerName(EnvironmentRoot environment)
        {
            if (environment == null || environment.Layers == null || environment.Layers.Count == 0)
                return "—";

            int index = Mathf.Clamp(environment.ActiveLayerIndex, 0, environment.Layers.Count - 1);
            EnvironmentLayerDefinition layer = environment.Layers[index];
            return layer != null && !string.IsNullOrEmpty(layer.name) ? layer.name : "—";
        }

        private static string ResolveBiomeDisplayName(string biomeId)
        {
            BiomeDefinition definition = BiomeRegistry.GetById(biomeId);
            return definition != null ? definition.displayName : biomeId;
        }

        private static List<EnvironmentCategory> GetVisibleCategories(BiomeEnvironmentLibraryDefinition library)
        {
            var visibleCategories = new List<EnvironmentCategory>();
            if (library?.categories == null)
                return visibleCategories;

            for (int i = 0; i < library.categories.Length; i++)
            {
                BiomeEnvironmentCategoryEntry entry = library.categories[i];
                if (entry != null && entry.HasPrefabs)
                    visibleCategories.Add(entry.category);
            }

            return visibleCategories;
        }

        private static void EnsurePreviewWarmup(
            string biomeId,
            EnvironmentCategory activeCategory,
            List<EnvironmentCategory> categories)
        {
            if (string.IsNullOrEmpty(biomeId) || categories == null || categories.Count == 0)
                return;

            if (previewWarmupBiomeId != biomeId)
            {
                previewWarmupBiomeId = biomeId;
                previewWarmupCategory = activeCategory;

                PrefabPreviewRenderer.Prefetch(
                    BiomeEnvironmentLibraryEditor.GetPrefabs(biomeId, activeCategory),
                    highPriority: true);

                for (int i = 0; i < categories.Count; i++)
                {
                    EnvironmentCategory category = categories[i];
                    if (category == activeCategory)
                        continue;

                    PrefabPreviewRenderer.Prefetch(
                        BiomeEnvironmentLibraryEditor.GetPrefabs(biomeId, category),
                        highPriority: false);
                }

                return;
            }

            if (previewWarmupCategory == activeCategory)
                return;

            previewWarmupCategory = activeCategory;
            PrefabPreviewRenderer.Prefetch(
                BiomeEnvironmentLibraryEditor.GetPrefabs(biomeId, activeCategory),
                highPriority: true);
        }

        private static void DrawCategoryToolbar(List<EnvironmentCategory> visibleCategories)
        {
            string[] labels = new string[visibleCategories.Count];
            for (int i = 0; i < visibleCategories.Count; i++)
                labels[i] = visibleCategories[i].GetTabLabel();

            int currentToolbarIndex = visibleCategories.IndexOf(EnvironmentPainterState.ActiveCategory);
            if (currentToolbarIndex < 0)
                currentToolbarIndex = 0;

            int newToolbarIndex = GUILayout.Toolbar(currentToolbarIndex, labels, GUILayout.Height(24f));
            if (newToolbarIndex >= 0 &&
                newToolbarIndex < visibleCategories.Count &&
                newToolbarIndex != currentToolbarIndex)
            {
                EnvironmentPainterState.SetCategory(visibleCategories[newToolbarIndex]);
                paletteScrollPosition = Vector2.zero;
                SceneView.RepaintAll();
            }
        }

        private static void DrawSearchField()
        {
            EditorGUI.BeginChangeCheck();
            string query = EditorGUILayout.TextField(
                EnvironmentPainterState.SearchQuery,
                EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
            {
                EnvironmentPainterState.SetSearchQuery(query);
                paletteScrollPosition = Vector2.zero;
            }
        }
    }
}
