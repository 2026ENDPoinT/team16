using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zzapmagochi.Room;

namespace Zzapmagochi.Character
{
    /// <summary>
    /// 자취방 안을 스스로 돌아다닌다.
    ///
    ///   무작위 빈 칸 고르기 → A* 경로 → 이동 → 무작위 시간 대기 → 반복
    ///
    /// 가구에 둘러싸여 닿을 수 없는 칸을 골랐으면 그 이동은 취소하고 다시 고른다.
    /// </summary>
    [RequireComponent(typeof(CharacterParts))]
    public class CharacterRoomWanderer : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private RoomGrid grid;

        [Tooltip("가구가 놓였을 때 알림을 받으려면 연결한다. 없으면 밀려나기가 동작하지 않는다.")]
        [SerializeField] private FurniturePlacementController placement;

        [Tooltip("화면 기준 좌우를 판단할 카메라. 비우면 Camera.main 을 쓴다.")]
        [SerializeField] private Camera viewCamera;

        [Header("이동")]
        [Tooltip("초당 이동 거리(유닛)")]
        [SerializeField] private float moveSpeed = 1.2f;

        [Tooltip("다음 목적지를 고르기 전 대기 시간(초) 최소/최대")]
        [SerializeField] private float minIdleSeconds = 1.0f;
        [SerializeField] private float maxIdleSeconds = 3.5f;

        [Tooltip("경로를 못 찾았을 때 다시 고르기까지 기다릴 시간(초)")]
        [SerializeField] private float retrySeconds = 0.4f;

        [Tooltip("가구에 깔려 밀려날 때의 이동 속도(유닛/초). 평소보다 빠르게 비킨다.")]
        [SerializeField] private float pushSpeed = 3.0f;

        [Header("방향")]
        [Tooltip("켜면 화면 오른쪽으로 갈 때 캐릭터를 좌우 반전한다.")]
        [SerializeField] private bool flipWhenMovingRight = true;

        private CharacterParts _parts;
        private Coroutine _loop;

        /// <summary>현재 서 있는 칸.</summary>
        public Vector2Int CurrentCell { get; private set; }

        private void Awake()
        {
            _parts = GetComponent<CharacterParts>();
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void OnEnable()
        {
            // CharacterParts 의 자동 걷기 루프와 겹치면 서 있는데 걷는 포즈가 나온다.
            // 포즈는 이 컴포넌트가 이동 상태에 맞춰 직접 제어한다.
            if (_parts != null) _parts.SetAutoWalkLoop(false);

            if (placement != null) placement.FurniturePlaced += OnFurniturePlaced;

            _loop = StartCoroutine(WanderLoop());
        }

        private void OnDisable()
        {
            if (placement != null) placement.FurniturePlaced -= OnFurniturePlaced;

            StopLoop();
            if (_parts != null) _parts.SetWalking(false);
        }

        private void StopLoop()
        {
            if (_loop == null) return;

            StopCoroutine(_loop);
            _loop = null;
        }

        // ─────────────────────────────────────────────────────────
        // 가구에 깔렸을 때 비키기
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// 가구가 놓일 때마다 호출된다.
        /// 서 있던 칸이 막혔으면 하던 이동을 멈추고 가까운 빈 칸으로 비킨다.
        /// </summary>
        private void OnFurniturePlaced(PlacedFurniture furniture)
        {
            if (!isActiveAndEnabled || grid == null) return;
            if (RoomPathfinder.IsWalkable(grid, CurrentCell)) return;

            StopLoop();
            _loop = StartCoroutine(EvictThenWander());
        }

        private IEnumerator EvictThenWander()
        {
            if (RoomPathfinder.TryFindNearestWalkable(grid, CurrentCell, out var free))
            {
                // 밀려나는 것이므로 A* 로 돌아가지 않고 가장 가까운 빈 칸으로 곧장 비킨다.
                _parts.SetWalking(true);
                yield return StartCoroutine(StepTo(free, pushSpeed));
                _parts.SetWalking(false);
            }
            else
            {
                // 방 전체가 막힌 경우. 비킬 곳이 없으니 제자리에 둔다.
                Debug.LogWarning("[Wanderer] 비킬 수 있는 빈 칸이 없습니다.");
            }

            yield return new WaitForSeconds(Random.Range(minIdleSeconds, maxIdleSeconds));
            _loop = StartCoroutine(WanderLoop());
        }

        private IEnumerator WanderLoop()
        {
            // 파츠가 만들어지고 그리드에 가구가 등록될 때까지 한 프레임 기다린다.
            yield return null;

            if (!RoomPathfinder.IsWalkable(grid, CurrentCell)) SnapToNearestCell();

            while (true)
            {
                var path = PickDestination();

                if (path == null)
                {
                    // 닿을 수 없는 칸을 골랐다. 이번 이동은 취소하고 잠시 뒤 다시 고른다.
                    yield return new WaitForSeconds(retrySeconds);
                    continue;
                }

                yield return StartCoroutine(FollowPath(path));
                yield return new WaitForSeconds(Random.Range(minIdleSeconds, maxIdleSeconds));
            }
        }

        /// <summary>
        /// 무작위 빈 칸을 고르고 경로를 찾는다.
        /// 닿을 수 없으면 null 을 돌려준다(이동 취소).
        /// </summary>
        private List<Vector2Int> PickDestination()
        {
            if (grid == null) return null;

            var candidates = RoomPathfinder.CollectWalkableCells(grid);
            if (candidates.Count == 0) return null;

            // 제자리를 다시 고르면 이동이 없어 어색하므로 뺀다.
            candidates.Remove(CurrentCell);
            if (candidates.Count == 0) return null;

            var goal = candidates[Random.Range(0, candidates.Count)];
            return RoomPathfinder.FindPath(grid, CurrentCell, goal);
        }

        private IEnumerator FollowPath(List<Vector2Int> path)
        {
            if (path.Count == 0) yield break;

            _parts.SetWalking(true);

            foreach (var step in path)
            {
                // 걷는 도중에 그 칸에 가구가 놓일 수 있다. 막혔으면 거기서 멈춘다.
                if (!RoomPathfinder.IsWalkable(grid, step)) break;

                yield return StartCoroutine(StepTo(step, moveSpeed));
            }

            _parts.SetWalking(false);
        }

        private IEnumerator StepTo(Vector2Int cell, float speed)
        {
            var target = RoomPathfinder.CellToWorld(grid, cell);
            var start = transform.position;

            UpdateFacing(target - start);

            var distance = Vector3.Distance(start, target);
            if (distance <= 0.0001f)
            {
                CurrentCell = cell;
                yield break;
            }

            var duration = distance / Mathf.Max(0.01f, speed);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            transform.position = target;
            CurrentCell = cell;
        }

        /// <summary>
        /// 화면 기준으로 오른쪽으로 가면 좌우 반전한다.
        /// 월드 축이 아니라 카메라의 right 에 투영해서 판단한다 —
        /// 아이소메트릭이라 월드 +X 가 화면 오른쪽과 일치하지 않기 때문이다.
        /// </summary>
        private void UpdateFacing(Vector3 worldDelta)
        {
            if (!flipWhenMovingRight || viewCamera == null) return;

            var horizontal = Vector3.Dot(worldDelta, viewCamera.transform.right);
            if (Mathf.Abs(horizontal) < 0.0001f) return;

            var scale = transform.localScale;
            var magnitude = Mathf.Abs(scale.x);
            scale.x = horizontal > 0f ? -magnitude : magnitude;
            transform.localScale = scale;
        }

        /// <summary>현재 위치에서 가장 가까운 밟을 수 있는 칸으로 자리를 맞춘다.</summary>
        private void SnapToNearestCell()
        {
            if (grid == null) return;

            var cell = RoomPathfinder.WorldToCell(grid, transform.position);

            if (!RoomPathfinder.IsWalkable(grid, cell))
            {
                var candidates = RoomPathfinder.CollectWalkableCells(grid);
                if (candidates.Count == 0) return;

                var best = candidates[0];
                var bestDistance = float.MaxValue;

                foreach (var candidate in candidates)
                {
                    var d = Vector3.SqrMagnitude(
                        RoomPathfinder.CellToWorld(grid, candidate) - transform.position);
                    if (d >= bestDistance) continue;

                    best = candidate;
                    bestDistance = d;
                }

                cell = best;
            }

            CurrentCell = cell;
            transform.position = RoomPathfinder.CellToWorld(grid, cell);
        }
    }
}
