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
                
                // В Environment-режиме подсветка следует точно за курсором внутри
                // клетки (свободное размещение, не по центру), поэтому обновляем
                // её каждый кадр, даже если клетка не изменилась.
                bool needHighlightUpdate = cellPosition != lastHighlightedCell || cellValid != lastCellValid || isErasing != lastErasing || paintMode == "Environment";

                if (needHighlightUpdate)
                {
                    lastHighlightedCell = cellPosition;
                    lastCellValid = cellValid;
                    lastErasing = isErasing;

                    if (isErasing)
                    {
                        // В режиме стирания всегда показываем красный курсор
                        UpdateHighlight(cellPosition, worldPos, showRed: true);
                    }
                    else if (occupied)
                    {
                        // Занятую клетку в обычном режиме больше не подсвечиваем красным - просто ничего не показываем
                        ClearHighlights();
                    }
                    else
                    {
                        UpdateHighlight(cellPosition, worldPos, showRed: false);
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
                            PaintTile(cellPosition, worldPos);
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
                                    PaintTile(cellPosition, worldPos);
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
        
        private void UpdateHighlight(Vector3Int cellPosition, Vector3 rawWorldPos, bool showRed)
        {
            ClearHighlights();

            Vector3 worldPos;
            if (paintMode == "Environment")
            {
                // Свободное размещение - подсветка идёт точно под курсором внутри
                // клетки, а не привязана к её центру.
                worldPos = rawWorldPos;
            }
            else
            {
                Vector3 localPos = targetLayer.GetCellCenterWorld(cellPosition);
                worldPos = targetLayer.transform.TransformPoint(localPos);
            }
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

        private void PaintTile(Vector3Int cellPosition, Vector3 rawWorldPos)
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
                    PaintEnvironment(cellPosition, rawWorldPos);
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
        
        private void PaintEnvironment(Vector3Int cellPosition, Vector3 rawWorldPos)
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
            
            // Свободное размещение: X/Z берём прямо из точки клика (не по центру
            // клетки), а Y - поверх tile (см. GetHighlightWorldY/GetTileTopWorldY),
            // а не на уровне земли под его мешем.
            Vector3 worldPos = rawWorldPos;
            worldPos.y = GetTileTopWorldY(cellPosition);
            
            GameObject obj = Object.Instantiate(prefab);
            obj.transform.position = worldPos;
            obj.transform.SetParent(envRoot.transform, true);
            obj.name = $"{prefab.name}_{envRoot.EnvironmentObjects.Count}";

            EnvironmentObjectMarker marker = obj.GetComponent<EnvironmentObjectMarker>();
            if (marker == null)
                marker = obj.AddComponent<EnvironmentObjectMarker>();
            marker.Initialize(cellPosition);
            
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

        /// <summary>
        /// Найти объект окружения, помеченный этой логической клеткой (см.
        /// EnvironmentObjectMarker) - не зависит от его точной X/Z позиции,
        /// т.к. объекты окружения ставятся свободно внутри клетки, а не по её центру.
        /// </summary>
        private GameObject FindEnvironmentObjectAtCell(Vector3Int cellPosition)
        {
            if (targetLayer == null) return null;

            WorldRoot worldRoot = targetLayer.GetComponentInParent<WorldRoot>();
            if (worldRoot == null) return null;

            EnvironmentRoot envRoot = worldRoot.Environment;
            if (envRoot == null) return null;

            foreach (GameObject obj in envRoot.EnvironmentObjects)
            {
                if (obj == null) continue;

                EnvironmentObjectMarker marker = obj.GetComponent<EnvironmentObjectMarker>();
                if (marker != null && marker.CellPosition.x == cellPosition.x && marker.CellPosition.z == cellPosition.z)
                    return obj;
            }

            return null;
        }

        /// <summary>
        /// Мировая высота верхней поверхности tile в этой клетке (или уровня
        /// слоя, если тайла там нет) - и подсветка, и объекты окружения должны
        /// стоять НА НЕЙ, а не быть спрятаны под мешем тайла.
        /// </summary>
        private float GetTileTopWorldY(Vector3Int cellPosition)
        {
            float baseY = targetLayer.transform.position.y;

            // Логический Tile - это пустой маркер без меша (вся геометрия
            // рисуется отдельно на смещённой dual-grid сетке), поэтому
            // высоту его "поверхности" берём из настроек биома.
            Tile tile = targetLayer.GetTileAt(cellPosition);
            if (tile == null) return baseY;

            float height = currentBiome != null ? currentBiome.tileHeight : 1f;
            return baseY + height;
        }

        /// <summary>
        /// Высота (мировая Y), на которой нужно рисовать курсор-подсветку, чтобы он был виден
        /// НАД тайлом в клетке, а не спрятан под его мешем.
        /// </summary>
        private float GetHighlightWorldY(Vector3Int cellPosition)
        {
            return GetTileTopWorldY(cellPosition) + 0.02f;
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
