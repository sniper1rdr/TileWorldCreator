using UnityEngine;
using System.Collections.Generic;

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
            
            // УСТАНАВЛИВАЕМ Y ПОЗИЦИЮ УРОВНЯ
            levelObject.transform.localPosition = new Vector3(0, yPosition, 0);
            
            Level level = levelObject.AddComponent<Level>();
            level.Initialize(levelName, yPosition);
            
            levels.Add(level);
            activeLevelIndex = levels.Count - 1;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(levelObject, "Create Level");
                UnityEditor.Undo.RecordObject(this, "Add Level");
            }
#endif

            return level;
        }

        public Level FindLevel(string levelName)
        {
            return levels.Find(l => l.LevelName == levelName);
        }

        public void RemoveLevel(Level level)
        {
            if (level != null && levels.Contains(level))
            {
                levels.Remove(level);
                if (activeLevelIndex >= levels.Count)
                    activeLevelIndex = levels.Count - 1;
                    
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.DestroyObjectImmediate(level.gameObject);
                else
#endif
                    Destroy(level.gameObject);
            }
        }

        public void SetActiveLevel(int index)
        {
            if (index >= 0 && index < levels.Count)
            {
                activeLevelIndex = index;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.RecordObject(this, "Set Active Level");
#endif
            }
        }

        public void SetActiveLevel(Level level)
        {
            int index = levels.IndexOf(level);
            if (index >= 0)
            {
                activeLevelIndex = index;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.RecordObject(this, "Set Active Level");
#endif
            }
        }

        public Level GetActiveLevel()
        {
            if (activeLevelIndex >= 0 && activeLevelIndex < levels.Count)
                return levels[activeLevelIndex];
            return null;
        }

        public void ClearAllLevels()
        {
            foreach (Level level in levels)
            {
                if (level != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        UnityEditor.Undo.DestroyObjectImmediate(level.gameObject);
                    else
#endif
                        Destroy(level.gameObject);
                }
            }
            levels.Clear();
            activeLevelIndex = -1;
        }

        public Grid GetGrid()
        {
            WorldRoot worldRoot = GetComponentInParent<WorldRoot>();
            if (worldRoot != null)
            {
                return worldRoot.EnsureGrid();
            }
            return null;
        }
    }
}