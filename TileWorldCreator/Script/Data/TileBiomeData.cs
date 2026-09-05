using UnityEngine;
using System.Collections.Generic;

namespace TileWorldCreator
{
    [CreateAssetMenu(menuName = "TileWorld/Biome Data", fileName = "TileBiomeData")]
    public class TileBiomeData : ScriptableObject
    {
        [Header("Identity")]
        public string biomeId;
        public string displayName;

        [Header("Tile Prefabs")]
        public GameObject[] groundTiles;
        public GameObject[] liquidTiles;
        public GameObject[] decorativeTiles;

        [Header("Environment - Rocks")]
        public GameObject[] rocks;

        [Header("Environment - Trees")]
        public GameObject[] trees;

        [Header("Environment - Vegetation")]
        public GameObject[] vegetation;

        [Header("Environment - Props")]
        public GameObject[] props;

        [Header("Materials")]
        public Material groundMaterial;
        public Material liquidMaterial;
        public Material decorativeMaterial;

        [Header("Settings")]
        public float tileHeight = 1f;
        public bool randomRotation = true;
        public Vector2 randomScaleRange = new Vector2(0.8f, 1.2f);

        public static class Categories
n        {
            public const string Rocks = "Rocks";
            public const string Trees = "Trees";
            public const string Vegetation = "Vegetation";
            public const string Props = "Props";
        }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(biomeId) &&
            GetTiles("Ground").Length > 0;

        public GameObject[] GetTiles(string tileType)
        {
            switch (tileType)
            {
                case "Ground":
                    return groundTiles ?? new GameObject[0];
                case "Liquid":
                    return liquidTiles ?? new GameObject[0];
                case "Decorative":
                    return decorativeTiles ?? new GameObject[0];
                default:
                    return groundTiles ?? new GameObject[0];
            }
        }

        public GameObject GetRandomTile(string tileType)
        {
            GameObject[] tiles = GetTiles(tileType);
            if (tiles == null || tiles.Length == 0)
                return null;

            return tiles[Random.Range(0, tiles.Length)];
        }

        public GameObject[] GetEnvironmentObjects(string category)
        {
            switch (category)
            {
                case Categories.Rocks:
                    return rocks ?? new GameObject[0];
                case Categories.Trees:
                    return trees ?? new GameObject[0];
                case Categories.Vegetation:
                    return vegetation ?? new GameObject[0];
                case Categories.Props:
                    return props ?? new GameObject[0];
                default:
                    return new GameObject[0];
            }
        }

        public GameObject GetRandomEnvironmentObject(string category)
        {
            GameObject[] objects = GetEnvironmentObjects(category);
            if (objects == null || objects.Length == 0)
                return null;

            return objects[Random.Range(0, objects.Length)];
        }

        public bool HasEnvironmentCategory(string category)
        {
            GameObject[] objects = GetEnvironmentObjects(category);
            return objects != null && objects.Length > 0;
        }
    }
}
