using UnityEngine;

namespace Zzapmagochi.Room
{
    /// <summary>자취방에 실제로 설치된 가구 하나. 배치 정보를 들고 있다.</summary>
    public class PlacedFurniture : MonoBehaviour
    {
        public FurnitureDefinition Definition { get; private set; }

        /// <summary>점유 영역의 최소 모서리 칸 좌표.</summary>
        public Vector3Int Cell { get; private set; }

        /// <summary>회전 단계 0~3 (90도 단위).</summary>
        public int RotationStep { get; private set; }

        public void Initialize(FurnitureDefinition definition, Vector3Int cell, int rotationStep)
        {
            Definition = definition;
            Cell = cell;
            RotationStep = rotationStep;
        }

        /// <summary>저장용 데이터. API문서.md 5.1 의 placements 항목과 맞춘다.</summary>
        public (string furnitureId, int gridX, int gridY, int gridZ, int rotation) ToPlacement()
        {
            return (Definition != null ? Definition.id : null,
                Cell.x, Cell.y, Cell.z, RotationStep * 90);
        }
    }
}
