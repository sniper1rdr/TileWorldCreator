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
        private bool lastErasing = false;
        
        private bool isMouseDown = false;
        private float lastPaintTime = 0f;
        private HashSet<Vector3Int> paintedCellsInSession = new HashSet<Vector3Int>();

        // Кэшированный материал для подсветки (чтобы не выделять память каждый кадр)
        private Material highlightMaterial;
        
        // ============ PUBLIC METHODS ============
        
        public void OnSceneGUI(SceneView sceneView)
        {
            if (!isActive || targetLayer == null) return;
            
            Grid grid = targetLayer.Grid;
            if (grid == null) return;

            Event e = Event.current;
            
            // Alt/Cmd остаются для навигации камерой (орбита/панорама) - не трогаем их
            bool isAltPressed = e.alt || e.command;
            // Ctrl - режим стирания: делает "дыры" в уровне
            bool isErasing = e.control;
            
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

                bool occupied = IsCellOccupied(cellPosition);
                // В режиме стирания клик что-то делает только если в клетке ЕСТЬ тайл.
                // В обычном режиме клик разрешён и на пустой, и на занятой клетке -
                // повторный клик по уже занятой клетке того же типа переключает
                // визуальный вариант тайла (см. PaintLevelTile), а не ставит новый.
                bool cellValid = isErasing ? occupied : true;
                
                if (cellPosition != lastHighlightedCell || cellValid != lastCellValid || isErasing != lastErasing)
                {
                    lastHighlightedCell = cellPosition;
                    lastCellValid = cellValid;
                    lastErasing = isErasing;

                    if (isErasing)
                    {
                        // В режиме стирания всегда показываем красный курсор
                        UpdateHighlight(cellPosition, showRed: true);
                    }
                    else if (occupied)
                    {
                        // Занятую клетку в обычном режиме больше не подсвечиваем красным - просто ничего не показываем
                        ClearHighlights();
                    }
                    else
                    {
                        UpdateHighlight(cellPosition, showRed: false);
                    }
                }

                if (e.type == EventType.MouseDown && e.button == 0 && !isAltPressed)
                {
                    isMouseDown = true;
                    paintedCellsInSession.Clear();
                    
                    if (cellValid)
                    {
                        if (isErasing)
                            EraseTile(cellPosition);
                        else
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
                                if (isErasing)
                                    EraseTile(cellPosition);
                                else
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

            // Очистим кэшированный материал
            if (highlightMaterial != null)
            {
                Object.DestroyImmediate(highlightMaterial);
                highlightMaterial = null;
            }
        }
        
        // ============ PRIVATE METHODS ============
        
        /// <summary>Занята ли клетка в текущем режиме рисования (Level: тайлом в этом слое, Environment: объектом окружения).</summary>
        private bool IsCellOccupied(Vector3Int cellPosition)
        {
            if (targetLayer == null) return false;
            
            if (paintMode == "Level")
            {
                // ИСПРАВЛЕНО: Используем новый метод проверки ТОЛЬКО в этом слое
                return targetLayer.IsCellOccupiedInThisLayer(cellPosition);
            }
            
            return IsEnvironmentObjectAtCell(cellPosition);
        }
        
        private void UpdateHighlight(Vector3Int cellPosition, bool showRed)
        {
            ClearHighlights();

            Vector3 localPos = targetLayer.GetCellCenterWorld(cellPosition);
            Vector3 worldPos = targetLayer.transform.TransformPoint(localPos);
            // Ставим курсор НАД тайлом/объектом в клетке (а не под его мешем) - иначе его не видно,
            // особенно в режиме стирания (Ctrl), когда клетка занята.
            worldPos.y = GetHighlightWorldY(cellPosition);
            
            GameObject highlight = GameObject.CreatePrimitive(PrimitiveType.Quad);
            highlight.name = "GridHighlight";
            highlight.transform.position = worldPos;
            highlight.transform.rotation = Quaternion.Euler(90, 0, 0);

            Vector3 cellSize = Vector3.one;
            if (targetLayer.Grid != null)
                cellSize = targetLayer.Grid.cellSize;

            highlight.transform.localScale = new Vector3(cellSize.x, cellSize.z, 1);

            Renderer renderer = highlight.GetComponent<Renderer>();

            if (highlightMaterial == null)
            {
                Shader shader = Shader.Find("Unlit/Color");
                highlightMaterial = new Material(shader ?? Shader.Find("Sprites/Default"));
                highlightMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            // Меняем цвет кэширующегося материала
            highlightMaterial.color = showRed ? invalidColor : validColor;
            renderer.sharedMaterial = highlightMaterial;

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
        
        /// <summary>Стирание тайла/объекта окружения под курсором (Ctrl зажат) - делает "дыру" в уровне.</summary>
        private void EraseTile(Vector3Int cellPosition)
        {
            if (targetLayer == null) return;

            try
            {
                if (paintMode == "Level")
                {
                    EraseLevelTile(cellPosition);
                }
                else if (paintMode == "Environment")
                {
                    EraseEnvironmentObject(cellPosition);
                }
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

            // Дырка могла изменить внешность окружающих display-клеток дуальной сетки.
            targetLayer.RefreshDualDisplayAround(cellPosition, tileType, currentBiome);
        }

        private void EraseEnvironmentObject(Vector3Int cellPosition)
        {
            if (targetLayer == null) return;

            WorldRoot worldRoot = targetLayer.GetComponentInParent<WorldRoot>();
            if (worldRoot == null) return;

            EnvironmentRoot envRoot = worldRoot.Environment;
            if (envRoot == null) return;

            GameObject closest = FindEnvironmentObjectAtCell(cellPosition);

            if (closest != null)
            {
                envRoot.EnvironmentObjects.Remove(closest);

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.DestroyObjectImmediate(closest);
                else
                    Object.Destroy(closest);
#else
                Object.Destroy(closest);
#endif
            }
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

            Tile existing = targetLayer.GetTileAt(cellPosition);
            if (existing != null)
            {
                // Клетка уже занята. Если это тот же тип тайла - переключаем
                // визуальный вариант (следующий префаб из пула), чтобы соседние
                // тайлы одного типа не выглядели одинаково. Другой тип поверх
                // существующего тайла клик не ставит.
                if (existing.TileType == currentTileType)
                {
                    existing.CycleVariant();
                    targetLayer.RefreshDualDisplayAround(cellPosition, currentTileType, currentBiome);
                }
                return;
            }

            // Логический тайл - это просто маркер занятости клетки, без
            // собственного меша: вся видимая геометрия строится отдельно
            // на дуальной (смещённой на пол-клетки) сетке display-тайлов.
            Tile t = targetLayer.CreateTile(cellPosition, currentTileType);
            if (t != null)
            {
                if (!targetLayer.Tiles.Contains(t))
                    targetLayer.Tiles.Add(t);

                // Пересчитываем 4 display-клетки дуальной сетки, которые
                // затрагивает эта логическая клетка.
                targetLayer.RefreshDualDisplayAround(cellPosition, currentTileType, currentBiome);
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
            return FindEnvironmentObjectAtCell(cellPosition) != null;
        }

        /// <summary>Найти ближайший объект окружения в клетке (в радиусе checkRadius от её центра), либо null.</summary>
        private GameObject FindEnvironmentObjectAtCell(Vector3Int cellPosition)
        {
            if (targetLayer == null) return null;

            WorldRoot worldRoot = targetLayer.GetComponentInParent<WorldRoot>();
            if (worldRoot == null) return null;

            EnvironmentRoot envRoot = worldRoot.Environment;
            if (envRoot == null) return null;

            Vector3 localPos = targetLayer.GetCellCenterWorld(cellPosition);
            Vector3 worldPos = targetLayer.transform.TransformPoint(localPos);
            worldPos.y = targetLayer.transform.position.y;

            float cellSize = 1f;
            if (targetLayer.Grid != null)
                cellSize = targetLayer.Grid.cellSize.x;
            float checkRadius = cellSize * 0.3f;

            GameObject closest = null;
            float closestDistance = float.MaxValue;

            foreach (GameObject obj in envRoot.EnvironmentObjects)
            {
                if (obj == null) continue;

                float distance = Vector3.Distance(obj.transform.position, worldPos);
                if (distance < checkRadius && distance < closestDistance)
                {
                    closest = obj;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        /// <summary>
        /// Высота (мировая Y), на которой нужно рисовать курсор-подсветку, чтобы он был виден
        /// НАД тайлом/объектом в клетке, а не спрятан под его мешем.
        /// </summary>
        private float GetHighlightWorldY(Vector3Int cellPosition)
        {
            float baseY = targetLayer.transform.position.y + 0.01f;

            if (paintMode == "Level")
            {
                // В Level-режиме логический Tile - это пустой маркер без меша
                // (вся геометрия рисуется отдельно на смещённой dual-grid
                // сетке), поэтому высоту берём из настроек биома, а не из
                // Renderer.bounds.
                Tile tile = targetLayer.GetTileAt(cellPosition);
                if (tile == null) return baseY;

                float height = currentBiome != null ? currentBiome.tileHeight : 1f;
                return baseY + height + 0.02f;
            }

            GameObject occupant = FindEnvironmentObjectAtCell(cellPosition);
            if (occupant == null) return baseY;

            Renderer[] renderers = occupant.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return baseY;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float topY = bounds.max.y + 0.02f;
            return Mathf.Max(baseY, topY);
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
