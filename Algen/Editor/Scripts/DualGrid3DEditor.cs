using UnityEditor;

using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    [CustomEditor(typeof(DualGrid3D), true)]
    public class DualGrid3DEditor : UnityEditor.Editor

    {

        private static readonly string[] HiddenInspectorProperties =

        {

            "m_Script",

            "levels",

            "activeLevelIndex",

            "levelPaintingEnabled",

            "levelPaintMode",

            "brushBiome",

            "brushMode",

            "activeSubLevelIndex",

            "savedLogicalGrid",

            "savedGroundDisplayVariants",

            "paintContent",

            "paintContentMigrated",

            "bakeStaticDisplayTiles"

        };



        public override void OnInspectorGUI()

        {

            if (Application.isPlaying)

            {

                EditorGUILayout.HelpBox("World Core works in Edit Mode only. Exit Play Mode to paint.", MessageType.Info);

                return;

            }



            EditorGUILayout.LabelField("World Root", EditorStyles.boldLabel);

            EditorGUILayout.Space(4f);



            serializedObject.Update();



            SerializedProperty property = serializedObject.GetIterator();

            bool enterChildren = true;



            while (property.NextVisible(enterChildren))

            {

                enterChildren = false;



                if (ShouldHideProperty(property.name))

                    continue;



                EditorGUILayout.PropertyField(property, true);

            }



            serializedObject.ApplyModifiedProperties();



            EditorGUILayout.Space(8f);

            EditorGUILayout.HelpBox(

                "Levels and painting are managed in Window → Aglen Realms → World Core. " +

                "Resync layer meshes and randomize ground variants with the refresh icon on the active Ground layer row.\n\n" +

                "Move the World root object to reposition the painted grid. Rotation and scale are reset automatically. " +

                "Do not move Level_* or Layer_* children — only the root Transform.",

                MessageType.Info);

            if (LandscapeLevelManagerWindow.TryHandleGlobalPaintingEscape())
                WorldCoreSceneToolController.ApplyPaintingEscapeSideEffects();
        }



        private static bool ShouldHideProperty(string propertyName)

        {

            for (int i = 0; i < HiddenInspectorProperties.Length; i++)

            {

                if (HiddenInspectorProperties[i] == propertyName)

                    return true;

            }



            return false;

        }

    }
}
