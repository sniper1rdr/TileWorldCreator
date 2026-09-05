using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AglenRealms.WorldCore
{
    [Serializable]
    public struct LogicalCellData
    {
        public int x;
        public int y;
        public int z;
        public int layer;
        public TileType tileType;
        public BrushBiome biome;
        public string biomeId;
    }

    [Serializable]
    public struct LogicalCellState
    {
        public TileType tileType;
        public BrushBiome biome;
        public string biomeId;

        public string GetEffectiveBiomeId() =>
            BiomeRegistry.ResolveBiomeId(biomeId, biome);
    }

    [Serializable]
    public struct GroundDisplayVariantData
    {
        public int x;
        public int y;
        public int z;
        public int layer;
        public bool useVariant;
    }


    public enum LandscapePaintMode
    {
        Paint,
        Erase
    }

    // Hidden: customers add LandscapeRoot via World Core. DualGrid3D remains for legacy scenes/migration.
    [AddComponentMenu("")]
    [ExecuteAlways]
    public partial class DualGrid3D : MonoBehaviour, ISerializationCallbackReceiver
    {
        protected static readonly Vector3Int[] NEIGHBOURS = new Vector3Int[]
        {
          new Vector3Int(0, 0, 0),
          new Vector3Int(1, 0, 0),
          new Vector3Int(0, 0, 1),
          new Vector3Int(1, 0, 1)
        };

        // =====================================================
        // PREFABS
        // =====================================================

        [Header("Logic Prefabs")]
        public GameObject grassPlaceholderPrefab;

        /// <summary>
        /// Runtime/editor brush prefab cache derived from <see cref="brushBiomeId"/>.
        /// Non-serialized: must never alias BiomeDefinition.groundTiles/liquidTiles.
        /// Painted cells use per-cell biomeId in LandscapePaintContent, not this array.
        /// </summary>
        [System.NonSerialized] private GameObject[] tiles;

        public bool HasActiveBrushTilePrefabs()
        {
            if (tiles == null || tiles.Length == 0)
                return false;

            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] != null)
                    return true;
            }

            return false;
        }

        // =====================================================
        // SETTINGS
        // =====================================================

        [Header("Levels")]
        [HideInInspector] public List<LandscapeLevelDefinition> levels = new() { new LandscapeLevelDefinition { name = "Level_01" } };
        [HideInInspector] public int activeLevelIndex = 0;
        [HideInInspector] public int activeSubLevelIndex = 0;
        [HideInInspector] public bool levelPaintingEnabled = false;
        [HideInInspector] public LandscapePaintMode levelPaintMode = LandscapePaintMode.Paint;
        [SerializeField] private bool subLevelVisibilityMigrated;
        [SerializeField] private bool subLevelsAlwaysEnabledMigrated;
        public float levelHeight = 1f;

        [Header("Brush Settings")]
        [HideInInspector] public BrushBiome brushBiome = BrushBiome.Grasslands;
        [HideInInspector] public string brushBiomeId = BiomeIds.Grasslands;
        [HideInInspector] public LandscapeBrushMode brushMode = LandscapeBrushMode.Ground;
        public TileType paintTile = TileType.Grass;

        [Header("Build Optimization")]
        [SerializeField] private bool bakeStaticDisplayTiles;

        public const float PaintMaskHeight = 0.03f;
        public const float EraseMaskHeight = 1.03f;
        private const float MaskCenterPlanarOffset = 0.5f;
        private static readonly Color PaintMaskColor = new Color(0f, 1f, 0f, 0.45f);
        private static readonly Color EraseMaskColor = new Color(1f, 0f, 0f, 0.45f);

        // =====================================================
        // DATA
        // =====================================================

        /// <summary>
        /// Owns serialized painted cells/variants. Paint/Erase Undo registers this object only,
        /// so session fields on DualGrid3D (active level, brush, etc.) are not restored.
        /// </summary>
        [SerializeField] private LandscapePaintContent paintContent;

        // Logical grid (runtime)
        private Dictionary<LandscapeCellKey, LogicalCellState> logicalGrid = new();

        /// <summary>Legacy pre-paint-content storage. Migrated once into <see cref="paintContent"/>.</summary>
        [SerializeField]
        private List<LogicalCellData> savedLogicalGrid = new();

        /// <summary>Legacy pre-paint-content storage. Migrated once into <see cref="paintContent"/>.</summary>
        [SerializeField]
        private List<GroundDisplayVariantData> savedGroundDisplayVariants = new();

        [SerializeField] private bool paintContentMigrated;

        private Dictionary<LandscapeCellKey, bool> groundDisplayVariants = new();

        private readonly Dictionary<(string biomeId, LandscapeLayerType layerType), GameObject[]> biomeTileCache = new();

        // Visual spawned tiles
        private Dictionary<LandscapeCellKey, GameObject> displayGrid = new();

        // Per-level hierarchy roots (Level_0_Name / Layer_*)
        private readonly Dictionary<int, Transform> levelRoots = new();
        private readonly Dictionary<(int logicalY, int layer), Transform> subLevelRoots = new();

        // Paint buffer while mouse held
        private HashSet<LandscapeCellKey> paintBuffer = new();

        // Erase buffer while mouse held
        private HashSet<LandscapeCellKey> eraseBuffer = new();

        // Mask preview while drawing
        private Dictionary<LandscapeCellKey, GameObject> paintMaskObjects = new();
        private Dictionary<LandscapeCellKey, GameObject> eraseMaskObjects = new();
        private Transform paintMaskRoot;
        private Transform eraseMaskRoot;
        private Material paintMaskMaterial;
        private Material eraseMaskMaterial;

        // State
        private bool isPainting = false;
        private bool isErasing = false;
        private bool suppressExternalSync = false;
        private readonly HashSet<LandscapeCellKey> internalTileReplacement = new();

        // Rules. ValueTuple key => no per-lookup heap allocation on the painting hot path.
        protected static Dictionary<(TileType, TileType, TileType, TileType), int> neighbourTupleToTileIndex;

        private struct TileDisplayInfo
        {
            public int basePrefabIndex;
            public float rotationY;
        }

        private const int BaseTileSlotCount = 5;
        private const int VariantSlotOffset = 5;


        // Maps dual-grid rule index -> base mesh slot (0-4) + Y rotation; variant picked at spawn (slot + 5)
        private static TileDisplayInfo[] tileIndexToDisplay;

        // =====================================================
        // UNITY
        // =====================================================

        void OnEnable()
        {
            biomeTileCache.Clear();

            EnsureTileRuleTablesInitialized();
            MigrateLegacySubLevelsAndBrushSettings();
            EnsurePaintContent();
            MigrateLegacyPaintContentIfNeeded();

            LoadLogicalGridFromSaved();

            if (logicalGrid.Count == 0)
                TryMigrateLogicalGridFromDisplayTiles();

            MigrateLegacyLiquidCells();
            ApplyActiveBrushTiles();

            if (levels != null && levels.Count > 0)
            {
                EnsureDefaultSubLevels();
                MigrateSubLevelVisibilityFromLegacy();
                EnsureLevelHeightUnitsAssigned();
                RebuildLevelRoots();
                MigrateSubLevelsAlwaysEnabled();
                SyncExistingDisplayTiles();
    #if UNITY_EDITOR
                MigrateMissingBiomesFromSceneDisplay();
    #endif
                CleanupMissingTiles();
            }
            else
            {
                levelRoots.Clear();
                subLevelRoots.Clear();
            }

            if (UsesExternalEnvironmentRoot)
            {
                suppressExternalSync = false;
                return;
            }

            if (HasLegacyEnvironmentData())
            {
                MigrateLegacyEnvironmentHierarchy();
                RebuildEnvironmentLayerRoots();
            }

            suppressExternalSync = false;
        }

        private bool HasLegacyEnvironmentData() =>
            environmentLayers != null && environmentLayers.Count > 0;

    #if UNITY_EDITOR
        private void OnValidate()
        {
            EnforceAxisAlignedTransform();
        }

        private void EnforceAxisAlignedTransform()
        {
            Transform t = transform;
            if (t.localRotation == Quaternion.identity && t.localScale == Vector3.one)
                return;

            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            Debug.LogWarning(
                $"DualGrid3D on '{name}': rotation and scale are locked. Only position can be changed.",
                this);
        }

        private void EnforceLevelHierarchyLayout()
        {
            foreach (KeyValuePair<int, Transform> entry in levelRoots)
            {
                Transform levelRoot = entry.Value;
                if (levelRoot == null)
                    continue;

                Vector3 expected = new Vector3(0f, GetLogicalYWorldHeight(entry.Key), 0f);
                if (levelRoot.localPosition != expected)
                    levelRoot.localPosition = expected;
            }

            foreach (KeyValuePair<(int logicalY, int layer), Transform> entry in subLevelRoots)
            {
                Transform layerRoot = entry.Value;
                if (layerRoot != null && layerRoot.localPosition != Vector3.zero)
                    layerRoot.localPosition = Vector3.zero;
            }
        }
    #endif

        public int ActiveLevelIndex => Mathf.Clamp(activeLevelIndex, 0, Mathf.Max(0, levels.Count - 1));

        public bool IsLevelPaintingActive => levelPaintingEnabled;

        public bool IsLevelPaintingActiveAt(int levelIndex) =>
            levelPaintingEnabled && ActiveLevelIndex == levelIndex;

        public bool IsSubLevelPaintingActive(int levelIndex, int subLevelIndex) =>
            IsLevelPaintingActiveAt(levelIndex) && ActiveSubLevelIndex == subLevelIndex;

        public bool IsPaintModeActive => levelPaintMode == LandscapePaintMode.Paint;

        public bool IsEraserModeActive => levelPaintMode == LandscapePaintMode.Erase;


        public float GetMaskCenterPlanarOffset() => MaskCenterPlanarOffset;

        public Vector3 GetBrushMaskWorldCenter(LandscapeCellKey coords, float maskHeight)
        {
            Vector3 localCenter = GetCellLocalPlanarCenter(coords, GetMaskCenterPlanarOffset());
            localCenter.y += maskHeight;
            return transform.TransformPoint(localCenter);
        }

        public LandscapeCellKey WorldPointToActiveLevelCell(Vector3 worldPoint)
        {
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            int x = Mathf.FloorToInt(localPoint.x);
            int z = Mathf.FloorToInt(localPoint.z);
            return ToActiveLevelCell(x, z);
        }

        private Vector3 GetCellLocalPlanarCenter(LandscapeCellKey coords, float planarOffset) =>
            new Vector3(
                coords.x + planarOffset,
                GetLogicalYWorldHeight(coords.y),
                coords.z + planarOffset);

        public float GetBrushMaskPickPlaneHeight(bool eraseActive) =>
            eraseActive ? EraseMaskHeight : PaintMaskHeight;

        public int ActiveSubLevelIndex => Mathf.Clamp(activeSubLevelIndex, 0, Mathf.Max(0, GetSubLevelCount(ActiveLevelIndex) - 1));

        public float GetLogicalYWorldHeight(int logicalYUnits) => logicalYUnits * levelHeight;

        public float GetLevelWorldY(int listIndex)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return transform.position.y;

            float localY = GetLogicalYWorldHeight(levels[listIndex].heightUnits);
            return transform.TransformPoint(new Vector3(0f, localY, 0f)).y;
        }

        public int GetLevelHeightUnits(int listIndex)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return 0;

            return levels[listIndex].heightUnits;
        }

        public int ActiveLevelLogicalY => GetLevelHeightUnits(ActiveLevelIndex);

        public float ActiveLevelWorldY => GetLevelWorldY(ActiveLevelIndex);

        public Transform GetActiveEnvironmentLayerRoot() =>
            GetOrCreateEnvironmentLayerRoot(ActiveEnvironmentLayerIndex);

        public float GetActiveEnvironmentLayerWorldPlaneY() =>
            GetEnvironmentLayerWorldPlaneY(ActiveEnvironmentLayerIndex);

        public void SetActiveLevel(int levelIndex)
        {
            activeLevelIndex = Mathf.Clamp(levelIndex, 0, Mathf.Max(0, levels.Count - 1));
            activeSubLevelIndex = Mathf.Clamp(activeSubLevelIndex, 0, Mathf.Max(0, GetSubLevelCount(activeLevelIndex) - 1));
            ClearPaintMask();
            ClearEraseMask();
        }

        public void SetActiveSubLevel(int subLevelIndex, bool syncBrushFromLayer = false)
        {
            activeSubLevelIndex = Mathf.Clamp(subLevelIndex, 0, Mathf.Max(0, GetSubLevelCount(ActiveLevelIndex) - 1));
            if (syncBrushFromLayer)
                SyncBrushModeFromActiveSubLevel();
            ClearPaintMask();
            ClearEraseMask();
        }

        public void SetLevelPaintingActive(bool active)
        {
            levelPaintingEnabled = active;
            ClearPaintMask();
            ClearEraseMask();
        }

        public void SetLevelPaintMode(LandscapePaintMode mode)
        {
            levelPaintMode = mode;
            ClearPaintMask();
            ClearEraseMask();
        }

        public void ClearSubLevelLayer(int listIndex, int subIndex)
        {
            int logicalY = GetLevelHeightUnits(listIndex);
            RemoveLogicalCellsAtLayer(logicalY, subIndex);
            RemoveGroundDisplayVariantsAtLayer(logicalY, subIndex);
            ClearLayerDisplayForResync(logicalY, subIndex);
            PersistLogicalGrid();
    #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
    #endif
        }

        public void EnsureDefaultLevel()
        {
            if (levels == null || levels.Count == 0)
                levels = new List<LandscapeLevelDefinition> { new LandscapeLevelDefinition { name = "Level_01", heightUnits = 0 } };
        }

        public void EnsureDefaultSubLevels()
        {
            if (levels == null)
                return;

            bool changed = false;

            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].subLevels == null || levels[i].subLevels.Count == 0)
                {
                    levels[i].subLevels = new List<LandscapeSubLevelDefinition> { new LandscapeSubLevelDefinition() };
                    changed = true;
                }
            }

            if (changed)
                PersistLevels();
        }

        public void SetBrushMode(LandscapeBrushMode mode)
        {
            brushMode = mode;
            ApplyActiveBrushTiles();
        }

        public void SyncBrushModeFromActiveSubLevel()
        {
            LandscapeLayerType layerType = GetSubLevelLayerType(ActiveLevelIndex, ActiveSubLevelIndex);
            brushMode = ToBrushMode(layerType);
            ApplyActiveBrushTiles();
        }

        public void ApplyActiveBrushTiles()
        {
            // Brush change only refreshes the active brush cache — not per-cell display libraries.
            // biomeTileCache is cleared so the next display resolve reloads detached copies.
            biomeTileCache.Clear();
            brushBiome = BiomeTileLibrary.NormalizeBiome(brushBiome);
            brushBiomeId = BiomeTileLibrary.NormalizeBiomeId(brushBiomeId, brushBiome);
            tiles = BiomeTileLibrary.Load(brushBiomeId, brushMode);

    #if UNITY_EDITOR
            BiomeTileLibrary.AssertDetachedFromBiomeAssets(tiles, brushBiomeId);
    #endif
        }

        public string GetActiveBrushBiomeId() =>
            BiomeTileLibrary.NormalizeBiomeId(brushBiomeId, brushBiome);

        public LandscapeLayerType GetSubLevelLayerType(int listIndex, int subIndex)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return LandscapeLayerType.Ground;

            EnsureDefaultSubLevels();
            if (subIndex < 0 || subIndex >= levels[listIndex].subLevels.Count)
                return LandscapeLayerType.Ground;

            return levels[listIndex].subLevels[subIndex].layerType;
        }

        public bool HasSubLevelOfType(int listIndex, LandscapeLayerType layerType)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return false;

            EnsureDefaultSubLevels();
            for (int i = 0; i < levels[listIndex].subLevels.Count; i++)
            {
                if (levels[listIndex].subLevels[i].layerType == layerType)
                    return true;
            }

            return false;
        }

        public int FindFirstSubLevelIndexOfType(int listIndex, LandscapeLayerType layerType)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return -1;

            EnsureDefaultSubLevels();
            for (int i = 0; i < levels[listIndex].subLevels.Count; i++)
            {
                if (levels[listIndex].subLevels[i].layerType == layerType)
                    return i;
            }

            return -1;
        }

        private bool TryPreparePaintLayer()
        {
            int listIndex = ActiveLevelIndex;
            LandscapeLayerType requiredType = ToLayerType(brushMode);
            LandscapeLayerType activeType = GetSubLevelLayerType(listIndex, ActiveSubLevelIndex);

            if (activeType == requiredType)
                return true;

            if (HasSubLevelOfType(listIndex, requiredType))
                return false;

            AddSubLevel(listIndex, requiredType);
    #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
    #endif
            return true;
        }

        public int GetSubLevelCount(int listIndex)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return 0;

            EnsureDefaultSubLevels();
            return levels[listIndex].subLevels.Count;
        }

        public static string GetDefaultSubLevelName(LandscapeLayerType layerType, int index) =>
            layerType == LandscapeLayerType.Liquid
                ? $"Liquid_{index:D2}"
                : $"Ground_{index:D2}";

        public static LandscapeLayerType ToLayerType(LandscapeBrushMode mode) =>
            mode == LandscapeBrushMode.Liquid ? LandscapeLayerType.Liquid : LandscapeLayerType.Ground;

        public static LandscapeBrushMode ToBrushMode(LandscapeLayerType layerType) =>
            layerType == LandscapeLayerType.Liquid ? LandscapeBrushMode.Liquid : LandscapeBrushMode.Ground;

        public int GetSubLevelCountOfType(int listIndex, LandscapeLayerType layerType)
        {
            int count = 0;
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return count;

            EnsureDefaultSubLevels();
            for (int i = 0; i < levels[listIndex].subLevels.Count; i++)
            {
                if (levels[listIndex].subLevels[i].layerType == layerType)
                    count++;
            }

            return count;
        }

        public bool HasGroundAndLiquidSubLevels(int listIndex) =>
            HasSubLevelOfType(listIndex, LandscapeLayerType.Ground) &&
            HasSubLevelOfType(listIndex, LandscapeLayerType.Liquid);

        private LandscapeLayerType GetLayerTypeAt(int logicalY, int layerIndex) =>
            GetSubLevelLayerType(GetListIndexForLogicalY(logicalY), layerIndex);

        private void MigrateLegacySubLevelsAndBrushSettings()
        {
            bool changed = false;

            if (brushBiome == BrushBiome.Liquid)
            {
                brushBiome = BrushBiome.Grasslands;
                brushBiomeId = BiomeIds.Grasslands;
                brushMode = LandscapeBrushMode.Liquid;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(brushBiomeId))
            {
                brushBiomeId = BiomeRegistry.GetIdFromLegacyBiome(brushBiome);
                changed = true;
            }

            if (levels == null)
                return;

            EnsureDefaultSubLevels();

            for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
            {
                List<LandscapeSubLevelDefinition> subLevels = levels[levelIndex].subLevels;
                for (int subIndex = 0; subIndex < subLevels.Count; subIndex++)
                {
                    LandscapeSubLevelDefinition subLevel = subLevels[subIndex];
                    if (IsLegacyIslandName(subLevel.name))
                    {
                        subLevel.name = GetDefaultSubLevelName(LandscapeLayerType.Ground, 1);
                        subLevel.layerType = LandscapeLayerType.Ground;
                        changed = true;
                    }
                    else if (subLevel.name.StartsWith("Liquid_", System.StringComparison.OrdinalIgnoreCase))
                    {
                        subLevel.layerType = LandscapeLayerType.Liquid;
                        changed = true;
                    }
                    else if (subLevel.name.StartsWith("Ground_", System.StringComparison.OrdinalIgnoreCase))
                    {
                        subLevel.layerType = LandscapeLayerType.Ground;
                        changed = true;
                    }
                }
            }

            if (changed)
                PersistLevels();
        }

        private void MigrateSubLevelVisibilityFromLegacy()
        {
            if (subLevelVisibilityMigrated || levels == null)
                return;

            for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
            {
                List<LandscapeSubLevelDefinition> subLevels = levels[levelIndex].subLevels;
                if (subLevels == null)
                    continue;

                for (int subIndex = 0; subIndex < subLevels.Count; subIndex++)
                {
                    LandscapeSubLevelDefinition subLevel = subLevels[subIndex];
                    if (!subLevel.enabled)
                        subLevel.visible = false;
                }
            }

            subLevelVisibilityMigrated = true;
            PersistLevels();
        }

        private void MigrateSubLevelsAlwaysEnabled()
        {
            if (subLevelsAlwaysEnabledMigrated || levels == null)
                return;

            bool changed = false;
            for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
            {
                LandscapeLevelDefinition level = levels[levelIndex];
                if (level.subLevels == null)
                    continue;

                int logicalY = level.heightUnits;
                for (int subIndex = 0; subIndex < level.subLevels.Count; subIndex++)
                {
                    if (level.subLevels[subIndex].enabled)
                        continue;

                    level.subLevels[subIndex].enabled = true;
                    changed = true;
                    ResyncLayerDisplayFromLogicalGrid(logicalY, subIndex);
                }
            }

            if (changed)
                PersistLevels();

            subLevelsAlwaysEnabledMigrated = true;
        }

        private void MigrateLegacyLiquidCells()
        {
            if (logicalGrid.Count == 0)
                return;

            bool changed = false;
            Dictionary<LandscapeCellKey, LogicalCellState> migratedCells = new();

            foreach (KeyValuePair<LandscapeCellKey, LogicalCellState> cell in logicalGrid)
            {
                LandscapeCellKey key = cell.Key;
                LogicalCellState state = cell.Value;

                if (state.biome != BrushBiome.Liquid)
                {
                    migratedCells[key] = state;
                    continue;
                }

                int listIndex = GetListIndexForLogicalY(key.y);
                if (listIndex >= 0)
                {
                    if (!HasSubLevelOfType(listIndex, LandscapeLayerType.Liquid))
                        AddSubLevel(listIndex, LandscapeLayerType.Liquid);

                    int liquidLayerIndex = FindFirstSubLevelIndexOfType(listIndex, LandscapeLayerType.Liquid);
                    if (liquidLayerIndex >= 0)
                        key = new LandscapeCellKey(key.x, key.y, key.z, liquidLayerIndex);
                }

                state.biome = BrushBiome.Grasslands;
                state.biomeId = BiomeIds.Grasslands;
                migratedCells[key] = state;
                changed = true;
            }

            if (!changed)
                return;

            logicalGrid = migratedCells;
            PersistLogicalGrid();
        }

        private static bool IsLegacyIslandName(string name) =>
            !string.IsNullOrWhiteSpace(name) &&
            name.StartsWith("Island", System.StringComparison.OrdinalIgnoreCase);

        public bool IsLevelEnabled(int listIndex)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return false;

            return levels[listIndex].enabled;
        }

        public bool IsActiveSubLevelEnabled()
        {
            int listIndex = ActiveLevelIndex;
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return false;

            if (!levels[listIndex].enabled)
                return false;

            EnsureDefaultSubLevels();
            int subIndex = ActiveSubLevelIndex;
            return subIndex >= 0 && subIndex < levels[listIndex].subLevels.Count;
        }

        public void SetLevelEnabled(int listIndex, bool enabled)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return;

            levels[listIndex].enabled = enabled;
            ApplyLevelVisibility(listIndex);
            PersistLevels();
        }

        public void AddSubLevel(int listIndex, LandscapeLayerType? layerTypeOverride = null)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return;

            EnsureDefaultSubLevels();
            LandscapeLayerType layerType = layerTypeOverride ?? ToLayerType(brushMode);
            int typeCount = GetSubLevelCountOfType(listIndex, layerType);
            levels[listIndex].subLevels.Add(new LandscapeSubLevelDefinition
            {
                name = GetDefaultSubLevelName(layerType, typeCount + 1),
                layerType = layerType,
                enabled = true,
                visible = true
            });

            int subIndex = levels[listIndex].subLevels.Count - 1;

            if (listIndex == ActiveLevelIndex)
                activeSubLevelIndex = subIndex;

            // Hierarchy roots are created here without Undo registration. Callers that need
            // undoable creation must own the Undo group and RegisterCreatedObjectUndo explicitly.
            RebuildLevelRoots();
            PersistLevels();
        }

        public void RemoveSubLevelAt(int listIndex, int subIndex)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return;

            EnsureDefaultSubLevels();
            if (levels[listIndex].subLevels.Count <= 1 || subIndex < 0 || subIndex >= levels[listIndex].subLevels.Count)
                return;

            int logicalY = levels[listIndex].heightUnits;
            RemoveLogicalCellsAtLayer(logicalY, subIndex);
            RemoveDisplayTilesAtLayer(logicalY, subIndex);
            RemoveGroundDisplayVariantsAtLayer(logicalY, subIndex);
            ShiftLogicalCellsLayerDown(logicalY, subIndex);
            ShiftDisplayGridLayerDown(logicalY, subIndex);
            ShiftGroundDisplayVariantsLayerDown(logicalY, subIndex);

            levels[listIndex].subLevels.RemoveAt(subIndex);

            if (listIndex == ActiveLevelIndex)
                activeSubLevelIndex = Mathf.Clamp(activeSubLevelIndex, 0, levels[listIndex].subLevels.Count - 1);

            RebuildLevelRoots();
            PersistLogicalGrid();
            PersistLevels();
        }

        public void SetSubLevelEnabled(int listIndex, int subIndex, bool enabled)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return;

            EnsureDefaultSubLevels();
            if (subIndex < 0 || subIndex >= levels[listIndex].subLevels.Count)
                return;

            levels[listIndex].subLevels[subIndex].enabled = enabled;
            ApplySubLevelVisibility(levels[listIndex].heightUnits, subIndex);
            PersistLevels();
        }

        public void SetSubLevelVisible(int listIndex, int subIndex, bool visible)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return;

            EnsureDefaultSubLevels();
            if (subIndex < 0 || subIndex >= levels[listIndex].subLevels.Count)
                return;

            levels[listIndex].subLevels[subIndex].visible = visible;
            ApplySubLevelVisibility(levels[listIndex].heightUnits, subIndex);
            PersistLevels();
        }

        public void EnsureLevelHeightUnitsAssigned()
        {
            if (levels == null || levels.Count <= 1)
                return;

            bool allZero = true;
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].heightUnits != 0)
                {
                    allZero = false;
                    break;
                }
            }

            if (!allZero)
                return;

            for (int i = 0; i < levels.Count; i++)
                levels[i].heightUnits = i;

            PersistLevels();
        }

        public void AddLevel(string levelName = null)
        {
            EnsureDefaultLevel();

            int index = levels.Count;
            levels.Add(new LandscapeLevelDefinition
            {
                name = string.IsNullOrWhiteSpace(levelName) ? $"Level_{index + 1:D2}" : levelName,
                heightUnits = GetNextDefaultHeightUnits()
            });

            activeLevelIndex = index;
            int logicalY = levels[index].heightUnits;
            // Hierarchy roots are created here without Undo registration. Callers that need
            // undoable creation must own the Undo group and RegisterCreatedObjectUndo explicitly
            // (see DualGridLandscapeUndo.ExecuteAddLevel).
            GetOrCreateLevelRoot(logicalY);
            PersistLevels();
        }

        public bool TrySetLevelHeightUnits(int listIndex, int newHeightUnits)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return false;

            newHeightUnits = Mathf.Max(0, newHeightUnits);

            int oldHeightUnits = levels[listIndex].heightUnits;
            if (oldHeightUnits == newHeightUnits)
                return true;

            for (int i = 0; i < levels.Count; i++)
            {
                if (i != listIndex && levels[i].heightUnits == newHeightUnits)
                    return false;
            }

            levels[listIndex].heightUnits = newHeightUnits;
            RemapLogicalGridY(oldHeightUnits, newHeightUnits);
            RebuildLevelRoots();
            RebuildAllDisplayTiles();
            PersistLogicalGrid();
            PersistLevels();
            return true;
        }

        private int GetNextDefaultHeightUnits()
        {
            int maxUnits = 0;
            for (int i = 0; i < levels.Count; i++)
                maxUnits = Mathf.Max(maxUnits, levels[i].heightUnits);

            return maxUnits + 1;
        }

        private void RemapLogicalGridY(int fromY, int toY)
        {
            Dictionary<LandscapeCellKey, LogicalCellState> remapped = new();

            foreach (KeyValuePair<LandscapeCellKey, LogicalCellState> cell in logicalGrid)
            {
                int y = cell.Key.y == fromY ? toY : cell.Key.y;
                remapped[new LandscapeCellKey(cell.Key.x, y, cell.Key.z, cell.Key.layer)] = cell.Value;
            }

            logicalGrid = remapped;
        }

        public void RemoveLevelAt(int index)
        {
            if (levels == null || levels.Count <= 1 || index < 0 || index >= levels.Count)
                return;

            int logicalY = levels[index].heightUnits;
            ClearLevelDisplayTiles(logicalY);
            RemoveLogicalCellsAtLevel(logicalY);

            levels.RemoveAt(index);

            if (activeLevelIndex >= levels.Count)
                activeLevelIndex = levels.Count - 1;
            else if (activeLevelIndex > index)
                activeLevelIndex--;

            levelPaintingEnabled = false;

            RebuildLevelRoots();
            PersistLogicalGrid();
            PersistLevels();
        }

        /// <summary>
        /// Moves a level one step in the list (direction -1 = up, +1 = down).
        /// Only reorders the list; heightUnits / world Y stay with each level.
        /// </summary>
        public bool TryMoveLevel(int listIndex, int direction)
        {
            if (direction != -1 && direction != 1)
                return false;

            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return false;

            int otherIndex = listIndex + direction;
            if (otherIndex < 0 || otherIndex >= levels.Count)
                return false;

            SwapLevelsAt(listIndex, otherIndex);
            return true;
        }

        public void SwapLevelsAt(int indexA, int indexB)
        {
            if (levels == null || indexA == indexB)
                return;
            if (indexA < 0 || indexA >= levels.Count)
                return;
            if (indexB < 0 || indexB >= levels.Count)
                return;

            LandscapeLevelDefinition temp = levels[indexA];
            levels[indexA] = levels[indexB];
            levels[indexB] = temp;

            if (activeLevelIndex == indexA)
                activeLevelIndex = indexB;
            else if (activeLevelIndex == indexB)
                activeLevelIndex = indexA;

            activeSubLevelIndex = Mathf.Clamp(
                activeSubLevelIndex,
                0,
                Mathf.Max(0, GetSubLevelCount(activeLevelIndex) - 1));

            ClearPaintMask();
            ClearEraseMask();
            PersistLevels();
        }

        private void RemoveLogicalCellsAtLevel(int logicalY)
        {
            List<LandscapeCellKey> toRemove = new();

            foreach (KeyValuePair<LandscapeCellKey, LogicalCellState> cell in logicalGrid)
            {
                if (cell.Key.y == logicalY)
                    toRemove.Add(cell.Key);
            }

            foreach (LandscapeCellKey key in toRemove)
                logicalGrid.Remove(key);
        }

        private void RemoveLogicalCellsAtLayer(int logicalY, int layer)
        {
            List<LandscapeCellKey> toRemove = new();

            foreach (KeyValuePair<LandscapeCellKey, LogicalCellState> cell in logicalGrid)
            {
                if (cell.Key.y == logicalY && cell.Key.layer == layer)
                    toRemove.Add(cell.Key);
            }

            foreach (LandscapeCellKey key in toRemove)
                logicalGrid.Remove(key);
        }

        private void ShiftLogicalCellsLayerDown(int logicalY, int removedLayer)
        {
            Dictionary<LandscapeCellKey, LogicalCellState> shifted = new();

            foreach (KeyValuePair<LandscapeCellKey, LogicalCellState> cell in logicalGrid)
            {
                int layer = cell.Key.layer;
                if (cell.Key.y == logicalY && layer > removedLayer)
                    layer--;

                shifted[new LandscapeCellKey(cell.Key.x, cell.Key.y, cell.Key.z, layer)] = cell.Value;
            }

            logicalGrid = shifted;
        }

        private void RemoveDisplayTilesAtLayer(int logicalY, int layer)
        {
            List<LandscapeCellKey> toRemove = new();

            foreach (KeyValuePair<LandscapeCellKey, GameObject> pair in displayGrid)
            {
                if (pair.Key.y == logicalY && pair.Key.layer == layer)
                    toRemove.Add(pair.Key);
            }

            foreach (LandscapeCellKey pos in toRemove)
                DestroyDisplayTile(pos);
        }

        private void ShiftDisplayGridLayerDown(int logicalY, int removedLayer)
        {
            Dictionary<LandscapeCellKey, GameObject> shifted = new();

            foreach (KeyValuePair<LandscapeCellKey, GameObject> pair in displayGrid)
            {
                LandscapeCellKey key = pair.Key;
                GameObject tile = pair.Value;

                if (key.y == logicalY && key.layer > removedLayer)
                {
                    int newLayer = key.layer - 1;
                    LandscapeCellKey newKey = new LandscapeCellKey(key.x, key.y, key.z, newLayer);
                    if (tile != null)
                    {
                        tile.name = GetTileObjectName(newKey);
                        DualGridTileProxy proxy = tile.GetComponent<DualGridTileProxy>();
                        if (proxy != null)
                            proxy.cellKey = newKey;
                    }

                    shifted[newKey] = tile;
                }
                else
                {
                    shifted[key] = tile;
                }
            }

            displayGrid = shifted;
        }

        // Single place that picks the correct destroy call for edit mode vs play mode.
        private static void DestroyObjectImmediateOrRuntime(UnityEngine.Object target)
        {
            if (target == null)
                return;

    #if UNITY_EDITOR
            DestroyImmediate(target);
    #else
            Destroy(target);
    #endif
        }

        // Suppresses the proxy's owner notification, then destroys the tile object.
        private static void DetachAndDestroyTile(GameObject tile)
        {
            if (tile == null)
                return;

            DualGridTileProxy proxy = tile.GetComponent<DualGridTileProxy>();
            if (proxy != null)
                proxy.notifyOwnerOnDestroy = false;

            DestroyObjectImmediateOrRuntime(tile);
        }

        // Destroys a tile tracked at a cell, guarding against re-entrant owner callbacks.
        private void DestroyTrackedDisplayTile(LandscapeCellKey pos, GameObject tile)
        {
            internalTileReplacement.Add(pos);
            DetachAndDestroyTile(tile);
            internalTileReplacement.Remove(pos);
            displayGrid.Remove(pos);
        }

        private void DestroyDisplayTile(LandscapeCellKey pos)
        {
            if (!displayGrid.TryGetValue(pos, out GameObject tile) || tile == null)
                return;

            DestroyTrackedDisplayTile(pos, tile);
        }

        private void ClearLevelDisplayTiles(int logicalY)
        {
            List<LandscapeCellKey> toRemove = new();

            foreach (KeyValuePair<LandscapeCellKey, GameObject> pair in displayGrid)
            {
                if (pair.Key.y == logicalY)
                    toRemove.Add(pair.Key);
            }

            foreach (LandscapeCellKey pos in toRemove)
                DestroyDisplayTile(pos);
        }

        private void RebuildAllDisplayTiles()
        {
            suppressExternalSync = true;

            foreach (GameObject tile in displayGrid.Values)
                DetachAndDestroyTile(tile);

            displayGrid.Clear();
            suppressExternalSync = false;

            List<LandscapeCellKey> logicalCells = new(logicalGrid.Keys);
            foreach (LandscapeCellKey pos in logicalCells)
                RefreshTile(pos);
        }

        public void RebuildLevelRoots()
        {
            levelRoots.Clear();
            subLevelRoots.Clear();

            EnsureDefaultSubLevels();

            for (int i = 0; i < levels.Count; i++)
            {
                int logicalY = levels[i].heightUnits;
                GetOrCreateLevelRoot(logicalY);

                for (int subIndex = 0; subIndex < levels[i].subLevels.Count; subIndex++)
                    GetOrCreateSubLevelRoot(logicalY, subIndex);

                ApplyLevelVisibility(i);
            }

            List<Transform> staleRoots = new();

            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Level_") && !levelRoots.ContainsValue(child))
                    staleRoots.Add(child);
            }

            foreach (Transform stale in staleRoots)
                DestroyObjectImmediateOrRuntime(stale.gameObject);

            foreach (KeyValuePair<LandscapeCellKey, GameObject> pair in displayGrid)
            {
                if (pair.Value == null)
                    continue;

                Transform root = GetOrCreateSubLevelRoot(pair.Key.y, pair.Key.layer);
                pair.Value.transform.SetParent(root, true);
            }

            RemoveStaleSubLevelRoots();
        }

        private void RemoveStaleSubLevelRoots()
        {
            HashSet<Transform> validSubLevelRoots = new(subLevelRoots.Values);

            foreach (Transform levelRoot in levelRoots.Values)
            {
                if (levelRoot == null)
                    continue;

                List<Transform> staleLayers = new();
                foreach (Transform child in levelRoot)
                {
                    if (child.name.StartsWith("Layer_") && !validSubLevelRoots.Contains(child))
                        staleLayers.Add(child);
                }

                foreach (Transform stale in staleLayers)
                    DestroyObjectImmediateOrRuntime(stale.gameObject);
            }
        }

        public void RenameLevelRoot(int listIndex)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return;

            int logicalY = levels[listIndex].heightUnits;
            string rootName = GetLevelRootName(logicalY, levels[listIndex].name);
            Transform root = FindLevelRootByLogicalY(logicalY);
            if (root == null)
                root = GetOrCreateLevelRoot(logicalY);
            else if (root.name != rootName)
                root.name = rootName;

            levelRoots[logicalY] = root;
            PersistLevels();
        }

        public void RenameSubLevelRoot(int listIndex, int subIndex)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return;

            EnsureDefaultSubLevels();
            if (subIndex < 0 || subIndex >= levels[listIndex].subLevels.Count)
                return;

            int logicalY = levels[listIndex].heightUnits;
            string layerName = levels[listIndex].subLevels[subIndex].name;
            Transform levelRoot = GetOrCreateLevelRoot(logicalY);
            string rootName = GetSubLevelRootName(subIndex, layerName);
            Transform layerRoot = FindSubLevelRoot(levelRoot, subIndex);
            if (layerRoot == null)
                layerRoot = GetOrCreateSubLevelRoot(logicalY, subIndex);
            else if (layerRoot.name != rootName)
                layerRoot.name = rootName;

            subLevelRoots[(logicalY, subIndex)] = layerRoot;
            PersistLevels();
        }

        private bool HasExistingLevelRoot(int logicalY)
        {
            if (levelRoots.TryGetValue(logicalY, out Transform cached) && cached != null)
                return true;

            return FindLevelRootByLogicalY(logicalY) != null;
        }

        private bool HasExistingSubLevelRoot(int logicalY, int layerIndex)
        {
            if (subLevelRoots.TryGetValue((logicalY, layerIndex), out Transform cached) && cached != null)
                return true;

            Transform levelRoot = FindLevelRootByLogicalY(logicalY);
            if (levelRoot == null && levelRoots.TryGetValue(logicalY, out Transform mapped) && mapped != null)
                levelRoot = mapped;

            if (levelRoot == null)
                return false;

            return FindSubLevelRoot(levelRoot, layerIndex) != null;
        }

        private Transform GetOrCreateLevelRoot(int logicalY)
        {
            if (levelRoots.TryGetValue(logicalY, out Transform cached) && cached != null)
                return cached;

            string rootName = GetLevelRootName(logicalY, GetLevelNameForLogicalY(logicalY));
            Transform existing = FindLevelRootByLogicalY(logicalY);

            if (existing == null)
            {
                GameObject rootObject = new GameObject(rootName);
                rootObject.transform.SetParent(transform, false);
                rootObject.transform.localPosition = new Vector3(0f, GetLogicalYWorldHeight(logicalY), 0f);
                existing = rootObject.transform;
            }
            else
            {
                if (existing.name != rootName)
                    existing.name = rootName;

                existing.localPosition = new Vector3(0f, GetLogicalYWorldHeight(logicalY), 0f);
            }

            levelRoots[logicalY] = existing;
            return existing;
        }

        private void ApplyLevelVisibility(int listIndex)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return;

            int logicalY = levels[listIndex].heightUnits;
            if (levelRoots.TryGetValue(logicalY, out Transform levelRoot) && levelRoot != null)
                levelRoot.gameObject.SetActive(levels[listIndex].enabled);

            EnsureDefaultSubLevels();
            for (int subIndex = 0; subIndex < levels[listIndex].subLevels.Count; subIndex++)
                ApplySubLevelVisibility(logicalY, subIndex);
        }

        private Transform GetOrCreateSubLevelRoot(int logicalY, int layerIndex)
        {
            (int logicalY, int layer) key = (logicalY, layerIndex);
            if (subLevelRoots.TryGetValue(key, out Transform cached) && cached != null)
                return cached;

            int listIndex = GetListIndexForLogicalY(logicalY);
            EnsureDefaultSubLevels();
            string layerName = listIndex >= 0 && layerIndex < levels[listIndex].subLevels.Count
                ? levels[listIndex].subLevels[layerIndex].name
                : $"Layer_{layerIndex + 1:D2}";

            Transform levelRoot = GetOrCreateLevelRoot(logicalY);
            string rootName = GetSubLevelRootName(layerIndex, layerName);
            Transform existing = FindSubLevelRoot(levelRoot, layerIndex);

            if (existing == null)
            {
                GameObject rootObject = new GameObject(rootName);
                rootObject.transform.SetParent(levelRoot, false);
                rootObject.transform.localPosition = Vector3.zero;
                existing = rootObject.transform;
            }
            else if (existing.name != rootName)
            {
                existing.name = rootName;
            }

            subLevelRoots[key] = existing;
            return existing;
        }

        private static string GetLevelRootName(int logicalY, string levelName) =>
            $"Level_{logicalY}_{SanitizeLevelRootName(levelName)}";

        private static string GetSubLevelRootName(int layerIndex, string layerName) =>
            $"Layer_{layerIndex:D2}_{SanitizeLevelRootName(layerName)}";

        private Transform FindLevelRootByLogicalY(int logicalY)
        {
            string prefix = $"Level_{logicalY}_";

            foreach (Transform child in transform)
            {
                if (child.name.StartsWith(prefix) || child.name == $"Level_{logicalY}")
                    return child;
            }

            return null;
        }

        private static Transform FindSubLevelRoot(Transform levelRoot, int layerIndex)
        {
            string indexPrefix = $"Layer_{layerIndex:D2}_";
            Transform legacyByOrder = null;
            int layerChildIndex = 0;

            foreach (Transform child in levelRoot)
            {
                if (!child.name.StartsWith("Layer_"))
                    continue;

                if (child.name.StartsWith(indexPrefix))
                    return child;

                if (layerChildIndex == layerIndex)
                    legacyByOrder = child;

                layerChildIndex++;
            }

            return legacyByOrder;
        }

        private void ApplySubLevelVisibility(int logicalY, int layerIndex)
        {
            (int logicalY, int layer) key = (logicalY, layerIndex);
            if (!subLevelRoots.TryGetValue(key, out Transform root) || root == null)
                return;

            int listIndex = GetListIndexForLogicalY(logicalY);
            bool isVisible = listIndex >= 0 &&
                             layerIndex >= 0 &&
                             layerIndex < levels[listIndex].subLevels.Count &&
                             levels[listIndex].subLevels[layerIndex].visible;

            root.gameObject.SetActive(isVisible);
        }

        private int GetListIndexForLogicalY(int logicalY)
        {
            if (levels == null)
                return -1;

            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].heightUnits == logicalY)
                    return i;
            }

            return -1;
        }

        private string GetLevelNameForLogicalY(int logicalY)
        {
            if (levels != null)
            {
                for (int i = 0; i < levels.Count; i++)
                {
                    if (levels[i].heightUnits == logicalY)
                        return levels[i].name;
                }
            }

            return $"Y{logicalY}";
        }

        private static string SanitizeLevelRootName(string levelName)
        {
            if (string.IsNullOrWhiteSpace(levelName))
                return "Unnamed";

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            string sanitized = levelName.Trim();

            foreach (char c in invalid)
                sanitized = sanitized.Replace(c, '_');

            return sanitized.Replace(' ', '_');
        }

        private void PersistLevels()
        {
    #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
    #endif
        }

        public void OnBeforeSerialize()
        {
            // Do not sync runtime dictionaries here. Paint content lives on LandscapePaintContent;
            // mutations call PersistLogicalGrid() explicitly. Paint undo registers that component only.
        }

        public void OnAfterDeserialize()
        {
            logicalGrid ??= new Dictionary<LandscapeCellKey, LogicalCellState>();
            groundDisplayVariants ??= new Dictionary<LandscapeCellKey, bool>();
            // Paint content component may not be assigned yet during deserialize; OnEnable loads after Ensure/Migrate.
            if (paintContent != null)
            {
                LoadLogicalGridFromSaved();
                LoadGroundDisplayVariantsFromSaved();
            }
        }

    #if UNITY_EDITOR
        /// <summary>
        /// Flush runtime grid into LandscapePaintContent before RegisterCompleteObjectUndo on that content.
        /// </summary>
        public void EditorPersistGridStateForUndo()
        {
            PersistLogicalGrid(markDirty: false);
        }

        public LandscapePaintContent EditorGetPaintContent() => EnsurePaintContent();
    #endif

        /// <summary>
        /// Hidden scene component that owns painted cell data for content-only Undo.
        /// </summary>
        public LandscapePaintContent EnsurePaintContent()
        {
            if (paintContent != null)
            {
                paintContent.hideFlags |= HideFlags.HideInInspector;
                return paintContent;
            }

            paintContent = GetComponent<LandscapePaintContent>();
            if (paintContent == null)
                paintContent = gameObject.AddComponent<LandscapePaintContent>();

            paintContent.hideFlags |= HideFlags.HideInInspector;

    #if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(this);
    #endif
            return paintContent;
        }

        private void MigrateLegacyPaintContentIfNeeded()
        {
            LandscapePaintContent content = EnsurePaintContent();

            bool hasLegacyCells = savedLogicalGrid != null && savedLogicalGrid.Count > 0;
            bool hasLegacyVariants = savedGroundDisplayVariants != null && savedGroundDisplayVariants.Count > 0;
            bool contentEmpty = content.CellCount == 0 && content.Variants.Count == 0;

            if (paintContentMigrated && !hasLegacyCells && !hasLegacyVariants)
                return;

            if (!hasLegacyCells && !hasLegacyVariants)
            {
                paintContentMigrated = true;
                return;
            }

            // Only copy when content is empty so repeated OnEnable never duplicates.
            if (contentEmpty)
            {
                if (hasLegacyCells)
                {
                    content.Cells.Clear();
                    content.Cells.AddRange(savedLogicalGrid);
                }

                if (hasLegacyVariants)
                {
                    content.Variants.Clear();
                    content.Variants.AddRange(savedGroundDisplayVariants);
                }
            }

            savedLogicalGrid.Clear();
            savedGroundDisplayVariants.Clear();
            paintContentMigrated = true;

    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
                EditorUtility.SetDirty(content);
            }
    #endif
        }

        private List<LogicalCellData> SerializedCells
        {
            get
            {
                LandscapePaintContent content = paintContent != null ? paintContent : GetComponent<LandscapePaintContent>();
                return content != null ? content.Cells : savedLogicalGrid;
            }
        }

        private List<GroundDisplayVariantData> SerializedVariants
        {
            get
            {
                LandscapePaintContent content = paintContent != null ? paintContent : GetComponent<LandscapePaintContent>();
                return content != null ? content.Variants : savedGroundDisplayVariants;
            }
        }

        private void LoadLogicalGridFromSaved()
        {
            logicalGrid.Clear();

            foreach (LogicalCellData cell in SerializedCells)
            {
                logicalGrid[new LandscapeCellKey(cell.x, cell.y, cell.z, cell.layer)] = new LogicalCellState
                {
                    tileType = cell.tileType,
                    biome = cell.biome,
                    biomeId = BiomeRegistry.ResolveBiomeId(cell.biomeId, cell.biome)
                };
            }
        }

        private void PersistLogicalGrid(bool markDirty = true)
        {
            LandscapePaintContent content = EnsurePaintContent();
            List<LogicalCellData> cells = content.Cells;
            cells.Clear();

            foreach (KeyValuePair<LandscapeCellKey, LogicalCellState> cell in logicalGrid)
            {
                cells.Add(new LogicalCellData
                {
                    x = cell.Key.x,
                    y = cell.Key.y,
                    z = cell.Key.z,
                    layer = cell.Key.layer,
                    tileType = cell.Value.tileType,
                    biome = cell.Value.biome,
                    biomeId = cell.Value.GetEffectiveBiomeId()
                });
            }

            PersistGroundDisplayVariants();

    #if UNITY_EDITOR
            if (markDirty)
            {
                EditorUtility.SetDirty(this);
                EditorUtility.SetDirty(content);
            }
    #endif
        }

        private void LoadGroundDisplayVariantsFromSaved()
        {
            groundDisplayVariants.Clear();

            foreach (GroundDisplayVariantData entry in SerializedVariants)
            {
                groundDisplayVariants[new LandscapeCellKey(entry.x, entry.y, entry.z, entry.layer)] = entry.useVariant;
            }
        }

        private void PersistGroundDisplayVariants()
        {
            LandscapePaintContent content = EnsurePaintContent();
            List<GroundDisplayVariantData> variants = content.Variants;
            variants.Clear();

            foreach (KeyValuePair<LandscapeCellKey, bool> entry in groundDisplayVariants)
            {
                variants.Add(new GroundDisplayVariantData
                {
                    x = entry.Key.x,
                    y = entry.Key.y,
                    z = entry.Key.z,
                    layer = entry.Key.layer,
                    useVariant = entry.Value
                });
            }
        }

        private void RandomizeGroundDisplayVariant(LandscapeCellKey displayKey)
        {
            if (GetLayerTypeAt(displayKey.y, displayKey.layer) != LandscapeLayerType.Ground)
                return;

            groundDisplayVariants[displayKey] = UnityEngine.Random.value >= 0.5f;
        }

        private void RandomizeGroundDisplayVariantsForLogicalCell(LandscapeCellKey logicalKey)
        {
            if (GetLayerTypeAt(logicalKey.y, logicalKey.layer) != LandscapeLayerType.Ground)
                return;

            for (int i = 0; i < NEIGHBOURS.Length; i++)
                RandomizeGroundDisplayVariant(logicalKey.Offset(NEIGHBOURS[i]));
        }

        private void RandomizeGroundDisplayVariantsForLayer(int logicalY, int layerIndex)
        {
            if (GetLayerTypeAt(logicalY, layerIndex) != LandscapeLayerType.Ground)
                return;

            HashSet<LandscapeCellKey> displayKeys = new();

            foreach (KeyValuePair<LandscapeCellKey, LogicalCellState> cell in logicalGrid)
            {
                if (cell.Key.y != logicalY || cell.Key.layer != layerIndex)
                    continue;

                for (int i = 0; i < NEIGHBOURS.Length; i++)
                    displayKeys.Add(cell.Key.Offset(NEIGHBOURS[i]));
            }

            foreach (LandscapeCellKey displayKey in displayKeys)
                RandomizeGroundDisplayVariant(displayKey);
        }

        private void RemoveGroundDisplayVariant(LandscapeCellKey displayKey)
        {
            groundDisplayVariants.Remove(displayKey);
        }

        private void RemoveGroundDisplayVariantsAtLayer(int logicalY, int layer)
        {
            List<LandscapeCellKey> toRemove = new();

            foreach (LandscapeCellKey key in groundDisplayVariants.Keys)
            {
                if (key.y == logicalY && key.layer == layer)
                    toRemove.Add(key);
            }

            foreach (LandscapeCellKey key in toRemove)
                groundDisplayVariants.Remove(key);
        }

        private void ShiftGroundDisplayVariantsLayerDown(int logicalY, int removedLayer)
        {
            Dictionary<LandscapeCellKey, bool> shifted = new();

            foreach (KeyValuePair<LandscapeCellKey, bool> entry in groundDisplayVariants)
            {
                LandscapeCellKey key = entry.Key;

                if (key.y == logicalY && key.layer > removedLayer)
                {
                    shifted[new LandscapeCellKey(key.x, key.y, key.z, key.layer - 1)] = entry.Value;
                    continue;
                }

                shifted[key] = entry.Value;
            }

            groundDisplayVariants = shifted;
        }

        private void TryMigrateLogicalGridFromDisplayTiles()
        {
            HashSet<LandscapeCellKey> displayPositions = new();
            CollectDisplayTilePositions(transform, displayPositions);

            if (displayPositions.Count == 0)
                return;

            HashSet<LandscapeCellKey> logicalCandidates = new();

            foreach (LandscapeCellKey displayPos in displayPositions)
            {
                for (int i = 0; i < NEIGHBOURS.Length; i++)
                    logicalCandidates.Add(displayPos.Offset(-NEIGHBOURS[i]));
            }

            foreach (LandscapeCellKey logicalPos in logicalCandidates)
            {
                bool hasFullBlock = true;

                for (int i = 0; i < NEIGHBOURS.Length; i++)
                {
                    if (!displayPositions.Contains(logicalPos.Offset(NEIGHBOURS[i])))
                    {
                        hasFullBlock = false;
                        break;
                    }
                }

                if (hasFullBlock)
                {
                    logicalGrid[logicalPos] = new LogicalCellState
                    {
                        tileType = TileType.Grass,
                        biome = BrushBiome.Grasslands,
                        biomeId = BiomeIds.Grasslands
                    };
                }
            }

            if (logicalGrid.Count > 0)
                PersistLogicalGrid();
        }

        private void SyncExistingDisplayTiles()
        {
            displayGrid.Clear();
            SyncDisplayTilesRecursive(transform);
        }

        private void SyncDisplayTilesRecursive(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (TryParseTileName(child.name, out LandscapeCellKey pos))
                {
                    displayGrid[pos] = child.gameObject;
                    AttachTileProxy(child.gameObject, pos);
                }
                else
                {
                    SyncDisplayTilesRecursive(child);
                }
            }
        }

        private static void CollectDisplayTilePositions(Transform parent, HashSet<LandscapeCellKey> displayPositions)
        {
            foreach (Transform child in parent)
            {
                if (TryParseTileName(child.name, out LandscapeCellKey displayPos))
                    displayPositions.Add(displayPos);
                else
                    CollectDisplayTilePositions(child, displayPositions);
            }
        }

        public void ResyncDisplayFromLogicalGrid()
        {
            suppressExternalSync = true;

            DestroyAllDisplayTilesInHierarchy();
            displayGrid.Clear();

            suppressExternalSync = false;

            RebuildLevelRoots();
            SyncExistingDisplayTiles();

            List<LandscapeCellKey> logicalCells = new(logicalGrid.Keys);
            foreach (LandscapeCellKey pos in logicalCells)
                RefreshTile(pos);

            PersistLogicalGrid();
        }

        public void ResyncLayerDisplayFromLogicalGrid(int logicalY, int layerIndex)
        {
            ClearLayerDisplayForResync(logicalY, layerIndex);
            GetOrCreateSubLevelRoot(logicalY, layerIndex);
            RandomizeGroundDisplayVariantsForLayer(logicalY, layerIndex);

            List<LandscapeCellKey> layerCells = new();
            foreach (KeyValuePair<LandscapeCellKey, LogicalCellState> cell in logicalGrid)
            {
                if (cell.Key.y == logicalY && cell.Key.layer == layerIndex)
                    layerCells.Add(cell.Key);
            }

            foreach (LandscapeCellKey pos in layerCells)
                RefreshTile(pos, randomizeGroundVariants: false);

            PersistLogicalGrid();
        }

        private void ClearLayerDisplayForResync(int logicalY, int layerIndex)
        {
            HashSet<LandscapeCellKey> keysToClear = new();

            foreach (LandscapeCellKey key in displayGrid.Keys)
            {
                if (key.y == logicalY && key.layer == layerIndex)
                    keysToClear.Add(key);
            }

            Transform levelRoot = FindLevelRootByLogicalY(logicalY);
            if (levelRoot != null)
            {
                Transform layerRoot = FindSubLevelRoot(levelRoot, layerIndex);
                if (layerRoot != null)
                {
                    List<GameObject> tilesInHierarchy = new();
                    CollectDisplayTileObjects(layerRoot, tilesInHierarchy);

                    foreach (GameObject tile in tilesInHierarchy)
                    {
                        if (tile != null && TryParseTileName(tile.name, out LandscapeCellKey pos))
                            keysToClear.Add(pos);
                    }
                }
            }

            suppressExternalSync = true;

            foreach (LandscapeCellKey pos in keysToClear)
            {
                RemoveGroundDisplayVariant(pos);
                DestroyDisplayTileForResync(pos);
            }

            suppressExternalSync = false;
        }

        private void DestroyDisplayTileForResync(LandscapeCellKey pos)
        {
            if (displayGrid.TryGetValue(pos, out GameObject trackedTile) && trackedTile != null)
            {
                DestroyDisplayTile(pos);
                return;
            }

            GameObject orphanTile = FindDisplayTileInHierarchy(pos);
            if (orphanTile == null)
                return;

            DestroyTrackedDisplayTile(pos, orphanTile);
        }

        private void DestroyAllDisplayTilesInHierarchy()
        {
            List<GameObject> tilesToDestroy = new();
            CollectDisplayTileObjects(transform, tilesToDestroy);

            foreach (GameObject tile in tilesToDestroy)
                DetachAndDestroyTile(tile);
        }

        private static void CollectDisplayTileObjects(Transform parent, List<GameObject> results)
        {
            foreach (Transform child in parent)
            {
                if (TryParseTileName(child.name, out _))
                    results.Add(child.gameObject);
                else
                    CollectDisplayTileObjects(child, results);
            }
        }

        private GameObject FindDisplayTileInHierarchy(LandscapeCellKey pos)
        {
            if (TryFindChildByName(transform, GetTileObjectName(pos), out GameObject found))
                return found;

            if (pos.layer > 0 &&
                TryFindChildByName(transform, $"Tile_{pos.x}_{pos.y}_{pos.z}_L{pos.layer}", out found))
                return found;

            if (pos.layer == 0 && pos.y == 0 && TryFindChildByName(transform, $"Tile_{pos.x}_{pos.z}", out found))
                return found;

            return null;
        }

        private static string GetTileObjectName(LandscapeCellKey pos) =>
            pos.layer == 0
                ? $"Tile_{pos.x}_{pos.y}_{pos.z}"
                : $"Tile_{pos.x}_{pos.y}_{pos.z}_L_{pos.layer}";

        private static bool TryFindChildByName(Transform parent, string objectName, out GameObject result)
        {
            foreach (Transform child in parent)
            {
                if (child.name == objectName)
                {
                    result = child.gameObject;
                    return true;
                }

                if (TryFindChildByName(child, objectName, out result))
                    return true;
            }

            result = null;
            return false;
        }

        public LandscapeCellKey ToActiveLevelCell(int x, int z) =>
            new LandscapeCellKey(x, ActiveLevelLogicalY, z, ActiveSubLevelIndex);

        public int GetLogicalCellCountAtLevel(int listIndex, int subLevelIndex = -1)
        {
            if (levels == null || listIndex < 0 || listIndex >= levels.Count)
                return 0;

            int logicalY = levels[listIndex].heightUnits;
            int count = 0;

            foreach (KeyValuePair<LandscapeCellKey, LogicalCellState> cell in logicalGrid)
            {
                if (cell.Key.y != logicalY || cell.Value.tileType == TileType.None)
                    continue;

                if (subLevelIndex >= 0 && cell.Key.layer != subLevelIndex)
                    continue;

                count++;
            }

            return count;
        }

    #if UNITY_EDITOR
        public void EditorResyncDisplayAfterUndoRedo()
        {
            biomeTileCache.Clear();
            EditorStripPaintUndoBaselineCells();
            LoadLogicalGridFromSaved();
            LoadGroundDisplayVariantsFromSaved();
            EditorPruneOrphanPaintDataAfterUndoRedo();
            // Refill NonSerialized brush tiles from brushBiomeId (not restored by Undo).
            ApplyActiveBrushTiles();

            suppressExternalSync = true;
            DestroyAllDisplayTilesInHierarchy();
            displayGrid.Clear();
            suppressExternalSync = false;

            // RebuildLevelRoots clears levelRoots/subLevelRoots and destroys orphan Level_* roots.
            RebuildLevelRoots();

            List<LandscapeCellKey> logicalCells = new(logicalGrid.Keys);
            foreach (LandscapeCellKey pos in logicalCells)
                RefreshTile(pos, randomizeGroundVariants: false);

            ClearPaintMask();
            ClearEraseMask();
        }

        /// <summary>
        /// Removes the editor-only sentinel cell used to make first-paint Undo work on Unity 2022.3.
        /// </summary>
        internal bool EditorStripPaintUndoBaselineCells()
        {
            LandscapePaintContent content = EnsurePaintContent();
            int removed = content.Cells.RemoveAll(IsPaintUndoBaselineCell);
            if (removed > 0)
                EditorUtility.SetDirty(content);
            return removed > 0;
        }

        /// <summary>Must match DualGridLandscapeUndo.PaintUndoBaselineCoord.</summary>
        public const int EditorPaintUndoBaselineCoord = -1000003;

        private static bool IsPaintUndoBaselineCell(LogicalCellData cell) =>
            cell.layer < 0 ||
            (cell.tileType == TileType.None &&
             cell.x == EditorPaintUndoBaselineCoord &&
             cell.y == EditorPaintUndoBaselineCoord &&
             cell.z == EditorPaintUndoBaselineCoord);

        /// <summary>
        /// Structural Undo/Redo recovery only: drop cells/variants whose level Y or layer
        /// index is not present in the current levels list. Preserves all valid level data.
        /// </summary>
        internal bool EditorPruneOrphanPaintDataAfterUndoRedo()
        {
            EnsureDefaultLevel();
            EnsureDefaultSubLevels();

            Dictionary<int, int> subLevelCountByY = new();
            for (int i = 0; i < levels.Count; i++)
            {
                int logicalY = levels[i].heightUnits;
                int subCount = levels[i].subLevels != null ? levels[i].subLevels.Count : 0;
                if (!subLevelCountByY.ContainsKey(logicalY) || subLevelCountByY[logicalY] < subCount)
                    subLevelCountByY[logicalY] = subCount;
            }

            bool pruned = false;
            List<LandscapeCellKey> orphanCells = new();
            foreach (LandscapeCellKey key in logicalGrid.Keys)
            {
                if (!subLevelCountByY.TryGetValue(key.y, out int subCount) ||
                    key.layer < 0 ||
                    key.layer >= subCount)
                {
                    orphanCells.Add(key);
                }
            }

            for (int i = 0; i < orphanCells.Count; i++)
            {
                logicalGrid.Remove(orphanCells[i]);
                pruned = true;
            }

            List<LandscapeCellKey> orphanVariants = new();
            foreach (LandscapeCellKey key in groundDisplayVariants.Keys)
            {
                if (!subLevelCountByY.TryGetValue(key.y, out int subCount) ||
                    key.layer < 0 ||
                    key.layer >= subCount)
                {
                    orphanVariants.Add(key);
                }
            }

            for (int i = 0; i < orphanVariants.Count; i++)
            {
                groundDisplayVariants.Remove(orphanVariants[i]);
                pruned = true;
            }

            if (pruned)
                PersistLogicalGrid();

            return pruned;
        }
    #endif

        [ContextMenu("Resync Display From Logical Grid")]
        public void ResyncDisplayFromLogicalGridMenu() => ResyncDisplayFromLogicalGrid();

        private static bool TryParseTileName(string objectName, out LandscapeCellKey pos) =>
            TryParseNamedTile(objectName, "Tile_", out pos);

        private static bool TryParseNamedTile(string objectName, string prefix, out LandscapeCellKey pos)
        {
            pos = default;

            if (!objectName.StartsWith(prefix))
                return false;

            string[] parts = objectName.Split('_');
            if (parts.Length == 6 && parts[4] == "L")
            {
                if (!int.TryParse(parts[1], out int x) ||
                    !int.TryParse(parts[2], out int y) ||
                    !int.TryParse(parts[3], out int z) ||
                    !int.TryParse(parts[5], out int layer))
                    return false;

                pos = new LandscapeCellKey(x, y, z, layer);
                return true;
            }

            if (parts.Length == 5 && parts[4].Length > 1 && parts[4][0] == 'L' &&
                int.TryParse(parts[4].Substring(1), out int legacyLayer))
            {
                if (!int.TryParse(parts[1], out int x) ||
                    !int.TryParse(parts[2], out int y) ||
                    !int.TryParse(parts[3], out int z))
                    return false;

                pos = new LandscapeCellKey(x, y, z, legacyLayer);
                return true;
            }

            if (parts.Length == 4)
            {
                if (!int.TryParse(parts[1], out int x) ||
                    !int.TryParse(parts[2], out int y) ||
                    !int.TryParse(parts[3], out int z))
                    return false;

                pos = new LandscapeCellKey(x, y, z, 0);
                return true;
            }

            if (parts.Length == 3)
            {
                if (!int.TryParse(parts[1], out int x) || !int.TryParse(parts[2], out int z))
                    return false;

                pos = new LandscapeCellKey(x, 0, z, 0);
                return true;
            }

            return false;
        }

        void LateUpdate()
        {
            SyncDestroyedDisplayTiles();
    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EnforceAxisAlignedTransform();
                EnforceLevelHierarchyLayout();
            }
    #endif
        }

        void OnDisable()
        {
            suppressExternalSync = true;
            displayGrid.Clear();
            levelRoots.Clear();
            subLevelRoots.Clear();
            paintBuffer.Clear();
            eraseBuffer.Clear();
            ClearPaintMask();
            ClearEraseMask();
            ReleaseMaskMaterials();
        }

        void OnDestroy()
        {
            ReleaseMaskMaterials();
        }

        // =====================================================
        // PAINT START
        // =====================================================

        public bool BeginPaint()
        {
            if (!TryPreparePaintLayer() || !IsActiveSubLevelEnabled())
                return false;

            isPainting = true;
            paintBuffer.Clear();
            ClearPaintMask();
            return true;
        }

        public void BeginErase()
        {
            isErasing = true;
            eraseBuffer.Clear();
            ClearEraseMask();
        }

        // =====================================================
        // PAINT UPDATE
        // =====================================================

        public void AddPaintCell(LandscapeCellKey coords)
        {
            if (!isPainting || !IsActiveSubLevelEnabled())
                return;

            coords = ToActiveLevelCell(coords.x, coords.z);

            if (paintBuffer.Add(coords))
            {
                Color maskColor = PaintMaskColor;
                float maskHeight = GetBrushMaskPickPlaneHeight(eraseActive: false);
                ShowMaskCell(coords, paintMaskObjects, ref paintMaskRoot, maskColor, ref paintMaskMaterial, "_PaintMask", maskHeight);
            }
        }

        public void AddEraseCell(LandscapeCellKey coords)
        {
            if (!isErasing)
                return;

            coords = ToActiveLevelCell(coords.x, coords.z);

            if (eraseBuffer.Add(coords))
            {
                Color maskColor = EraseMaskColor;
                float maskHeight = GetBrushMaskPickPlaneHeight(eraseActive: true);
                ShowMaskCell(coords, eraseMaskObjects, ref eraseMaskRoot, maskColor, ref eraseMaskMaterial, "_EraseMask", maskHeight);
            }
        }

        // =====================================================
        // PAINT END
        // =====================================================

        public void EndPaint()
        {
            isPainting = false;
            ClearPaintMask();

            foreach (var pos in paintBuffer)
            {
                logicalGrid[pos] = new LogicalCellState
                {
                    tileType = paintTile,
                    biome = brushBiome,
                    biomeId = GetActiveBrushBiomeId()
                };

            }

            foreach (var pos in paintBuffer)
            {
                RefreshTile(pos);
            }

            paintBuffer.Clear();
            PersistLogicalGrid();
        }

        public void EndErase()
        {
            isErasing = false;
            ClearEraseMask();

            foreach (var pos in eraseBuffer)
                EraseLogicalCell(pos, persist: false);

            eraseBuffer.Clear();
            PersistLogicalGrid();
        }

        public void CancelPaintStroke()
        {
            if (!isPainting)
                return;

            isPainting = false;
            paintBuffer.Clear();
            ClearPaintMask();
        }

        public void CancelEraseStroke()
        {
            if (!isErasing)
                return;

            isErasing = false;
            eraseBuffer.Clear();
            ClearEraseMask();
        }

        // =====================================================
        // EXTERNAL DELETE (HIERARCHY)
        // =====================================================

        internal void HandleDisplayTileDestroyed(LandscapeCellKey pos)
        {
            if (suppressExternalSync || internalTileReplacement.Contains(pos))
                return;

            displayGrid.Remove(pos);

    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorPersistGridStateForUndo();
                LandscapePaintContent content = EnsurePaintContent();
                Undo.RegisterCompleteObjectUndo(content, "Delete Tile");
            }
    #endif

            EraseLogicalCell(pos);

    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
                if (paintContent != null)
                    EditorUtility.SetDirty(paintContent);
            }
    #endif
        }

        private void EraseLogicalCell(LandscapeCellKey pos, bool persist = true)
        {
            logicalGrid.Remove(pos);
            RefreshTile(pos);

            if (persist)
                PersistLogicalGrid();
        }


        // =====================================================
        // SINGLE CELL
        // =====================================================

        public void SetCell(LandscapeCellKey coords, TileType tileType)
        {
            if (tileType == TileType.None)
            {
                if (logicalGrid.ContainsKey(coords))
                {
                    logicalGrid.Remove(coords);
                }
            }
            else
            {
                string biomeId = GetActiveBrushBiomeId();
                if (logicalGrid.TryGetValue(coords, out LogicalCellState existing))
                    biomeId = existing.GetEffectiveBiomeId();

                logicalGrid[coords] = new LogicalCellState
                {
                    tileType = tileType,
                    biome = BiomeRegistry.TryGetLegacyBiome(biomeId, out BrushBiome legacyBiome)
                        ? legacyBiome
                        : brushBiome,
                    biomeId = biomeId
                };
            }

            RefreshTile(coords);
            PersistLogicalGrid();
        }

        // =====================================================
        // GET TILE TYPE
        // =====================================================

        private TileType GetTileTypeAt(LandscapeCellKey coords)
        {
            if (logicalGrid.TryGetValue(coords, out LogicalCellState cell))
                return cell.tileType;

            return TileType.None;
        }

        private string GetDisplayBiomeIdAt(LandscapeCellKey coords)
        {
            for (int i = 0; i < NEIGHBOURS.Length; i++)
            {
                LandscapeCellKey source = coords.Offset(-NEIGHBOURS[i]);
                if (logicalGrid.TryGetValue(source, out LogicalCellState state) && state.tileType != TileType.None)
                    return state.GetEffectiveBiomeId();
            }

            if (logicalGrid.TryGetValue(coords, out LogicalCellState center) && center.tileType != TileType.None)
                return center.GetEffectiveBiomeId();

            return BiomeIds.Grasslands;
        }

        private GameObject[] GetTilesForBiomeId(string biomeId, LandscapeLayerType layerType)
        {
            var cacheKey = (biomeId, layerType);
            if (biomeTileCache.TryGetValue(cacheKey, out GameObject[] cached) && cached != null && cached.Length > 0)
                return cached;

            LandscapeBrushMode mode = ToBrushMode(layerType);
            GameObject[] loaded = BiomeTileLibrary.Load(biomeId, mode);
            if (loaded == null || loaded.Length == 0)
                return null;

    #if UNITY_EDITOR
            BiomeTileLibrary.AssertDetachedFromBiomeAssets(loaded, biomeId);
    #endif

            // Cache holds the detached copy from Load — never the asset array.
            biomeTileCache[cacheKey] = loaded;
            return loaded;
        }

        private bool TryGetGroundDisplayInfo(LandscapeCellKey coords, out int basePrefabIndex, out float rotationY)
        {
            basePrefabIndex = -1;
            rotationY = 0f;

            TileType topRight = GetTileTypeAt(coords.Offset(-NEIGHBOURS[0]));
            TileType topLeft = GetTileTypeAt(coords.Offset(-NEIGHBOURS[1]));
            TileType botRight = GetTileTypeAt(coords.Offset(-NEIGHBOURS[2]));
            TileType botLeft = GetTileTypeAt(coords.Offset(-NEIGHBOURS[3]));

            if (topRight == TileType.None &&
                topLeft == TileType.None &&
                botRight == TileType.None &&
                botLeft == TileType.None)
            {
                return false;
            }

            (TileType, TileType, TileType, TileType) neighbourTuple =
                (topLeft, topRight, botLeft, botRight);

            if (!neighbourTupleToTileIndex.TryGetValue(neighbourTuple, out int tileIndex))
                return false;

            TileDisplayInfo display = tileIndexToDisplay[tileIndex];
            basePrefabIndex = display.basePrefabIndex;
            rotationY = display.rotationY;
            return true;
        }
        // =====================================================
        // CALCULATE DISPLAY
        // =====================================================

        // Builds the constant dual-grid rule tables once. Content is deterministic,
        // so it is shared across all instances and survives until the next domain reload.
        private static void EnsureTileRuleTablesInitialized()
        {
            if (neighbourTupleToTileIndex != null && tileIndexToDisplay != null)
                return;

            neighbourTupleToTileIndex = new()
            {
                {(TileType.Grass, TileType.Grass, TileType.Grass, TileType.Grass), 6},

                {(TileType.None, TileType.None, TileType.None, TileType.Grass), 13},
                {(TileType.None, TileType.None, TileType.Grass, TileType.None), 0},
                {(TileType.None, TileType.Grass, TileType.None, TileType.None), 8},
                {(TileType.Grass, TileType.None, TileType.None, TileType.None), 15},

                {(TileType.None, TileType.Grass, TileType.None, TileType.Grass), 1},
                {(TileType.Grass, TileType.None, TileType.Grass, TileType.None), 11},
                {(TileType.None, TileType.None, TileType.Grass, TileType.Grass), 3},
                {(TileType.Grass, TileType.Grass, TileType.None, TileType.None), 9},

                {(TileType.None, TileType.Grass, TileType.Grass, TileType.Grass), 5},
                {(TileType.Grass, TileType.None, TileType.Grass, TileType.Grass), 2},
                {(TileType.Grass, TileType.Grass, TileType.None, TileType.Grass), 10},
                {(TileType.Grass, TileType.Grass, TileType.Grass, TileType.None), 7},

                {(TileType.None, TileType.Grass, TileType.Grass, TileType.None), 14},
                {(TileType.Grass, TileType.None, TileType.None, TileType.Grass), 4},
            };

            InitTileDisplayMap();
        }

        private static void InitTileDisplayMap()
        {
            tileIndexToDisplay = new TileDisplayInfo[16];

            // Corner (base slot 0, variant slot 5)
            tileIndexToDisplay[0] = new TileDisplayInfo { basePrefabIndex = 0, rotationY = 0f };
            tileIndexToDisplay[8] = new TileDisplayInfo { basePrefabIndex = 0, rotationY = 180f };
            tileIndexToDisplay[13] = new TileDisplayInfo { basePrefabIndex = 0, rotationY = 270f };
            tileIndexToDisplay[15] = new TileDisplayInfo { basePrefabIndex = 0, rotationY = 90f };

            // Edge (base 1, variant 6)
            tileIndexToDisplay[1] = new TileDisplayInfo { basePrefabIndex = 1, rotationY = 0f };
            tileIndexToDisplay[3] = new TileDisplayInfo { basePrefabIndex = 1, rotationY = 90f };
            tileIndexToDisplay[9] = new TileDisplayInfo { basePrefabIndex = 1, rotationY = 270f };
            tileIndexToDisplay[11] = new TileDisplayInfo { basePrefabIndex = 1, rotationY = 180f };

            // Three-sided (base 2, variant 7)
            tileIndexToDisplay[2] = new TileDisplayInfo { basePrefabIndex = 2, rotationY = 0f };
            tileIndexToDisplay[5] = new TileDisplayInfo { basePrefabIndex = 2, rotationY = 270f };
            tileIndexToDisplay[7] = new TileDisplayInfo { basePrefabIndex = 2, rotationY = 90f };
            tileIndexToDisplay[10] = new TileDisplayInfo { basePrefabIndex = 2, rotationY = 180f };

            // Diagonal fill (base 3, variant 8)
            tileIndexToDisplay[4] = new TileDisplayInfo { basePrefabIndex = 3, rotationY = 0f };
            tileIndexToDisplay[14] = new TileDisplayInfo { basePrefabIndex = 3, rotationY = 90f };

            // Flat top (base 4, variant 9)
            tileIndexToDisplay[6] = new TileDisplayInfo { basePrefabIndex = 4, rotationY = 0f };
        }

        private int PickPrefabIndex(int baseIndex, GameObject[] biomeTiles, LandscapeCellKey coords, LandscapeLayerType layerType)
        {
            if (biomeTiles == null || baseIndex < 0 || baseIndex >= BaseTileSlotCount)
                return -1;

            int variantIndex = baseIndex + VariantSlotOffset;
            bool hasBase = baseIndex < biomeTiles.Length && biomeTiles[baseIndex] != null;
            bool hasVariant = variantIndex < biomeTiles.Length && biomeTiles[variantIndex] != null;

            if (!hasBase && !hasVariant)
                return -1;

            if (!hasVariant)
                return baseIndex;

            if (!hasBase)
                return variantIndex;

            if (layerType == LandscapeLayerType.Ground)
            {
                if (!groundDisplayVariants.TryGetValue(coords, out bool useVariant))
                {
                    RandomizeGroundDisplayVariant(coords);
                    useVariant = groundDisplayVariants[coords];
                }

                return useVariant ? variantIndex : baseIndex;
            }

            uint hash = (uint)(coords.x * 73856093 ^ coords.z * 19349663 ^ coords.y * 83492791 ^ coords.layer * 50331653);
            return (hash & 1) == 0 ? baseIndex : variantIndex;
        }

        protected bool TryGetTileDisplay(LandscapeCellKey coords, out GameObject prefab, out Quaternion rotation)
        {
            prefab = null;
            rotation = Quaternion.identity;

            if (!TryGetGroundDisplayInfo(coords, out int basePrefabIndex, out float groundRotationY))
                return false;

            GameObject[] biomeTiles = GetTilesForBiomeId(GetDisplayBiomeIdAt(coords), GetLayerTypeAt(coords.y, coords.layer));
            if (biomeTiles == null || biomeTiles.Length == 0)
                return false;

            LandscapeLayerType layerType = GetLayerTypeAt(coords.y, coords.layer);

            int prefabIndex = PickPrefabIndex(basePrefabIndex, biomeTiles, coords, layerType);
            if (prefabIndex < 0 || prefabIndex >= biomeTiles.Length)
                return false;

            prefab = biomeTiles[prefabIndex];
            rotation = Quaternion.Euler(0f, groundRotationY, 0f);
            return prefab != null;
        }

        // =====================================================
        // REFRESH
        // =====================================================

        protected void RefreshTile(LandscapeCellKey pos, bool randomizeGroundVariants = true)
        {
            if (randomizeGroundVariants)
                RandomizeGroundDisplayVariantsForLogicalCell(pos);

            for (int i = 0; i < NEIGHBOURS.Length; i++)
                SpawnOrReplaceTile(pos.Offset(NEIGHBOURS[i]));
        }

        // =====================================================
        // SPAWN / REPLACE
        // =====================================================

        private void SpawnOrReplaceTile(LandscapeCellKey pos)
        {
            if (!IsSubLevelEnabledAt(pos.y, pos.layer))
            {
                if (displayGrid.TryGetValue(pos, out GameObject disabledTile) && disabledTile != null)
                    DestroyTrackedDisplayTile(pos, disabledTile);

                return;
            }

            GameObject oldTile = null;

            if (displayGrid.TryGetValue(pos, out GameObject trackedTile))
                oldTile = trackedTile;
            else
                oldTile = FindDisplayTileInHierarchy(pos);

            if (oldTile != null)
                DestroyTrackedDisplayTile(pos, oldTile);

            if (internalTileReplacement.Count == 0)
                CleanupMissingTiles();

            if (!TryGetTileDisplay(pos, out GameObject prefab, out Quaternion rotation))
            {
                RemoveGroundDisplayVariant(pos);
                return;
            }

            Transform layerRoot = GetOrCreateSubLevelRoot(pos.y, pos.layer);

            GameObject spawned = Instantiate(prefab, layerRoot);
            spawned.transform.SetLocalPositionAndRotation(new Vector3(pos.x, 0f, pos.z), rotation);

            spawned.name = GetTileObjectName(pos);
            AttachTileProxy(spawned, pos);
            ApplyBakeStaticToDisplayTile(spawned);
            displayGrid[pos] = spawned;
        }

        public bool BakeStaticDisplayTiles
        {
            get => bakeStaticDisplayTiles;
            set => bakeStaticDisplayTiles = value;
        }

        public void ApplyBakeStaticToAllDisplayTiles()
        {
            foreach (KeyValuePair<LandscapeCellKey, GameObject> entry in displayGrid)
            {
                if (entry.Value != null)
                    SetBakeStaticOnDisplayTile(entry.Value);
            }
        }

        public void ApplyBakeStaticToLayer(int logicalY, int layerIndex)
        {
            foreach (KeyValuePair<LandscapeCellKey, GameObject> entry in displayGrid)
            {
                if (entry.Key.y != logicalY || entry.Key.layer != layerIndex || entry.Value == null)
                    continue;

                SetBakeStaticOnDisplayTile(entry.Value);
            }
        }

        public void ClearBakeStaticFromAllDisplayTiles()
        {
            foreach (KeyValuePair<LandscapeCellKey, GameObject> entry in displayGrid)
            {
                if (entry.Value != null)
                    ClearBakeStaticFromDisplayTile(entry.Value);
            }
        }

        public void ClearBakeStaticFromLayer(int logicalY, int layerIndex)
        {
            foreach (KeyValuePair<LandscapeCellKey, GameObject> entry in displayGrid)
            {
                if (entry.Key.y != logicalY || entry.Key.layer != layerIndex || entry.Value == null)
                    continue;

                ClearBakeStaticFromDisplayTile(entry.Value);
            }
        }

        private void ApplyBakeStaticToDisplayTile(GameObject tile)
        {
            if (tile == null || !bakeStaticDisplayTiles)
                return;

            SetBakeStaticOnDisplayTile(tile);
        }

        private static void SetBakeStaticOnDisplayTile(GameObject tile)
        {
            if (tile == null)
                return;

    #if UNITY_EDITOR
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(tile);
            flags |= StaticEditorFlags.BatchingStatic;
            GameObjectUtility.SetStaticEditorFlags(tile, flags);
    #else
            tile.isStatic = true;
    #endif
        }

        private static void ClearBakeStaticFromDisplayTile(GameObject tile)
        {
            if (tile == null)
                return;

    #if UNITY_EDITOR
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(tile);
            flags &= ~StaticEditorFlags.BatchingStatic;
            GameObjectUtility.SetStaticEditorFlags(tile, flags);
    #else
            tile.isStatic = false;
    #endif
        }

        private bool IsSubLevelEnabledAt(int logicalY, int layerIndex)
        {
            int listIndex = GetListIndexForLogicalY(logicalY);
            if (listIndex < 0)
                return true;

            EnsureDefaultSubLevels();
            if (!levels[listIndex].enabled)
                return false;

            if (layerIndex < 0 || layerIndex >= levels[listIndex].subLevels.Count)
                return false;

            return levels[listIndex].subLevels[layerIndex].enabled;
        }

        private void AttachTileProxy(GameObject tile, LandscapeCellKey pos)
        {
            DualGridTileProxy proxy = tile.GetComponent<DualGridTileProxy>();
            if (proxy == null)
                proxy = tile.AddComponent<DualGridTileProxy>();

            proxy.owner = this;
            proxy.cellKey = pos;
            proxy.notifyOwnerOnDestroy = true;
        }

        // =====================================================
        // CLEANUP
        // =====================================================

        private void SyncDestroyedDisplayTiles()
        {
            if (suppressExternalSync)
                return;

            List<LandscapeCellKey> destroyedPositions = new();

            foreach (var pair in displayGrid)
            {
                if (pair.Value == null)
                    destroyedPositions.Add(pair.Key);
            }

            foreach (LandscapeCellKey pos in destroyedPositions)
                displayGrid.Remove(pos);

            foreach (LandscapeCellKey pos in destroyedPositions)
                EraseLogicalCell(pos, persist: false);

            if (destroyedPositions.Count > 0)
                PersistLogicalGrid();
        }

        private void CleanupMissingTiles()
        {
            SyncDestroyedDisplayTiles();
        }

        // =====================================================
        // CLEAR GRID
        // =====================================================

        [ContextMenu("Clear Grid")]
        public void ClearAll()
        {
            suppressExternalSync = true;

            foreach (GameObject tile in displayGrid.Values)
                DetachAndDestroyTile(tile);

            displayGrid.Clear();
            logicalGrid.Clear();
            groundDisplayVariants.Clear();
            savedLogicalGrid.Clear();
            savedGroundDisplayVariants.Clear();
            if (paintContent != null)
                paintContent.ClearAll();
            suppressExternalSync = false;
            paintBuffer.Clear();
            eraseBuffer.Clear();
            ClearPaintMask();
            ClearEraseMask();
            PersistLogicalGrid();
        }

        // =====================================================
        // MASK PREVIEW
        // =====================================================

        private void ShowMaskCell(
            LandscapeCellKey coords,
            Dictionary<LandscapeCellKey, GameObject> maskObjects,
            ref Transform maskRoot,
            Color color,
            ref Material maskMaterial,
            string rootName,
            float maskHeight)
        {
            if (maskObjects.ContainsKey(coords))
                return;

            maskRoot = EnsureMaskRoot(maskRoot, rootName);

            GameObject mask = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mask.name = coords.layer == 0
                ? $"Mask_{coords.x}_{coords.y}_{coords.z}"
                : $"Mask_{coords.x}_{coords.y}_{coords.z}_L{coords.layer}";
            mask.transform.SetParent(maskRoot, false);
            Vector3 localMaskCenter = GetCellLocalPlanarCenter(coords, GetMaskCenterPlanarOffset());
            localMaskCenter.y += maskHeight;
            mask.transform.localPosition = localMaskCenter;
            mask.transform.localScale = new Vector3(0.98f, 0.02f, 0.98f);

            Collider collider = mask.GetComponent<Collider>();
            if (collider != null)
                DestroyObjectImmediateOrRuntime(collider);

            mask.GetComponent<Renderer>().sharedMaterial = GetMaskMaterial(color, ref maskMaterial);
            maskObjects[coords] = mask;
        }

        private Transform EnsureMaskRoot(Transform root, string rootName)
        {
            if (root != null)
                return root;

            Transform existing = transform.Find(rootName);
            if (existing != null)
                return existing;

            GameObject rootObject = new GameObject(rootName);
            rootObject.transform.SetParent(transform, false);
            return rootObject.transform;
        }

        private Material GetMaskMaterial(Color color, ref Material cachedMaterial)
        {
            if (cachedMaterial != null)
                return cachedMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            cachedMaterial = new Material(shader);
            cachedMaterial.color = color;
            return cachedMaterial;
        }

        private void ClearPaintMask()
        {
            ClearMaskDictionary(paintMaskObjects);
            DestroyMaskRoot(ref paintMaskRoot, "_PaintMask");
        }

        private void ClearEraseMask()
        {
            ClearMaskDictionary(eraseMaskObjects);
            DestroyMaskRoot(ref eraseMaskRoot, "_EraseMask");
        }

        // Frees the lazily-created mask materials. Idempotent: GetMaskMaterial
        // recreates them on demand, so this is safe to call from OnDisable and OnDestroy.
        private void ReleaseMaskMaterials()
        {
            DestroyObjectImmediateOrRuntime(paintMaskMaterial);
            paintMaskMaterial = null;

            DestroyObjectImmediateOrRuntime(eraseMaskMaterial);
            eraseMaskMaterial = null;
        }

        private void DestroyMaskRoot(ref Transform root, string rootName)
        {
            if (root != null)
            {
                DestroyObjectImmediateOrRuntime(root.gameObject);
            }
            else
            {
                Transform existing = transform.Find(rootName);
                if (existing != null)
                    DestroyObjectImmediateOrRuntime(existing.gameObject);
            }

            root = null;
        }

        private void ClearMaskDictionary(Dictionary<LandscapeCellKey, GameObject> maskObjects)
        {
            foreach (GameObject mask in maskObjects.Values)
                DestroyObjectImmediateOrRuntime(mask);

            maskObjects.Clear();
        }

    #if UNITY_EDITOR
        private void MigrateMissingBiomesFromSceneDisplay()
        {
            if (displayGrid.Count == 0)
                return;

            bool migrated = false;
            List<LandscapeCellKey> keys = new(logicalGrid.Keys);

            foreach (LandscapeCellKey logicalPos in keys)
            {
                LogicalCellState state = logicalGrid[logicalPos];
                // Only fill missing ids. Never overwrite a stored biome from display inference —
                // wrong meshes after undo would permanently recolor other levels.
                if (!string.IsNullOrWhiteSpace(state.biomeId))
                    continue;

                if (!TryInferBiomeIdForLogicalCell(logicalPos, out string inferredBiomeId))
                    continue;

                state.biomeId = inferredBiomeId;
                if (BiomeRegistry.TryGetLegacyBiome(inferredBiomeId, out BrushBiome legacyBiome))
                    state.biome = legacyBiome;

                logicalGrid[logicalPos] = state;
                migrated = true;
            }

            if (migrated)
                PersistLogicalGrid();
        }

        private bool TryInferBiomeIdForLogicalCell(LandscapeCellKey logicalPos, out string biomeId)
        {
            for (int i = 0; i < NEIGHBOURS.Length; i++)
            {
                LandscapeCellKey displayPos = logicalPos.Offset(NEIGHBOURS[i]);
                if (displayGrid.TryGetValue(displayPos, out GameObject tile) &&
                    tile != null &&
                    TryInferBiomeIdFromDisplayObject(tile, out biomeId))
                {
                    return true;
                }
            }

            if (displayGrid.TryGetValue(logicalPos, out GameObject centerTile) &&
                centerTile != null &&
                TryInferBiomeIdFromDisplayObject(centerTile, out biomeId))
            {
                return true;
            }

            biomeId = BiomeIds.Grasslands;
            return false;
        }

        private static bool TryInferBiomeIdFromDisplayObject(GameObject tile, out string biomeId)
        {
            biomeId = BiomeIds.Grasslands;

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(tile);
            if (source == null)
                source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(tile);

            if (source == null)
                return false;

            if (BiomeRegistry.TryInferBiomeFromPrefab(source, out biomeId))
                return true;

            return false;
        }
    #endif
    }

    // =====================================================
    // TILE TYPES
    // =====================================================

    public enum TileType
    {
        None,
        Grass
    }
}
