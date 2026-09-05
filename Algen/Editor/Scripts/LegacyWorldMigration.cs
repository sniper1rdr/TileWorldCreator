using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal static class LegacyWorldMigration
    {
        public static bool TryFindLegacyDualGrid(out DualGrid3D legacy)
        {
    #if UNITY_2023_1_OR_NEWER
            DualGrid3D[] all = Object.FindObjectsByType<DualGrid3D>(FindObjectsSortMode.None);
    #else
            DualGrid3D[] all = Object.FindObjectsOfType<DualGrid3D>();
    #endif
            for (int i = 0; i < all.Length; i++)
            {
                DualGrid3D candidate = all[i];
                if (candidate == null)
                    continue;

                if (candidate is LandscapeRoot)
                    continue;

                if (candidate.GetComponentInParent<WorldRoot>() != null)
                    continue;

                legacy = candidate;
                return true;
            }

            legacy = null;
            return false;
        }

        public static bool TryMigrate(DualGrid3D legacy, out WorldRoot worldRoot)
        {
            worldRoot = null;
            if (legacy == null)
                return false;

            string preview =
                $"Migrate '{legacy.gameObject.name}' to:\n\n" +
                $"{WorldRoot.WorldObjectName}\n" +
                $"├── {WorldRoot.LandscapeObjectName} (LandscapeRoot)\n" +
                $"│   └── Level_* / Layer_*\n" +
                $"└── {WorldRoot.EnvironmentObjectName} (EnvironmentRoot, if present)\n\n" +
                "The legacy DualGrid3D component will be removed from the world root. Undo supported.";

            if (!EditorUtility.DisplayDialog("Migrate Legacy World", preview, "Migrate", "Cancel"))
                return false;

            Undo.SetCurrentGroupName("Migrate Legacy World");
            int undoGroup = Undo.GetCurrentGroup();

            GameObject worldObject = legacy.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(worldObject, "Migrate Legacy World");

            if (worldObject.name != WorldRoot.WorldObjectName)
            {
                Undo.RecordObject(worldObject, "Migrate Legacy World");
                worldObject.name = WorldRoot.WorldObjectName;
            }

            WorldRoot world = worldObject.GetComponent<WorldRoot>();
            if (world == null)
                world = Undo.AddComponent<WorldRoot>(worldObject);

            LandscapeRoot landscape = world.FindOrCreateLandscape();
            EditorUtility.CopySerialized(legacy, landscape);

            MoveChildren(worldObject.transform, landscape.transform, child => child.name.StartsWith("Level_"));

            Transform environmentChild = worldObject.transform.Find(WorldRoot.EnvironmentObjectName);
            EnvironmentRoot environmentRoot = null;
            if (environmentChild != null)
            {
                environmentRoot = environmentChild.GetComponent<EnvironmentRoot>();
                if (environmentRoot == null)
                    environmentRoot = Undo.AddComponent<EnvironmentRoot>(environmentChild.gameObject);

                ImportEnvironmentLayers(legacy, environmentRoot);
                MoveDirectPropsToActiveLayer(environmentChild, environmentRoot);
                environmentRoot.RebuildLayerRoots();
                EditorUtility.SetDirty(environmentRoot);
            }
            else if (legacy.environmentLayers != null && legacy.environmentLayers.Count > 0)
            {
                environmentRoot = world.FindOrCreateEnvironment();
                ImportEnvironmentLayers(legacy, environmentRoot);
                environmentRoot.RebuildLayerRoots();
            }

            if (environmentRoot != null && landscape != null)
                environmentRoot.LinkedLandscape = landscape;

            MigrateNestedLevelEnvironmentObjects(landscape.transform, environmentRoot);

            landscape.EnsureDefaultLevel();
            landscape.EnsureDefaultSubLevels();
            landscape.RebuildLevelRoots();
            EditorUtility.SetDirty(landscape);
            EditorUtility.SetDirty(world);

            Undo.DestroyObjectImmediate(legacy);

            Undo.CollapseUndoOperations(undoGroup);
            worldRoot = world;

            return true;
        }

        private static void ImportEnvironmentLayers(DualGrid3D legacy, EnvironmentRoot environmentRoot)
        {
            if (legacy.environmentLayers == null || legacy.environmentLayers.Count == 0)
            {
                environmentRoot.EnsureDefaultLayer();
                return;
            }

            var copiedLayers = new List<EnvironmentLayerDefinition>(legacy.environmentLayers.Count);
            for (int i = 0; i < legacy.environmentLayers.Count; i++)
            {
                EnvironmentLayerDefinition source = legacy.environmentLayers[i];
                copiedLayers.Add(new EnvironmentLayerDefinition
                {
                    name = source.name,
                    visible = source.visible,
                    height = source.height
                });
            }

            SerializedObject serializedEnvironment = new SerializedObject(environmentRoot);
            SerializedProperty layersProperty = serializedEnvironment.FindProperty("layers");
            SerializedProperty activeLayerProperty = serializedEnvironment.FindProperty("activeLayerIndex");
            if (layersProperty == null || activeLayerProperty == null)
                return;

            layersProperty.ClearArray();
            for (int i = 0; i < copiedLayers.Count; i++)
            {
                layersProperty.InsertArrayElementAtIndex(i);
                SerializedProperty element = layersProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("name").stringValue = copiedLayers[i].name;
                element.FindPropertyRelative("visible").boolValue = copiedLayers[i].visible;
                element.FindPropertyRelative("height").floatValue = copiedLayers[i].height;
            }

            activeLayerProperty.intValue = legacy.ActiveEnvironmentLayerIndex;
            serializedEnvironment.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void MigrateNestedLevelEnvironmentObjects(Transform landscapeRoot, EnvironmentRoot environmentRoot)
        {
            if (landscapeRoot == null || environmentRoot == null)
                return;

            Transform targetLayer = environmentRoot.GetActiveLayerRoot();
            List<Transform> nestedEnvironmentRoots = new();

            foreach (Transform levelChild in landscapeRoot)
            {
                if (!levelChild.name.StartsWith("Level_"))
                    continue;

                Transform nestedEnvironment = levelChild.Find(WorldRoot.EnvironmentObjectName);
                if (nestedEnvironment != null)
                    nestedEnvironmentRoots.Add(nestedEnvironment);
            }

            for (int i = 0; i < nestedEnvironmentRoots.Count; i++)
                MoveChildren(nestedEnvironmentRoots[i], targetLayer, _ => true);

            for (int i = 0; i < nestedEnvironmentRoots.Count; i++)
            {
                if (nestedEnvironmentRoots[i].childCount == 0)
                    Undo.DestroyObjectImmediate(nestedEnvironmentRoots[i].gameObject);
            }
        }

        private static void MoveDirectPropsToActiveLayer(Transform environmentTransform, EnvironmentRoot environmentRoot)
        {
            environmentRoot.EnsureDefaultLayer();
            Transform targetLayer = environmentRoot.GetOrCreateLayerRoot(environmentRoot.ActiveLayerIndex);
            List<Transform> toMove = new();
            foreach (Transform child in environmentTransform)
            {
                if (child.gameObject == environmentRoot.gameObject)
                    continue;

                if (child.name.StartsWith("Layer"))
                    continue;

                toMove.Add(child);
            }

            for (int i = 0; i < toMove.Count; i++)
                toMove[i].SetParent(targetLayer, true);
        }

        private static void MoveChildren(Transform from, Transform to, System.Func<Transform, bool> predicate)
        {
            List<Transform> children = new();
            foreach (Transform child in from)
            {
                if (predicate(child))
                    children.Add(child);
            }

            for (int i = 0; i < children.Count; i++)
                children[i].SetParent(to, true);
        }
    }
}
