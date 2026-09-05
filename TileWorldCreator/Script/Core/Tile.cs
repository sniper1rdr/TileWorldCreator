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
        /// Определяет, какой конкретный префаб из пула выбирается для
        /// display-клеток дуальной сетки, на которые влияет этот тайл (см.
        /// Layer.RefreshDualDisplayCell). Каждый новый тайл получает
        /// случайное значение при создании, чтобы одинаковые тайлы рядом не
        /// выглядели одинаково; CycleVariant() позволяет вручную переключить
        /// вариант повторным кликом по уже занятой клетке.
        /// </summary>
        public int VariantSeed => variantSeed;

        public void Initialize(Vector3Int position, string type = "Default")
        {
            // Сохраняем только X и Z, Y всегда 0
            cellPosition = new Vector3Int(position.x, 0, position.z);
            tileType = type;
            variantSeed = Random.Range(0, 9973);
        }

        /// <summary>Переключает визуальный вариант тайла на следующий в пуле префабов.</summary>
        public void CycleVariant()
        {
            variantSeed++;
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