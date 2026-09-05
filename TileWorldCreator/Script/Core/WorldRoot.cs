using UnityEngine;
using System.Collections.Generic;

namespace TileWorldCreator
{
    [AddComponentMenu("TileWorld/Core/World Root")]
    [ExecuteAlways]
    public class WorldRoot : MonoBehaviour
    {
        public const string WorldObjectName = "WorldRoot";
        
        [SerializeField] private string worldName = "MyWorld";
        [SerializeField] private Grid grid;
        [SerializeField] private LevelsRoot levelsRoot;
        [SerializeField] private EnvironmentRoot environmentRoot;
        
        public Grid Grid => grid;
        public LevelsRoot Levels => levelsRoot;
        public EnvironmentRoot Environment => environmentRoot;
        
        private void Awake()
        {
            EnsureGrid();
            EnsureLevelsRoot();
            EnsureEnvironmentRoot();
        }
        
        public void SetWorldName(string name)
        {
            worldName = name;
            gameObject.name = name;
        }
        
        public Grid EnsureGrid()
        {
            if (grid == null)
            {
                grid = GetComponentInChildren<Grid>();
                if (grid == null)
                {
                    GameObject gridObject = new GameObject("Grid");
                    gridObject.transform.SetParent(transform);
                    grid = gridObject.AddComponent<Grid>();
                }
            }
            return grid;
        }
        
        public LevelsRoot EnsureLevelsRoot()
        {
            if (levelsRoot == null)
            {
                levelsRoot = GetComponentInChildren<LevelsRoot>();
                if (levelsRoot == null)
                {
                    GameObject levelsObject = new GameObject("Levels");
                    levelsObject.transform.SetParent(transform);
                    levelsRoot = levelsObject.AddComponent<LevelsRoot>();
                }
            }
            return levelsRoot;
        }
        
        public LevelsRoot FindOrCreateLevels()
        {
            return EnsureLevelsRoot();
        }
        
        public EnvironmentRoot EnsureEnvironmentRoot()
        {
            if (environmentRoot == null)
            {
                environmentRoot = GetComponentInChildren<EnvironmentRoot>();
                if (environmentRoot == null)
                {
                    GameObject envObject = new GameObject("Environment");
                    envObject.transform.SetParent(transform);
                    environmentRoot = envObject.AddComponent<EnvironmentRoot>();
                }
            }
            return environmentRoot;
        }
        
        public static WorldRoot FindInScene()
        {
            return FindObjectOfType<WorldRoot>();
        }
        
        public GameObject CreateTile(Vector3Int cellPosition, string tileType = "Default")
        {
            if (levelsRoot == null) EnsureLevelsRoot();
            if (levelsRoot == null) return null;
            
            Level level = levelsRoot.ActiveLevel;
            if (level == null)
            {
                level = levelsRoot.CreateLevel("Level_01");
            }
            
            Layer layer = level.ActiveLayer;
            if (layer == null)
            {
                layer = level.CreateDefaultLayer();
            }
            
            return layer.CreateTile(cellPosition, tileType);
        }
    }
}
