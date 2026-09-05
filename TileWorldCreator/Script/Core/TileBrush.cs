using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace TileWorldCreator
{
    public class TileBrush
    {
        // ============================================================
        // SETTINGS
        // ============================================================
        public string paintMode = "Level";
        public TileBiomeData currentBiome;
        public string currentTileType = "Ground";
        public TileBiomeData environmentBiome;
        public string environmentCategory = "Rocks";

        // ============================================================
        // VISUAL
        // ============================================================
        public Color highlightColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
        public Color validColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
        public Color invalidColor = new Color(0.8f, 0.2f, 0.2f, 0.5f);

        // ============================================================
        // BRUSH SETTINGS
        // ============================================================
        public bool paintOnDrag = true;
        public float paintInterval = 0.05f;

        // ============================================================
        // ENVIRONMENT TRANSFORM
        // ============================================================
        public bool environmentRotationEnabled = true;
        public bool environmentRandomRotation = true;
        public float environmentRotation = 0f;
        public bool environmentScaleEnabled = true;
        public float environmentScale = 1f;

        // ============================================================
        // ROTATION
        // ============================================================
        private float currentPreviewRotation = 0f;
        private const float ShiftRotationSpeed = 90f; // градусов в секунду

        // ============================================================
        // STATE
        // ============================================================
        private bool isActive;
        public Layer targetLayer;
        public bool IsActive
        {
            get => isActive;
            set => isActive = value;
        }

        // ============================================================
        // INTERNAL STATE
        // ============================================================
        private readonly List<GameObject> highlightedObjects = new List<GameObject>();
        private Vector3Int lastHighlightedCell = new Vector3Int(-999, -999, -999);
        private Vector3Int lastPaintedCell = new Vector3Int(-999, -999, -999);
        private bool lastCellValid;
        private bool lastErasing;
        private bool isMouseDown;
        private float lastPaintTime;
        private readonly HashSet<Vector3Int> paintedCellsInSession = new HashSet<Vector3Int>();

        // ============================================================
        // HIGHLIGHT
        // ============================================================
        private Material highlightMaterial;

        // ============================================================
        // ENVIRONMENT PREVIEW
        // ============================================================
        private GameObject environmentPreviewObject;
        private GameObject environmentPreviewSourcePrefab;
        private string environmentPreviewCategory;

        // ============================================================
        // PUBLIC METHODS
        // ============================================================
        public void OnSceneGUI(SceneView sceneView)
        {
            if (!isActive) return;

            Level level = GetLevel();
            if (level == null) return;

            // ===== ВАЖНО: всегда берём слои от АКТИВНОГО Level =====
            Layer groundLayer = level.GetGroundLayer();
            if (groundLayer == null) return;

            groundLayer.EnsureGrid();
            Grid grid = groundLayer.Grid;
            if (grid == null) return;

            // Target layer тоже всегда от текущего Level
            if (paintMode == "Environment")
            {
                Layer envLayer = level.GetEnvironmentLayer();
                targetLayer = envLayer != null ? envLayer : groundLayer;
            }
            else
            {
                targetLayer = currentTileType == "Liquid"
                    ? level.GetLiquidLayer()
                    : groundLayer;

                if (targetLayer != null)
                    targetLayer.EnsureGrid();
            }

            Event e = Event.current;
            bool isAltPressed = e.alt || e.command;
            bool isErasing = e.control;

            // Alt = camera navigation
            if (isAltPressed)
            {
                ClearHighlights();
                ClearEnvironmentPreview();
                lastHighlightedCell = new Vector3Int(-999, -999, -999);
                return;
            }

            // ========================================================
            // SHIFT ЗАЖАТ → ПРЕФАБ КРУТИТСЯ ПО ЧАСОВОЙ
            // ========================================================
            if (paintMode == "Environment" && e.shift && !e.control && !e.alt && !e.command)
            {
                environmentRandomRotation = false;
                currentPreviewRotation += ShiftRotationSpeed * Time.deltaTime;
                currentPreviewRotation = Mathf.Repeat(currentPreviewRotation, 360f);
                environmentRotation = currentPreviewRotation;

                if (environmentPreviewObject != null)
                {
                    environmentPreviewObject.transform.rotation =
                        Quaternion.Euler(0f, currentPreviewRotation, 0f);
                }

                e.Use();
                sceneView.Repaint();
            }

            // Mouse ray
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            float groundY = groundLayer.transform.position.y;
            Plane plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));

            if (!plane.Raycast(ray, out float distance))
            {
                ClearHighlights();
                ClearEnvironmentPreview();
                lastHighlightedCell = new Vector3Int(-999, -999, -999);
                sceneView.Repaint();
                return;
            }

            Vector3 hitPoint = ray.GetPoint(distance);
            Vector3 worldPos = new Vector3(hitPoint.x, groundY, hitPoint.z);
            Vector3Int cellPosition = grid.WorldToCell(worldPos);
            cellPosition.y = 0;

            bool occupied = paintMode != "Environment" &&
                            targetLayer != null &&
                            targetLayer.IsCellOccupiedInThisLayer(cellPosition);

            bool cellValid = paintMode == "Environment" || (isErasing ? occupied : true);

            // Highlight / Preview
            bool needUpdate = cellPosition != lastHighlightedCell ||
                              cellValid != lastCellValid ||
                              isErasing != lastErasing ||
                              paintMode == "Environment";

            if (needUpdate)
            {
                lastHighlightedCell = cellPosition;
                lastCellValid = cellValid;
                lastErasing = isErasing;

                if (isErasing)
                {
                    ClearEnvironmentPreview();
                    UpdateHighlight(cellPosition, groundLayer, true);
                }
                else if (paintMode == "Environment")
                {
                    ClearHighlights();
                    UpdateEnvironmentPreview(cellPosition, worldPos, groundLayer);
                }
                else if (occupied)
                {
                    ClearHighlights();
                    ClearEnvironmentPreview();
                }
                else
                {
                    UpdateHighlight(cellPosition, groundLayer, false);
                }
            }

            // Mouse Down
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                isMouseDown = true;
                paintedCellsInSession.Clear();

                if (cellValid)
                {
                    if (isErasing)
                        EraseTile(cellPosition, worldPos, groundLayer);
                    else
                        PaintTile(cellPosition, worldPos, groundLayer);

                    lastPaintedCell = cellPosition;
                    paintedCellsInSession.Add(cellPosition);
                    lastPaintTime = Time.realtimeSinceStartup;
                }
                e.Use();
            }

            // Mouse Up
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                isMouseDown = false;
                paintedCellsInSession.Clear();
                e.Use();
            }

            // Mouse Drag
            if (e.type == EventType.MouseDrag && e.button == 0 && paintOnDrag)
            {
                if (cellValid &&
                    cellPosition != lastPaintedCell &&
                    Time.realtimeSinceStartup - lastPaintTime >= paintInterval &&
                    !paintedCellsInSession.Contains(cellPosition))
                {
                    if (isErasing)
                        EraseTile(cellPosition, worldPos, groundLayer);
                    else
                        PaintTile(cellPosition, worldPos, groundLayer);

                    lastPaintedCell = cellPosition;
                    paintedCellsInSession.Add(cellPosition);
                    lastPaintTime = Time.realtimeSinceStartup;
                }
                e.Use();
            }

            sceneView.Repaint();
        }

        // ============================================================
        // CLEAR
        // ============================================================
        public void ClearAll()
        {
            ClearHighlights();
            ClearEnvironmentPreview();
            lastHighlightedCell = new Vector3Int(-999, -999, -999);
            lastPaintedCell = new Vector3Int(-999, -999, -999);
            isMouseDown = false;
            paintedCellsInSession.Clear();

            if (highlightMaterial != null)
            {
                Object.DestroyImmediate(highlightMaterial);
                highlightMaterial = null;
            }
        }

        private Level GetLevel()
        {
            // Всегда берём АКТИВНЫЙ Level из LevelsRoot
            LevelsRoot levelsRoot =
        #if UNITY_EDITOR
                Object.FindObjectOfType<LevelsRoot>();
        #else
                Object.FindFirstObjectByType<LevelsRoot>();
        #endif

            if (levelsRoot != null)
            {
                Level active = levelsRoot.GetActiveLevel();
                if (active != null)
                    return active;
            }

            // Fallback (если LevelsRoot нет)
        #if UNITY_EDITOR
            return Object.FindObjectOfType<Level>();
        #else
            return Object.FindFirstObjectByType<Level>();
        #endif
        }

        // ============================================================
        // TILE HEIGHT
        // ============================================================
        private float GetTileHeight(Vector3Int cellPosition, Layer groundLayer)
        {
            if (groundLayer == null) return 1f;
            
            Tile tile = groundLayer.GetTileAt(cellPosition);
            if (tile != null)
            {
                // ВАРИАНТ 1: Если у Tile есть свойство Height
                // if (tile.Height > 0)
                //     return tile.Height;
                
                // ВАРИАНТ 2: Если у Tile есть метод GetHeight()
                // return tile.GetHeight();
                
                // ВАРИАНТ 3: Если высота хранится в биоме тайла
                // if (tile.Biome != null && tile.Biome.tileHeight > 0)
                //     return tile.Biome.tileHeight;
                
                // ВАРИАНТ 4: Если высота - это просто Y позиция тайла
                // return tile.transform.position.y - groundLayer.transform.position.y;
            }
            
            // Если тайла нет, используем высоту из биома для превью
            if (currentBiome != null && currentBiome.tileHeight > 0)
                return currentBiome.tileHeight;
            
            return 1f;
        }

        // ============================================================
        // ПОЛУЧЕНИЕ ВЫСОТЫ ДЛЯ ХАЙЛАЙТА (С УЧЁТОМ НАЛИЧИЯ ТАЙЛА)
        // ============================================================
     // ============================================================
// ============================================================
// ПОЛУЧЕНИЕ ВЫСОТЫ ДЛЯ ХАЙЛАЙТА
// ============================================================
private float GetHighlightHeight(Vector3Int cellPosition, Layer groundLayer, bool isErasing)
{
    if (groundLayer == null) return 0f;
    
    float baseY = groundLayer.transform.position.y;
    
    // Проверяем наличие тайла
    Tile tile = groundLayer.GetTileAt(cellPosition);
    if (tile != null)
    {
        // Если тайл есть - поднимаемся на его высоту (для всех цветов)
        float tileHeight = GetTileHeight(cellPosition, groundLayer);
        return baseY + tileHeight + 0.02f;
    }
    
    // Для Environment проверяем объекты окружения
    if (paintMode == "Environment")
    {
        GameObject envObject = FindEnvironmentObjectAtCell(cellPosition);
        if (envObject != null)
        {
            Bounds bounds = GetObjectBounds(envObject);
            return bounds.center.y + bounds.extents.y + 0.02f;
        }
    }
    
    // Если ничего нет - на уровне земли
    return baseY + 0.02f;
}

        private Bounds GetObjectBounds(GameObject obj)
        {
            Renderer renderer = obj.GetComponentInChildren<Renderer>();
            if (renderer != null)
                return renderer.bounds;
            
            // Если нет рендерера, используем позицию
            return new Bounds(obj.transform.position, Vector3.one);
        }

        // ============================================================
        // HIGHLIGHT
        // ============================================================
        private void UpdateHighlight(Vector3Int cellPosition, Layer gridLayer, bool showRed)
        {
            ClearHighlights();
            if (gridLayer == null || gridLayer.Grid == null) return;

            Vector3 worldPos = gridLayer.Grid.GetCellCenterWorld(cellPosition);
            
            // Получаем высоту для хайлайта с учётом режима (удаление/рисование)
            float highlightY = GetHighlightHeight(cellPosition, gridLayer, showRed);
            worldPos.y = highlightY;

            GameObject highlight = GameObject.CreatePrimitive(PrimitiveType.Quad);
            highlight.name = "GridHighlight";
            highlight.transform.position = worldPos;
            highlight.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Vector3 cellSize = gridLayer.Grid.cellSize;
            highlight.transform.localScale = new Vector3(cellSize.x, cellSize.z, 1f);

            Renderer renderer = highlight.GetComponent<Renderer>();
            if (highlightMaterial == null)
            {
                Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                highlightMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            highlightMaterial.color = showRed ? invalidColor : validColor;
            renderer.sharedMaterial = highlightMaterial;
            highlight.hideFlags = HideFlags.HideAndDontSave;
            highlightedObjects.Add(highlight);
        }

        private void ClearHighlights()
        {
            foreach (var obj in highlightedObjects)
                if (obj != null)
                    Object.DestroyImmediate(obj);

            highlightedObjects.Clear();
        }

        // ============================================================
        // ENVIRONMENT PREVIEW
        // ============================================================
        private void UpdateEnvironmentPreview(Vector3Int cellPosition, Vector3 rawWorldPos, Layer groundLayer)
        {
            if (environmentBiome == null)
            {
                ClearEnvironmentPreview();
                return;
            }

            bool categoryChanged = environmentPreviewCategory != environmentCategory;

            if (environmentPreviewObject == null || categoryChanged)
            {
                ClearEnvironmentPreview();

                GameObject prefab = environmentBiome.GetRandomEnvironmentObject(environmentCategory);
                if (prefab == null) return;

                environmentPreviewObject = Object.Instantiate(prefab);
                environmentPreviewObject.name = "EnvironmentPreview";
                environmentPreviewObject.hideFlags = HideFlags.HideAndDontSave;
                environmentPreviewSourcePrefab = prefab;
                environmentPreviewCategory = environmentCategory;

                foreach (var col in environmentPreviewObject.GetComponentsInChildren<Collider>())
                    col.enabled = false;

                currentPreviewRotation = environmentRandomRotation
                    ? Random.Range(0f, 360f)
                    : environmentRotation;
            }

            Vector3 position = rawWorldPos;
            position.y = GetTileTopWorldY(cellPosition, groundLayer);
            environmentPreviewObject.transform.position = position;

            if (environmentRotationEnabled)
                environmentPreviewObject.transform.rotation = Quaternion.Euler(0f, currentPreviewRotation, 0f);

            if (environmentScaleEnabled && environmentPreviewSourcePrefab != null)
                environmentPreviewObject.transform.localScale =
                    environmentPreviewSourcePrefab.transform.localScale * environmentScale;
        }

        private void ClearEnvironmentPreview()
        {
            if (environmentPreviewObject != null)
                Object.DestroyImmediate(environmentPreviewObject);

            environmentPreviewObject = null;
            environmentPreviewSourcePrefab = null;
            environmentPreviewCategory = null;
        }

        // ============================================================
        // ERASE
        // ============================================================
        private void EraseTile(Vector3Int cellPosition, Vector3 worldPos, Layer groundLayer)
        {
            try
            {
                if (paintMode == "Level")
                    EraseLevelTile(cellPosition);
                else
                    EraseEnvironmentObject(cellPosition, worldPos);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error erasing tile: {ex.Message}");
            }
        }

        private void EraseLevelTile(Vector3Int cellPosition)
        {
            if (targetLayer == null) return;

            Tile tile = targetLayer.GetTileAt(cellPosition);
            if (tile == null) return;

            string tileType = tile.TileType;
            targetLayer.Tiles.Remove(tile);
            targetLayer.DestroyTile(tile);
            targetLayer.RefreshDualDisplayAround(cellPosition, currentBiome, tileType);
        }

        private void EraseEnvironmentObject(Vector3Int cellPosition, Vector3 worldPos)
        {
            WorldRoot worldRoot = FindWorldRoot();
            if (worldRoot == null) return;

            EnvironmentRoot envRoot = worldRoot.Environment;
            if (envRoot == null) return;

            GameObject closest = null;
            float closestDistance = float.MaxValue;

            foreach (var obj in envRoot.EnvironmentObjects)
            {
                if (obj == null) continue;

                var marker = obj.GetComponent<EnvironmentObjectMarker>();
                if (marker == null) continue;
                if (marker.CellPosition.x != cellPosition.x || marker.CellPosition.z != cellPosition.z)
                    continue;

                float distance = Vector2.Distance(
                    new Vector2(obj.transform.position.x, obj.transform.position.z),
                    new Vector2(worldPos.x, worldPos.z));

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = obj;
                }
            }

            if (closest != null)
            {
                envRoot.EnvironmentObjects.Remove(closest);

        #if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.DestroyObjectImmediate(closest);
                else
                    Object.Destroy(closest);
        #else
                Object.Destroy(closest);
        #endif
            }
        }

        // ============================================================
        // PAINT
        // ============================================================
        private void PaintTile(Vector3Int cellPosition, Vector3 rawWorldPos, Layer groundLayer)
        {
            try
            {
                if (paintMode == "Level")
                    PaintLevelTile(cellPosition);
                else
                    PaintEnvironment(cellPosition, rawWorldPos, groundLayer);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error painting tile: {ex.Message}");
            }
        }

        private void PaintLevelTile(Vector3Int cellPosition)
        {
            if (targetLayer == null) return;

            Tile existing = targetLayer.GetTileAt(cellPosition);
            if (existing != null)
            {
                if (existing.TileType == currentTileType)
                {
                    existing.CycleVariant();
                    targetLayer.RefreshDualDisplayAround(cellPosition, currentBiome, currentTileType);
                }
                return;
            }

            Tile tile = targetLayer.CreateTile(cellPosition, currentTileType);
            if (tile != null)
            {
                if (!targetLayer.Tiles.Contains(tile))
                    targetLayer.Tiles.Add(tile);

                targetLayer.RefreshDualDisplayAround(cellPosition, currentBiome, currentTileType);
            }
        }

        private void PaintEnvironment(Vector3Int cellPosition, Vector3 rawWorldPos, Layer groundLayer)
        {
            if (environmentBiome == null)
            {
                Debug.LogWarning("No environment biome selected!");
                return;
            }

            // Используем тот же префаб, что и в preview
            GameObject prefab = environmentPreviewSourcePrefab
                                ?? environmentBiome.GetRandomEnvironmentObject(environmentCategory);

            if (prefab == null)
            {
                Debug.LogWarning($"No objects in category '{environmentCategory}'");
                return;
            }

            WorldRoot worldRoot = FindWorldRoot();
            if (worldRoot == null)
            {
                Debug.LogWarning("WorldRoot not found!");
                return;
            }

            EnvironmentRoot envRoot = worldRoot.Environment;
            if (envRoot == null)
            {
                Transform existing = worldRoot.transform.Find("Environment");
                if (existing != null)
                    envRoot = existing.GetComponent<EnvironmentRoot>();

                if (envRoot == null)
                {
                    GameObject envObject = new GameObject("Environment");
                    envObject.transform.SetParent(worldRoot.transform, false);
                    envRoot = envObject.AddComponent<EnvironmentRoot>();
                }
            }

            Vector3 worldPos = rawWorldPos;
            worldPos.y = GetTileTopWorldY(cellPosition, groundLayer);

            GameObject obj = Object.Instantiate(prefab);
            obj.transform.SetParent(envRoot.transform, true);
            obj.transform.position = worldPos;
            obj.name = $"{prefab.name}_{envRoot.EnvironmentObjects.Count}";

            var marker = obj.GetComponent<EnvironmentObjectMarker>()
                         ?? obj.AddComponent<EnvironmentObjectMarker>();
            marker.Initialize(cellPosition);

            // Ротация — точно как в preview
            if (environmentRotationEnabled)
                obj.transform.rotation = Quaternion.Euler(0f, currentPreviewRotation, 0f);

            // Скейл
            if (environmentScaleEnabled)
                obj.transform.localScale = prefab.transform.localScale * environmentScale;

            if (!envRoot.EnvironmentObjects.Contains(obj))
                envRoot.EnvironmentObjects.Add(obj);

        #if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(obj, $"Place {environmentCategory}");
        #endif
        }

        // ============================================================
        // FIND WORLD ROOT
        // ============================================================
        private WorldRoot FindWorldRoot()
        {
            if (targetLayer != null)
            {
                var root = targetLayer.GetComponentInParent<WorldRoot>();
                if (root != null) return root;
            }

            Level level = GetLevel();
            if (level != null)
            {
                var root = level.GetComponentInParent<WorldRoot>();
                if (root != null) return root;
            }

        #if UNITY_EDITOR
            return Object.FindObjectOfType<WorldRoot>();
        #else
            return Object.FindFirstObjectByType<WorldRoot>();
        #endif
        }

        // ============================================================
        // ENVIRONMENT OBJECT SEARCH
        // ============================================================
        private bool IsEnvironmentObjectAtCell(Vector3Int cellPosition)
        {
            return FindEnvironmentObjectAtCell(cellPosition) != null;
        }

        private GameObject FindEnvironmentObjectAtCell(Vector3Int cellPosition)
        {
            WorldRoot worldRoot = FindWorldRoot();
            if (worldRoot == null) return null;

            EnvironmentRoot envRoot = worldRoot.Environment;
            if (envRoot == null) return null;

            foreach (var obj in envRoot.EnvironmentObjects)
            {
                if (obj == null) continue;

                var marker = obj.GetComponent<EnvironmentObjectMarker>();
                if (marker != null &&
                    marker.CellPosition.x == cellPosition.x &&
                    marker.CellPosition.z == cellPosition.z)
                    return obj;
            }
            return null;
        }

        // ============================================================
        // TILE HEIGHT (FOR ENVIRONMENT PREVIEW)
        // ============================================================
        private float GetTileTopWorldY(Vector3Int cellPosition, Layer groundLayer)
        {
            if (groundLayer == null) return 0f;

            float baseY = groundLayer.transform.position.y;
            float tileHeight = GetTileHeight(cellPosition, groundLayer);
            return baseY + tileHeight;
        }

        // ============================================================
        // HELPERS
        // ============================================================
        public void SetTargetLayer(Layer layer) => targetLayer = layer;

        public void SetBiome(TileBiomeData biome) => currentBiome = biome;

        public void SetTileType(string type)
        {
            currentTileType = type;
            ClearHighlights();

            Level level = GetLevel();
            if (level == null) return;

            targetLayer = currentTileType == "Liquid"
                ? level.GetLiquidLayer()
                : level.GetGroundLayer();

            if (targetLayer != null)
                targetLayer.EnsureGrid();
        }

        public void SetEnvironmentBiome(TileBiomeData biome)
        {
            environmentBiome = biome;
            ClearEnvironmentPreview();
            currentPreviewRotation = environmentRotation;
        }

        public void SetEnvironmentCategory(string category)
        {
            environmentCategory = category;
            ClearEnvironmentPreview();
        }

        public void SetPaintMode(string mode)
        {
            paintMode = mode;
            ClearHighlights();
            ClearEnvironmentPreview();
            lastHighlightedCell = new Vector3Int(-999, -999, -999);

            Level level = GetLevel();
            if (level == null) return;

            if (paintMode == "Environment")
            {
                Layer envLayer = level.GetEnvironmentLayer();
                targetLayer = envLayer != null ? envLayer : level.GetGroundLayer();
            }
            else if (currentTileType == "Liquid")
            {
                targetLayer = level.GetLiquidLayer();
            }
            else
            {
                targetLayer = level.GetGroundLayer();
            }

            if (targetLayer != null)
                targetLayer.EnsureGrid();
        }

        public void SetActive(bool active)
        {
            isActive = active;
            if (!active)
            {
                ClearAll();
                return;
            }

            Level level = GetLevel();
            if (level == null) return;

            if (paintMode == "Environment")
            {
                Layer envLayer = level.GetEnvironmentLayer();
                targetLayer = envLayer != null ? envLayer : level.GetGroundLayer();
            }
            else if (currentTileType == "Liquid")
            {
                targetLayer = level.GetLiquidLayer();
            }
            else
            {
                targetLayer = level.GetGroundLayer();
            }

            if (targetLayer != null)
                targetLayer.EnsureGrid();
        }

        public void LoadBiomes() { }
    }
}