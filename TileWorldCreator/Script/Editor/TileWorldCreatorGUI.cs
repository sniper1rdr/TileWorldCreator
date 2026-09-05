using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace TileWorldCreator
{
    public class TileWorldCreatorGUI : EditorWindow
    {
        // === Core ===
        private WorldRoot worldRoot;
        private TileBrush brush;

        private enum PaintMode { Level, Environment }
        private PaintMode currentMode = PaintMode.Level;

        private List<Level> availableLevels = new List<Level>();
        private List<Layer> availableLayers = new List<Layer>();
        private int selectedLevelIndex = 0;
        private int selectedLayerIndex = 0;

        // === UI Modules ===
        private LevelPainterUI levelUI;
        private EnvironmentPainterUI environmentUI;
        private ToolPanelUI toolUI;

        private Vector2 scrollPosition;

        // Настройки
        private float levelHeight = 1f;

        [MenuItem("Tools/TileWorld Creator")]
        public static void ShowWindow()
        {
            var window = GetWindow<TileWorldCreatorGUI>("TileWorld Creator");
            window.minSize = new Vector2(300, 500);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;

            FindWorldRoot();
            RefreshLevelsAndLayers();
            
            FindOrCreateBrush();
            
            levelUI = new LevelPainterUI(brush);
            environmentUI = new EnvironmentPainterUI(brush);
            toolUI = new ToolPanelUI(brush);

            levelUI.LoadBiomes();
            environmentUI.LoadBiomes();
            
            SyncBrush();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            brush?.ClearAll();
        }

        private void OnDestroy()
        {
            brush?.ClearAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (brush == null)
            {
                FindOrCreateBrush();
                SyncBrush();
            }
            
            // Всегда обновляем targetLayer перед рисованием
            if (brush != null)
            {
                Layer currentLayer = GetCurrentLayer();
                if (brush != null)
                    brush.SetTargetLayer(currentLayer);
            }
            
            brush?.OnSceneGUI(sceneView);
        }

        private void OnGUI()
        {
            if (brush == null)
            {
                FindOrCreateBrush();
                SyncBrush();
            }

            RefreshLevelsAndLayers();

            // Всегда обновляем targetLayer при каждом GUI обновлении
            if (brush != null)
            {
                Layer currentLayer = GetCurrentLayer();
                brush.SetTargetLayer(currentLayer);
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            
            EditorGUILayout.Space(5);
            DrawLevelManagement();
            
            EditorGUILayout.Space(5);
            DrawLayerManagement();
            
            EditorGUILayout.Space(5);
            DrawModeSelection();

            if (currentMode == PaintMode.Level)
                levelUI.Draw();
            else
                environmentUI.Draw();

            toolUI.Draw(
                isLevelMode: currentMode == PaintMode.Level,
                currentLayer: GetCurrentLayer(),
                onClearLevel: ClearCurrentLayer,
                onClearEnvironment: ClearEnvironment
            );

            DrawStatusBar();

            EditorGUILayout.EndScrollView();
        }

        private void DrawLevelManagement()
        {
            EditorGUILayout.LabelField("Levels", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Height Step:", GUILayout.Width(80));
            float newHeight = EditorGUILayout.FloatField(levelHeight, GUILayout.Width(60));
            if (newHeight > 0 && newHeight != levelHeight)
            {
                levelHeight = newHeight;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("⬆ Above (+1)", GUILayout.Height(30)))
            {
                CreateLevelAbove();
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.9f);
            if (GUILayout.Button("⬇ Below (-1)", GUILayout.Height(30)))
            {
                CreateLevelBelow();
            }
            GUI.backgroundColor = Color.white;

            if (availableLevels.Count > 1)
            {
                GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                if (GUILayout.Button("✖ Remove", GUILayout.Height(30)))
                {
                    RemoveSelectedLevel();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            if (availableLevels.Count > 0)
            {
                List<Level> sortedLevels = new List<Level>(availableLevels);
                sortedLevels.Sort((a, b) => b.Height.CompareTo(a.Height));

                for (int i = 0; i < sortedLevels.Count; i++)
                {
                    Level level = sortedLevels[i];
                    if (level == null) continue;

                    bool isBase = (level.Height == 0f);
                    bool isActive = (level == GetCurrentLevel());
                    
                    if (isBase)
                    {
                        GUI.backgroundColor = new Color(0.9f, 0.8f, 0.3f, 0.5f);
                    }
                    else if (isActive)
                    {
                        GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
                    }
                    else
                    {
                        GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                    }
                    
                    string label = isBase ? $"⭐ {level.LevelName} (Y: {level.Height:F1})" : $"{level.LevelName} (Y: {level.Height:F1})";
                    
                    if (GUILayout.Button(label, GUILayout.Height(25)))
                    {
                        selectedLevelIndex = availableLevels.IndexOf(level);
                        if (worldRoot?.Levels != null)
                        {
                            worldRoot.Levels.SetActiveLevel(selectedLevelIndex);
                            RefreshLevelsAndLayers();
                            SyncBrush();
                        }
                    }

                    GUI.backgroundColor = Color.white;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No levels. Create Base Level first!", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLayerManagement()
        {
            EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            Level currentLevel = GetCurrentLevel();
            if (currentLevel != null)
            {
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("➕ Add Layer", GUILayout.Height(25)))
                {
                    CreateNewLayer();
                }

                if (currentLevel.Layers.Count > 1)
                {
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("✖ Remove", GUILayout.Height(25)))
                    {
                        RemoveCurrentLayer();
                    }
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);

                if (currentLevel.Layers.Count > 0)
                {
                    for (int i = 0; i < currentLevel.Layers.Count; i++)
                    {
                        Layer layer = currentLevel.Layers[i];
                        if (layer == null) continue;

                        bool isBaseLayer = (layer.LayerName == "Base Layer");
                        bool isActive = (i == selectedLayerIndex);
                        
                        if (isBaseLayer)
                        {
                            GUI.backgroundColor = new Color(0.9f, 0.8f, 0.3f, 0.5f);
                        }
                        else if (isActive)
                        {
                            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.9f);
                        }
                        else
                        {
                            GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
                        }
                        
                        string label = isBaseLayer ? $"⭐ {layer.LayerName} ({layer.Tiles.Count} tiles)" : $"{layer.LayerName} ({layer.Tiles.Count} tiles)";
                        
                        if (GUILayout.Button(label, GUILayout.Height(22)))
                        {
                            selectedLayerIndex = i;
                            currentLevel.SetActiveLayer(selectedLayerIndex);
                            if (brush != null)
                                brush.SetTargetLayer(currentLevel.ActiveLayer);
                            RefreshLevelsAndLayers();
                            SyncBrush();
                        }

                        GUI.backgroundColor = Color.white;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No layers. Click 'Add Layer' to create one.", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Create a level first!", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🏗️ TileWorld Creator", EditorStyles.boldLabel);
            
            if (worldRoot != null && GUILayout.Button("📍 Focus", GUILayout.Width(60)))
            {
                Selection.activeGameObject = worldRoot.gameObject;
                EditorGUIUtility.PingObject(worldRoot);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(4);
        }

        private void DrawModeSelection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mode:", GUILayout.Width(45));

            GUI.backgroundColor = currentMode == PaintMode.Level
                ? new Color(0.35f, 0.85f, 0.35f)
                : Color.gray;

            if (GUILayout.Button("🧱 Tiles", GUILayout.Height(28)))
            {
                currentMode = PaintMode.Level;
                brush?.SetPaintMode("Level");
                levelUI?.SyncToBrush(brush);
            }

            GUI.backgroundColor = currentMode == PaintMode.Environment
                ? new Color(0.35f, 0.65f, 0.95f)
                : Color.gray;

            if (GUILayout.Button("🌳 Environment", GUILayout.Height(28)))
            {
                currentMode = PaintMode.Environment;
                brush?.SetPaintMode("Environment");
                environmentUI?.SyncToBrush(brush);
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            string modeIcon = currentMode == PaintMode.Level ? "🧱" : "🌳";
            string status = $"{modeIcon}  Ready";

            if (worldRoot == null)
                status = "❌  No World";
            else if (GetCurrentLayer() == null)
                status = "⚠️  No Layer";
            else if (brush != null && brush.IsActive)
                status = $"{modeIcon}  Brush Active";

            GUILayout.Label(status, GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            Level level = GetCurrentLevel();
            if (level != null)
            {
                bool isBase = (level.Height == 0f);
                string icon = isBase ? "⭐" : "📊";
                GUILayout.Label($"{icon} {level.LevelName}", GUILayout.Width(100));
            }

            Layer layer = GetCurrentLayer();
            if (layer != null)
            {
                bool isBaseLayer = (layer.LayerName == "Base Layer");
                string icon = isBaseLayer ? "⭐" : "📋";
                GUILayout.Label($"{icon} {layer.LayerName}", GUILayout.Width(100));
                GUILayout.Label($"🟦 {layer.Tiles.Count}", GUILayout.Width(50));
            }

            EditorGUILayout.EndHorizontal();
        }

        private Level GetCurrentLevel()
        {
            if (availableLevels.Count > 0 && selectedLevelIndex < availableLevels.Count)
                return availableLevels[selectedLevelIndex];
            return null;
        }

        private Layer GetCurrentLayer()
        {
            Level level = GetCurrentLevel();
            if (level != null && selectedLayerIndex < level.Layers.Count)
                return level.Layers[selectedLayerIndex];
            return null;
        }

        private void RefreshLevelsAndLayers()
        {
            availableLevels.Clear();
            availableLayers.Clear();

            if (worldRoot?.Levels == null) return;

            foreach (Level level in worldRoot.Levels.Levels)
            {
                if (level != null)
                    availableLevels.Add(level);
            }

            availableLevels.Sort((a, b) => b.Height.CompareTo(a.Height));

            if (selectedLevelIndex >= availableLevels.Count)
                selectedLevelIndex = Mathf.Max(0, availableLevels.Count - 1);

            Level currentLevel = GetCurrentLevel();
            if (currentLevel != null)
            {
                foreach (Layer layer in currentLevel.Layers)
                {
                    if (layer != null)
                        availableLayers.Add(layer);
                }

                if (selectedLayerIndex >= availableLayers.Count)
                    selectedLayerIndex = Mathf.Max(0, availableLayers.Count - 1);
            }
        }

        private void FindWorldRoot()
        {
            worldRoot = WorldRoot.FindInScene();

            if (worldRoot == null)
            {
                GameObject root = GameObject.Find(WorldRoot.WorldObjectName);
                if (root != null)
                    worldRoot = root.GetComponent<WorldRoot>();
            }
        }

        private void FindOrCreateBrush()
        {
            if (brush == null)
            {
                brush = new TileBrush();
            }
        }

        private void CreateWorldRoot()
        {
            GameObject rootObject = new GameObject(WorldRoot.WorldObjectName);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(rootObject, "Create WorldRoot");
#endif
            worldRoot = rootObject.AddComponent<WorldRoot>();
            worldRoot.SetWorldName("MyWorld");

            LevelsRoot levels = worldRoot.FindOrCreateLevels();

            Level baseLevel = levels.CreateLevel("Base_Level", 0f);
            baseLevel.CreateDefaultLayer();

            Selection.activeGameObject = rootObject;

            RefreshLevelsAndLayers();
            SyncBrush();
        }

        private void CreateLevelAbove()
        {
            if (worldRoot?.Levels == null)
            {
                CreateWorldRoot();
                return;
            }

            float maxHeight = float.MinValue;
            foreach (Level level in availableLevels)
            {
                if (level.Height > maxHeight)
                    maxHeight = level.Height;
            }

            if (availableLevels.Count == 0 || maxHeight == float.MinValue)
                maxHeight = 0f;

            float newHeight = maxHeight + levelHeight;

            Level newLevel = worldRoot.Levels.CreateLevel($"Level_{availableLevels.Count + 1:00}", newHeight);
            newLevel.CreateDefaultLayer();

            RefreshLevelsAndLayers();
            SyncBrush();

            for (int i = 0; i < availableLevels.Count; i++)
            {
                if (availableLevels[i] == newLevel)
                {
                    selectedLevelIndex = i;
                    break;
                }
            }

            if (worldRoot != null)
            {
                EditorGUIUtility.PingObject(newLevel);
            }
        }

        private void CreateLevelBelow()
        {
            if (worldRoot?.Levels == null)
            {
                CreateWorldRoot();
                return;
            }

            float minHeight = float.MaxValue;
            foreach (Level level in availableLevels)
            {
                if (level.Height < minHeight)
                    minHeight = level.Height;
            }

            if (availableLevels.Count == 0 || minHeight == float.MaxValue)
                minHeight = 0f;

            float newHeight = minHeight - levelHeight;

            Level newLevel = worldRoot.Levels.CreateLevel($"Level_{availableLevels.Count + 1:00}", newHeight);
            newLevel.CreateDefaultLayer();

            RefreshLevelsAndLayers();
            SyncBrush();

            for (int i = 0; i < availableLevels.Count; i++)
            {
                if (availableLevels[i] == newLevel)
                {
                    selectedLevelIndex = i;
                    break;
                }
            }

            if (worldRoot != null)
            {
                EditorGUIUtility.PingObject(newLevel);
            }
        }

        private void RemoveSelectedLevel()
        {
            Level level = GetCurrentLevel();
            if (level == null) return;

            if (level.Height == 0f)
            {
                EditorUtility.DisplayDialog(
                    "Cannot Remove",
                    "Base Level (Y: 0) cannot be removed!",
                    "OK");
                return;
            }

            if (EditorUtility.DisplayDialog(
                "Remove Level",
                $"Delete level \"{level.LevelName}\" at height {level.Height:F1}?",
                "Yes", "No"))
            {
                worldRoot.Levels.RemoveLevel(level);
                RefreshLevelsAndLayers();
                SyncBrush();
            }
        }

        private void CreateNewLayer()
        {
            Level level = GetCurrentLevel();
            if (level == null) return;

            string layerName = $"Layer_{level.Layers.Count:00}";
            Layer newLayer = level.CreateLayer(layerName);
            
            if (newLayer != null)
            {
                selectedLayerIndex = level.Layers.Count - 1;
                level.SetActiveLayer(selectedLayerIndex);
                
                if (brush != null)
                {
                    brush.SetTargetLayer(newLayer);
                }
            }
            
            RefreshLevelsAndLayers();
            SyncBrush();
        }

        private void RemoveCurrentLayer()
        {
            Level level = GetCurrentLevel();
            if (level == null) return;

            Layer layer = GetCurrentLayer();
            if (layer == null) return;

            if (layer.LayerName == "Base Layer")
            {
                EditorUtility.DisplayDialog(
                    "Cannot Remove",
                    "Base layer 'Base Layer' cannot be removed!",
                    "OK");
                return;
            }

            if (EditorUtility.DisplayDialog(
                "Remove Layer",
                $"Delete layer \"{layer.LayerName}\" and all its tiles?",
                "Yes", "No"))
            {
                level.RemoveLayer(layer);
                selectedLayerIndex = Mathf.Max(0, level.Layers.Count - 1);
                
                if (brush != null)
                {
                    brush.SetTargetLayer(level.ActiveLayer);
                }
                
                RefreshLevelsAndLayers();
                SyncBrush();
            }
        }

        private void SyncBrush()
        {
            if (brush == null) 
            {
                FindOrCreateBrush();
                return;
            }
            
            if (levelUI != null) levelUI.SyncToBrush(brush);
            if (environmentUI != null) environmentUI.SyncToBrush(brush);

            brush.SetPaintMode(currentMode.ToString());
            brush.SetTargetLayer(GetCurrentLayer());
        }

        private void ClearCurrentLayer()
        {
            Layer layer = GetCurrentLayer();
            if (layer == null) return;

            if (EditorUtility.DisplayDialog(
                "Clear Tiles",
                $"Delete all tiles on layer \"{layer.LayerName}\"?",
                "Yes", "No"))
            {
                layer.ClearAllTiles();
                RefreshLevelsAndLayers();
            }
        }

        private void ClearEnvironment()
        {
            if (worldRoot?.Environment != null)
            {
                if (EditorUtility.DisplayDialog(
                    "Clear Environment",
                    "Delete all environment objects?",
                    "Yes", "No"))
                {
                    worldRoot.Environment.ClearEnvironment();
                }
            }
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }
    }
}
