using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

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
        public Color highlightColor = new Color(0.2f, 0.8f, 0.2f, 0.25f);
        public Color outlineColor = new Color(0f, 0f, 0f, 0.5f);
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
        private Vector3Int lastHighlightedCell = new Vector3Int(-999, -999, -999);
        private Vector3Int lastPaintedCell = new Vector3Int(-999, -999, -999);
        private bool lastCellValid = false;
        
        private bool isMouseDown = false;
        private float lastPaintTime = 0f;
        private HashSet<Vector3Int> paintedCellsInSession = new HashSet<Vector3Int>();
        
        // ============ PUBLIC METHODS ============
        
        public void OnSceneGUI(SceneView sceneView)
        {
#if UNITY_EDITOR
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
                }

                // Рисуем подсветку через Handles
                DrawHighlight(cellPosition, cellValid);

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
                }
                
                if (e.type == EventType.MouseUp && e.button == 0)
                {
                    isMouseDown = false;
                    paintedCellsInSession.Clear();
                }
            }

            sceneView.Repaint();
#endif
        }
        
        public void ClearAll()
        {
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
                return !targetLayer.IsCellOccupiedInThisLayer(cellPosition);
            }
            
            return !IsEnvironmentObjectAtCell(cellPosition);
        }
        
#if UNITY_EDITOR
        private void DrawHighlight(Vector3Int cellPosition, bool valid)
        {
            if (targetLayer == null || targetLayer.Grid == null) return;

            Vector3 center = targetLayer.GetCellCenterWorld(cellPosition);
            float layerY = targetLayer.transform.position.y + 0.01f;
            center.y = layerY;

            Vector3 cellSize = targetLayer.Grid.cellSize;
            float hx = cellSize.x * 0.5f;
            float hz = cellSize.z * 0.5f;

            Vector3 bl = new Vector3(center.x - hx, center.y, center.z - hz);
            Vector3 tl = new Vector3(center.x - hx, center.y, center.z + hz);
            Vector3 tr = new Vector3(center.x + hx, center.y, center.z + hz);
            Vector3 br = new Vector3(center.x + hx, center.y, center.z - hz);

            Color fill = valid ? validColor : invalidColor;
            Color outline = outlineColor;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.DrawSolidRectangleWithOutline(new Vector3[] { bl, tl, tr, br }, fill, outline);
        }
#endif
        
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
            
            if (!targetLayer.IsCellOccupiedInThisLayer(cellPosition))
            {
                GameObject tilePrefab = null;
                
                if (currentBiome != null)
                {
                    tilePrefab = currentBiome.GetRandomTile(currentTileType);
                }

                if (tilePrefab != null)
                {
                    Tile tile = targetLayer.CreateTile(cellPosition, currentTileType, tilePrefab);
                    // CreateTile already handles visual instantiation and Undo.
                }
                else
                {
                    targetLayer.CreateTile(cellPosition, "Default", null);
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
            
            float cellSize = 1f;
            if (targetLayer.Grid != null)
                cellSize = targetLayer.Grid.cellSize.x;
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
