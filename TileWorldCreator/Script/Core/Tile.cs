using UnityEngine;

namespace TileWorldCreator
{
    [AddComponentMenu("TileWorld/Core/Tile")]
    [ExecuteAlways]
    public class Tile : MonoBehaviour
    {
        [SerializeField] private Vector3Int cellPosition;
        [SerializeField] private string tileType = "Default";

        public Vector3Int CellPosition => cellPosition;
        public string TileType => tileType;

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