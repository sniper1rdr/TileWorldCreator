using System.Collections.Generic;
using UnityEngine;

namespace AglenRealms.WorldCore
{
    /// <summary>
    /// Scene-owned painting content for a Landscape. Holds only per-cell grid data so
    /// Paint/Erase Undo can snapshot this component without restoring DualGrid3D
    /// navigation/tool session fields (active level, brush biome, etc.).
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("")]
    public sealed class LandscapePaintContent : MonoBehaviour
    {
        [SerializeField] private List<LogicalCellData> cells = new();
        [SerializeField] private List<GroundDisplayVariantData> variants = new();

        /// <summary>
        /// Retained for serialization compatibility with scenes created while an earlier
        /// (ineffective) first-paint Undo workaround existed. Not read or written by runtime/editor logic.
        /// </summary>
        [SerializeField] private int paintUndoAnchor;

        public List<LogicalCellData> Cells => cells ??= new List<LogicalCellData>();
        public List<GroundDisplayVariantData> Variants => variants ??= new List<GroundDisplayVariantData>();

        public int CellCount => Cells.Count;

        void OnEnable()
        {
            hideFlags |= HideFlags.HideInInspector;
        }

        public void ClearAll()
        {
            Cells.Clear();
            Variants.Clear();
        }
    }
}
