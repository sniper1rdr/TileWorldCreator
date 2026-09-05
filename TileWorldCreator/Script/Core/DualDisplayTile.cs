using UnityEngine;

namespace TileWorldCreator
{
    /// <summary>
    /// Marks a spawned prefab instance as belonging to the dual-grid VISUAL
    /// tile at displayCellPosition for tileType. Lets Layer find/replace it
    /// when that display cell needs re-evaluation. Purely a visual marker —
    /// no occupancy meaning (see Tile for that).
    /// </summary>
    [AddComponentMenu("TileWorld/Core/Dual Display Tile")]
    [ExecuteAlways]
    public class DualDisplayTile : MonoBehaviour
    {
        [SerializeField] private Vector3Int cellPosition;
        [SerializeField] private string tileType = "Default";

        public Vector3Int CellPosition => cellPosition;
        public string TileType => tileType;

        public void Initialize(Vector3Int displayCellPosition, string type)
        {
            cellPosition = new Vector3Int(displayCellPosition.x, 0, displayCellPosition.z);
            tileType = type;
        }
    }
}