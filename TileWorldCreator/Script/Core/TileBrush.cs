using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace TileWorldCreator
{
    public class TileBrush
    {
        // === Settings ===
        public string paintMode = "Level";
        public TileBiomeData currentBiome;
        public string currentTileType = "Ground";
        public TileBiomeData environmentBiome;
        public string environmentCategory = "Rocks";
        
        // === Visual ===
        public Color highlightColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
        public Color validColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
        public Color invalidColor = new Color(0.8f, 0.2f, 0.2f, 0.5f);
        
        // === Brush Settings ===
        public bool paintOnDrag = true;
        public float paintInterval = 0.05f;
        
        // === State ===
        private bool isActive = false;
        public Layer targetLayer;
        
        public bool IsActive 
        { 
            get => isActive; 
            set => isActive = value; 
        }
        
        // Внутренние переменные
        private List<GameObject> highlightedObjects = new List<GameObject>();
        private Vector3Int lastHighlightedCell = new Vector3Int(-999, -999, -999);
        private Vector3Int lastPaintedCell = new Vector3Int(-999, -999, -999);
        private bool lastCellValid = false;
        
        private bool isMouseDown = false;
        private float lastPaintTime = 0f;
        private HashSet<Vector3Int> paintedCellsInSession = new HashSet<Vector3Int>();
        
        // ============ PUBLIC METHODS ============
        
        public void OnSceneGUI(SceneView sceneView)
        {
            if (!isActive || targetLayer == null) return;
            
            Grid grid = targetLayer.Grid;
            if (grid == null) return;

            Event e = Event.current;
            
            bool isAltPressed = e.alt || e.control || e.command;
            
            if (isAltPressed)
            {
                if (lastHighlightedCell != new Vector3Int(-999, -999, -999))
                {
                    lastHighlightedCell = new Vector3Int(-999, -999, -999);
                    ClearHighlights();
                }
                return;
            }

            Vector3 mousePosition = e.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            
            float layerY = targetLayer.transform.position.y;
            Plane plane = new Plane(Vector3.up, new Vector3(0, layerY, 0));
            float distance;

            if (plane.Raycast(ray, out distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                
                Vector3 worldPos = new Vector3(hitPoint.x, 0f, hitPoint.z);
                Vector3Int cellPosition = grid.WorldToCell(worldPos);
                cellPosition.y = 0;

                bool cellValid = IsCellValid(cellPosition);
                
                if (cellPosition != lastHighlightedCell || cellValid != lastCellValid)
                {
                    lastHighlightedCell = cellPosition;
                    lastCellValid = cellValid;
                    UpdateHighlight(cellPosition, cellValid);
                }

                if (e.type == EventType.MouseDown && e.button == 0 && !isAltPressed)
                {
                    isMouseDown = true;
                    paintedCellsInSession.Clear();
                    
                    if (cellValid)
                    {
                        PaintTile(cellPosition);
                        lastPaintedCell = cellPosition;
                        paintedCellsInSession.Add(cellPosition);
                    }
                    e.Use();
                }
                
                if (e.type == EventType.MouseUp && e.button == 0)
                {
                    isMouseDown = false;
                    paintedCellsInSession.Clear();
                    e.Use();
                }
                
                if (e.type == EventType.MouseDrag && e.button == 0 && paintOnDrag && !isAltPressed)
                {
                    if (cellValid && cellPosition != lastPaintedCell)
                    {
                        if (Time.realtimeSinceStartup - lastPaintTime >= paintInterval)
                        {
                            if (!paintedCellsInSession.Contains(cellPosition))
                            {
                                PaintTile(cellPosition);
                                lastPaintedCell = cellPosition;
                                paintedCellsInSession.Add(cellPosition);
                                lastPaintTime = Time.realtimeSinceStartup;
                            }
                        }
                    }
                    e.Use();
                }
            }
            else
            {
                if (lastHighlightedCell != new Vector3Int(-999, -999, -999))
                {
                    lastHighlightedCell = new Vector3Int(-999, -999, -999);
                    ClearHighlights();
                }
                
                if (e.type == EventType.MouseUp && e.button == 0)
                {
                    isMouseDown = false;
                    paintedCellsInSession.Clear();
                }
            }

            sceneView.Repaint();
        }
        
        public void ClearAll()
        {
            ClearHighlights();
            lastHighlightedCell = new Vector3Int(-999, -999, -999);
            lastPaintedCell = new Vector3Int(-999, -999, -999);
            isMouseDown = false;
            paintedCellsInSession.Clear();
        }
        
        // ============ PRIVATE METHODS ============
        
        private bool IsCellValid(Vector3Int cellPosition)
        {
            if (targetLayer == null) return false;
            
            if (paintMode == "Level")
            {
                // ИСПРАВЛЕНО: Используем новый метод проверки ТОЛЬКО в этом слое
                return !targetLayer.IsCellOccupiedInThisLayer(cellPosition);
            }
            
            return !IsEnvironmentObjectAtCell(cellPosition);
        }
        
        private void UpdateHighlight(Vector3Int cellPosition, bool valid)
        {
            ClearHighlights();

            Vector3 localPos = targetLayer.GetCellCenterWorld(cellPosition);
            Vector3 worldPos = targetLayer.transform.TransformPoint(localPos);
            worldPos.y = targetLayer.transform.position.y + 0.01f;
            
            GameObject highlight = GameObject.CreatePrimitive(PrimitiveType.Quad);
            highlight.name = "GridHighlight";
            highlight.transform.position = worldPos;
            highlight.transform.rotation = Quaternion.Euler(90, 0, 0);

            Vector3 cellSize = targetLayer.Grid.cellSize;
            highlight.transform.localScale = new Vector3(cellSize.x, cellSize.z, 1);

            Renderer renderer = highlight.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = valid ? validColor : invalidColor;
            renderer.sharedMaterial = mat;

            highlight.hideFlags = HideFlags.HideAndDontSave;
            highlightedObjects.Add(highlight);
        }
        
        private void ClearHighlights()
        {
            foreach (GameObject obj in highlightedObjects)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }
            highlightedObjects.Clear();
        }
        
        private void PaintTile(Vector3Int cellPosition)
        {
            if (targetLayer == null) return;
            
            try
            {
                if (paintMode == "Level")
                {
                    PaintLevelTile(cellPosition);
                }
                else if (paintMode == "Environment")
                {
                    PaintEnvironment(cellPosition);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error painting tile: {ex.Message}");
            }
        }
        
        private void PaintLevelTile(Vector3Int cellPosition)
        {
            if (targetLayer == null) return;
            
            // ИСПРАВЛЕНО: Используем новый метод проверки ТОЛЬКО в этом слое
            if (!targetLayer.IsCellOccupiedInThisLayer(cellPosition))
            {
                GameObject tilePrefab = null;
                
                if (currentBiome != null)
                {
                    tilePrefab = currentBiome.GetRandomTile(currentTileType);
                }
                
                if (tilePrefab != null)
                {
                    // Создаем через Layer
                    Tile tile = targetLayer.CreateTile(cellPosition, currentTileType);
                    
                    if (tile != null)
                    {
                        GameObject tileGO = tile.gameObject;
                        Vector3 localPos = tileGO.transform.localPosition;
                        Vector3 localScale = tileGO.transform.localScale;
                        
                        Object.DestroyImmediate(tileGO);
                        
#if UNITY_EDITOR
                        GameObject newTile = PrefabUtility.InstantiatePrefab(tilePrefab) as GameObject;
                        if (newTile == null)
                            newTile = Object.Instantiate(tilePrefab);
#else
                        GameObject newTile = Object.Instantiate(tilePrefab);
#endif
                        newTile.transform.SetParent(targetLayer.transform, false);
                        newTile.transform.localPosition = localPos;
                        newTile.transform.localScale = localScale;
                        newTile.name = $"Tile_{cellPosition.x}_{cellPosition.z}";
                        
                        Tile tileComponent = newTile.GetComponent<Tile>();
                        if (tileComponent == null)
                            tileComponent = newTile.AddComponent<Tile>();
                        tileComponent.Initialize(cellPosition, currentTileType);
                        
                        if (!targetLayer.Tiles.Contains(tileComponent))
                        {
                            targetLayer.Tiles.Add(tileComponent);
                        }
                    }
                }
                else
                {
                    targetLayer.CreateTile(cellPosition, "Default");
                }
            }
        }
        
        private void PaintEnvironment(Vector3Int cellPosition)
        {
            if (targetLayer == null) return;
            
            if (IsEnvironmentObjectAtCell(cellPosition))
            {
                return;
            }
            
            if (environmentBiome == null)
            {
                Debug.LogWarning("⚠️ No environment biome selected!");
                return;
            }
            
            GameObject prefab = environmentBiome.GetRandomEnvironmentObject(environmentCategory);
            if (prefab == null)
            {
                Debug.LogWarning($"⚠️ No objects in category '{environmentCategory}' for biome '{environmentBiome.displayName}'!");
                return;
            }
            
            WorldRoot worldRoot = targetLayer.GetComponentInParent<WorldRoot>();
            if (worldRoot == null)
            {
                Debug.LogWarning("⚠️ WorldRoot not found!");
                return;
            }
            
            EnvironmentRoot envRoot = worldRoot.Environment;
            if (envRoot == null)
            {
                GameObject envObject = new GameObject("Environment");
                envObject.transform.SetParent(worldRoot.transform);
                envRoot = envObject.AddComponent<EnvironmentRoot>();
            }
            
            Vector3 localPos = targetLayer.GetCellCenterWorld(cellPosition);
            Vector3 worldPos = targetLayer.transform.TransformPoint(localPos);
            worldPos.y = targetLayer.transform.position.y;
            
            GameObject obj = Object.Instantiate(prefab);
            obj.transform.position = worldPos;
            obj.transform.SetParent(envRoot.transform, true);
            obj.name = $"{prefab.name}_{envRoot.EnvironmentObjects.Count}";
            
            if (environmentBiome.randomRotation)
            {
                obj.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            }
            
            float scale = Random.Range(environmentBiome.randomScaleRange.x, environmentBiome.randomScaleRange.y);
            obj.transform.localScale = Vector3.one * scale;
            
            envRoot.EnvironmentObjects.Add(obj);
            
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(obj, $"Place {environmentCategory}");
#endif
        }
        
        private bool IsEnvironmentObjectAtCell(Vector3Int cellPosition)
        {
            if (targetLayer == null) return false;
            
            WorldRoot worldRoot = targetLayer.GetComponentInParent<WorldRoot>();
            if (worldRoot == null) return false;
            
            EnvironmentRoot envRoot = worldRoot.Environment;
            if (envRoot == null) return false;
            
            Vector3 localPos = targetLayer.GetCellCenterWorld(cellPosition);
            Vector3 worldPos = targetLayer.transform.TransformPoint(localPos);
            worldPos.y = targetLayer.transform.position.y;
            
            float cellSize = targetLayer.Grid.cellSize.x;
            float checkRadius = cellSize * 0.3f;
            
            foreach (GameObject obj in envRoot.EnvironmentObjects)
            {
                if (obj == null) continue;
                
                float distance = Vector3.Distance(obj.transform.position, worldPos);
                if (distance < checkRadius)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        // ============ HELPER METHODS ============
        
        public void SetTargetLayer(Layer layer)
        {
            targetLayer = layer;
        }
        
        public void SetBiome(TileBiomeData biome)
        {
            currentBiome = biome;
        }
        
        public void SetTileType(string type)
        {
            currentTileType = type;
        }
        
        public void SetEnvironmentBiome(TileBiomeData biome)
        {
            environmentBiome = biome;
        }
        
        public void SetEnvironmentCategory(string category)
        {
            environmentCategory = category;
        }
        
        public void SetPaintMode(string mode)
        {
            paintMode = mode;
        }
        
        public void SetActive(bool active)
        {
            isActive = active;
            if (!active)
            {
                ClearAll();
            }
        }
        
        public void LoadBiomes()
        {
            // Метод для совместимости с UI
        }
    }
}