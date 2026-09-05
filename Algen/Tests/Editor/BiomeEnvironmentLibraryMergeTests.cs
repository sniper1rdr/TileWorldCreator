using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor.Tests
{
    /// <summary>
    /// Edit Mode tests validating the "add-on category pack" contract: a separately shipped
    /// Biome Environment Library asset (Buildings, VFX, ...) sharing a biomeId with an existing
    /// biome must be merged into that biome's palette automatically, with no World Core code
    /// change or package update.
    ///
    /// BiomeEnvironmentLibraryRegistry discovers libraries via AssetDatabase.FindAssets, so these
    /// tests create real, disk-backed assets under a throwaway folder rather than in-memory
    /// instances, then clean everything up in TearDown.
    /// </summary>
    public sealed class BiomeEnvironmentLibraryMergeTests
    {
        private const string RootFolder = "Assets/__MergeTestTemp__";

        private string _testFolder;
        private string _biomeId;
        private readonly List<string> _assetPaths = new();

        [SetUp]
        public void SetUp()
        {
            _biomeId = "__mergetest_" + Guid.NewGuid().ToString("N");
            _testFolder = $"{RootFolder}/{Guid.NewGuid():N}";
            CreateFolderRecursive(_testFolder);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _assetPaths.Count; i++)
                AssetDatabase.DeleteAsset(_assetPaths[i]);

            _assetPaths.Clear();

            if (AssetDatabase.IsValidFolder(_testFolder))
                AssetDatabase.DeleteAsset(_testFolder);

            if (AssetDatabase.IsValidFolder(RootFolder) &&
                AssetDatabase.FindAssets(string.Empty, new[] { RootFolder }).Length == 0)
            {
                AssetDatabase.DeleteAsset(RootFolder);
            }

            AssetDatabase.Refresh();
            BiomeEnvironmentLibraryRegistry.Invalidate();
        }

        [Test]
        public void SingleLibrary_NoAddOn_ResolvesToOriginalInstance_NoMergeAllocation()
        {
            GameObject rock = CreatePrefab("Rock");
            BiomeEnvironmentLibraryDefinition lib = CreateLibrary(
                "Official",
                (EnvironmentCategory.Rocks, new[] { rock }));

            RefreshAndInvalidate();

            BiomeEnvironmentLibraryDefinition resolved = BiomeEnvironmentLibrary.Resolve(_biomeId);

            Assert.IsNotNull(resolved);
            Assert.AreSame(lib, resolved,
                "A biomeId backed by a single library must resolve to that exact asset instance, not a synthetic merge.");
        }

        [Test]
        public void AddOnBuildingsPack_MergesIntoExistingBiome_WithoutCodeChange()
        {
            GameObject rock = CreatePrefab("Rock");
            GameObject building = CreatePrefab("Building");

            CreateLibrary("Official", (EnvironmentCategory.Rocks, new[] { rock }));
            CreateLibrary("BuildingsAddOn", (EnvironmentCategory.Buildings, new[] { building }));

            RefreshAndInvalidate();

            IReadOnlyList<BiomeEnvironmentCategoryEntry> categories = BiomeEnvironmentLibrary.GetCategories(_biomeId);
            Assert.AreEqual(2, categories.Count, "Rocks (official) + Buildings (add-on) must both surface.");

            GameObject[] rocks = BiomeEnvironmentLibrary.GetPrefabs(_biomeId, EnvironmentCategory.Rocks);
            GameObject[] buildings = BiomeEnvironmentLibrary.GetPrefabs(_biomeId, EnvironmentCategory.Buildings);
            GameObject[] vfx = BiomeEnvironmentLibrary.GetPrefabs(_biomeId, EnvironmentCategory.VFX);

            Assert.AreEqual(1, rocks.Length);
            Assert.AreSame(rock, rocks[0]);
            Assert.AreEqual(1, buildings.Length);
            Assert.AreSame(building, buildings[0]);
            Assert.AreEqual(0, vfx.Length, "Categories with no contributing pack must stay empty.");
        }

        [Test]
        public void ThreeAddOnPacks_AllCategoriesMergeTogether()
        {
            GameObject rock = CreatePrefab("Rock");
            GameObject veg = CreatePrefab("Veg");
            GameObject building = CreatePrefab("Building");
            GameObject vfx = CreatePrefab("VFX");

            CreateLibrary("Official",
                (EnvironmentCategory.Rocks, new[] { rock }),
                (EnvironmentCategory.Vegetation, new[] { veg }));
            CreateLibrary("BuildingsAddOn", (EnvironmentCategory.Buildings, new[] { building }));
            CreateLibrary("VFXAddOn", (EnvironmentCategory.VFX, new[] { vfx }));

            RefreshAndInvalidate();

            IReadOnlyList<BiomeEnvironmentCategoryEntry> categories = BiomeEnvironmentLibrary.GetCategories(_biomeId);
            Assert.AreEqual(4, categories.Count, "Official (Rocks+Vegetation) plus two single-category add-on packs must yield 4 categories.");
        }

        [Test]
        public void SamePrefabReferencedByTwoPacks_IsNotDuplicatedInMergedResult()
        {
            GameObject sharedRock = CreatePrefab("SharedRock");

            CreateLibrary("Official", (EnvironmentCategory.Rocks, new[] { sharedRock }));
            CreateLibrary("OverlapAddOn", (EnvironmentCategory.Rocks, new[] { sharedRock }));

            RefreshAndInvalidate();

            GameObject[] rocks = BiomeEnvironmentLibrary.GetPrefabs(_biomeId, EnvironmentCategory.Rocks);
            Assert.AreEqual(1, rocks.Length, "The same prefab referenced by two libraries must be deduplicated in the merged result.");
        }

        [Test]
        public void RegistryInvalidate_PicksUpAddOnPack_ImportedAfterFirstResolve()
        {
            GameObject rock = CreatePrefab("Rock");
            CreateLibrary("Official", (EnvironmentCategory.Rocks, new[] { rock }));
            RefreshAndInvalidate();

            Assert.AreEqual(1, BiomeEnvironmentLibrary.GetCategories(_biomeId).Count,
                "Only Rocks exists before the add-on pack is imported.");

            GameObject building = CreatePrefab("Building");
            CreateLibrary("BuildingsAddOn", (EnvironmentCategory.Buildings, new[] { building }));
            RefreshAndInvalidate();

            Assert.AreEqual(2, BiomeEnvironmentLibrary.GetCategories(_biomeId).Count,
                "Buildings must appear as soon as the add-on pack is discovered, matching a real 'import a new pack' scenario.");
        }

        private void RefreshAndInvalidate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BiomeEnvironmentLibraryRegistry.Invalidate();
        }

        private GameObject CreatePrefab(string name)
        {
            var source = new GameObject(name);
            try
            {
                string path = $"{_testFolder}/{name}_{Guid.NewGuid():N}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
                _assetPaths.Add(path);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private BiomeEnvironmentLibraryDefinition CreateLibrary(
            string displayName,
            params (EnvironmentCategory category, GameObject[] prefabs)[] entries)
        {
            var library = ScriptableObject.CreateInstance<BiomeEnvironmentLibraryDefinition>();
            library.biomeId = _biomeId;
            library.displayName = displayName;

            var categories = new BiomeEnvironmentCategoryEntry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                categories[i] = new BiomeEnvironmentCategoryEntry
                {
                    category = entries[i].category,
                    prefabs = entries[i].prefabs
                };
            }

            library.categories = categories;

            string path = $"{_testFolder}/{displayName}_{Guid.NewGuid():N}.asset";
            AssetDatabase.CreateAsset(library, path);
            AssetDatabase.SaveAssets();
            _assetPaths.Add(path);

            return AssetDatabase.LoadAssetAtPath<BiomeEnvironmentLibraryDefinition>(path);
        }

        private static void CreateFolderRecursive(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
