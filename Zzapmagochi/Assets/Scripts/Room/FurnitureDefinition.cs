using UnityEngine;

namespace Zzapmagochi.Room
{
    /// <summary>가구가 붙을 수 있는 면.</summary>
    public enum PlacementSurface
    {
        /// <summary>바닥에 놓는다. 항상 y = 0 층에 앉는다.</summary>
        Floor,

        /// <summary>벽에 붙인다. 점유 칸이 벽면에 닿아야 한다.</summary>
        Wall
    }

    /// <summary>
    /// 가구 한 종류의 정의. 씬이 아니라 에셋으로 관리한다.
    /// 서버는 id 만 주고받고, 표시 정보와 크기는 이 에셋에서 읽는다 (API문서.md 6.1 참고).
    /// </summary>
    [CreateAssetMenu(menuName = "Zzapmagochi/Furniture Definition", fileName = "furn_new")]
    public class FurnitureDefinition : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("서버와 주고받는 고유 ID")]
        public string id;

        [Tooltip("인벤토리에 보이는 이름")]
        public string displayName;

        [Header("크기")]
        [Tooltip("점유 칸 수 (X=가로, Y=높이, Z=세로). 1유닛 = 1칸")]
        public Vector3Int size = Vector3Int.one;

        [Header("배치 규칙")]
        [Tooltip("바닥 가구인지 벽 가구인지")]
        public PlacementSurface surface = PlacementSurface.Floor;

        [Tooltip("캐릭터의 길을 막는지 여부. 배치 가능 판정에는 쓰이지 않고 이동 경로 계산에 쓴다.")]
        public bool blocksPath = true;

        [Tooltip("R 키로 직접 회전할 수 있는지. 끄면 벽 가구는 닿는 벽에 맞춰 자동으로 회전한다.")]
        public bool allowRotation = true;

        [Header("외형")]
        [Tooltip("배치될 실제 오브젝트. 루트는 점유 영역의 바닥 중앙이어야 한다.")]
        public GameObject prefab;

        /// <summary>회전 단계(0~3, 90도 단위)를 적용한 점유 크기.</summary>
        public Vector3Int GetRotatedSize(int rotationStep)
        {
            // 90도 / 270도 회전에서는 가로와 세로가 바뀐다. 높이는 그대로다.
            return (rotationStep & 1) == 0
                ? size
                : new Vector3Int(size.z, size.y, size.x);
        }

        public string DisplayNameOrId => string.IsNullOrEmpty(displayName) ? id : displayName;
    }
}
