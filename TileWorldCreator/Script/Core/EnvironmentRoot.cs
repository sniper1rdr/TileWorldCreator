using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TileWorldCreator
{
    [AddComponentMenu("TileWorld/Core/Environment Root")]
    [ExecuteAlways]
    public class EnvironmentRoot : MonoBehaviour
    {
        [SerializeField] private List<GameObject> environmentObjects = new List<GameObject>();

        public List<GameObject> EnvironmentObjects => environmentObjects;

        public GameObject AddEnvironmentObject(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return null;

#if UNITY_EDITOR
            GameObject obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (obj == null)
                obj = Instantiate(prefab);
            Undo.RegisterCreatedObjectUndo(obj, "Add Environment Object");
#else
            GameObject obj = Instantiate(prefab);
#endif
            obj.transform.position = position;
            obj.transform.SetParent(transform, true);
            obj.name = $"{prefab.name}_{environmentObjects.Count}";

            environmentObjects.Add(obj);

            return obj;
        }

        public void RemoveEnvironmentObject(GameObject obj)
        {
            if (environmentObjects.Contains(obj))
            {
                environmentObjects.Remove(obj);
#if UNITY_EDITOR
                Undo.DestroyObjectImmediate(obj);
#else
                DestroyImmediate(obj);
#endif
            }
        }

        public void ClearEnvironment()
        {
#if UNITY_EDITOR
            for (int i = environmentObjects.Count - 1; i >= 0; i--)
            {
                GameObject obj = environmentObjects[i];
                if (obj != null)
                    Undo.DestroyObjectImmediate(obj);
            }
#else
            foreach (GameObject obj in environmentObjects)
            {
                if (obj != null)
                    DestroyImmediate(obj);
            }
#endif
            environmentObjects.Clear();
        }
    }
}
