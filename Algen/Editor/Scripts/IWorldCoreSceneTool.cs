using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal interface IWorldCoreSceneTool
    {
        int Priority { get; }
        bool CanActivate();
        void OnActivate();
        void OnDeactivate();
        void OnSceneGUI(SceneView sceneView);
        void CancelActiveOperation();
    }
}
