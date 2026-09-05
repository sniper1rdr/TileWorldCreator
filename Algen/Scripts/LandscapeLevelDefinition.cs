using System;
using System.Collections.Generic;

namespace AglenRealms.WorldCore
{
    [Serializable]
    public class LandscapeLevelDefinition
    {
        public string name = "Level_01";
        /// <summary>Logical Y in levelHeight steps (0 = ground, 1 = one unit up, ...).</summary>
        public int heightUnits = 0;
        public bool enabled = true;
        public List<LandscapeSubLevelDefinition> subLevels = new() { new LandscapeSubLevelDefinition() };
    }
}
