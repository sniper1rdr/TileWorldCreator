using UnityEngine;

namespace TileWorldCreator
{
    [AddComponentMenu("TileWorld/Core/Tile")]
    [ExecuteAlways]
    public class Tile : MonoBehaviour
    {
        [SerializeField] private Vector3Int cellPosition;
        [SerializeField] private string tileType = "Default";
        [SerializeField] private int variantSeed;

        public Vector3Int CellPosition => cellPosition;
        public string TileType => tileType;

        /// <summary>
        /// Определяет, какой префаб из пула выбирается для display-клеток
        /// дуальной сетки. Каждый новый тайл получает случайное значение,
        /// CycleVariant() позволяет переключить вариант повторным кликом.
        /// </summary>
        public int VariantSeed => variantSeed;

        public void Initialize(Vector3Int position, string type = "Default")
        {
            // Сохраняем только X и Z, Y всегда 0 (логическая клетка)
            cellPosition = new Vector3Int(position.x, 0, position.z);
            tileType = type;
            variantSeed = Random.Range(0, 9973);
        }

        /// <summary>Переключает визуальный вариант тайла на следующий.</summary>
        public void CycleVariant()
        {
            variantSeed++;
        }

        public void SetTileType(string type)
        {
            tileType = type;
        }

        public Layer GetLayer()
        {
            return GetComponentInParent<Layer>();
        }

        /// <summary>
        /// Мировая позиция тайла (с реальной высотой).
        /// </summary>
        public Vector3 GetWorldPosition()
        {
            return transform.position;
        }

        /// <summary>
        /// Локальная позиция тайла (с реальной высотой).
        /// </summary>
        public Vector3 GetLocalPosition()
        {
            return transform.localPosition;
        }
    }
}