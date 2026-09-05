using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace TileWorldCreator
{
public class TileWorldCreatorGUI : EditorWindow
{
// =========================================================
// CORE
// =========================================================

    private WorldRoot worldRoot;
    private TileBrush brush;

    private enum PaintMode
    {
        Level,
        Environment
    }

    private PaintMode currentMode = PaintMode.Level;

    private List<Level> availableLevels =
        new List<Level>();

    private List<Layer> availableLayers =
        new List<Layer>();

    private int selectedLevelIndex;

    private Vector2 scrollPosition;

    // =========================================================
    // UI
    // =========================================================

    private LevelPainterUI levelUI;
    private EnvironmentPainterUI environmentUI;
    private ToolPanelUI toolUI;

    // =========================================================
    // LEVEL
    // =========================================================

    private float levelHeight = 1f;

    // =========================================================
    // ENVIRONMENT TRANSFORM
    // =========================================================

    private bool environmentRotationEnabled;
    private bool environmentRandomRotation = true;
    private float environmentRotation;

    private bool environmentScaleEnabled;
    private float environmentScale = 1f;

    // =========================================================
    // WINDOW
    // =========================================================

    [MenuItem("Tools/TileWorld Creator")]
    public static void ShowWindow()
    {
        TileWorldCreatorGUI window =
            GetWindow<TileWorldCreatorGUI>(
                "TileWorld Creator"
            );

        window.minSize =
            new Vector2(300, 500);

        window.Show();
    }

    // =========================================================
    // ENABLE
    // =========================================================

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;

        FindWorldRoot();
        FindOrCreateBrush();

        levelUI =
            new LevelPainterUI(brush);

        environmentUI =
            new EnvironmentPainterUI(brush);

        toolUI =
            new ToolPanelUI(brush);

        levelUI.LoadBiomes();
        environmentUI.LoadBiomes();

        RefreshLevelsAndLayers();
        SyncBrush();
    }

    // =========================================================
    // DISABLE
    // =========================================================

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;

        brush?.ClearAll();
    }

    private void OnDestroy()
    {
        brush?.ClearAll();
    }

    // =========================================================
    // SCENE GUI
    // =========================================================

    private void OnSceneGUI(SceneView sceneView)
    {
        if (brush == null)
        {
            FindOrCreateBrush();
            SyncBrush();
        }

        // IMPORTANT:
        // GUI does not select or assign a layer.
        // TileBrush decides the destination itself.

        brush?.OnSceneGUI(sceneView);
    }

    // =========================================================
    // MAIN GUI
    // =========================================================

    private void OnGUI()
    {
        if (brush == null)
            FindOrCreateBrush();

        FindWorldRoot();

        // =====================================================
        // NO WORLD
        // =====================================================

        if (worldRoot == null)
        {
            DrawCreateWorldScreen();
            return;
        }

        RefreshLevelsAndLayers();

        scrollPosition =
            EditorGUILayout.BeginScrollView(
                scrollPosition
            );

        DrawHeader();

        EditorGUILayout.Space(4);

        DrawLevelManagement();

        EditorGUILayout.Space(4);

        DrawLayerManagement();

        EditorGUILayout.Space(5);

        DrawModeSelection();

        if (currentMode == PaintMode.Level)
        {
            levelUI?.Draw();
        }
        else
        {
            environmentUI?.Draw();

            EditorGUILayout.Space(5);

            DrawEnvironmentTransform();
        }

        toolUI?.Draw(
            isLevelMode:
                currentMode == PaintMode.Level,

            currentLayer:
                GetPaintLayer(),

            onClearLevel:
                ClearCurrentPaintLayer,

            onClearEnvironment:
                ClearEnvironment
        );

        DrawStatusBar();

        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    // CREATE WORLD SCREEN
    // =========================================================

    private void DrawCreateWorldScreen()
    {
        EditorGUILayout.Space(60);

        GUIStyle titleStyle =
            new GUIStyle(
                EditorStyles.boldLabel
            )
            {
                alignment =
                    TextAnchor.MiddleCenter,

                fontSize = 20
            };

        GUIStyle textStyle =
            new GUIStyle(
                EditorStyles.label
            )
            {
                alignment =
                    TextAnchor.MiddleCenter,

                wordWrap = true
            };

        GUILayout.Label(
            "TileWorld Creator",
            titleStyle
        );

        EditorGUILayout.Space(10);

        GUILayout.Label(
            "No World has been created yet.",
            textStyle
        );

        EditorGUILayout.Space(25);

        GUI.backgroundColor =
            new Color(
                0.3f,
                0.75f,
                0.35f
            );

        if (
            GUILayout.Button(
                "CREATE WORLD",
                GUILayout.Height(45)
            ))
        {
            CreateWorldRoot();
        }

        GUI.backgroundColor =
            Color.white;

        EditorGUILayout.Space(15);

        EditorGUILayout.HelpBox(
            "Create a World to start building your TileWorld.",
            MessageType.Info
        );
    }

    // =========================================================
    // ENVIRONMENT TRANSFORM
    // =========================================================

    private void DrawEnvironmentTransform()
    {
        EditorGUILayout.LabelField(
            "Environment Transform",
            EditorStyles.boldLabel
        );

        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox
        );

        // =====================================================
        // ROTATION
        // =====================================================

        EditorGUILayout.BeginHorizontal();

        environmentRotationEnabled =
            EditorGUILayout.Toggle(
                environmentRotationEnabled,
                GUILayout.Width(20)
            );

        EditorGUILayout.LabelField(
            "Rotation",
            GUILayout.Width(65)
        );

        if (environmentRotationEnabled)
        {
            environmentRandomRotation =
                EditorGUILayout.ToggleLeft(
                    "Random",
                    environmentRandomRotation,
                    GUILayout.Width(70)
                );

            if (!environmentRandomRotation)
            {
                environmentRotation =
                    EditorGUILayout.Slider(
                        environmentRotation,
                        0f,
                        360f
                    );

                GUILayout.Label(
                    $"{environmentRotation:F0}°",
                    GUILayout.Width(38)
                );
            }
        }
        else
        {
            GUI.enabled = false;

            EditorGUILayout.ToggleLeft(
                "Random",
                environmentRandomRotation,
                GUILayout.Width(70)
            );

            GUI.enabled = true;
        }

        EditorGUILayout.EndHorizontal();

        // =====================================================
        // SCALE
        // =====================================================

        EditorGUILayout.BeginHorizontal();

        environmentScaleEnabled =
            EditorGUILayout.Toggle(
                environmentScaleEnabled,
                GUILayout.Width(20)
            );

        EditorGUILayout.LabelField(
            "Scale",
            GUILayout.Width(65)
        );

        if (environmentScaleEnabled)
        {
            environmentScale =
                EditorGUILayout.Slider(
                    environmentScale,
                    0.1f,
                    3f
                );

            GUILayout.Label(
                $"{environmentScale:F2}x",
                GUILayout.Width(42)
            );
        }
        else
        {
            GUI.enabled = false;

            EditorGUILayout.LabelField(
                "Original size",
                EditorStyles.miniLabel
            );

            GUI.enabled = true;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        SyncEnvironmentTransform();
    }

    private void SyncEnvironmentTransform()
    {
        if (brush == null)
            return;

        brush.environmentRotationEnabled =
            environmentRotationEnabled;

        brush.environmentRandomRotation =
            environmentRandomRotation;

        brush.environmentRotation =
            environmentRotation;

        brush.environmentScaleEnabled =
            environmentScaleEnabled;

        brush.environmentScale =
            environmentScale;
    }

    // =========================================================
    // LEVEL MANAGEMENT
    // =========================================================

    private void DrawLevelManagement()
    {
        EditorGUILayout.LabelField(
            "Level",
            EditorStyles.boldLabel
        );

        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox
        );

        Level currentLevel =
            GetCurrentLevel();

        // =====================================================
        // LEVEL SELECT
        // =====================================================

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(
            "Current:",
            GUILayout.Width(55)
        );

        if (availableLevels.Count > 0)
        {
            string[] levelNames =
                new string[
                    availableLevels.Count
                ];

            for (
                int i = 0;
                i < availableLevels.Count;
                i++)
            {
                Level level =
                    availableLevels[i];

                if (level == null)
                {
                    levelNames[i] =
                        "Missing";

                    continue;
                }

                string star =
                    level.Height == 0f
                        ? "★ "
                        : "";

                levelNames[i] =
                    $"{star}{level.LevelName} " +
                    $"(Y {level.Height:F1})";
            }

            int newIndex =
                EditorGUILayout.Popup(
                    selectedLevelIndex,
                    levelNames
                );

            if (
                newIndex !=
                selectedLevelIndex)
            {
                selectedLevelIndex =
                    newIndex;

                SelectLevel(
                    availableLevels[
                        selectedLevelIndex
                    ]
                );
            }
        }
        else
        {
            EditorGUILayout.LabelField(
                "No levels"
            );
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // =====================================================
        // LEVEL INFO
        // =====================================================

        if (currentLevel != null)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(
                $"Y = {currentLevel.Height:F1}",
                EditorStyles.miniLabel
            );

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField(
                "3 fixed layers",
                EditorStyles.miniLabel,
                GUILayout.Width(90)
            );

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);

        // =====================================================
        // LEVEL CONTROLS
        // =====================================================

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(
            "Step:",
            GUILayout.Width(35)
        );

        levelHeight =
            EditorGUILayout.FloatField(
                levelHeight,
                GUILayout.Width(55)
            );

        if (levelHeight <= 0f)
            levelHeight = 1f;

        GUILayout.FlexibleSpace();

        GUI.backgroundColor =
            new Color(
                0.3f,
                0.8f,
                0.3f
            );

        if (
            GUILayout.Button(
                "▲",
                GUILayout.Width(35),
                GUILayout.Height(24)
            ))
        {
            CreateLevelAbove();
        }

        GUI.backgroundColor =
            new Color(
                0.3f,
                0.6f,
                0.9f
            );

        if (
            GUILayout.Button(
                "▼",
                GUILayout.Width(35),
                GUILayout.Height(24)
            ))
        {
            CreateLevelBelow();
        }

        GUI.backgroundColor =
            Color.white;

        bool canRemove =
            currentLevel != null &&
            currentLevel.Height != 0f &&
            availableLevels.Count > 1;

        GUI.enabled =
            canRemove;

        GUI.backgroundColor =
            new Color(
                0.85f,
                0.3f,
                0.3f
            );

        if (
            GUILayout.Button(
                "✕",
                GUILayout.Width(35),
                GUILayout.Height(24)
            ))
        {
            RemoveSelectedLevel();
        }

        GUI.backgroundColor =
            Color.white;

        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // LAYERS
    // =========================================================

    private void DrawLayerManagement()
    {
        EditorGUILayout.LabelField(
            "Layers",
            EditorStyles.boldLabel
        );

        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox
        );

        Level level =
            GetCurrentLevel();

        if (level == null)
        {
            EditorGUILayout.HelpBox(
                "No Level.",
                MessageType.Info
            );

            EditorGUILayout.EndVertical();
            return;
        }

        // -----------------------------------------------------
        // Ground
        // -----------------------------------------------------

        DrawLayerInfo(
            "Ground",
            level.GetGroundLayer()
        );

        // -----------------------------------------------------
        // Liquid
        // -----------------------------------------------------

        DrawLayerInfo(
            "Liquid",
            level.GetLiquidLayer()
        );

        // -----------------------------------------------------
        // Environment
        // -----------------------------------------------------

        DrawLayerInfo(
            "Environment",
            level.GetEnvironmentLayer()
        );

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // LAYER INFO
    // =========================================================

    private void DrawLayerInfo(
        string name,
        Layer layer)
    {
        EditorGUILayout.BeginHorizontal(
            EditorStyles.helpBox
        );

        EditorGUILayout.LabelField(
            name,
            EditorStyles.boldLabel
        );

        GUILayout.FlexibleSpace();

        if (layer == null)
        {
            EditorGUILayout.LabelField(
                "Missing",
                EditorStyles.miniLabel,
                GUILayout.Width(55)
            );

            EditorGUILayout.EndHorizontal();
            return;
        }

        int tileCount =
            layer.Tiles != null
                ? layer.Tiles.Count
                : 0;

        int displayCount =
            layer.DisplayTiles != null
                ? layer.DisplayTiles.Count
                : 0;

        EditorGUILayout.LabelField(
            $"Tiles: {tileCount}",
            EditorStyles.miniLabel,
            GUILayout.Width(65)
        );

        if (
            GUILayout.Button(
                "Clear",
                GUILayout.Width(50)
            ))
        {
            ClearLayer(layer);
        }

        EditorGUILayout.EndHorizontal();

        if (displayCount > 0)
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Space(15);

            EditorGUILayout.LabelField(
                $"Display Tiles: {displayCount}",
                EditorStyles.miniLabel
            );

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(2);
    }

    // =========================================================
    // CLEAR LAYER
    // =========================================================

    private void ClearLayer(Layer layer)
    {
        if (layer == null)
            return;

        int tileCount =
            layer.Tiles != null
                ? layer.Tiles.Count
                : 0;

        int displayCount =
            layer.DisplayTiles != null
                ? layer.DisplayTiles.Count
                : 0;

        if (
            tileCount == 0 &&
            displayCount == 0)
        {
            return;
        }

        if (
            !EditorUtility.DisplayDialog(
                "Clear Layer",
                $"Clear layer '{layer.LayerName}'?",
                "Clear",
                "Cancel"
            ))
        {
            return;
        }

        Undo.RegisterCompleteObjectUndo(
            layer,
            "Clear Layer"
        );

        layer.ClearAllTiles();

        EditorUtility.SetDirty(layer);

        RefreshLevelsAndLayers();

        Repaint();

        SceneView.RepaintAll();
    }

    // =========================================================
    // HEADER
    // =========================================================

    private void DrawHeader()
    {
        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();

        GUILayout.Label(
            "🏗️ TileWorld Creator",
            EditorStyles.boldLabel
        );

        GUILayout.FlexibleSpace();

        if (
            worldRoot != null &&
            GUILayout.Button(
                "📍 Focus",
                GUILayout.Width(60)
            ))
        {
            Selection.activeGameObject =
                worldRoot.gameObject;

            EditorGUIUtility.PingObject(
                worldRoot
            );
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
    }

    // =========================================================
    // MODE
    // =========================================================

    private void DrawModeSelection()
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(
            "Mode:",
            GUILayout.Width(45)
        );

        GUI.backgroundColor =
            currentMode == PaintMode.Level
                ? new Color(
                    0.35f,
                    0.85f,
                    0.35f
                )
                : Color.gray;

        if (
            GUILayout.Button(
                "🧱 Tiles",
                GUILayout.Height(28)
            ))
        {
            currentMode =
                PaintMode.Level;

            if (brush != null)
                brush.paintMode =
                    "Level";

            levelUI?.SyncToBrush(
                brush
            );
        }

        GUI.backgroundColor =
            currentMode ==
            PaintMode.Environment
                ? new Color(
                    0.35f,
                    0.65f,
                    0.95f
                )
                : Color.gray;

        if (
            GUILayout.Button(
                "🌳 Environment",
                GUILayout.Height(28)
            ))
        {
            currentMode =
                PaintMode.Environment;

            if (brush != null)
                brush.paintMode =
                    "Environment";

            environmentUI?.SyncToBrush(
                brush
            );
        }

        GUI.backgroundColor =
            Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
    }

    // =========================================================
    // STATUS
    // =========================================================

    private void DrawStatusBar()
    {
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal(
            EditorStyles.helpBox
        );

        string modeIcon =
            currentMode == PaintMode.Level
                ? "🧱"
                : "🌳";

        string status =
            $"{modeIcon} Ready";

        if (worldRoot == null)
        {
            status =
                "❌ No World";
        }
        else if (GetPaintLayer() == null)
        {
            status =
                "⚠️ No Layer";
        }
        else if (
            brush != null &&
            brush.IsActive)
        {
            status =
                $"{modeIcon} Brush Active";
        }

        GUILayout.Label(
            status,
            GUILayout.Width(130)
        );

        GUILayout.FlexibleSpace();

        Level level =
            GetCurrentLevel();

        if (level != null)
        {
            GUILayout.Label(
                $"Y {level.Height:F1}",
                GUILayout.Width(55)
            );
        }

        Layer paintLayer =
            GetPaintLayer();

        if (paintLayer != null)
        {
            GUILayout.Label(
                $"🟦 {paintLayer.Tiles.Count}",
                GUILayout.Width(55)
            );
        }

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================
    // SELECT LEVEL
    // =========================================================

    private void SelectLevel(Level level)
    {
        if (level == null)
            return;

        if (worldRoot?.Levels != null)
        {
            int realIndex =
                worldRoot.Levels.Levels.IndexOf(
                    level
                );

            if (realIndex >= 0)
            {
                worldRoot.Levels.SetActiveLevel(
                    realIndex
                );
            }
        }

        selectedLevelIndex =
            availableLevels.IndexOf(level);

        if (selectedLevelIndex < 0)
            selectedLevelIndex = 0;

        RefreshLevelsAndLayers();

        SyncBrush();

        Repaint();

        SceneView.RepaintAll();
    }

    // =========================================================
    // CURRENT LEVEL
    // =========================================================

    private Level GetCurrentLevel()
    {
        if (
            availableLevels.Count > 0 &&
            selectedLevelIndex >= 0 &&
            selectedLevelIndex <
                availableLevels.Count)
        {
            return
                availableLevels[
                    selectedLevelIndex
                ];
        }

        return null;
    }

    // =========================================================
    // PAINT LAYER
    // =========================================================
    //
    // This is only for GUI information / clearing.
    //
    // The actual Tile placement is handled by TileBrush.
    //

    private Layer GetPaintLayer()
    {
        Level level =
            GetCurrentLevel();

        if (level == null)
            return null;

        if (
            currentMode ==
            PaintMode.Environment)
        {
            return level.GetEnvironmentLayer();
        }

        if (
            brush != null &&
            brush.currentTileType ==
            "Liquid")
        {
            return level.GetLiquidLayer();
        }

        return level.GetGroundLayer();
    }

    // =========================================================
    // REFRESH
    // =========================================================

    private void RefreshLevelsAndLayers()
    {
        availableLevels.Clear();
        availableLayers.Clear();

        if (worldRoot?.Levels == null)
            return;

        foreach (
            Level level
            in worldRoot.Levels.Levels)
        {
            if (level != null)
                availableLevels.Add(level);
        }

        // =====================================================
        // SORT LEVELS BY HEIGHT
        // =====================================================

        availableLevels.Sort(
            (a, b) =>
                b.Height.CompareTo(
                    a.Height
                )
        );

        if (availableLevels.Count == 0)
        {
            selectedLevelIndex = 0;
            return;
        }

        selectedLevelIndex =
            Mathf.Clamp(
                selectedLevelIndex,
                0,
                availableLevels.Count - 1
            );

        Level currentLevel =
            GetCurrentLevel();

        if (currentLevel == null)
            return;

        // =====================================================
        // FIXED LAYERS
        // =====================================================

        Layer ground =
            currentLevel.GetGroundLayer();

        Layer liquid =
            currentLevel.GetLiquidLayer();

        Layer environment =
            currentLevel.GetEnvironmentLayer();

        if (ground != null)
            availableLayers.Add(ground);

        if (liquid != null)
            availableLayers.Add(liquid);

        if (environment != null)
            availableLayers.Add(environment);
    }

    // =========================================================
    // WORLD ROOT
    // =========================================================

    private void FindWorldRoot()
    {
        worldRoot =
            WorldRoot.FindInScene();

        if (worldRoot == null)
        {
            GameObject root =
                GameObject.Find(
                    WorldRoot.WorldObjectName
                );

            if (root != null)
            {
                worldRoot =
                    root.GetComponent<WorldRoot>();
            }
        }
    }

    // =========================================================
    // BRUSH
    // =========================================================

    private void FindOrCreateBrush()
    {
        if (brush == null)
            brush = new TileBrush();
    }

    // =========================================================
    // CREATE WORLD
    // =========================================================

    private void CreateWorldRoot()
    {
        GameObject rootObject =
            new GameObject(
                WorldRoot.WorldObjectName
            );

        worldRoot =
            rootObject.AddComponent<WorldRoot>();

        worldRoot.SetWorldName(
            "MyWorld"
        );

        LevelsRoot levels =
            worldRoot.FindOrCreateLevels();

        Level baseLevel =
            levels.CreateLevel(
                "Level_01",
                0f
            );

        // Ensure fixed:
        // Ground
        // Liquid
        // Environment
        baseLevel.EnsureLayers();

        Selection.activeGameObject =
            rootObject;

        RefreshLevelsAndLayers();

        SyncBrush();

        Repaint();

        SceneView.RepaintAll();
    }

    // =========================================================
    // LEVEL ABOVE
    // =========================================================

    private void CreateLevelAbove()
    {
        if (worldRoot?.Levels == null)
        {
            CreateWorldRoot();
            return;
        }

        float maxHeight =
            float.MinValue;

        foreach (
            Level level
            in availableLevels)
        {
            if (level.Height > maxHeight)
                maxHeight =
                    level.Height;
        }

        if (
            availableLevels.Count == 0 ||
            maxHeight == float.MinValue)
        {
            maxHeight = 0f;
        }

        float newHeight =
            maxHeight +
            levelHeight;

        Level newLevel =
            worldRoot.Levels.CreateLevel(
                $"Level_{availableLevels.Count + 1:00}",
                newHeight
            );

        newLevel.EnsureLayers();

        RefreshLevelsAndLayers();

        int index =
            availableLevels.IndexOf(
                newLevel
            );

        if (index >= 0)
        {
            selectedLevelIndex =
                index;

            SelectLevel(
                newLevel
            );
        }

        EditorGUIUtility.PingObject(
            newLevel
        );
    }

    // =========================================================
    // LEVEL BELOW
    // =========================================================

    private void CreateLevelBelow()
    {
        if (worldRoot?.Levels == null)
        {
            CreateWorldRoot();
            return;
        }

        float minHeight =
            float.MaxValue;

        foreach (
            Level level
            in availableLevels)
        {
            if (level.Height < minHeight)
                minHeight =
                    level.Height;
        }

        if (
            availableLevels.Count == 0 ||
            minHeight == float.MaxValue)
        {
            minHeight = 0f;
        }

        float newHeight =
            minHeight -
            levelHeight;

        Level newLevel =
            worldRoot.Levels.CreateLevel(
                $"Level_{availableLevels.Count + 1:00}",
                newHeight
            );

        newLevel.EnsureLayers();

        RefreshLevelsAndLayers();

        int index =
            availableLevels.IndexOf(
                newLevel
            );

        if (index >= 0)
        {
            selectedLevelIndex =
                index;

            SelectLevel(
                newLevel
            );
        }

        EditorGUIUtility.PingObject(
            newLevel
        );
    }

    // =========================================================
    // REMOVE LEVEL
    // =========================================================

    private void RemoveSelectedLevel()
    {
        Level level =
            GetCurrentLevel();

        if (level == null)
            return;

        if (level.Height == 0f)
        {
            EditorUtility.DisplayDialog(
                "Cannot Remove",
                "Base Level (Y: 0) cannot be removed!",
                "OK"
            );

            return;
        }

        if (
            EditorUtility.DisplayDialog(
                "Remove Level",
                $"Delete level \"{level.LevelName}\" " +
                $"at height {level.Height:F1}?",
                "Yes",
                "No"
            ))
        {
            worldRoot.Levels.RemoveLevel(
                level
            );

            selectedLevelIndex =
                Mathf.Max(
                    0,
                    selectedLevelIndex - 1
                );

            RefreshLevelsAndLayers();

            SyncBrush();

            Repaint();

            SceneView.RepaintAll();
        }
    }

    // =========================================================
    // SYNC
    // =========================================================

    private void SyncBrush()
    {
        if (brush == null)
        {
            FindOrCreateBrush();
            return;
        }

        levelUI?.SyncToBrush(
            brush
        );

        environmentUI?.SyncToBrush(
            brush
        );

        brush.paintMode =
            currentMode.ToString();

        // IMPORTANT:
        // There is NO:
        //
        // brush.targetLayer = ...
        //
        // TileBrush decides the target layer itself.

        brush.environmentRotationEnabled =
            environmentRotationEnabled;

        brush.environmentRandomRotation =
            environmentRandomRotation;

        brush.environmentRotation =
            environmentRotation;

        brush.environmentScaleEnabled =
            environmentScaleEnabled;

        brush.environmentScale =
            environmentScale;
    }

    // =========================================================
    // CLEAR CURRENT PAINT LAYER
    // =========================================================

    private void ClearCurrentPaintLayer()
    {
        Layer layer =
            GetPaintLayer();

        if (layer == null)
            return;

        ClearLayer(layer);
    }

    // =========================================================
    // CLEAR ENVIRONMENT
    // =========================================================

    private void ClearEnvironment()
    {
        Level level =
            GetCurrentLevel();

        if (level == null)
            return;

        Layer environment =
            level.GetEnvironmentLayer();

        if (environment == null)
            return;

        Transform environmentTransform =
            environment.transform;

        if (
            environmentTransform.childCount == 0)
        {
            return;
        }

        if (
            !EditorUtility.DisplayDialog(
                "Clear Environment",
                $"Delete all environment objects " +
                $"from '{level.LevelName}'?",
                "Clear",
                "Cancel"
            ))
        {
            return;
        }

        // Keep fixed category folders.
        // Delete only objects inside them.

        EnvironmentCategory[] categories =
        {
            EnvironmentCategory.Rocks,
            EnvironmentCategory.Trees,
            EnvironmentCategory.Vegetation,
            EnvironmentCategory.Props
        };

        Undo.RegisterFullObjectHierarchyUndo(
            environment.gameObject,
            "Clear Environment"
        );

        foreach (
            EnvironmentCategory category
            in categories)
        {
            Transform categoryTransform =
                environment.GetEnvironmentCategory(
                    category
                );

            if (categoryTransform == null)
                continue;

            for (
                int i =
                    categoryTransform.childCount - 1;
                i >= 0;
                i--)
            {
                GameObject child =
                    categoryTransform
                        .GetChild(i)
                        .gameObject;

                Undo.DestroyObjectImmediate(
                    child
                );
            }
        }

        EditorUtility.SetDirty(
            environment
        );

        Repaint();

        SceneView.RepaintAll();
    }

    // =========================================================
    // INSPECTOR UPDATE
    // =========================================================

    private void OnInspectorUpdate()
    {
        Repaint();
    }
}

}
