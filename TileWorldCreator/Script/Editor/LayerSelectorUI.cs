using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace TileWorldCreator
{
    public class LayerSelectorUI
    {
        public void Draw(
            WorldRoot worldRoot,
            List<Layer> availableLayers,
            ref int selectedLayerIndex,
            Action<Layer> onLayerChanged,
            Action onCreateWorld,
            Action onCreateLayer)
        {
            EditorGUILayout.LabelField("Layer", EditorStyles.boldLabel);

            if (worldRoot == null)
            {
                EditorGUILayout.HelpBox("No World Root!", MessageType.Warning);
                if (GUILayout.Button("Create World"))
                    onCreateWorld?.Invoke();
                return;
            }

            if (availableLayers.Count > 0)
            {
                string[] layerNames = new string[availableLayers.Count];
                for (int i = 0; i < availableLayers.Count; i++)
                {
                    string name = availableLayers[i].LayerName;
                    int tileCount = availableLayers[i].Tiles.Count;
                    layerNames[i] = $"{name} ({tileCount} tiles)";
                }

                int newIndex = EditorGUILayout.Popup("Active Layer", selectedLayerIndex, layerNames);
                if (newIndex != selectedLayerIndex)
                {
                    selectedLayerIndex = newIndex;
                    onLayerChanged?.Invoke(availableLayers[selectedLayerIndex]);
                }

                if (selectedLayerIndex >= 0 && selectedLayerIndex < availableLayers.Count)
                {
                    Layer currentLayer = availableLayers[selectedLayerIndex];
                    if (currentLayer != null)
                    {
                        EditorGUILayout.LabelField(
                            $"Y: {currentLayer.transform.position.y:F1}",
                            EditorStyles.miniLabel);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No layers found!", MessageType.Info);
                if (GUILayout.Button("Create Layer"))
                    onCreateLayer?.Invoke();
            }

            EditorGUILayout.Space(5);
        }
    }
}