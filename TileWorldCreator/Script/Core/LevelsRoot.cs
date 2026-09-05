using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TileWorldCreator
{
    [AddComponentMenu("TileWorld/Core/Levels Root")]
    [ExecuteAlways]
    public class LevelsRoot : MonoBehaviour
    {
        [SerializeField] private List<Level> levels = new List<Level>();
        [SerializeField] private int activeLevelIndex = -1;

        public List<Level> Levels => levels;
        public Level ActiveLevel => GetActiveLevel();
        public int ActiveLevelIndex => activeLevelIndex;

        public Level CreateLevel(string levelName, float yPosition = 0f)
        {
            GameObject levelObject = new GameObject(levelName);
            levelObject.transform.SetParent(transform, false);
            levelObject.transform.localPosition = new Vector3(0f, yPosition, 0f);

            Level level = levelObject.AddComponent<Level>();
            level.Initialize(levelName, yPosition);

            levels.Add(level);
            activeLevelIndex = levels.Count - 1;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(levelObject, "Create Level");
                Undo.RecordObject(this, "Add Level");
            }
#endif
            return level;
        }

        public Level FindLevel(string levelName)
        {
            return levels.Find(l => l != null && l.LevelName == levelName);
        }

        public void RemoveLevel(Level level)
        {
            if (level == null || !levels.Contains(level)) return;

            levels.Remove(level);
            if (activeLevelIndex >= levels.Count)
                activeLevelIndex = levels.Count - 1;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.DestroyObjectImmediate(level.gameObject);
            else
#endif
                Destroy(level.gameObject);
        }

        public void SetActiveLevel(int index)
        {
            if (index < 0 || index >= levels.Count) return;
            activeLevelIndex = index;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.RecordObject(this, "Set Active Level");
#endif
        }

        public void SetActiveLevel(Level level)
        {
            int index = levels.IndexOf(level);
            if (index >= 0)
                SetActiveLevel(index);
        }

        public Level GetActiveLevel()
        {
            if (activeLevelIndex >= 0 && activeLevelIndex < levels.Count)
                return levels[activeLevelIndex];
            return null;
        }

        public void ClearAllLevels()
        {
            for (int i = levels.Count - 1; i >= 0; i--)
            {
                Level level = levels[i];
                if (level == null) continue;

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.DestroyObjectImmediate(level.gameObject);
                else
#endif
                    Destroy(level.gameObject);
            }

            levels.Clear();
            activeLevelIndex = -1;
        }

        public Grid GetGrid()
        {
            WorldRoot worldRoot = GetComponentInParent<WorldRoot>();
            return worldRoot != null ? worldRoot.EnsureGrid() : null;
        }
    }
}