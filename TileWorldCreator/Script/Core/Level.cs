using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TileWorldCreator
{
    [AddComponentMenu("TileWorld/Core/Level")]
    [ExecuteAlways]
    public class Level : MonoBehaviour
    {
        [SerializeField] private string levelName = "Level_01";
        [SerializeField] private List<Layer> layers = new List<Layer>();
        [SerializeField] private int activeLayerIndex = 0;

        public string LevelName => levelName;
        public float Height => transform.position.y;
        public List<Layer> Layers => layers;
        public Layer ActiveLayer => GetActiveLayer();
        public int ActiveLayerIndex => activeLayerIndex;

        // ---------------------------------------------------------
        // INITIALIZE
        // ---------------------------------------------------------
        public void Initialize(string name, float yPosition = 0f)
        {
            levelName = name;
            Vector3 pos = transform.position;
            pos.y = yPosition;
            transform.position = pos;
            EnsureLayers();
        }

public void SetHeight(float newY)
{
    Vector3 pos = transform.position;
    pos.y = newY;
    transform.position = pos;

    // Синхронизируем Grid по XZ с этим Level
    SyncGridToThisLevel();
}

private void SyncGridToThisLevel()
{
    Grid grid = GetGrid();
    if (grid == null) return;

    Vector3 gridPos = grid.transform.position;
    gridPos.x = transform.position.x;
    gridPos.z = transform.position.z;
    // Y Grid’а оставляем как есть (или тоже можно синхронизировать)
    grid.transform.position = gridPos;
}

        // ---------------------------------------------------------
        // LAYERS
        // ---------------------------------------------------------
        public void EnsureLayers()
        {
            CleanupLayerList();

            Layer ground = GetLayerByName("Ground") ?? CreateLayerInternal("Ground");
            Layer liquid = GetLayerByName("Liquid") ?? CreateLayerInternal("Liquid");
            Layer environment = GetLayerByName("Environment") ?? CreateLayerInternal("Environment");

            layers.Clear();
            layers.Add(ground);
            layers.Add(liquid);
            layers.Add(environment);

            EnsureEnvironmentCategories(environment);

            if (activeLayerIndex < 0 || activeLayerIndex >= layers.Count)
                activeLayerIndex = 0;
        }

        private Layer CreateLayerInternal(string layerName)
        {
            GameObject layerObject = new GameObject(layerName);
            layerObject.transform.SetParent(transform, false);
            layerObject.transform.localPosition = Vector3.zero;
            layerObject.transform.localRotation = Quaternion.identity;
            layerObject.transform.localScale = Vector3.one;

            Layer layer = layerObject.AddComponent<Layer>();
            layer.Initialize(layerName);

            Grid grid = GetGrid();
            if (grid != null)
                layer.SetGrid(grid);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.RegisterCreatedObjectUndo(layerObject, "Create " + layerName + " Layer");
#endif
            return layer;
        }

        private void CleanupLayerList()
        {
            layers.RemoveAll(layer => layer == null);
        }

        // ---------------------------------------------------------
        // ENVIRONMENT CATEGORIES
        // ---------------------------------------------------------
        private void EnsureEnvironmentCategories(Layer environment)
        {
            if (environment == null) return;

            CreateCategoryIfMissing(environment.transform, "Rocks");
            CreateCategoryIfMissing(environment.transform, "Trees");
            CreateCategoryIfMissing(environment.transform, "Vegetation");
            CreateCategoryIfMissing(environment.transform, "Props");
        }

        private Transform CreateCategoryIfMissing(Transform parent, string categoryName)
        {
            Transform existing = parent.Find(categoryName);
            if (existing != null) return existing;

            GameObject categoryObject = new GameObject(categoryName);
            categoryObject.transform.SetParent(parent, false);
            categoryObject.transform.localPosition = Vector3.zero;
            categoryObject.transform.localRotation = Quaternion.identity;
            categoryObject.transform.localScale = Vector3.one;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.RegisterCreatedObjectUndo(categoryObject, "Create Environment Category");
#endif
            return categoryObject.transform;
        }

        public Transform GetEnvironmentCategory(string category)
        {
            Layer environment = GetEnvironmentLayer();
            if (environment == null) return null;

            EnsureEnvironmentCategories(environment);
            return environment.transform.Find(category);
        }

        public Transform GetEnvironmentCategory(EnvironmentCategory category)
        {
            return GetEnvironmentCategory(category.ToString());
        }

        // ---------------------------------------------------------
        // STANDARD LAYERS
        // ---------------------------------------------------------
        public Layer GetGroundLayer() => GetLayerByName("Ground");
        public Layer GetLiquidLayer() => GetLayerByName("Liquid");
        public Layer GetEnvironmentLayer() => GetLayerByName("Environment");

        // ---------------------------------------------------------
        // LAYER ACCESS
        // ---------------------------------------------------------
        public Layer GetLayer(int index)
        {
            return index >= 0 && index < layers.Count ? layers[index] : null;
        }

        public Layer GetLayerByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return layers.Find(layer => layer != null && layer.LayerName == name);
        }

        public void SetActiveLayer(int index)
        {
            if (index < 0 || index >= layers.Count) return;
            activeLayerIndex = index;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.RecordObject(this, "Set Active Layer");
#endif
        }

        public void SetActiveLayer(Layer layer)
        {
            if (layer == null) return;
            int index = layers.IndexOf(layer);
            if (index >= 0)
                SetActiveLayer(index);
        }

        public Layer GetActiveLayer() => GetLayer(activeLayerIndex);

        // ---------------------------------------------------------
        // TYPE HELPERS
        // ---------------------------------------------------------
        public Layer GetLayerForTileType(string tileType)
        {
            if (string.IsNullOrEmpty(tileType))
                return GetGroundLayer();

            return tileType switch
            {
                "Ground" => GetGroundLayer(),
                "Liquid" => GetLiquidLayer(),
                "Environment" => GetEnvironmentLayer(),
                _ => GetGroundLayer()
            };
        }

        // ---------------------------------------------------------
        // GRID
        // ---------------------------------------------------------
        public Grid GetGrid()
        {
            LevelsRoot levelsRoot = GetComponentInParent<LevelsRoot>();
            return levelsRoot != null ? levelsRoot.GetGrid() : null;
        }

        // ---------------------------------------------------------
        // CLEAR / RENAME
        // ---------------------------------------------------------
        public void ClearAllLayers()
        {
            CleanupLayerList();
            foreach (Layer layer in layers)
            {
                if (layer != null)
                    layer.ClearAllTiles();
            }
        }

        public void Rename(string newName)
        {
            if (string.IsNullOrEmpty(newName)) return;
            levelName = newName;
            gameObject.name = newName;
        }

        // ---------------------------------------------------------
        // LEGACY COMPATIBILITY
        // ---------------------------------------------------------
        public Layer CreateDefaultLayer()
        {
            EnsureLayers();
            return GetGroundLayer();
        }

        public bool IsBaseLayer(Layer layer) => layer == GetGroundLayer();
        public Layer GetBaseLayer() => GetGroundLayer();

        public Layer CreateLayer(string layerName = null)
        {
            Debug.LogWarning("Level.CreateLayer() is deprecated. Use Ground, Liquid or Environment layers.");
            EnsureLayers();
            return string.IsNullOrEmpty(layerName) ? GetGroundLayer() : GetLayerByName(layerName);
        }

        public void RemoveLayer(Layer layer)
        {
            Debug.LogWarning("Level.RemoveLayer() is disabled. Ground, Liquid and Environment layers are fixed.");
        }

        public void RemoveLayerAt(int index)
        {
            Debug.LogWarning("Level.RemoveLayerAt() is disabled. Ground, Liquid and Environment layers are fixed.");
        }
    }
}