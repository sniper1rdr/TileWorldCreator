using UnityEngine;
using System.Collections.Generic;

namespace TileWorldCreator
{
    [AddComponentMenu("TileWorld/Core/Tile")]
    [ExecuteAlways]
    public class Tile : MonoBehaviour
    {
        [SerializeField] private Vector3Int cellPosition;
        [SerializeField] private string tileType = "Default";
        [SerializeField] private List<GameObject> extraPieces = new List<GameObject>();

        public Vector3Int CellPosition => cellPosition;
        public string TileType => tileType;

        /// <summary>
        /// Extra visual pieces layered on top of this tile (used when the auto
        /// tile system needs more than one prefab to fully border an
        /// isolated/partially-connected tile). Kept here so they get
        /// destroyed together with the tile when it is erased or replaced.
        /// </summary>
        public List<GameObject> ExtraPieces => extraPieces;

        public void Initialize(Vector3Int position, string type = "Default")
        {
            // Сохраняем только X и Z, Y всегда 0
            cellPosition = new Vector3Int(position.x, 0, position.z);
            tileType = type;
        }

        public void SetTileType(string type)
        {
            tileType = type;
        }

        /// <summary>
        /// Получить родительский слой
        /// </summary>
        public Layer GetLayer()
        {
            return GetComponentInParent<Layer>();
        }

        /// <summary>
        /// Получить мировую позицию тайла (только X и Z)
        /// </summary>
        public Vector3 GetWorldPosition()
        {
            Vector3 pos = transform.position;
            return new Vector3(pos.x, 0f, pos.z);
        }
        
        /// <summary>
        /// Получить локальную позицию тайла (только X и Z)
        /// </summary>
        public Vector3 GetLocalPosition()
        {
            Vector3 pos = transform.localPosition;
            return new Vector3(pos.x, 0f, pos.z);
        }
    }
}