using System.Collections.Generic;
using UnityEngine;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// 자취방의 3D 그리드와 점유 상태를 관리한다.
    /// 좌표계: 칸 (0,0,0) 은 originCorner 에서 시작하고, +X / +Y / +Z 방향으로 자란다.
    /// 배치 가능 여부 판정은 전부 여기에 모아 둔다. 컨트롤러와 UI 는 결과만 쓴다.
    /// </summary>
    public class RoomGrid : MonoBehaviour
    {
        [Header("그리드")]
        [Tooltip("방 크기 (X=가로, Y=높이, Z=세로) 단위: 유닛")]
        [SerializeField] private Vector3Int gridSize = new Vector3Int(5, 4, 5);

        [Tooltip("칸 (0,0,0) 의 최소 모서리 월드 좌표")]
        [SerializeField] private Vector3 originCorner = new Vector3(-2.5f, 0f, -2.5f);

        [Tooltip("한 칸의 월드 크기. 1유닛 = 1")]
        [SerializeField] private float cellSize = 1f;

        [Header("벽 위치")]
        [Tooltip("X 최소쪽에 벽이 있는지 (현재 씬: hous_wall x=-2.5)")]
        [SerializeField] private bool wallAtMinX = true;

        [Tooltip("Z 최대쪽에 벽이 있는지 (현재 씬: hous_wall z=+2.5)")]
        [SerializeField] private bool wallAtMaxZ = true;

        [Tooltip("나머지 두 면. 카메라가 보는 쪽이라 기본값은 벽 없음")]
        [SerializeField] private bool wallAtMaxX = false;
        [SerializeField] private bool wallAtMinZ = false;

        /// <summary>점유된 칸 → 그 칸을 쓰는 가구.</summary>
        private readonly Dictionary<Vector3Int, PlacedFurniture> _occupied =
            new Dictionary<Vector3Int, PlacedFurniture>();

        private readonly List<PlacedFurniture> _placed = new List<PlacedFurniture>();

        public Vector3Int GridSize => gridSize;
        public Vector3 OriginCorner => originCorner;
        public float CellSize => cellSize;
        public IReadOnlyList<PlacedFurniture> Placed => _placed;

        /// <summary>방의 네 면 중 실제로 벽이 있는 쪽.</summary>
        public enum WallSide
        {
            MinX,
            MaxZ,
            MaxX,
            MinZ
        }

        public bool HasWall(WallSide side)
        {
            switch (side)
            {
                case WallSide.MinX: return wallAtMinX;
                case WallSide.MaxZ: return wallAtMaxZ;
                case WallSide.MaxX: return wallAtMaxX;
                case WallSide.MinZ: return wallAtMinZ;
                default: return false;
            }
        }

        /// <summary>
        /// 그 벽에 붙은 가구가 가져야 할 회전 단계.
        /// 기준(0단계)은 X 최소쪽 벽에 붙어 +X 를 바라보는 자세다.
        /// </summary>
        public static int GetWallRotationStep(WallSide side)
        {
            switch (side)
            {
                case WallSide.MinX: return 0;   // +X 를 본다
                case WallSide.MaxZ: return 1;   // -Z 를 본다
                case WallSide.MaxX: return 2;   // -X 를 본다
                case WallSide.MinZ: return 3;   // +Z 를 본다
                default: return 0;
            }
        }

        // ─────────────────────────────────────────────────────────
        // 좌표 변환
        // ─────────────────────────────────────────────────────────

        /// <summary>칸 좌표 → 그 칸의 최소 모서리 월드 좌표.</summary>
        public Vector3 CellToWorldCorner(Vector3Int cell)
        {
            return originCorner + new Vector3(cell.x, cell.y, cell.z) * cellSize;
        }

        /// <summary>
        /// 점유 영역의 바닥 중앙 월드 좌표. 가구 프리팹의 루트가 놓일 자리다.
        /// </summary>
        public Vector3 FootprintToWorld(Vector3Int cell, Vector3Int rotatedSize)
        {
            var corner = CellToWorldCorner(cell);
            return corner + new Vector3(rotatedSize.x, 0f, rotatedSize.z) * (cellSize * 0.5f);
        }

        /// <summary>월드 좌표 → 칸 좌표. 그리드 밖이어도 그대로 변환한다(검사는 별도).</summary>
        public Vector3Int WorldToCell(Vector3 world)
        {
            var local = (world - originCorner) / cellSize;
            return new Vector3Int(
                Mathf.FloorToInt(local.x),
                Mathf.FloorToInt(local.y),
                Mathf.FloorToInt(local.z));
        }

        public bool IsInside(Vector3Int cell)
        {
            return cell.x >= 0 && cell.x < gridSize.x
                && cell.y >= 0 && cell.y < gridSize.y
                && cell.z >= 0 && cell.z < gridSize.z;
        }

        // ─────────────────────────────────────────────────────────
        // 점유 영역
        // ─────────────────────────────────────────────────────────

        /// <summary>가구가 차지하는 칸들을 모두 돌려준다.</summary>
        public IEnumerable<Vector3Int> EnumerateFootprint(Vector3Int cell, Vector3Int rotatedSize)
        {
            for (var x = 0; x < rotatedSize.x; x++)
            for (var y = 0; y < rotatedSize.y; y++)
            for (var z = 0; z < rotatedSize.z; z++)
                yield return new Vector3Int(cell.x + x, cell.y + y, cell.z + z);
        }

        // ─────────────────────────────────────────────────────────
        // 배치 판정
        // ─────────────────────────────────────────────────────────

        /// <summary>배치 불가 사유. UI 에 그대로 보여줄 수 있다.</summary>
        public enum PlacementError
        {
            None,
            OutOfBounds,
            Overlap,
            NoSupport
        }

        public bool CanPlace(FurnitureDefinition definition, Vector3Int cell, int rotationStep)
        {
            return Validate(definition, cell, rotationStep) == PlacementError.None;
        }

        /// <summary>배치 가능 여부를 검사하고 실패 사유를 돌려준다.</summary>
        public PlacementError Validate(FurnitureDefinition definition, Vector3Int cell, int rotationStep)
        {
            if (definition == null) return PlacementError.OutOfBounds;

            var rotated = definition.GetRotatedSize(rotationStep);

            // 1. 방을 벗어나는가
            foreach (var c in EnumerateFootprint(cell, rotated))
            {
                if (!IsInside(c)) return PlacementError.OutOfBounds;
            }

            // 2. 다른 가구와 겹치는가
            foreach (var c in EnumerateFootprint(cell, rotated))
            {
                if (_occupied.ContainsKey(c)) return PlacementError.Overlap;
            }

            // 3. 공중에 떠 있는가
            if (!HasSupport(definition, cell, rotated)) return PlacementError.NoSupport;

            return PlacementError.None;
        }

        /// <summary>바닥 가구는 바닥에, 벽 가구는 벽에 닿아야 한다.</summary>
        private bool HasSupport(FurnitureDefinition definition, Vector3Int cell, Vector3Int rotatedSize)
        {
            if (definition.surface == PlacementSurface.Floor)
                return cell.y == 0;

            // 벽 가구: 점유 영역 중 한 칸이라도 실제 벽면에 닿아야 한다.
            var maxX = cell.x + rotatedSize.x - 1;
            var maxZ = cell.z + rotatedSize.z - 1;

            if (wallAtMinX && cell.x == 0) return true;
            if (wallAtMaxX && maxX == gridSize.x - 1) return true;
            if (wallAtMinZ && cell.z == 0) return true;
            if (wallAtMaxZ && maxZ == gridSize.z - 1) return true;

            return false;
        }

        // ─────────────────────────────────────────────────────────
        // 설치 / 제거
        // ─────────────────────────────────────────────────────────

        /// <summary>검사를 통과하면 점유 상태에 등록한다. 실패하면 false.</summary>
        public bool Occupy(PlacedFurniture furniture)
        {
            if (furniture == null || furniture.Definition == null) return false;

            var rotated = furniture.Definition.GetRotatedSize(furniture.RotationStep);
            if (Validate(furniture.Definition, furniture.Cell, furniture.RotationStep) != PlacementError.None)
                return false;

            foreach (var c in EnumerateFootprint(furniture.Cell, rotated))
                _occupied[c] = furniture;

            _placed.Add(furniture);
            return true;
        }

        public void Release(PlacedFurniture furniture)
        {
            if (furniture == null) return;

            var remove = new List<Vector3Int>();
            foreach (var pair in _occupied)
            {
                if (pair.Value == furniture) remove.Add(pair.Key);
            }

            foreach (var c in remove) _occupied.Remove(c);
            _placed.Remove(furniture);
        }

        public PlacedFurniture GetAt(Vector3Int cell)
        {
            return _occupied.TryGetValue(cell, out var f) ? f : null;
        }

        public void Clear()
        {
            _occupied.Clear();
            _placed.Clear();
        }

        // ─────────────────────────────────────────────────────────
        // 기즈모
        // ─────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);

            for (var x = 0; x <= gridSize.x; x++)
            {
                var a = originCorner + new Vector3(x, 0, 0) * cellSize;
                Gizmos.DrawLine(a, a + new Vector3(0, 0, gridSize.z) * cellSize);
            }

            for (var z = 0; z <= gridSize.z; z++)
            {
                var a = originCorner + new Vector3(0, 0, z) * cellSize;
                Gizmos.DrawLine(a, a + new Vector3(gridSize.x, 0, 0) * cellSize);
            }

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.2f);
            var size = new Vector3(gridSize.x, gridSize.y, gridSize.z) * cellSize;
            Gizmos.DrawWireCube(originCorner + size * 0.5f, size);
        }
    }
}
