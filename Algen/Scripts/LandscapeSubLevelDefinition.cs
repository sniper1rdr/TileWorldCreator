using System;

namespace AglenRealms.WorldCore
{
    [Serializable]
    public class LandscapeSubLevelDefinition
    {
        public string name = "Ground_01";
        public LandscapeLayerType layerType = LandscapeLayerType.Ground;
        public bool enabled = true;
        public bool visible = true;
    }
}
