using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    public class LandscapeLevelManagerWindow : EditorWindow
    {
        private DualGrid3D target;
        private WorldRoot worldRoot;
        private EnvironmentRoot environmentRoot;
        private SerializedObject serializedTarget;
        private SerializedProperty levelsProperty;

        private Vector2 scrollPosition;
        private bool exitGuiRequested;
        private WorldCoreEditorMode activeMode;
        private readonly Dictionary<int, bool> expandedLevels = new();
        private GUIStyle layersBoxStyle;
        private GUIStyle levelBlockLayoutStyle;

        private static LandscapeLevelManagerWindow instance;

        private float environmentHeaderBottom = 220f;
        private float landscapeLevelsScrollHeight = 200f;

        private const float UiElementSpacing = 20f;
        private const float LevelRowEdgeInset = 10f;
        private const float LevelRowCheckboxLeftInset = 15f;
        private const float LayerRowTightSpacing = 5f;

        public static bool TryGetLandscapeTarget(out DualGrid3D paintTarget)
        {
            paintTarget = instance != null && instance.target != null ? instance.target : null;
            return paintTarget != null;
        }

        public static bool TryGetEnvironmentTarget(out EnvironmentRoot environmentTarget)
        {
            environmentTarget = instance != null && instance.environmentRoot != null ? instance.environmentRoot : null;
            return environmentTarget != null;
        }

        public static bool TryGetPaintTarget(out DualGrid3D paintTarget) =>
            TryGetLandscapeTarget(out paintTarget);

        public static void RequestRepaintIfOpen()
        {
            if (instance != null)
                instance.Repaint();
        }

    #if UNITY_EDITOR
        [InitializeOnLoad]
        static class BiomeRegistryRepaintHook
        {
            static BiomeRegistryRepaintHook()
            {
                BiomeRegistry.Invalidated += RequestRepaintIfOpen;
                BiomeEnvironmentLibraryRegistry.Invalidated += RequestRepaintIfOpen;
            }
        }
    #endif

        [MenuItem("Window/Aglen Realms/World Core")]
        public static void ShowWindow()
        {
            LandscapeLevelManagerWindow window = GetWindow<LandscapeLevelManagerWindow>();
            window.titleContent = new GUIContent("World Core");
            window.minSize = new Vector2(520f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            instance = this;
            activeMode = WorldCoreWindowTabs.LoadActiveMode();
            WorldCoreEditorSession.SetActiveEditorMode(activeMode);
            BiomeRegistry.Invalidate();
            TryAssignTargetFromSceneOrSelection();
            ResolveEnvironmentContext();
            ResolveLandscapeContext();
            BuildReorderableList();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnFocus()
        {
            BiomeRegistry.Invalidate();
            Repaint();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (instance == this)
                instance = null;
        }

        private void OnEditorUpdate()
        {
            if (string.IsNullOrEmpty(transientNotificationMessage))
                return;

            if (EditorApplication.timeSinceStartup >= transientNotificationExpireTime)
                transientNotificationMessage = null;
            else
                Repaint();
        }

        private void OnSelectionChange()
        {
            if (activeMode == WorldCoreEditorMode.Environment)
                ResolveEnvironmentContext();
            else
                ResolveLandscapeContext();

            if (target != null || worldRoot != null || environmentRoot != null)
                BuildReorderableList();

            Repaint();
        }

        private void OnHierarchyChange()
        {
            if (activeMode == WorldCoreEditorMode.Environment)
                ResolveEnvironmentContext();
            else
                ResolveLandscapeContext();

            Repaint();
        }

        private Vector2 lastHoverMousePosition = new Vector2(float.MinValue, float.MinValue);
        private readonly List<int> textFieldControlIds = new();
        private static MethodInfo endEditingActiveTextFieldMethod;

        private string transientNotificationMessage;
        private double transientNotificationExpireTime;

        private const double TransientNotificationDurationSeconds = 1.5;
        private const double TransientNotificationFadeSeconds = 0.35;

        private void OnGUI()
        {
            exitGuiRequested = false;
            InitStyles();
            HandleInstantHoverRepaint();
            textFieldControlIds.Clear();

            EditorGUILayout.Space(6f);

            activeMode = WorldCoreWindowTabs.Draw(activeMode);
            WorldCoreEditorSession.SetActiveEditorMode(activeMode);

            if (activeMode == WorldCoreEditorMode.Environment)
            {
                DrawEnvironmentModeGUI();
                TryHandleEnvironmentEscapeInWindow();
                TryDefocusTextFieldsOnInteraction();
                DrawTransientNotificationOverlay();

                if (exitGuiRequested)
                    GUIUtility.ExitGUI();
                return;
            }

            DrawLandscapeModeGUI();
            TryHandleBrushEscapeInWindow();
            TryDefocusTextFieldsOnInteraction();
            DrawTransientNotificationOverlay();

            if (exitGuiRequested)
                GUIUtility.ExitGUI();
        }

        internal static bool TryDeactivateBrushFromEscape()
        {
            if (WorldCoreEditorSession.ActiveEditorMode != WorldCoreEditorMode.Landscape)
                return false;

            DualGrid3D paintTarget = WorldCoreEditorSession.PaintTarget;
            if (paintTarget == null || !paintTarget.IsLevelPaintingActive)
                return false;

            if (instance != null && instance.target == paintTarget)
                instance.SetBrushPaintingActive(false, showEscNotification: true);
            else
                DeactivateBrushOnTarget(paintTarget);

            return true;
        }

        internal static bool TryDeactivateEnvironmentPaintFromEscape()
        {
            if (WorldCoreEditorSession.ActiveEditorMode != WorldCoreEditorMode.Environment)
                return false;

            if (!EnvironmentPainterState.TryDeactivatePainting())
                return false;

            if (instance != null)
                instance.ShowEnvironmentPaintDisabledNotification();

            SceneView.RepaintAll();
            RequestRepaintIfOpen();
            return true;
        }

        internal static bool TryHandleGlobalPaintingEscape()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Escape)
                return false;

            if (EditorGUIUtility.editingTextField)
                return false;

            if (instance != null && instance.HasTrackedTextFieldFocus())
            {
                ClearTextFieldFocus();
                instance.Repaint();
                e.Use();
                return true;
            }

            if (!TryDeactivateBrushFromEscape() && !TryDeactivateEnvironmentPaintFromEscape())
                return false;

            e.Use();
            return true;
        }

        internal static void HandleLeavingEditorMode(WorldCoreEditorMode mode)
        {
            switch (mode)
            {
                case WorldCoreEditorMode.Landscape:
                    DeactivateLandscapeBrushForModeSwitch();
                    break;
                case WorldCoreEditorMode.Environment:
                    DeactivateEnvironmentPaintForModeSwitch();
                    break;
            }
        }

        private static void DeactivateLandscapeBrushForModeSwitch()
        {
            WorldCoreSceneToolController.CancelActiveOperations();

            if (!TryResolveLandscapeTargetForModeSwitch(out DualGrid3D paintTarget) ||
                !paintTarget.IsLevelPaintingActive)
                return;

            if (instance != null && instance.target == paintTarget)
                instance.SetBrushPaintingActive(false, recordUndo: false);
            else
                DeactivateBrushOnTarget(paintTarget, recordUndo: false);

            instance?.ShowTransientNotification("Landscape brush disabled — switched to Environment.");
            RequestRepaintIfOpen();
        }

        private static void DeactivateEnvironmentPaintForModeSwitch()
        {
            WorldCoreSceneToolController.CancelActiveOperations();

            if (!EnvironmentPainterState.TryDeactivatePainting())
                return;

            instance?.ShowTransientNotification("Environment prefab deselected — switched to Landscape.");
            RequestRepaintIfOpen();
        }

        private static bool TryResolveLandscapeTargetForModeSwitch(out DualGrid3D paintTarget)
        {
            if (TryGetLandscapeTarget(out paintTarget))
                return true;

    #if UNITY_2023_1_OR_NEWER
            paintTarget = Object.FindFirstObjectByType<DualGrid3D>();
    #else
            paintTarget = Object.FindObjectOfType<DualGrid3D>();
    #endif
            return paintTarget != null;
        }

        private void RequestExitGUI() => exitGuiRequested = true;

        internal float EnvironmentHeaderBottom => environmentHeaderBottom;

        internal void RecordEnvironmentHeaderBottom(float yMax)
        {
            if (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout)
                environmentHeaderBottom = yMax;
        }

        private void DrawLandscapeModeGUI()
        {
            ResolveLandscapeContext();
            if (target == null)
                TryAssignLandscapeTargetFromScene();
            DrawLegacyMigrationBanner();

            bool bindingInvalid = worldRoot == null || target == null;
            string bindingSummary =
                $"World: {WorldCoreSceneBindingUI.FormatName(worldRoot, "—")}  ·  Landscape: {WorldCoreSceneBindingUI.FormatName(target, "—")}";
            bool bindingExpanded = WorldCoreSceneBindingUI.DrawFoldout(
                WorldCoreSceneBindingUI.LandscapeExpandedPrefKey,
                bindingSummary,
                forceOpen: bindingInvalid);

            if (bindingExpanded)
                DrawLandscapeBindingFields();

            if (target == null)
            {
                DrawModeContentWithoutTarget();
                return;
            }

            if (target is not LandscapeRoot && target.GetComponentInParent<WorldRoot>() == null)
            {
                EditorGUILayout.HelpBox(
                    "Legacy DualGrid3D target. Use the Migrate Legacy World button below to move it under World Root/Landscape.",
                    MessageType.Warning);
                EditorGUILayout.Space(4f);
            }

            DrawTargetUI(bindingExpanded);
        }

        private void DrawLandscapeBindingFields()
        {
            EditorGUI.BeginChangeCheck();
            WorldRoot newWorldRoot = (WorldRoot)EditorGUILayout.ObjectField(
                "Target World",
                worldRoot,
                typeof(WorldRoot),
                true);

            if (EditorGUI.EndChangeCheck())
            {
                worldRoot = newWorldRoot;
                target = null;
                if (worldRoot != null && worldRoot.TryGetLandscape(out LandscapeRoot landscape))
                    target = landscape;
                BuildReorderableList();
            }

            if (worldRoot != null)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("Landscape", target, typeof(LandscapeRoot), true);
                EditorGUI.EndDisabledGroup();
            }
        }

        private void DrawLegacyMigrationBanner()
        {
            if (!LegacyWorldMigration.TryFindLegacyDualGrid(out DualGrid3D legacy) || worldRoot != null)
                return;

            EditorGUILayout.HelpBox(
                $"Legacy World detected on '{legacy.gameObject.name}'. Migrate to World Root/Landscape + Environment structure.",
                MessageType.Warning);

            if (GUILayout.Button("Migrate Legacy World", GUILayout.Height(24f)))
            {
                if (LegacyWorldMigration.TryMigrate(legacy, out WorldRoot migratedWorld))
                {
                    worldRoot = migratedWorld;
                    migratedWorld.TryGetLandscape(out LandscapeRoot landscape);
                    target = landscape;
                    BuildReorderableList();
                    Selection.activeGameObject = migratedWorld.gameObject;
                }

                RequestExitGUI();
            }

            EditorGUILayout.Space(4f);
        }

        private void ResolveLandscapeContext()
        {
            if (Selection.activeGameObject != null)
            {
                WorldRoot selectedWorld = Selection.activeGameObject.GetComponentInParent<WorldRoot>();
                if (selectedWorld != null)
                    worldRoot = selectedWorld;

                LandscapeRoot selectedLandscape = Selection.activeGameObject.GetComponentInParent<LandscapeRoot>();
                if (selectedLandscape != null)
                {
                    target = selectedLandscape;
                    worldRoot = selectedLandscape.GetComponentInParent<WorldRoot>();
                    return;
                }

                DualGrid3D legacyLandscape = Selection.activeGameObject.GetComponentInParent<DualGrid3D>();
                if (legacyLandscape != null && legacyLandscape.GetComponentInParent<WorldRoot>() == null)
                    target = legacyLandscape;
            }

            if (worldRoot != null && worldRoot.TryGetLandscape(out LandscapeRoot landscape))
                target = landscape;
        }

        private bool TryAssignLandscapeTargetFromScene()
        {
            ResolveLandscapeContext();
            if (target != null)
                return true;

            WorldRoot existingWorld = WorldRoot.FindInScene();
            if (existingWorld != null)
            {
                worldRoot = existingWorld;
                if (existingWorld.TryGetLandscape(out LandscapeRoot landscape))
                {
                    target = landscape;
                    BuildReorderableList();
                    return true;
                }
            }

            if (LegacyWorldMigration.TryFindLegacyDualGrid(out DualGrid3D legacy))
            {
                target = legacy;
                BuildReorderableList();
                return true;
            }

            return false;
        }

        private void DrawEnvironmentModeGUI()
        {
            ResolveEnvironmentContext();
            if (worldRoot == null)
                TryAssignWorldRootFromSceneOrSelection();
            DrawLegacyMigrationBanner();

            DualGrid3D legacyWorld = FindLandscapeInOpenScenes();
            if (legacyWorld != null && worldRoot == null && legacyWorld.GetComponentInParent<WorldRoot>() == null)
            {
                EditorGUILayout.HelpBox(
                    "Legacy World (DualGrid3D) detected in the scene. Create a new World Root for Environment, or migrate the legacy world.",
                    MessageType.Info);
                EditorGUILayout.Space(4f);
            }

            if (worldRoot != null && environmentRoot == null)
                worldRoot.TryGetEnvironment(out environmentRoot);

            bool bindingInvalid = worldRoot == null || environmentRoot == null;
            string linkedName = environmentRoot != null
                ? WorldCoreSceneBindingUI.FormatName(environmentRoot.LinkedLandscape, "—")
                : "—";
            string bindingSummary =
                $"World: {WorldCoreSceneBindingUI.FormatName(worldRoot, "—")}  ·  Env: {WorldCoreSceneBindingUI.FormatName(environmentRoot, "—")}  ·  Linked: {linkedName}";
            bool bindingExpanded = WorldCoreSceneBindingUI.DrawFoldout(
                WorldCoreSceneBindingUI.EnvironmentExpandedPrefKey,
                bindingSummary,
                forceOpen: bindingInvalid);

            if (bindingExpanded)
                DrawEnvironmentBindingFields();

            if (worldRoot == null)
            {
                DrawEnvironmentEmptyState();
                return;
            }

            if (environmentRoot == null)
                worldRoot.TryGetEnvironment(out environmentRoot);

            if (environmentRoot == null)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox("This world has no Environment module yet.", MessageType.Info);
                if (GUILayout.Button("Create Environment Module", GUILayout.Height(26f)))
                {
                    environmentRoot = worldRoot.FindOrCreateEnvironment();
                    Selection.activeGameObject = environmentRoot.gameObject;
                    EditorUtility.SetDirty(worldRoot);
                    RequestExitGUI();
                }

                return;
            }

            EditorGUILayout.Space(2f);
            EnvironmentPainterPanel.DrawTabContent(environmentRoot, this);
        }

        private void DrawEnvironmentBindingFields()
        {
            EditorGUI.BeginChangeCheck();
            WorldRoot newWorldRoot = (WorldRoot)EditorGUILayout.ObjectField(
                "Target World",
                worldRoot,
                typeof(WorldRoot),
                true);

            if (EditorGUI.EndChangeCheck())
            {
                worldRoot = newWorldRoot;
                environmentRoot = null;
                if (worldRoot != null)
                    worldRoot.TryGetEnvironment(out environmentRoot);
            }

            if (worldRoot == null)
                return;

            if (environmentRoot == null)
                worldRoot.TryGetEnvironment(out environmentRoot);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Environment", environmentRoot, typeof(EnvironmentRoot), true);
            EditorGUI.EndDisabledGroup();

            if (environmentRoot != null)
                DrawEnvironmentLinkedLandscapeField(environmentRoot);
        }

        private void DrawEnvironmentLinkedLandscapeField(EnvironmentRoot environment)
        {
            EditorGUI.BeginChangeCheck();
            DualGrid3D linkedLandscape = (DualGrid3D)EditorGUILayout.ObjectField(
                "Linked Landscape",
                environment.LinkedLandscape,
                typeof(DualGrid3D),
                true);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(environment, "Set Linked Landscape");
                environment.LinkedLandscape = linkedLandscape;
                EditorUtility.SetDirty(environment);
            }
        }

        private void ResolveEnvironmentContext()
        {
            if (Selection.activeGameObject != null)
            {
                WorldRoot selectedWorld = Selection.activeGameObject.GetComponentInParent<WorldRoot>();
                if (selectedWorld != null)
                    worldRoot = selectedWorld;

                EnvironmentRoot selectedEnvironment = Selection.activeGameObject.GetComponentInParent<EnvironmentRoot>();
                if (selectedEnvironment != null)
                {
                    environmentRoot = selectedEnvironment;
                    worldRoot = selectedEnvironment.GetComponentInParent<WorldRoot>();
                }
            }

            if (worldRoot != null)
                worldRoot.TryGetEnvironment(out environmentRoot);
        }

        private bool TryAssignWorldRootFromSceneOrSelection()
        {
            ResolveEnvironmentContext();
            if (worldRoot != null)
                return true;

            WorldRoot existing = WorldRoot.FindInScene();
            if (existing == null)
                return false;

            worldRoot = existing;
            worldRoot.TryGetEnvironment(out environmentRoot);
            return true;
        }

        private void DrawModeContentWithoutTarget()
        {
            DrawEmptyState();
        }

        private void DrawEnvironmentEmptyState()
        {
            EditorGUILayout.HelpBox(
                "Create a World Root with an Environment module. Landscape is optional and can be added later.",
                MessageType.Info);

            EditorGUILayout.Space(8f);

            if (GUILayout.Button("Create Environment", GUILayout.Height(28f)))
            {
                CreateEnvironmentInScene();
                RequestExitGUI();
            }
        }

        private void DrawTargetUI(bool bindingExpanded)
        {
            if (serializedTarget == null || serializedTarget.targetObject != target)
                BuildReorderableList();

            serializedTarget.Update();

            if (bindingExpanded)
                DrawBakeStaticSettings();

            // Paint Session
            DrawSharedBiomeSelector();
            DrawLandscapeBrushSettings();
            DrawLandscapeStatusBar();

            EditorGUILayout.Space(4f);
            DrawToolbar(createLandscapeIfMissing: false);
            EditorGUILayout.Space(2f);

            float scrollHeight = CalculateLandscapeLevelsScrollHeight();
            scrollPosition = EditorGUILayout.BeginScrollView(
                scrollPosition,
                GUILayout.Height(scrollHeight));
            DrawLevelsHierarchy();
            EditorGUILayout.EndScrollView();

            DrawLandscapeCellsWarning();
            WorldCoreHelpFoldout.Draw(
                WorldCoreHelpFoldout.LandscapeExpandedPrefKey,
                LandscapeHelpLines,
                null);

            PersistSubLevelsSerialization();

            if (serializedTarget.ApplyModifiedProperties())
            {
                if (target.levels != null && target.levels.Count > 0)
                    target.RebuildLevelRoots();
            }

            if (GUI.changed)
                SceneView.RepaintAll();
        }

        private static readonly string[] LandscapeHelpLines =
        {
            "Alt — orbit camera (blocks painting)",
            "Esc — stop painting",
            "LMB — paint (brush active)",
            "Ctrl + LMB — erase",
            "Click layer row — select active layer",
            "Click layer again — stop painting",
            "Brush On — start painting",
        };

        private const float LandscapeLevelsMinScrollHeight = 120f;
        private const float LandscapeLevelsBottomPadding = 10f;

        private float CalculateLandscapeLevelsScrollHeight()
        {
            // Update on Repaint using previous layout's last rect to keep IMGUI height stable.
            if (Event.current.type == EventType.Repaint)
            {
                float yAfterToolbar = GUILayoutUtility.GetLastRect().yMax;
                float bottomReserve = WorldCoreHelpFoldout.GetReserveHeight(
                                          WorldCoreHelpFoldout.LandscapeExpandedPrefKey,
                                          LandscapeHelpLines,
                                          null)
                                      + LandscapeLevelsBottomPadding;
                float available = position.height - yAfterToolbar - bottomReserve;
                landscapeLevelsScrollHeight = Mathf.Max(LandscapeLevelsMinScrollHeight, available);
            }

            return Mathf.Max(LandscapeLevelsMinScrollHeight, landscapeLevelsScrollHeight);
        }

        private void DrawSharedBiomeSelector()
        {
            SerializedProperty brushBiomeProperty = serializedTarget.FindProperty("brushBiome");
            SerializedProperty brushBiomeIdProperty = serializedTarget.FindProperty("brushBiomeId");
            if (brushBiomeProperty == null || brushBiomeIdProperty == null)
                return;

            IReadOnlyList<BiomeDefinition> biomes = BiomeRegistry.All;
            if (biomes.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No biomes found. Import a biome pack, or create your own via Create → Aglen Realms → Biome Definition. See Documentation~/CUSTOM_BIOME_GUIDE.md in the World Core package.",
                    MessageType.Warning);
                return;
            }

            string activeBiomeId = BiomeTileLibrary.NormalizeBiomeId(
                target.brushBiomeId,
                target.brushBiome);
            int biomeIndex = GetDisplayBiomeIndex(biomes, activeBiomeId);
            string[] biomeNames = GetDisplayBiomeNames(biomes);

            EditorGUI.BeginChangeCheck();
            int newBiomeIndex = EditorGUILayout.Popup(
                new GUIContent("Biome", "Biome for landscape tile painting"),
                biomeIndex,
                biomeNames);

            if (EditorGUI.EndChangeCheck())
            {
                BiomeDefinition selected = biomes[newBiomeIndex];
                // Tool session only — must not create Undo entries (paint Undo must not roll biome back).
                target.brushBiomeId = selected.biomeId;
                if (BiomeRegistry.TryGetLegacyBiome(selected.biomeId, out BrushBiome legacyBiome))
                    target.brushBiome = legacyBiome;
                target.ApplyActiveBrushTiles();
                EditorUtility.SetDirty(target);
                serializedTarget.Update();
                brushBiomeIdProperty.stringValue = target.brushBiomeId;
                brushBiomeProperty.enumValueIndex = (int)target.brushBiome;
                serializedTarget.ApplyModifiedPropertiesWithoutUndo();
                SceneView.RepaintAll();
            }
            else if (!HasAssignedTilePrefabs())
            {
                // Refresh tiles without spamming the undo stack (every-repaint path).
                ApplyBrushSettings(recordUndo: false);
            }
        }

        private void DrawLandscapeBrushSettings()
        {
            SerializedProperty brushModeProperty = serializedTarget.FindProperty("brushMode");
            if (brushModeProperty == null)
                return;

            if (target.HasGroundAndLiquidSubLevels(target.ActiveLevelIndex))
            {
                LandscapeBrushMode expectedMode = DualGrid3D.ToBrushMode(
                    target.GetSubLevelLayerType(target.ActiveLevelIndex, target.ActiveSubLevelIndex));
                if (target.brushMode != expectedMode)
                {
                    target.SyncBrushModeFromActiveSubLevel();
                    brushModeProperty.enumValueIndex = (int)target.brushMode;
                    serializedTarget.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorGUILayout.BeginHorizontal();
            bool brushLockedToLayers = target.HasGroundAndLiquidSubLevels(target.ActiveLevelIndex);
            string brushTooltip = "Enable or disable landscape painting in Scene View. Esc disables brush.";
            EditorGUILayout.PrefixLabel(new GUIContent("Brush", brushTooltip));

            bool brushActive = target.IsLevelPaintingActive;
            if (DrawBrushOnOffButton(brushActive))
                SetBrushPaintingActive(!brushActive);

            GUILayout.FlexibleSpace();

            string tilesTooltip = brushLockedToLayers
                ? "Ground or Liquid tiles for the selected biome. Switch layer type by selecting a layer in Layers."
                : "Paint ground tiles or liquid tiles for the selected biome";
            GUILayout.Label(new GUIContent("Tiles", tilesTooltip), EditorStyles.label, GUILayout.ExpandWidth(false));

            LandscapeBrushMode currentMode = target.brushMode;
            LandscapeBrushMode selectedMode = currentMode;

            bool groundInteractable = !brushLockedToLayers || currentMode == LandscapeBrushMode.Ground;
            bool liquidInteractable = !brushLockedToLayers || currentMode == LandscapeBrushMode.Liquid;

            if (DrawBrushModeButton("Ground", LandscapeBrushMode.Ground, currentMode, groundInteractable))
                selectedMode = LandscapeBrushMode.Ground;
            if (DrawBrushModeButton("Liquid", LandscapeBrushMode.Liquid, currentMode, liquidInteractable))
                selectedMode = LandscapeBrushMode.Liquid;

            GUILayout.FlexibleSpace();

            if (selectedMode != currentMode)
            {
                // Tool session only — no Undo entry.
                target.SetBrushMode(selectedMode);
                brushModeProperty.enumValueIndex = (int)target.brushMode;
                serializedTarget.ApplyModifiedPropertiesWithoutUndo();
                ApplyBrushSettings(recordUndo: false);
                EditorUtility.SetDirty(target);
                GUI.changed = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void SetBrushPaintingActive(bool active, bool showEscNotification = false, bool recordUndo = false)
        {
            if (target == null)
                return;

            if (target.IsLevelPaintingActive == active && !showEscNotification)
                return;

            // Brush enable/disable is editor session state — never push onto the Undo stack.
            _ = recordUndo;
            if (active)
                target.SetLevelPaintMode(LandscapePaintMode.Paint);
            target.SetLevelPaintingActive(active);
            SyncPaintStateToSerializedObject();
            ClearTextFieldFocus();
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
            Repaint();

            if (!active && showEscNotification)
                ShowBrushDisabledNotification();
        }

        private static void DeactivateBrushOnTarget(DualGrid3D paintTarget, bool recordUndo = false)
        {
            _ = recordUndo;
            paintTarget.SetLevelPaintingActive(false);
            EditorUtility.SetDirty(paintTarget);
            SceneView.RepaintAll();
        }

        private void ShowBrushDisabledNotification() =>
            ShowTransientNotification("Brush disabled. Press Brush or click a layer to continue painting.");

        private void ShowEnvironmentPaintDisabledNotification() =>
            ShowTransientNotification("Prefab deselected. Pick a prefab from the palette to continue painting.");

        private void ShowTransientNotification(string message)
        {
            transientNotificationMessage = message;
            transientNotificationExpireTime =
                EditorApplication.timeSinceStartup + TransientNotificationDurationSeconds;
            Repaint();
        }

        private void TryHandleBrushEscapeInWindow()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Escape)
                return;

            if (target == null || !target.IsLevelPaintingActive)
                return;

            if (HasTrackedTextFieldFocus())
            {
                ClearTextFieldFocus();
                e.Use();
                Repaint();
                return;
            }

            SetBrushPaintingActive(false, showEscNotification: true);
            e.Use();
        }

        private void TryHandleEnvironmentEscapeInWindow()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Escape)
                return;

            if (!EnvironmentPainterState.HasActivePrefab)
                return;

            if (HasTrackedTextFieldFocus())
            {
                ClearTextFieldFocus();
                e.Use();
                Repaint();
                return;
            }

            EnvironmentPainterState.TryDeactivatePainting();
            ShowEnvironmentPaintDisabledNotification();
            SceneView.RepaintAll();
            e.Use();
        }

        private void DrawTransientNotificationOverlay()
        {
            if (string.IsNullOrEmpty(transientNotificationMessage))
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now >= transientNotificationExpireTime)
            {
                transientNotificationMessage = null;
                return;
            }

            const float barHeight = 32f;
            float fadeStart = (float)(transientNotificationExpireTime - TransientNotificationFadeSeconds);
            float alpha = now >= fadeStart
                ? Mathf.Clamp01((float)((transientNotificationExpireTime - now) / TransientNotificationFadeSeconds))
                : 1f;

            Rect rect = new Rect(4f, position.height - barHeight - 4f, position.width - 8f, barHeight);
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 0.92f * alpha));

            GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            style.normal.textColor = new Color(0.9f, 0.9f, 0.9f, alpha);
            GUI.Label(rect, transientNotificationMessage, style);
        }

        private static bool DrawBrushOnOffButton(bool isActive)
        {
            Color previousColor = GUI.backgroundColor;
            if (isActive)
                GUI.backgroundColor = new Color(0.45f, 0.72f, 1f);

            bool clicked = GUILayout.Button(
                isActive ? "On" : "Off",
                EditorStyles.toolbarButton,
                GUILayout.Height(20f),
                GUILayout.MinWidth(48f));

            GUI.backgroundColor = previousColor;
            return clicked;
        }

        private string DrawTrackedTextField(string value, string controlName, params GUILayoutOption[] options)
        {
            Rect rect = EditorGUILayout.GetControlRect(options);
            return DrawTrackedTextFieldInRect(rect, value, controlName);
        }

        private string DrawTrackedTextFieldInRect(Rect rect, string value, string controlName)
        {
            GUI.SetNextControlName(controlName);
            string result = EditorGUI.TextField(rect, value);
            RegisterLastTextFieldControlId();
            return result;
        }

        private int DrawTrackedIntField(int value, params GUILayoutOption[] options)
        {
            Rect rect = EditorGUILayout.GetControlRect(options);
            return DrawTrackedIntFieldInRect(rect, value);
        }

        private int DrawTrackedIntFieldInRect(Rect rect, int value)
        {
            int result = EditorGUI.IntField(rect, value);
            RegisterLastTextFieldControlId();
            return result;
        }

        private void RegisterLastTextFieldControlId()
        {
            int controlId = GetLastControlId();
            if (controlId != 0 && !textFieldControlIds.Contains(controlId))
                textFieldControlIds.Add(controlId);
        }

        private static int GetLastControlId()
        {
            FieldInfo field = typeof(EditorGUIUtility).GetField(
                "s_LastControlID",
                BindingFlags.Static | BindingFlags.NonPublic);

            return field != null ? (int)field.GetValue(null) : 0;
        }

        private static void ClearTextFieldFocus()
        {
            endEditingActiveTextFieldMethod ??= typeof(EditorGUI).GetMethod(
                "EndEditingActiveTextField",
                BindingFlags.Static | BindingFlags.NonPublic);

            endEditingActiveTextFieldMethod?.Invoke(null, null);
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
        }

        private void TryDefocusTextFieldsOnInteraction()
        {
            Event e = Event.current;
            if (e.button != 0 || (e.type != EventType.MouseDown && e.type != EventType.MouseUp))
                return;

            if (!HasTrackedTextFieldFocus())
                return;

            if (GUIUtility.keyboardControl != 0 && GUIUtility.hotControl == GUIUtility.keyboardControl)
                return;

            ClearTextFieldFocus();
            e.Use();
            Repaint();
        }

        private bool HasTrackedTextFieldFocus()
        {
            if (EditorGUIUtility.editingTextField)
                return true;

            string focusedName = GUI.GetNameOfFocusedControl();
            if (!string.IsNullOrEmpty(focusedName) &&
                (focusedName.StartsWith("LevelName_") || focusedName.StartsWith("LayerName_")))
                return true;

            return GUIUtility.keyboardControl != 0 && textFieldControlIds.Contains(GUIUtility.keyboardControl);
        }

        private void HandleInstantHoverRepaint()
        {
            Event e = Event.current;
            if (e.type != EventType.MouseMove &&
                e.type != EventType.MouseEnterWindow &&
                e.type != EventType.MouseLeaveWindow)
                return;

            if (e.mousePosition == lastHoverMousePosition)
                return;

            lastHoverMousePosition = e.mousePosition;
            Repaint();
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.HelpBox(
                "No World Root yet. Press Add Level to create World Root with a Landscape module and the first level (Y = 0).",
                MessageType.Info);

            EditorGUILayout.Space(8f);
            DrawToolbar(createLandscapeIfMissing: true);

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Levels", EditorStyles.boldLabel);

            Rect placeholderRect = GUILayoutUtility.GetRect(0f, 120f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(placeholderRect, new Color(0f, 0f, 0f, 0.12f));

            GUIStyle placeholderStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            GUI.Label(
                placeholderRect,
                "Level list will appear here after World Root / Landscape is created.",
                placeholderStyle);
        }

        private void DrawLandscapeStatusBar()
        {
            if (target == null)
                return;

            target.EnsureDefaultLevel();
            target.EnsureDefaultSubLevels();

            if (target.levels == null || target.levels.Count == 0)
                return;

            int activeIndex = target.ActiveLevelIndex;
            if (activeIndex < 0 || activeIndex >= target.levels.Count)
                return;

            LandscapeLevelDefinition activeLevel = target.levels[activeIndex];
            if (activeLevel.subLevels == null || activeLevel.subLevels.Count == 0)
                return;

            int activeSubIndex = Mathf.Clamp(target.ActiveSubLevelIndex, 0, activeLevel.subLevels.Count - 1);
            int activeHeightUnits = target.GetLevelHeightUnits(activeIndex);
            string layerName = activeLevel.subLevels[activeSubIndex].name;
            string brushLabel = target.brushMode.ToString();
            string biomeName = ResolveLandscapeBiomeDisplayName();
            string armed = target.IsLevelPaintingActive ? "Brush: Active" : "Brush: Off";
            string targetLabel = $"{activeLevel.name} / {layerName}";

            WorldCoreStatusBar.Draw(
                $"{armed} · {biomeName} · {brushLabel} · {targetLabel} · Y={activeHeightUnits}");
        }

        private void DrawLandscapeCellsWarning()
        {
            if (target == null || target.levels == null || target.levels.Count == 0)
                return;

            int activeIndex = target.ActiveLevelIndex;
            if (activeIndex < 0 || activeIndex >= target.levels.Count)
                return;

            LandscapeLevelDefinition activeLevel = target.levels[activeIndex];
            if (activeLevel.subLevels == null || activeLevel.subLevels.Count == 0)
                return;

            int activeSubIndex = Mathf.Clamp(target.ActiveSubLevelIndex, 0, activeLevel.subLevels.Count - 1);
            int activeCells = target.GetLogicalCellCountAtLevel(activeIndex, activeSubIndex);
            if (activeCells != 0)
                return;

            for (int i = 0; i < target.levels.Count; i++)
            {
                int cells = target.GetLogicalCellCountAtLevel(i);
                if (cells > 0)
                {
                    int heightUnits = target.GetLevelHeightUnits(i);
                    EditorGUILayout.HelpBox(
                        $"Level \"{target.levels[i].name}\" (Y={heightUnits}) has {cells} cells. Click that level in the list to edit it.",
                        MessageType.Warning);
                    break;
                }
            }
        }

        private string ResolveLandscapeBiomeDisplayName()
        {
            if (target == null)
                return "—";

            string biomeId = BiomeTileLibrary.NormalizeBiomeId(target.brushBiomeId, target.brushBiome);
            BiomeDefinition definition = BiomeRegistry.GetById(biomeId);
            return definition != null ? definition.displayName : target.brushBiome.ToString();
        }

        private void DrawToolbar(bool createLandscapeIfMissing)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Levels", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            GUIContent addButtonContent = EditorGUIUtility.TrTextContentWithIcon(
                " Add Level",
                createLandscapeIfMissing
                    ? "Create World with World Root and add the first level"
                    : "Add a new level",
                "Toolbar Plus");

            if (GUILayout.Button(addButtonContent, GUILayout.Height(22f)))
            {
                if (createLandscapeIfMissing)
                    CreateLandscapeInScene();
                else
                    AddLevelToTarget();

                BuildReorderableList();
                serializedTarget?.Update();
                RequestExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void AddLevelToTarget()
        {
            if (target == null && worldRoot != null)
                target = worldRoot.FindOrCreateLandscape();

            if (target == null)
                return;

            DualGridLandscapeUndo.ExecuteAddLevel(target);
            EnableLevelPainting(target.ActiveLevelIndex);
        }

        private void CreateLandscapeInScene()
        {
            Undo.SetCurrentGroupName("Create Landscape");
            int undoGroup = Undo.GetCurrentGroup();

            WorldRoot world = worldRoot;
            if (world == null)
            {
                GameObject worldObject = new GameObject(WorldRoot.WorldObjectName);
                Undo.RegisterCreatedObjectUndo(worldObject, "Create Landscape");
                world = Undo.AddComponent<WorldRoot>(worldObject);
                worldRoot = world;
            }

            LandscapeRoot landscape = world.FindOrCreateLandscape();
            landscape.brushBiome = BrushBiome.Grasslands;
            landscape.brushBiomeId = BiomeIds.Grasslands;
            landscape.brushMode = LandscapeBrushMode.Ground;
            landscape.EnsurePaintContent();
            landscape.ApplyActiveBrushTiles();
            landscape.EnsureDefaultLevel();
            landscape.EnsureDefaultSubLevels();
            landscape.SetActiveLevel(0);
            landscape.SetActiveSubLevel(0);
            landscape.SetLevelPaintingActive(true);
            landscape.RebuildLevelRoots();

            Undo.RegisterCompleteObjectUndo(landscape, "Create Landscape");
            LandscapePaintContent paintContent = landscape.EditorGetPaintContent();
            if (paintContent != null)
                Undo.RegisterCompleteObjectUndo(paintContent, "Create Landscape");
            Undo.RegisterCompleteObjectUndo(world, "Create Landscape");

            target = landscape;
            Selection.activeGameObject = landscape.gameObject;
            EditorGUIUtility.PingObject(landscape.gameObject);

            EditorUtility.SetDirty(landscape);
            EditorUtility.SetDirty(world);
            Undo.CollapseUndoOperations(undoGroup);
        }

        private void CreateEnvironmentInScene()
        {
            Undo.SetCurrentGroupName("Create Environment");
            int undoGroup = Undo.GetCurrentGroup();

            GameObject worldObject = new GameObject(WorldRoot.WorldObjectName);
            Undo.RegisterCreatedObjectUndo(worldObject, "Create Environment");

            WorldRoot world = Undo.AddComponent<WorldRoot>(worldObject);
            EnvironmentRoot environment = world.FindOrCreateEnvironment();

            Undo.RegisterCompleteObjectUndo(world, "Create Environment");
            Undo.RegisterCompleteObjectUndo(environment, "Create Environment");

            worldRoot = world;
            environmentRoot = environment;
            Selection.activeGameObject = worldObject;
            EditorGUIUtility.PingObject(worldObject);

            EditorUtility.SetDirty(world);
            EditorUtility.SetDirty(environment);
            Undo.CollapseUndoOperations(undoGroup);
        }

        private static bool DrawBrushModeButton(string label, LandscapeBrushMode mode, LandscapeBrushMode activeMode, bool interactable = true)
        {
            bool isActive = activeMode == mode;
            GUIStyle style = isActive ? EditorStyles.toolbarButton : EditorStyles.toolbarButton;
            Color previousColor = GUI.backgroundColor;
            if (isActive)
                GUI.backgroundColor = new Color(0.45f, 0.72f, 1f);

            EditorGUI.BeginDisabledGroup(!interactable);
            bool clicked = GUILayout.Button(label, style, GUILayout.Height(20f), GUILayout.MinWidth(64f));
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = previousColor;
            return clicked && interactable;
        }

        private static int GetDisplayBiomeIndex(IReadOnlyList<BiomeDefinition> biomes, string biomeId)
        {
            for (int i = 0; i < biomes.Count; i++)
            {
                if (biomes[i].biomeId == biomeId)
                    return i;
            }

            return 0;
        }

        private static string[] GetDisplayBiomeNames(IReadOnlyList<BiomeDefinition> biomes)
        {
            string[] names = new string[biomes.Count];
            for (int i = 0; i < biomes.Count; i++)
                names[i] = biomes[i].displayName;
            return names;
        }

        private bool HasAssignedTilePrefabs() =>
            target != null && target.HasActiveBrushTilePrefabs();

        private void ApplyBrushSettings(bool recordUndo = false)
        {
            // Active brush cache refresh only — never an Undo entry.
            _ = recordUndo;
            target.brushBiome = BiomeTileLibrary.NormalizeBiome(target.brushBiome);
            target.brushBiomeId = BiomeTileLibrary.NormalizeBiomeId(target.brushBiomeId, target.brushBiome);
            target.ApplyActiveBrushTiles();
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }

        private void BuildReorderableList()
        {
            if (target == null)
            {
                serializedTarget = null;
                levelsProperty = null;
                return;
            }

            target.EnsureDefaultLevel();
            target.EnsureLevelHeightUnitsAssigned();
            target.EnsureDefaultSubLevels();
            target.ApplyActiveBrushTiles();

            serializedTarget = new SerializedObject(target);
            serializedTarget.Update();
            PersistSubLevelsSerialization();

            levelsProperty = serializedTarget.FindProperty("levels");
        }

        private void SyncTargetLevelsBeforeDraw()
        {
            if (target == null)
                return;

            target.EnsureDefaultLevel();
            target.EnsureDefaultSubLevels();
            EditorUtility.SetDirty(target);
        }

        private void PersistSubLevelsSerialization()
        {
            if (serializedTarget == null || target == null)
                return;

            SyncTargetLevelsBeforeDraw();
            serializedTarget.Update();

            SerializedProperty levels = serializedTarget.FindProperty("levels");
            if (levels == null)
                return;

            while (levels.arraySize < target.levels.Count)
                levels.InsertArrayElementAtIndex(levels.arraySize);

            for (int i = 0; i < target.levels.Count; i++)
            {
                SerializedProperty levelProp = levels.GetArrayElementAtIndex(i);
                SerializedProperty subLevels = levelProp.FindPropertyRelative("subLevels");
                if (subLevels == null || !subLevels.isArray || subLevels.arraySize == 0)
                {
                    serializedTarget.ApplyModifiedProperties();
                    serializedTarget.Update();
                    return;
                }
            }
        }

        private void DrawLevelsHierarchy()
        {
            if (target == null)
                return;

            SyncTargetLevelsBeforeDraw();

            if (target.levels == null || target.levels.Count == 0)
            {
                EditorGUILayout.HelpBox("No levels yet. Click Add Level.", MessageType.Info);
                return;
            }

            Rect topSeparatorLine = DrawLevelBlockSeparator();

            for (int levelIndex = 0; levelIndex < target.levels.Count; levelIndex++)
            {
                EditorGUILayout.BeginVertical(levelBlockLayoutStyle, GUILayout.ExpandWidth(true));
                DrawLevelBlockContent(levelIndex);
                EditorGUILayout.EndVertical();

                Rect bottomSeparatorLine = DrawLevelBlockSeparator();
                if (target.ActiveLevelIndex == levelIndex && Event.current.type == EventType.Repaint)
                    DrawActiveLevelBlockHighlight(topSeparatorLine, bottomSeparatorLine);

                topSeparatorLine = bottomSeparatorLine;
            }
        }

        private static void DrawActiveLevelBlockHighlight(Rect topSeparatorLine, Rect bottomSeparatorLine)
        {
            Rect highlightRect = new Rect(
                topSeparatorLine.x,
                topSeparatorLine.y,
                topSeparatorLine.width,
                bottomSeparatorLine.yMax - topSeparatorLine.y);

            EditorGUI.DrawRect(highlightRect, ActiveLevelBlockFillColor);
        }

        private const float LevelBlockSeparatorHeight = 1f;
        private const float LevelBlockSeparatorMargin = 6f;

        private static Rect DrawLevelBlockSeparator()
        {
            EditorGUILayout.Space(LevelBlockSeparatorMargin);

            Rect lineRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(LevelBlockSeparatorHeight));

            if (Event.current.type == EventType.Repaint)
            {
                Color lineColor = EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.14f)
                    : new Color(0f, 0f, 0f, 0.22f);
                EditorGUI.DrawRect(lineRect, lineColor);
            }

            EditorGUILayout.Space(LevelBlockSeparatorMargin);
            return lineRect;
        }

        private bool IsLayersExpanded(int levelIndex)
        {
            if (!expandedLevels.TryGetValue(levelIndex, out bool expanded))
            {
                expanded = true;
                expandedLevels[levelIndex] = expanded;
            }

            return expanded;
        }

        private void SetLayersExpanded(int levelIndex, bool expanded) => expandedLevels[levelIndex] = expanded;

        private static float LandscapeListRowHeight => EditorGUIUtility.singleLineHeight + 2f;

        private const float LayersHeaderRowHeight = 22f;
        private const float LevelRowIconSize = 18f;
        private const float LayersHeaderIndent = 8f;
        private const float LevelHeightFieldWidth = 36f;
        private const float LayerRowActionButtonSize = 22f;
        private const float LayerRowActionSpacing = 6f;
        private const float LayerRowMoveButtonSpacing = 0f;
        private const float LayerRowEyeToActionsGap = 10f;
        private static readonly Color LayersHeaderHoverColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color IconHoverBackgroundColor = new Color(1f, 1f, 1f, 0.1f);
        private static readonly Color ActiveLevelBlockFillColor = new Color(1f, 1f, 1f, 0.07f);

        private void DrawLevelBlockContent(int levelIndex)
        {
            if (target == null || target.levels == null || levelIndex < 0 || levelIndex >= target.levels.Count)
                return;

            DrawParentLevelRowGUILayout(levelIndex);

            if (levelIndex >= target.levels.Count)
                return;

            DrawLayersSection(levelIndex);
        }

        private void DrawParentLevelRowGUILayout(int index)
        {
            if (target == null || target.levels == null || index >= target.levels.Count)
                return;

            LandscapeLevelDefinition level = target.levels[index];
            bool canMoveUp = index > 0;
            bool canMoveDown = index < target.levels.Count - 1;

            Rect rowRect = EditorGUILayout.GetControlRect(false, LandscapeListRowHeight, GUILayout.ExpandWidth(true));

            float moveClusterWidth = LayerRowActionButtonSize * 2f + LayerRowMoveButtonSpacing;
            float reservedRightWidth = LevelRowEdgeInset + LayerRowActionSpacing + moveClusterWidth;

            float right = rowRect.xMax - LevelRowEdgeInset;
            Rect moveDownRect = new Rect(right - LayerRowActionButtonSize, rowRect.y, LayerRowActionButtonSize, rowRect.height);
            right = moveDownRect.x - LayerRowMoveButtonSpacing;
            Rect moveUpRect = new Rect(right - LayerRowActionButtonSize, rowRect.y, LayerRowActionButtonSize, rowRect.height);

            LayerRowOptions options = LayerRowDrawer.LandscapeLevelDefaults(
                reservedRightWidth,
                LevelRowCheckboxLeftInset,
                LevelHeightFieldWidth,
                $"Height in levelHeight units ({target.levelHeight:g})",
                target.levels.Count > 1);
            options.selectionExcludeRects = new[] { moveUpRect, moveDownRect };

            LayerRowOutput output = LayerRowDrawer.DrawInRect(
                rowRect,
                new LayerRowInput
                {
                    name = level.name,
                    nameControlName = $"LevelName_{index}",
                    enabled = level.enabled,
                    height = level.heightUnits,
                    isActive = target.ActiveLevelIndex == index
                },
                options);

            RegisterLastTextFieldControlId();

            if (output.enableChanged)
            {
                Undo.RecordObject(target, "Toggle Level");
                target.SetLevelEnabled(index, output.enabled);
                EditorUtility.SetDirty(target);
            }

            if (output.nameChanged)
            {
                Undo.RecordObject(target, "Rename Level");
                level.name = output.name;
                target.RenameLevelRoot(index);
                EditorUtility.SetDirty(target);
            }

            if (output.heightChanged)
            {
                DualGridLandscapeUndo.RegisterGridStateUndo(target, "Set Level Height");
                int editedHeightUnits = Mathf.Max(0, Mathf.RoundToInt(output.height));
                if (!target.TrySetLevelHeightUnits(index, editedHeightUnits))
                {
                    EditorUtility.DisplayDialog(
                        "World Core",
                        $"Level Y={editedHeightUnits} is already in use. Choose another value.",
                        "OK");
                }
                else
                {
                    EditorUtility.SetDirty(target);
                }
            }

            if (output.selectRow)
            {
                ClearTextFieldFocus();
                HandleLevelRowClick(index, target.ActiveLevelIndex == index ? target.ActiveSubLevelIndex : 0);
            }

            if (DrawLevelMoveButtonInRect(moveUpRect, "↑", "Move level up", canMoveUp))
                TryMoveLevel(index, -1);

            if (DrawLevelMoveButtonInRect(moveDownRect, "↓", "Move level down", canMoveDown))
                TryMoveLevel(index, 1);

            if (output.deleteClicked)
                TryDeleteLevel(index, level.name);
        }

        private static bool DrawLevelMoveButtonInRect(Rect rect, string label, string tooltip, bool enabled)
        {
            EditorGUI.BeginDisabledGroup(!enabled);
            bool clicked = GUI.Button(rect, new GUIContent(label, tooltip));
            EditorGUI.EndDisabledGroup();
            return clicked;
        }

        private void TryMoveLevel(int levelIndex, int direction)
        {
            if (target == null)
                return;

            bool expandedA = IsLayersExpanded(levelIndex);
            int otherIndex = levelIndex + direction;
            bool expandedB = otherIndex >= 0 && otherIndex < target.levels.Count && IsLayersExpanded(otherIndex);

            Undo.RecordObject(target, "Move Level");
            if (!target.TryMoveLevel(levelIndex, direction))
                return;

            if (otherIndex >= 0 && otherIndex < target.levels.Count)
            {
                SetLayersExpanded(levelIndex, expandedB);
                SetLayersExpanded(otherIndex, expandedA);
            }

            serializedTarget?.Update();
            EditorUtility.SetDirty(target);
            RequestExitGUI();
        }

        private void TryDeleteLevel(int index, string levelName)
        {
            if (target == null || target.levels == null || target.levels.Count <= 1)
            {
                EditorUtility.DisplayDialog("World Core", "At least one level is required.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Delete Level",
                    $"Delete \"{levelName}\" and all tiles on this level?",
                    "Delete",
                    "Cancel"))
                return;

            DualGridLandscapeUndo.RegisterGridStateUndo(target, "Delete Level");
            target.RemoveLevelAt(index);
            PruneExpandedLevels();
            BuildReorderableList();
            RequestExitGUI();
        }

        private void PruneExpandedLevels()
        {
            if (target?.levels == null)
            {
                expandedLevels.Clear();
                return;
            }

            List<int> staleKeys = null;
            foreach (KeyValuePair<int, bool> entry in expandedLevels)
            {
                if (entry.Key < 0 || entry.Key >= target.levels.Count)
                {
                    staleKeys ??= new List<int>();
                    staleKeys.Add(entry.Key);
                }
            }

            if (staleKeys == null)
                return;

            for (int i = 0; i < staleKeys.Count; i++)
                expandedLevels.Remove(staleKeys[i]);
        }

        private void DrawLayersSection(int levelIndex)
        {
            if (target == null || target.levels == null || levelIndex < 0 || levelIndex >= target.levels.Count)
                return;

            target.EnsureDefaultSubLevels();
            LandscapeLevelDefinition level = target.levels[levelIndex];
            if (level.subLevels == null)
            {
                level.subLevels = new List<LandscapeSubLevelDefinition> { new LandscapeSubLevelDefinition() };
                EditorUtility.SetDirty(target);
            }

            bool layersExpanded = IsLayersExpanded(levelIndex);
            int subLevelCount = level.subLevels.Count;

            Rect headerRect = EditorGUILayout.GetControlRect(false, LayersHeaderRowHeight, GUILayout.ExpandWidth(true));
            DrawLayersHeaderRow(headerRect, levelIndex, layersExpanded, subLevelCount);

            if (!layersExpanded)
                return;

            EditorGUILayout.BeginVertical(layersBoxStyle);
            for (int subIndex = 0; subIndex < subLevelCount; subIndex++)
                DrawSubLevelRowGUILayout(levelIndex, subIndex, level.subLevels[subIndex]);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIContent addLayerContent = EditorGUIUtility.TrTextContentWithIcon(
                " Add Layer",
                "Add a landscape layer",
                "Toolbar Plus");
            if (GUILayout.Button(addLayerContent, GUILayout.Width(110f)))
            {
                DualGridLandscapeUndo.ExecuteAddSubLevel(target, levelIndex);
                SetLayersExpanded(levelIndex, true);
                serializedTarget?.Update();
                RequestExitGUI();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawLayersHeaderRow(Rect rect, int levelIndex, bool isExpanded, int layerCount)
        {
            bool isHovered = rect.Contains(Event.current.mousePosition);

            if (isHovered && Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, LayersHeaderHoverColor);

            Rect countRect = new Rect(rect.xMax - 36f, rect.y + 2f, 32f, rect.height - 4f);
            Rect foldoutRect = new Rect(
                rect.x + LayersHeaderIndent,
                rect.y,
                rect.width - LayersHeaderIndent - 40f,
                rect.height);

            EditorGUI.BeginChangeCheck();
            bool newExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, "Layers", true, EditorStyles.foldout);
            if (EditorGUI.EndChangeCheck())
            {
                SetLayersExpanded(levelIndex, newExpanded);
                Repaint();
            }

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                isHovered &&
                !foldoutRect.Contains(Event.current.mousePosition))
            {
                SetLayersExpanded(levelIndex, !isExpanded);
                Event.current.Use();
                Repaint();
            }

            GUI.Label(countRect, layerCount.ToString(), EditorStyles.miniLabel);
        }

        private void DrawSubLevelRowGUILayout(int levelIndex, int subIndex, LandscapeSubLevelDefinition subLevel)
        {
            bool isSelectedLayer = target.ActiveLevelIndex == levelIndex && target.ActiveSubLevelIndex == subIndex;

            Rect rowRect = EditorGUILayout.GetControlRect(false, LandscapeListRowHeight, GUILayout.ExpandWidth(true));

            float right = rowRect.xMax - LevelRowEdgeInset;
            Rect deleteRect = new Rect(right - LayerRowActionButtonSize, rowRect.y, LayerRowActionButtonSize, rowRect.height);
            right = deleteRect.x - LayerRowActionSpacing;
            Rect overflowRect = new Rect(right - LayerRowActionButtonSize, rowRect.y, LayerRowActionButtonSize, rowRect.height);

            float actionsStartX = overflowRect.x - LayerRowEyeToActionsGap;
            float actionsWidth = rowRect.xMax - LevelRowEdgeInset - actionsStartX;

            LayerRowOptions options = LayerRowDrawer.LandscapeSubLevelDefaults(actionsWidth);
            options.suffixLabel = $"| {subLevel.layerType}";
            options.selectionExcludeRects = new[] { overflowRect, deleteRect };

            LayerRowOutput output = LayerRowDrawer.DrawInRect(
                rowRect,
                new LayerRowInput
                {
                    name = subLevel.name,
                    enabled = true,
                    visible = subLevel.visible,
                    isActive = isSelectedLayer
                },
                options);

            if (output.selectRow)
                HandleSubLevelRowClick(levelIndex, subIndex);

            if (output.nameChanged)
            {
                Undo.RecordObject(target, "Rename Sub Level");
                subLevel.name = output.name;
                target.RenameSubLevelRoot(levelIndex, subIndex);
                EditorUtility.SetDirty(target);
            }

            if (output.visibilityToggled)
            {
                Undo.RecordObject(target, "Toggle Sub Level Visibility");
                target.SetSubLevelVisible(levelIndex, subIndex, !subLevel.visible);
                EditorUtility.SetDirty(target);
                SceneView.RepaintAll();
            }

            if (DrawSubLevelOverflowButtonInRect(overflowRect))
                ShowSubLevelOverflowMenu(levelIndex, subIndex, subLevel.name, isSelectedLayer);

            if (DrawSubLevelDeleteButtonInRect(deleteRect))
            {
                TryDeleteSubLevel(levelIndex, subIndex, subLevel.name);
            }
        }

        private static bool DrawSubLevelOverflowButtonInRect(Rect rect)
        {
            GUIContent menuContent = EditorGUIUtility.IconContent("_Menu");
            if (menuContent.image == null)
                menuContent = new GUIContent("⋮", "Layer actions");

            menuContent.tooltip = "Layer actions";
            return DrawHoverableIconButtonInRect(rect, menuContent);
        }

        private static bool DrawSubLevelDeleteButtonInRect(Rect rect)
        {
            DrawIconHoverBackground(rect);
            return GUI.Button(rect, "×");
        }

        private void ShowSubLevelOverflowMenu(int levelIndex, int subIndex, string layerName, bool isSelectedLayer)
        {
            GenericMenu menu = new GenericMenu();

            if (isSelectedLayer)
            {
                menu.AddItem(
                    new GUIContent("Resync Layer Display"),
                    false,
                    () => ResyncSubLevelDisplay(levelIndex, subIndex));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Resync Layer Display"));
            }

            menu.AddItem(
                new GUIContent("Apply Bake Static To Layer"),
                false,
                () => ApplyBakeStaticToSubLevel(levelIndex, subIndex));

            menu.AddItem(
                new GUIContent("Clear Bake Static From Layer"),
                false,
                () => ClearBakeStaticFromSubLevel(levelIndex, subIndex));

            menu.AddItem(
                new GUIContent("Clear Layer"),
                false,
                () => TryClearSubLevel(levelIndex, subIndex, layerName));

            menu.ShowAsContext();
        }

        private void ApplyBakeStaticToSubLevel(int levelIndex, int subIndex)
        {
            DualGridLandscapeUndo.RegisterGridStateUndo(target, "Apply Bake Static To Layer");
            target.ApplyBakeStaticToLayer(target.GetLevelHeightUnits(levelIndex), subIndex);
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }

        private void ClearBakeStaticFromSubLevel(int levelIndex, int subIndex)
        {
            DualGridLandscapeUndo.RegisterGridStateUndo(target, "Clear Bake Static From Layer");
            target.ClearBakeStaticFromLayer(target.GetLevelHeightUnits(levelIndex), subIndex);
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }

        private void DrawBakeStaticSettings()
        {
            SerializedProperty bakeStaticProperty = serializedTarget.FindProperty("bakeStaticDisplayTiles");
            if (bakeStaticProperty == null)
                return;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                bakeStaticProperty,
                new GUIContent(
                    "Bake Static Tiles",
                    "When enabled, newly spawned display tiles are marked Batching Static. Disable while painting."));
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(target, "Set Bake Static Tiles");
            serializedTarget.ApplyModifiedProperties();

            if (target.BakeStaticDisplayTiles)
                target.ApplyBakeStaticToAllDisplayTiles();
            else
                target.ClearBakeStaticFromAllDisplayTiles();

            EditorUtility.SetDirty(target);
        }

        private void ResyncSubLevelDisplay(int levelIndex, int subIndex)
        {
            DualGridLandscapeUndo.RegisterGridStateUndo(target, "Resync Layer Display");
            target.ResyncLayerDisplayFromLogicalGrid(target.GetLevelHeightUnits(levelIndex), subIndex);
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }

        private void TryClearSubLevel(int levelIndex, int subIndex, string layerName)
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear Layer",
                    $"Remove all meshes from \"{layerName}\" on this layer?",
                    "Clear",
                    "Cancel"))
                return;

            DualGridLandscapeUndo.RegisterGridStateUndo(target, "Clear Layer");
            target.ClearSubLevelLayer(levelIndex, subIndex);
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }

        private void TryDeleteSubLevel(int levelIndex, int subIndex, string layerName)
        {
            if (target.GetSubLevelCount(levelIndex) <= 1)
            {
                EditorUtility.DisplayDialog("World Core", "At least one layer is required.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Delete Layer",
                    $"Delete \"{layerName}\" and all tiles on this layer?",
                    "Delete",
                    "Cancel"))
                return;

            DualGridLandscapeUndo.RegisterGridStateUndo(target, "Delete Sub Level");
            target.RemoveSubLevelAt(levelIndex, subIndex);
            serializedTarget?.Update();
            RequestExitGUI();
        }

        private const float CenteredIconSize = 16f;

        private static bool DrawHoverableIconButtonInRect(Rect rect, GUIContent content)
        {
            DrawIconHoverBackground(rect);
            bool clicked = GUI.Button(rect, new GUIContent(string.Empty, content.tooltip), GUIStyle.none);

            if (Event.current.type == EventType.Repaint && content.image != null)
            {
                float size = Mathf.Min(CenteredIconSize, rect.width, rect.height);
                Rect iconRect = new Rect(
                    rect.x + (rect.width - size) * 0.5f,
                    rect.y + (rect.height - size) * 0.5f,
                    size,
                    size);
                GUI.DrawTexture(iconRect, content.image, ScaleMode.ScaleToFit, true);
            }

            return clicked;
        }

        private void HandleSubLevelRowClick(int levelIndex, int subIndex)
        {
            if (target == null)
                return;

            bool sameLevel = target.ActiveLevelIndex == levelIndex;
            bool sameSubLevel = target.ActiveSubLevelIndex == subIndex;
            bool paintWasActive = target.IsLevelPaintingActive;

            // Navigation / tool session — no Undo entry.
            if (sameLevel && sameSubLevel && paintWasActive)
                SetBrushPaintingActive(false);
            else
            {
                target.SetActiveLevel(levelIndex);
                target.SetActiveSubLevel(subIndex, syncBrushFromLayer: true);

                if (target.IsLevelEnabled(levelIndex))
                    SetBrushPaintingActive(true);
                else
                    SyncPaintStateToSerializedObject();
            }

            Selection.activeGameObject = target.gameObject;
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
            Repaint();
        }

        private static void DrawIconHoverBackground(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (rect.Contains(Event.current.mousePosition))
                EditorGUI.DrawRect(rect, IconHoverBackgroundColor);
        }

        private void HandleLevelRowClick(int levelIndex, int selectSubLevelIndex = 0)
        {
            if (target == null)
                return;

            // Navigation / tool session — no Undo entry.
            bool sameLevel = target.ActiveLevelIndex == levelIndex;
            bool sameSubLevel = target.ActiveSubLevelIndex == selectSubLevelIndex;
            bool paintWasActive = target.IsLevelPaintingActive;

            if (sameLevel && sameSubLevel && paintWasActive)
                SetBrushPaintingActive(false);
            else
            {
                target.SetActiveLevel(levelIndex);
                target.SetActiveSubLevel(selectSubLevelIndex, syncBrushFromLayer: true);
                SetBrushPaintingActive(true);
            }

            SyncPaintStateToSerializedObject();

            Selection.activeGameObject = target.gameObject;
            EditorUtility.SetDirty(target);

            SceneView.RepaintAll();
            Repaint();
        }

        private void SyncPaintStateToSerializedObject()
        {
            if (serializedTarget == null)
                return;

            serializedTarget.Update();
            serializedTarget.FindProperty("activeLevelIndex").intValue = target.ActiveLevelIndex;
            serializedTarget.FindProperty("activeSubLevelIndex").intValue = target.ActiveSubLevelIndex;
            SerializedProperty paintEnabledProperty = serializedTarget.FindProperty("levelPaintingEnabled");
            if (paintEnabledProperty != null)
                paintEnabledProperty.boolValue = target.IsLevelPaintingActive;
            SerializedProperty paintModeProperty = serializedTarget.FindProperty("levelPaintMode");
            if (paintModeProperty != null)
                paintModeProperty.enumValueIndex = (int)target.levelPaintMode;
            SerializedProperty brushModeProperty = serializedTarget.FindProperty("brushMode");
            if (brushModeProperty != null)
                brushModeProperty.enumValueIndex = (int)target.brushMode;
            // Session fields — never push onto the Undo stack via ApplyModifiedProperties.
            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
        }

        private void EnableLevelPainting(int levelIndex)
        {
            if (target == null)
                return;

            // Navigation / brush session — no Undo entry.
            target.SetActiveLevel(levelIndex);
            target.SetActiveSubLevel(0);
            SetBrushPaintingActive(true);

            SyncPaintStateToSerializedObject();

            Selection.activeGameObject = target.gameObject;
            EditorUtility.SetDirty(target);

            SceneView.RepaintAll();
            Repaint();
        }

        private bool TryAssignTargetFromSceneOrSelection()
        {
            if (Selection.activeGameObject != null)
            {
                DualGrid3D selected = Selection.activeGameObject.GetComponentInParent<DualGrid3D>();
                if (selected != null)
                {
                    target = selected;
                    return true;
                }
            }

            DualGrid3D existingInScene = FindLandscapeInOpenScenes();
            if (existingInScene != null)
            {
                target = existingInScene;
                return true;
            }

            return false;
        }

        private static DualGrid3D FindLandscapeInOpenScenes()
        {
    #if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<DualGrid3D>();
    #else
            return Object.FindObjectOfType<DualGrid3D>();
    #endif
        }

        private void InitStyles()
        {
            if (layersBoxStyle == null)
            {
                layersBoxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(6, 6, 6, 6)
                };
            }

            if (levelBlockLayoutStyle == null)
            {
                levelBlockLayoutStyle = new GUIStyle
                {
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(0, 0, 0, 0)
                };
            }
        }
    }
}
