using UnityEngine;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// 아이소메트릭 화면에서 스프라이트끼리의 앞뒤 순서를 정한다.
    ///
    /// 캐릭터와 가구가 모두 SpriteRenderer 라 같은 반투명 큐에서 그려진다.
    /// 이 경우 깊이 버퍼가 가려 주지 않고 sortingLayer → sortingOrder 순서가 전부를 결정한다.
    /// 그래서 "카메라에서 얼마나 먼가"를 sortingOrder 로 환산해 넣어 준다.
    ///
    /// 결과는 항상 0 이상이다. 방의 벽·바닥이 음수 order(-10)를 쓰고 있어서,
    /// 방 안의 물건이 음수로 내려가면 벽 뒤로 숨어 버리기 때문이다.
    /// </summary>
    public static class IsoSorting
    {
        /// <summary>
        /// 1 유닛 깊이당 order 간격.
        ///
        /// 캐릭터 파츠가 자체 order(인스펙터의 값)를 이 기준값에 더해서 쓰므로,
        /// 반드시 <b>파츠 order 의 최댓값보다 커야</b> 한다.
        /// 그렇지 않으면 머리카락처럼 큰 order 를 가진 파츠가
        /// 한 칸 앞에 있는 가구를 뚫고 나온다.
        ///
        /// 현재 파츠 order 는 0~120 이라 1000 으로 둔다.
        /// 파츠 order 를 0~12 로 줄이면 100 정도로 낮춰도 안전하다.
        /// </summary>
        public const int DepthStep = 1000;

        /// <summary>
        /// 방 안에서 가장 먼 지점을 0 으로 두고, 카메라에 가까울수록 큰 값을 돌려준다.
        /// 방 전체가 양수 구간에 들어가므로 벽·바닥(-10) 뒤로 숨지 않는다.
        /// </summary>
        public static int GetBaseOrder(Camera camera, Vector3 worldPosition, RoomGrid grid)
        {
            if (camera == null || grid == null) return 0;

            var forward = camera.transform.forward;
            var depth = Vector3.Dot(worldPosition, forward);
            var farthest = GetFarthestDepth(grid, forward);

            // farthest 가 가장 큰 depth 이므로 (farthest - depth) 는 항상 0 이상이다.
            var order = Mathf.RoundToInt((farthest - depth) * DepthStep);

            // sortingOrder 는 short 범위다. 방 밖(미리보기 무대 등)에서 넘치지 않게 자른다.
            return Mathf.Clamp(order, 0, short.MaxValue - DepthStep);
        }

        /// <summary>방 영역의 여덟 모서리 중 카메라에서 가장 먼 지점의 깊이.</summary>
        private static float GetFarthestDepth(RoomGrid grid, Vector3 forward)
        {
            var min = grid.OriginCorner;
            var size = new Vector3(grid.GridSize.x, grid.GridSize.y, grid.GridSize.z) * grid.CellSize;
            var max = min + size;

            var farthest = float.MinValue;

            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);

                farthest = Mathf.Max(farthest, Vector3.Dot(corner, forward));
            }

            return farthest;
        }

        /// <summary>오브젝트 하위의 모든 렌더러에 같은 기준값을 적용한다. 가구처럼 통째로 움직이는 것에 쓴다.</summary>
        public static void ApplyBaseOrder(GameObject target, int baseOrder)
        {
            if (target == null) return;

            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                renderer.sortingOrder = baseOrder;
            }
        }
    }
}
