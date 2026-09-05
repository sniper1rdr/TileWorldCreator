using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AglenRealms.WorldCore
{
    [AddComponentMenu("Aglen Realms/World Core/World Root")]
    [ExecuteAlways]
    public class WorldRoot : MonoBehaviour
    {
        public const string WorldObjectName = "World Root";
        public const string LandscapeObjectName = "Landscape";
        public const string EnvironmentObjectName = "Environment";

        [SerializeField] private string worldName;
        [SerializeField] private EnvironmentRoot environment;
        [SerializeField] private LandscapeRoot landscape;

        public string WorldName => worldName;
        public EnvironmentRoot Environment => environment;
        public LandscapeRoot Landscape => landscape;

        public void SetWorldName(string name) => worldName = name;

        public bool TryGetLandscape(out LandscapeRoot land)
        {
            land = ResolveLandscapeReference();
            return land != null;
        }

        public bool TryGetEnvironment(out EnvironmentRoot env)
        {
            env = ResolveEnvironmentReference();
            return env != null;
        }

        public EnvironmentRoot FindOrCreateEnvironment()
        {
            EnvironmentRoot existing = ResolveEnvironmentReference();
            if (existing != null)
            {
                environment = existing;
                return existing;
            }

            Transform child = transform.Find(EnvironmentObjectName);
            GameObject environmentObject;
            if (child != null)
            {
                environmentObject = child.gameObject;
            }
            else
            {
                environmentObject = new GameObject(EnvironmentObjectName);
                environmentObject.transform.SetParent(transform, false);
                environmentObject.transform.localPosition = Vector3.zero;
    #if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.RegisterCreatedObjectUndo(environmentObject, "Create Environment");
    #endif
            }

            EnvironmentRoot root = environmentObject.GetComponent<EnvironmentRoot>();
            if (root == null)
            {
    #if UNITY_EDITOR
                if (!Application.isPlaying)
                    root = Undo.AddComponent<EnvironmentRoot>(environmentObject);
                else
    #endif
                    root = environmentObject.AddComponent<EnvironmentRoot>();
            }

            environment = root;
            root.EnsureDefaultLayer();
            root.RebuildLayerRoots();
            return root;
        }

        public LandscapeRoot FindOrCreateLandscape()
        {
            LandscapeRoot existing = ResolveLandscapeReference();
            if (existing != null)
            {
                landscape = existing;
                return existing;
            }

            Transform child = transform.Find(LandscapeObjectName);
            GameObject landscapeObject;
            if (child != null)
            {
                landscapeObject = child.gameObject;
            }
            else
            {
                landscapeObject = new GameObject(LandscapeObjectName);
                landscapeObject.transform.SetParent(transform, false);
                landscapeObject.transform.localPosition = Vector3.zero;
    #if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.RegisterCreatedObjectUndo(landscapeObject, "Create Landscape");
    #endif
            }

            LandscapeRoot root = landscapeObject.GetComponent<LandscapeRoot>();
            if (root == null)
            {
    #if UNITY_EDITOR
                if (!Application.isPlaying)
                    root = Undo.AddComponent<LandscapeRoot>(landscapeObject);
                else
    #endif
                    root = landscapeObject.AddComponent<LandscapeRoot>();
            }

            landscape = root;
            root.EnsurePaintContent();
            return root;
        }

        private LandscapeRoot ResolveLandscapeReference()
        {
            if (landscape != null)
                return landscape;

            Transform child = transform.Find(LandscapeObjectName);
            if (child != null && child.TryGetComponent(out LandscapeRoot found))
            {
                landscape = found;
                return found;
            }

            return null;
        }

        private EnvironmentRoot ResolveEnvironmentReference()
        {
            if (environment != null)
                return environment;

            Transform child = transform.Find(EnvironmentObjectName);
            if (child != null && child.TryGetComponent(out EnvironmentRoot found))
            {
                environment = found;
                return found;
            }

            return null;
        }

    #if UNITY_2023_1_OR_NEWER
        public static WorldRoot FindInScene() => Object.FindFirstObjectByType<WorldRoot>();
    #else
        public static WorldRoot FindInScene() => Object.FindObjectOfType<WorldRoot>();
    #endif
    }
}
