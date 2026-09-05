using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace TileWorldCreator
{
    public class EnvironmentPainterUI
    {
        private TileBrush brush;
        private TileBiomeData currentEnvironmentBiome;
        private List<TileBiomeData> availableBiomes = new List<TileBiomeData>();
        
        private readonly string[] environmentCategories = { TileBiomeData.Categories.Rocks, TileBiomeData.Categories.Trees, TileBiomeData.Categories.Vegetation, TileBiomeData.Categories.Props };
        private int selectedCategoryIndex = 0;

        public EnvironmentPainterUI(TileBrush brush)
        {
            this.brush = brush;
        }

        public void LoadBiomes()
        {
            availableBiomes.Clear();
            string[] guids = AssetDatabase.FindAssets("t:TileBiomeData");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var biome = AssetDatabase.LoadAssetAtPath<TileBiomeData>(path);
                if (biome != null)
                    availableBiomes.Add(biome);
            }

            if (availableBiomes.Count > 0 && currentEnvironmentBiome == null)
                currentEnvironmentBiome = availableBiomes[0];
        }

        public void Draw()
        {
            EditorGUILayout.LabelField("Environment", EditorStyles.boldLabel);

            if (availableBiomes.Count == 0)
            {
                EditorGUILayout.HelpBox("No biomes! Create one.", MessageType.Info);
                if (GUILayout.Button("Create Biome"))
                    CreateBiomeDefinition();
                return;
            }

            int currentIndex = Mathf.Max(0, availableBiomes.IndexOf(currentEnvironmentBiome));
            string[] biomeNames = new string[availableBiomes.Count];
            for (int i = 0; i < availableBiomes.Count; i++)
                biomeNames[i] = availableBiomes[i].displayName;

            int newIndex = EditorGUILayout.Popup("Biome", currentIndex, biomeNames);
            if (newIndex != currentIndex)
            {
                currentEnvironmentBiome = availableBiomes[newIndex];
                if (brush != null)
                    brush.SetEnvironmentBiome(currentEnvironmentBiome);
            }

            if (currentEnvironmentBiome == null) return;

            // Category selection
            int newCategoryIndex = EditorGUILayout.Popup("Category", selectedCategoryIndex, environmentCategories);
            if (newCategoryIndex != selectedCategoryIndex)
            {
                selectedCategoryIndex = newCategoryIndex;
                if (brush != null)
                    brush.SetEnvironmentCategory(environmentCategories[selectedCategoryIndex]);
            }

            string category = environmentCategories[selectedCategoryIndex];
            GameObject[] objects = currentEnvironmentBiome.GetEnvironmentObjects(category);
            int count = objects?.Length ?? 0;

            EditorGUILayout.LabelField($"Objects in {category}: {count}", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            if (count > 0)
            {
                EditorGUILayout.LabelField("Prefabs:", EditorStyles.miniLabel);

                int previewSize = 64;
                int columns = Mathf.Max(1, (int)(EditorGUIUtility.currentViewWidth - 40) / (previewSize + 8));

                int drawn = 0;
                while (drawn < count)
                {
                    EditorGUILayout.BeginHorizontal();

                    for (int col = 0; col < columns && drawn < count; col++, drawn++)
                    {
                        GameObject prefab = objects[drawn];
                        if (prefab == null) continue;

                        Texture2D preview = AssetPreview.GetAssetPreview(prefab);
                        if (preview == null)
                            preview = AssetPreview.GetMiniThumbnail(prefab);

                        GUIContent content = new GUIContent(preview, prefab.name);

                        if (GUILayout.Button(content, GUILayout.Width(previewSize), GUILayout.Height(previewSize)))
                        {
                            Selection.activeObject = prefab;
                            EditorGUIUtility.PingObject(prefab);
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(4);
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"No objects in '{category}' category.\nAdd prefabs in the Biome Data asset.", MessageType.Info);
            }

            EditorGUILayout.Space(5);
        }

        public void SyncToBrush(TileBrush targetBrush)
        {
            if (targetBrush == null) return;

            targetBrush.SetEnvironmentBiome(currentEnvironmentBiome);
            targetBrush.SetEnvironmentCategory(environmentCategories[selectedCategoryIndex]);
        }

        private void CreateBiomeDefinition()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Biome Data",
                "NewBiomeData",
                "asset",
                "Enter a name for the biome data");

            if (string.IsNullOrEmpty(path)) return;

            var biome = ScriptableObject.CreateInstance<TileBiomeData>();
            biome.biomeId = System.IO.Path.GetFileNameWithoutExtension(path);
            biome.displayName = biome.biomeId;

            AssetDatabase.CreateAsset(biome, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LoadBiomes();
            currentEnvironmentBiome = biome;
        }
    }
}
