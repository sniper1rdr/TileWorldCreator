using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace AglenRealms.WorldCore
{
    [Serializable]
    public class EnvironmentLayerDefinition
    {
        public string name = "Layer_01";

        [FormerlySerializedAs("enabled")]
        public bool visible = true;

        public float height;
    }
}
