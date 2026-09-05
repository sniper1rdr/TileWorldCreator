using System.Collections.Generic;

namespace AglenRealms.WorldCore.Editor
{
    internal static class WorldCoreToolRegistry
    {
        private static readonly List<IWorldCoreSceneTool> Tools = new();

        internal static IReadOnlyList<IWorldCoreSceneTool> All => Tools;

        static WorldCoreToolRegistry()
        {
            Register(new EnvironmentScenePaintTool());
            Register(new GroundScenePaintTool());
        }

        internal static void Register(IWorldCoreSceneTool tool)
        {
            if (tool == null || Tools.Contains(tool))
                return;

            Tools.Add(tool);
            Tools.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        internal static GroundScenePaintTool FindGroundTool()
        {
            for (int i = 0; i < Tools.Count; i++)
            {
                if (Tools[i] is GroundScenePaintTool groundTool)
                    return groundTool;
            }

            return null;
        }
    }
}
