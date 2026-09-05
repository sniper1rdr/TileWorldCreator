using System;
using UnityEngine;

namespace AglenRealms.WorldCore
{
    [Serializable]
    public class BiomeEnvironmentCategoryEntry
    {
        public EnvironmentCategory category;
        public GameObject[] prefabs;

        public bool HasPrefabs
        {
            get
            {
                if (prefabs == null || prefabs.Length == 0)
                    return false;

                for (int i = 0; i < prefabs.Length; i++)
                {
                    if (prefabs[i] != null)
                        return true;
                }

                return false;
            }
        }
    }
}
