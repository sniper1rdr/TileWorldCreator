using UnityEngine;
using System.Collections.Generic;

namespace TileWorldCreator
{
    [AddComponentMenu("TileWorld/Core/Level")]
    [ExecuteAlways]
    public class Level : MonoBehaviour
    {
        [SerializeField] private string levelName = "Level_01";
        [SerializeField] private List<Layer> layers = new List<Layer>();
        [SerializeField] private int activeLayerIndex = -1;

        public string LevelName => levelName;
        public float Height => transform.position.y;
        public List<Layer> Layers => layers;
        public Layer ActiveLayer => GetActiveLayer();
        public int ActiveLayerIndex => activeLayerIndex;

        public void Initialize(string name, float yPosition = 0f)
        {
            levelName = name;
            
            Vector3 pos = transform.position;
            pos.y = yPosition;
            transform.position = pos;
            
            CreateDefaultLayer();
        }

        public void SetHeight(float newY)
        {
            Vector3 pos = transform.position;
            pos.y = newY;
            transform.position = pos;
        }

        public Layer CreateLayer(string layerName = null)
        {
            if (string.IsNullOrEmpty(layerName))
            {
                layerName = $"Layer_{layers.Count:00}";
            }
            
            Layer existingLayer = GetLayerByName(layerName);
            if (existingLayer != null)
            {
                int counter = 1;
                string newName;
                do
                {
                    newName = $"{layerName}_{counter}";
                    counter++;
                } while (GetLayerByName(newName) != null);
                layerName = newName;
            }

            GameObject layerObject = new GameObject(layerName);
            layerObject.transform.SetParent(transform, false);
            layerObject.transform.localPosition = Vector3.zero;

            Layer layer = layerObject.AddComponent<Layer>();
            layer.Initialize(layerName);
            
            Grid grid = GetGrid();
            if (grid != null)
            {
                layer.SetGrid(grid);
            }

            layers.Add(layer);
            
            if (layers.Count == 1)
            {
                activeLayerIndex = 0;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(layerObject, "Create Layer");
                UnityEditor.Undo.RecordObject(this, "Add Layer");
            }
#endif

            return layer;
        }

        public Layer CreateDefaultLayer()
        {
            // Проверяем, есть ли уже Base Layer
            Layer existing = GetLayerByName("Base Layer");
            if (existing != null)
            {
                SetActiveLayer(existing);
                return existing;
            }

            // Создаем базовый слой
            GameObject layerObject = new GameObject("Base Layer");
            layerObject.transform.SetParent(transform, false);
            layerObject.transform.localPosition = Vector3.zero;

            Layer layer = layerObject.AddComponent<Layer>();
            layer.Initialize("Base Layer");
            
            Grid grid = GetGrid();
            if (grid != null)
            {
                layer.SetGrid(grid);
            }

            layers.Add(layer);
            activeLayerIndex = 0;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(layerObject, "Create Layer");
                UnityEditor.Undo.RecordObject(this, "Add Layer");
            }
#endif

            return layer;
        }

        public bool IsBaseLayer(Layer layer)
        {
            if (layer == null) return false;
            return layer.LayerName == "Base Layer";
        }

        public Layer GetBaseLayer()
        {
            return GetLayerByName("Base Layer");
        }

        public Layer GetLayer(int index)
        {
            if (index >= 0 && index < layers.Count)
                return layers[index];
            return null;
        }

        public Layer GetLayerByName(string name)
        {
            return layers.Find(l => l.LayerName == name);
        }

        public void SetActiveLayer(int index)
        {
            if (index >= 0 && index < layers.Count)
            {
                activeLayerIndex = index;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.RecordObject(this, "Set Active Layer");
#endif
            }
        }

        public void SetActiveLayer(Layer layer)
        {
            int index = layers.IndexOf(layer);
            if (index >= 0)
            {
                activeLayerIndex = index;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.RecordObject(this, "Set Active Layer");
#endif
            }
        }

        public Layer GetActiveLayer()
        {
            return GetLayer(activeLayerIndex);
        }

        public void RemoveLayer(Layer layer)
        {
            if (layer != null && layers.Contains(layer))
            {
                if (layers.Count <= 1)
                {
                    Debug.LogWarning("Cannot remove the last layer! Create a new layer first.");
                    return;
                }

                // Не даем удалить базовый слой "Base Layer"
                if (layer.LayerName == "Base Layer")
                {
                    Debug.LogWarning("Cannot remove the base layer 'Base Layer'!");
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.DisplayDialog(
                        "Cannot Remove",
                        "Base layer 'Base Layer' cannot be removed!",
                        "OK");
#endif
                    return;
                }

                layers.Remove(layer);
                
                if (activeLayerIndex >= layers.Count)
                    activeLayerIndex = layers.Count - 1;
                    
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.DestroyObjectImmediate(layer.gameObject);
                else
#endif
                    Destroy(layer.gameObject);
            }
        }

        public void RemoveLayerAt(int index)
        {
            Layer layer = GetLayer(index);
            if (layer != null)
            {
                RemoveLayer(layer);
            }
        }

        public void ClearAllLayers()
        {
            foreach (Layer layer in layers)
            {
                if (layer != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        UnityEditor.Undo.DestroyObjectImmediate(layer.gameObject);
                    else
#endif
                        Destroy(layer.gameObject);
                }
            }
            layers.Clear();
            activeLayerIndex = -1;
        }

        public Grid GetGrid()
        {
            LevelsRoot levelsRoot = GetComponentInParent<LevelsRoot>();
            if (levelsRoot != null)
            {
                return levelsRoot.GetGrid();
            }
            return null;
        }

        public void Rename(string newName)
        {
            levelName = newName;
            gameObject.name = newName;
        }
    }
}