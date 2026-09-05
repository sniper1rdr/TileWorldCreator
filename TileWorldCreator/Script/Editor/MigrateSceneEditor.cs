#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace TileWorldCreator
{
    public static class MigrateSceneEditor
    {
        [MenuItem("Tools/TileWorld Creator/Migrate Scene (Tiles -> Containers)")]
        public static void MigrateScene()
        {
            if (!EditorUtility.DisplayDialog("Migrate Scene",
                "This will convert existing tile visuals named like 'Tile_x_z' (parented under a Layer) into container GameObjects with a Tile component. This operation supports Undo. Proceed?",
                "Yes", "No"))
            {
                return;
            }

            int migrated = 0;
            // Find all Layer components in the scene
            Layer[] layers = Object.FindObjectsOfType<Layer>();

            foreach (Layer layer in layers)
            {
                Transform layerT = layer.transform;
                List<Transform> toProcess = new List<Transform>();
                // collect children first to avoid modifying while iterating
                for (int i = 0; i < layerT.childCount; i++)
                {
                    Transform child = layerT.GetChild(i);
                    toProcess.Add(child);
                }

                foreach (Transform child in toProcess)
                {
                    if (child == null) continue;
                    // skip if already has Tile component on self
                    Tile existingTile = child.GetComponent<Tile>();
                    if (existingTile != null) continue;

                    // Name should match pattern Tile_x_z
                    string name = child.name;
                    if (!name.StartsWith("Tile_")) continue;

                    // Try parse coordinates
                    string[] parts = name.Split('_');
                    if (parts.Length < 3) continue;
                    int x, z;
                    if (!int.TryParse(parts[1], out x)) continue;
                    if (!int.TryParse(parts[2], out z)) continue;

                    // Create container
                    GameObject container = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(container, "Migrate Tile to Container");
                    container.transform.SetParent(layerT, false);
                    // place container at child's localPosition
                    container.transform.localPosition = child.localPosition;

                    // Move visual under container
                    Undo.SetTransformParent(child, container.transform, "Migrate Tile to Container");
                    child.localPosition = Vector3.zero;

                    // Add Tile component to container and initialize
                    Tile tileComp = container.AddComponent<Tile>();
                    tileComp.Initialize(new Vector3Int(x, 0, z), "Migrated");

                    // Ensure layer's Tiles list includes it
                    if (!layer.Tiles.Contains(tileComp))
                    {
                        layer.Tiles.Add(tileComp);
                    }

                    migrated++;
                }
            }

            EditorUtility.DisplayDialog("Migrate Scene", $"Migration complete. Converted {migrated} objects.", "OK");
        }
    }
}
#endif
