using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace TileWorldCreator
{
    public class LevelPainterUI
    {
        private TileBrush brush;
        private TileBiomeData currentBiome;
        private List<TileBiomeData> availableBiomes = new List<TileBiomeData>();
        private string currentTileType = "Ground";
        private readonly string[] tileTypes = { "Ground", "Liquid", "Decorative" };

        public LevelPainterUI(TileBrush brush)
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
                TileBiomeData biome = AssetDatabase.LoadAssetAtPath<TileBiomeData>(path);
                if (biome != null)
                    availableBiomes.Add(biome);
            }

            if (availableBiomes.Count > 0 && currentBiome == null)
                currentBiome = availableBiomes[0];
        }

        public void Draw()
        {
            EditorGUILayout.LabelField("Biome", EditorStyles.boldLabel);

            if (availableBiomes.Count == 0)
            {
                EditorGUILayout.HelpBox("No biomes! Create one.", MessageType.Info);
                if (GUILayout.Button("Create Biome"))
                    CreateBiomeDefinition();
                return;
            }

            int currentIndex = Mathf.Max(0, availableBiomes.IndexOf(currentBiome));
            string[] names = new string[availableBiomes.Count];
            for (int i = 0; i < availableBiomes.Count; i++)
                names[i] = availableBiomes[i].displayName;

            int newIndex = EditorGUILayout.Popup("Biome", currentIndex, names);
            if (newIndex != currentIndex)
            {
                currentBiome = availableBiomes[newIndex];
                if (brush != null)
                    brush.SetBiome(currentBiome);
            }

            if (currentBiome != null)
            {
                int typeIndex = Mathf.Max(0, System.Array.IndexOf(tileTypes, currentTileType));
                typeIndex = EditorGUILayout.Popup("Tile Type", typeIndex, tileTypes);
                currentTileType = tileTypes[typeIndex];

                if (brush != null)
                    brush.SetTileType(currentTileType);
            }

            EditorGUILayout.Space(5);
        }

        public void SyncToBrush(TileBrush targetBrush)
        {
            if (targetBrush == null) return;
            if (currentBiome != null)
                targetBrush.SetBiome(currentBiome);
            targetBrush.SetTileType(currentTileType);
        }

        private void CreateBiomeDefinition()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Biome Data",
                "NewBiomeData",
                "asset",
                "Enter a name for the biome data");

            if (string.IsNullOrEmpty(path)) return;

            TileBiomeData biome = ScriptableObject.CreateInstance<TileBiomeData>();
            biome.biomeId = System.IO.Path.GetFileNameWithoutExtension(path);
            biome.displayName = biome.biomeId;

            AssetDatabase.CreateAsset(biome, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LoadBiomes();
            currentBiome = biome;
            if (brush != null)
                brush.SetBiome(currentBiome);
        }
    }
}
