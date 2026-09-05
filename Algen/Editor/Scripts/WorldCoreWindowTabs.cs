using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal static class WorldCoreWindowTabs
    {
        private const string ActiveModePrefKey = "AglenRealms.WorldCore.ActiveMode";
        private static GUIContent[] tabLabels;

        public static WorldCoreEditorMode LoadActiveMode()
        {
            int stored = EditorPrefs.GetInt(ActiveModePrefKey, (int)WorldCoreEditorMode.Landscape);
            if (stored < 0 || stored >= WorldCoreEditorModeExtensions.All.Length)
                return WorldCoreEditorMode.Landscape;

            return (WorldCoreEditorMode)stored;
        }

        public static void SaveActiveMode(WorldCoreEditorMode mode) =>
            EditorPrefs.SetInt(ActiveModePrefKey, (int)mode);

        public static WorldCoreEditorMode Draw(WorldCoreEditorMode activeMode)
        {
            EnsureTabLabels();

            EditorGUILayout.Space(2f);
            int selectedIndex = (int)activeMode;
            int newIndex = GUILayout.Toolbar(selectedIndex, tabLabels, GUILayout.Height(26f));
            EditorGUILayout.Space(6f);

            if (newIndex == selectedIndex)
                return activeMode;

            WorldCoreEditorMode newMode = WorldCoreEditorModeExtensions.All[newIndex];
            SaveActiveMode(newMode);
            WorldCoreEditorSession.SetActiveEditorMode(newMode);
            return newMode;
        }

        private static void EnsureTabLabels()
        {
            if (tabLabels != null && tabLabels.Length == WorldCoreEditorModeExtensions.All.Length)
                return;

            tabLabels = new GUIContent[WorldCoreEditorModeExtensions.All.Length];
            for (int i = 0; i < WorldCoreEditorModeExtensions.All.Length; i++)
                tabLabels[i] = new GUIContent(WorldCoreEditorModeExtensions.All[i].GetLabel());
        }
    }
}
