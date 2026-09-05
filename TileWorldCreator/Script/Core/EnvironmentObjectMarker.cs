using UnityEngine;

namespace TileWorldCreator
{
    /// <summary>
    /// Marks a spawned environment prop with the logical grid cell it was
    /// placed in. The prop's actual X/Z position is NOT snapped to the
    /// cell centre (it follows the exact point the user clicked within the
    /// cell), so this marker is what lets the brush still reliably find /
    /// erase / limit-to-one-per-cell it.
    /// </summary>
    [AddComponentMenu("TileWorld/Core/Environment Object Marker")]
    [ExecuteAlways]
    public class EnvironmentObjectMarker : MonoBehaviour
    {
        [SerializeField] private Vector3Int cellPosition;

        public Vector3Int CellPosition => cellPosition;

        public void Initialize(Vector3Int cell)
        {
            cellPosition = new Vector3Int(cell.x, 0, cell.z);
        }
    }
}
