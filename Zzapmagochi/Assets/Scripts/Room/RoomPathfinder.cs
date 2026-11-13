using System.Collections.Generic;
using UnityEngine;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// 자취방 바닥(y = 0 층)에서 A* 로 경로를 찾는다.
    ///
    /// 가구가 막은 칸은 지나갈 수 없다. 막혔는지는 FurnitureDefinition.blocksPath 로 판단하므로,
    /// 액자처럼 길을 막지 않는 가구 아래로는 지나갈 수 있다.
    ///
    /// 대각선 이동은 하지 않는다. 대각선을 허용하면 가구 모서리 사이를 비집고 지나가
    /// 벽을 뚫은 것처럼 보이기 때문이다.
    /// </summary>
    public static class RoomPathfinder
    {
        /// <summary>바닥 층. 캐릭터는 항상 1층 바닥 위를 걷는다.</summary>
        private const int FloorY = 0;

        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        /// <summary>그 칸을 밟을 수 있는지. 방 안이고, 길을 막는 가구가 없어야 한다.</summary>
        public static bool IsWalkable(RoomGrid grid, Vector2Int cell)
        {
            if (grid == null) return false;

            var cell3 = new Vector3Int(cell.x, FloorY, cell.y);
            if (!grid.IsInside(cell3)) return false;

            var furniture = grid.GetAt(cell3);
            if (furniture == null) return true;

            // 정의가 비어 있으면 안전하게 막힌 것으로 본다.
            return furniture.Definition != null && !furniture.Definition.blocksPath;
        }

        /// <summary>
        /// start 에서 goal 까지의 경로를 찾는다.
        /// 결과는 start 를 뺀 이동할 칸들이고, 길이 없으면 null 을 돌려준다.
        /// </summary>
        public static List<Vector2Int> FindPath(RoomGrid grid, Vector2Int start, Vector2Int goal)
        {
            if (grid == null) return null;
            if (!IsWalkable(grid, goal)) return null;
            if (start == goal) return new List<Vector2Int>();

            var open = new List<Vector2Int> { start };
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gScore = new Dictionary<Vector2Int, int> { [start] = 0 };
            var fScore = new Dictionary<Vector2Int, int> { [start] = Heuristic(start, goal) };
            var closed = new HashSet<Vector2Int>();

            while (open.Count > 0)
            {
                // 방이 25칸 남짓이라 우선순위 큐 없이 선형 탐색으로 충분하다.
                var current = open[0];
                var currentF = fScore.TryGetValue(current, out var f0) ? f0 : int.MaxValue;

                for (var i = 1; i < open.Count; i++)
                {
                    var candidate = open[i];
                    var candidateF = fScore.TryGetValue(candidate, out var f) ? f : int.MaxValue;
                    if (candidateF >= currentF) continue;

                    current = candidate;
                    currentF = candidateF;
                }

                if (current == goal) return Reconstruct(cameFrom, current);

                open.Remove(current);
                closed.Add(current);

                foreach (var dir in Directions)
                {
                    var next = current + dir;

                    if (closed.Contains(next)) continue;
                    if (!IsWalkable(grid, next)) continue;

                    var tentative = (gScore.TryGetValue(current, out var g) ? g : int.MaxValue) + 1;
                    if (gScore.TryGetValue(next, out var known) && tentative >= known) continue;

                    cameFrom[next] = current;
                    gScore[next] = tentative;
                    fScore[next] = tentative + Heuristic(next, goal);

                    if (!open.Contains(next)) open.Add(next);
                }
            }

            // 가구에 둘러싸여 닿을 수 없는 칸이다.
            return null;
        }

        /// <summary>밟을 수 있는 칸을 전부 모은다. 무작위 목적지를 고를 때 쓴다.</summary>
        public static List<Vector2Int> CollectWalkableCells(RoomGrid grid)
        {
            var cells = new List<Vector2Int>();
            if (grid == null) return cells;

            var size = grid.GridSize;
            for (var x = 0; x < size.x; x++)
            for (var z = 0; z < size.z; z++)
            {
                var cell = new Vector2Int(x, z);
                if (IsWalkable(grid, cell)) cells.Add(cell);
            }

            return cells;
        }

        /// <summary>칸의 바닥 중앙 월드 좌표.</summary>
        public static Vector3 CellToWorld(RoomGrid grid, Vector2Int cell)
        {
            return grid.FootprintToWorld(new Vector3Int(cell.x, FloorY, cell.y), Vector3Int.one);
        }

        public static Vector2Int WorldToCell(RoomGrid grid, Vector3 world)
        {
            var cell = grid.WorldToCell(world);
            return new Vector2Int(cell.x, cell.z);
        }

        /// <summary>대각선을 쓰지 않으므로 맨해튼 거리가 정확한 하한이다.</summary>
        private static int Heuristic(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static List<Vector2Int> Reconstruct(
            IReadOnlyDictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
        {
            var path = new List<Vector2Int> { current };

            while (cameFrom.TryGetValue(current, out var previous))
            {
                current = previous;
                path.Add(current);
            }

            path.Reverse();
            path.RemoveAt(0); // 출발 칸은 뺀다
            return path;
        }
    }
}
