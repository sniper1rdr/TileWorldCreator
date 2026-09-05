using UnityEngine;
using System.Collections.Generic;

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
            
            GameObject obj = Instantiate(prefab);
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
                DestroyImmediate(obj);
            }
        }

        public void ClearEnvironment()
        {
            foreach (GameObject obj in environmentObjects)
            {
                if (obj != null)
                    DestroyImmediate(obj);
            }
            environmentObjects.Clear();
        }
    }
}