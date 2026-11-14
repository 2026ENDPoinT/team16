using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// 그리드 기반 가구 배치 모드.
    ///
    /// 흐름:
    ///   Off → (배치 모드 버튼) → Browsing(인벤토리 열림)
    ///       → (가구 선택) → Positioning(반투명 미리보기가 마우스를 따라다님)
    ///       → 좌클릭(가능한 위치) → 설치 후 Browsing 으로 복귀
    ///       → 우클릭 → 취소하고 Browsing 으로 복귀
    /// </summary>
    public class FurniturePlacementController : MonoBehaviour
    {
        public enum Mode
        {
            Off,
            Browsing,
            Positioning,

            /// <summary>배치된 가구를 클릭해서 치우는 상태.</summary>
            Removing
        }

        [Header("참조")]
        [SerializeField] private RoomGrid grid;
        [SerializeField] private Camera targetCamera;

        [Tooltip("설치된 가구가 들어갈 부모. 비우면 이 오브젝트 아래에 들어간다.")]
        [SerializeField] private Transform furnitureRoot;

        [Header("미리보기")]
        [Tooltip("미리보기에 입힐 반투명 머티리얼. 비우면 원래 머티리얼 그대로 보인다.")]
        [SerializeField] private Material previewMaterial;

        [Tooltip("미리보기의 정렬 순서. 방의 벽/바닥 스프라이트(0)보다 커야 가려지지 않는다.")]
        [SerializeField] private int previewSortingOrder = 30000;

        [Tooltip("배치 가능할 때 미리보기 색")]
        [SerializeField] private Color validColor = new Color(0.3f, 0.9f, 0.4f, 0.45f);

        [Tooltip("제거 모드에서 마우스가 올라간 가구를 표시할 색")]
        [SerializeField] private Color removeHighlightColor = new Color(1f, 0.35f, 0.35f, 1f);

        [Tooltip("배치 불가능할 때 미리보기 색")]
        [SerializeField] private Color invalidColor = new Color(0.95f, 0.3f, 0.3f, 0.45f);

        [Header("입력")]
        [SerializeField] private Key rotateKey = Key.R;
        [SerializeField] private Key exitKey = Key.Escape;

        private Mode _mode = Mode.Off;
        private FurnitureDefinition _selected;
        private int _rotationStep;

        private GameObject _preview;
        private readonly List<Renderer> _previewRenderers = new List<Renderer>();
        private MaterialPropertyBlock _block;

        private Vector3Int _currentCell;
        private bool _currentValid;

        /// <summary>모드가 바뀔 때 알린다. UI 가 인벤토리 열고 닫는 데 쓴다.</summary>
        public event Action<Mode> ModeChanged;

        /// <summary>가구가 설치될 때마다 알린다.</summary>
        public event Action<PlacedFurniture> FurniturePlaced;

        public Mode CurrentMode => _mode;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (furnitureRoot == null) furnitureRoot = transform;
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (_mode == Mode.Off) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[exitKey].wasPressedThisFrame)
            {
                ExitPlacementMode();
                return;
            }

            if (_mode == Mode.Removing)
            {
                UpdateRemoving();
                return;
            }

            if (_mode != Mode.Positioning) return;

            if (keyboard != null && keyboard[rotateKey].wasPressedThisFrame)
                Rotate();

            UpdatePreview();

            var mouse = Mouse.current;
            if (mouse == null) return;

            // UI 위에서 누른 클릭은 배치로 취급하지 않는다.
            if (IsPointerOverUI()) return;

            if (mouse.leftButton.wasPressedThisFrame && _currentValid)
                ConfirmPlacement();
            else if (mouse.rightButton.wasPressedThisFrame)
                CancelPositioning();
        }

        // ─────────────────────────────────────────────────────────
        // 모드 전환
        // ─────────────────────────────────────────────────────────

        public void EnterPlacementMode()
        {
            if (_mode != Mode.Off) return;
            SetMode(Mode.Browsing);
        }

        public void ExitPlacementMode()
        {
            ClearRemoveHover();
            DestroyPreview();
            _selected = null;
            _rotationStep = 0;
            SetMode(Mode.Off);
        }

        public void TogglePlacementMode()
        {
            if (_mode == Mode.Off) EnterPlacementMode();
            else ExitPlacementMode();
        }

        // ─────────────────────────────────────────────────────────
        // 가구 제거
        // ─────────────────────────────────────────────────────────

        /// <summary>마우스가 올라가 있는 가구. 강조 표시를 되돌리려고 들고 있는다.</summary>
        private PlacedFurniture _removeHover;
        private readonly List<Renderer> _hoverRenderers = new List<Renderer>();

        /// <summary>강조 전 스프라이트 색. 호버가 풀리면 그대로 되돌린다.</summary>
        private readonly Dictionary<SpriteRenderer, Color> _hoverSpriteColors =
            new Dictionary<SpriteRenderer, Color>();

        /// <summary>
        /// 제거 모드로 들어간다. 인벤토리는 ModeChanged 를 받은 UI 가 닫는다.
        /// </summary>
        public void EnterRemoveMode()
        {
            if (_mode == Mode.Off) return;

            DestroyPreview();
            _selected = null;
            _rotationStep = 0;

            SetMode(Mode.Removing);
        }

        /// <summary>제거를 그만두고 인벤토리로 돌아간다.</summary>
        public void CancelRemoving()
        {
            ClearRemoveHover();
            SetMode(Mode.Browsing);
        }

        private void UpdateRemoving()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (IsPointerOverUI())
            {
                ClearRemoveHover();
                return;
            }

            var target = FindFurnitureUnderPointer();
            SetRemoveHover(target);

            if (mouse.leftButton.wasPressedThisFrame && target != null)
            {
                ClearRemoveHover();
                RemoveFurniture(target);
                // 여러 개를 연달아 치울 수 있도록 제거 모드를 유지한다.
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                CancelRemoving();
            }
        }

        /// <summary>
        /// 마우스 아래의 설치된 가구를 찾는다.
        /// 가구 프리팹의 콜라이더는 isTrigger 라서 트리거도 맞도록 질의한다.
        /// </summary>
        private PlacedFurniture FindFurnitureUnderPointer()
        {
            var mouse = Mouse.current;
            if (mouse == null || targetCamera == null) return null;

            var ray = targetCamera.ScreenPointToRay(mouse.position.ReadValue());

            var hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
            if (hits.Length == 0) return null;

            // 카메라에 가장 가까운 가구를 고른다. 겹쳐 보일 때 앞의 것이 집히도록.
            PlacedFurniture best = null;
            var bestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                var furniture = hit.collider.GetComponentInParent<PlacedFurniture>();
                if (furniture == null) continue;
                if (hit.distance >= bestDistance) continue;

                best = furniture;
                bestDistance = hit.distance;
            }

            return best;
        }

        private void SetRemoveHover(PlacedFurniture furniture)
        {
            if (_removeHover == furniture) return;

            ClearRemoveHover();
            if (furniture == null) return;

            _removeHover = furniture;
            furniture.GetComponentsInChildren(true, _hoverRenderers);

            foreach (var renderer in _hoverRenderers)
            {
                if (renderer == null) continue;

                // 스프라이트는 머티리얼 색이 아니라 SpriteRenderer.color 로 틴트된다.
                // _BaseColor 만 건드리면 가구가 스프라이트로 바뀐 뒤로 강조가 먹히지 않는다.
                if (renderer is SpriteRenderer sprite)
                {
                    _hoverSpriteColors[sprite] = sprite.color;
                    sprite.color = removeHighlightColor;
                    continue;
                }

                // 메시는 머티리얼이 에셋이라 직접 건드리면 안 된다.
                // MaterialPropertyBlock 은 렌더러 단위라 원본 머티리얼을 바꾸지 않는다.
                renderer.GetPropertyBlock(_block);
                _block.SetColor("_BaseColor", removeHighlightColor);
                _block.SetColor("_Color", removeHighlightColor);
                renderer.SetPropertyBlock(_block);
            }
        }

        private void ClearRemoveHover()
        {
            foreach (var pair in _hoverSpriteColors)
            {
                if (pair.Key != null) pair.Key.color = pair.Value;
            }
            _hoverSpriteColors.Clear();

            foreach (var renderer in _hoverRenderers)
            {
                if (renderer == null || renderer is SpriteRenderer) continue;
                renderer.SetPropertyBlock(null);
            }

            _hoverRenderers.Clear();
            _removeHover = null;
        }

        /// <summary>인벤토리에서 가구를 고르면 호출된다.</summary>
        public void SelectFurniture(FurnitureDefinition definition)
        {
            if (definition == null || definition.prefab == null)
            {
                Debug.LogWarning($"[Placement] 프리팹이 없는 가구입니다: {definition?.id}");
                return;
            }

            _selected = definition;
            _rotationStep = 0;

            BuildPreview(definition);
            SetMode(Mode.Positioning);
        }

        /// <summary>설치를 취소하고 인벤토리로 돌아간다.</summary>
        public void CancelPositioning()
        {
            DestroyPreview();
            _selected = null;
            _rotationStep = 0;
            SetMode(Mode.Browsing);
        }

        private void SetMode(Mode mode)
        {
            if (_mode == mode) return;
            _mode = mode;
            ModeChanged?.Invoke(_mode);
        }

        /// <summary>
        /// R 키 회전. 회전이 금지된 가구(액자처럼 벽을 따라 자동으로 도는 것)는 무시한다.
        /// </summary>
        public void Rotate()
        {
            if (_selected != null && !_selected.allowRotation) return;
            _rotationStep = (_rotationStep + 1) & 3;
        }

        // ─────────────────────────────────────────────────────────
        // 미리보기
        // ─────────────────────────────────────────────────────────

        private void BuildPreview(FurnitureDefinition definition)
        {
            DestroyPreview();

            _preview = Instantiate(definition.prefab);
            _preview.name = $"Preview_{definition.id}";

            // 미리보기는 물리·충돌에 참여하면 안 된다.
            foreach (var collider in _preview.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            _preview.GetComponentsInChildren(true, _previewRenderers);

            foreach (var renderer in _previewRenderers)
            {
                if (renderer == null) continue;

                // 방의 벽·바닥은 SpriteRenderer 라 반투명 큐에서 그려지고, 정렬 순서가 전부 0이다.
                // 반투명끼리는 오브젝트 원점까지의 거리로 순서가 정해지므로,
                // 미리보기가 방 안쪽 칸에 있으면 바닥 스프라이트보다 "멀다"고 판정되어 덮인다.
                // 정렬 순서를 크게 올려 항상 방 스프라이트 뒤에 그려지도록 고정한다.
                renderer.sortingOrder = previewSortingOrder;

                // 미리보기는 반투명으로 보여야 하므로 머티리얼을 통째로 갈아끼운다.
                // 원본 머티리얼이 불투명이면 알파를 아무리 낮춰도 비치지 않는다.
                if (previewMaterial == null) continue;

                var materials = new Material[renderer.sharedMaterials.Length];
                for (var i = 0; i < materials.Length; i++) materials[i] = previewMaterial;
                renderer.sharedMaterials = materials;
            }
        }

        private void DestroyPreview()
        {
            _previewRenderers.Clear();
            if (_preview == null) return;

            if (Application.isPlaying) Destroy(_preview);
            else DestroyImmediate(_preview);

            _preview = null;
        }

        private void UpdatePreview()
        {
            if (_preview == null || _selected == null || grid == null) return;

            if (!TryGetCellUnderPointer(_selected, out var cell))
            {
                _currentValid = false;
                SetPreviewColor(invalidColor);
                return;
            }

            _currentCell = cell;
            var rotated = _selected.GetRotatedSize(_rotationStep);

            _preview.transform.position = grid.FootprintToWorld(cell, rotated);
            _preview.transform.rotation = Quaternion.Euler(0f, _rotationStep * 90f, 0f);

            _currentValid = grid.CanPlace(_selected, cell, _rotationStep);
            SetPreviewColor(_currentValid ? validColor : invalidColor);
        }

        private void SetPreviewColor(Color color)
        {
            foreach (var renderer in _previewRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(_block);
                _block.SetColor("_BaseColor", color);
                _block.SetColor("_Color", color);
                renderer.SetPropertyBlock(_block);
            }
        }

        // ─────────────────────────────────────────────────────────
        // 마우스 위치 → 칸
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// 마우스가 가리키는 칸을 구한다.
        /// 바닥 가구는 바닥면(y=0)과, 벽 가구는 벽면과 교차시킨다.
        /// 벽 가구는 닿는 벽에 따라 회전 단계도 같이 정해진다.
        /// </summary>
        private bool TryGetCellUnderPointer(FurnitureDefinition definition, out Vector3Int cell)
        {
            cell = default;

            var mouse = Mouse.current;
            if (mouse == null || targetCamera == null) return false;

            var ray = targetCamera.ScreenPointToRay(mouse.position.ReadValue());

            return definition.surface == PlacementSurface.Floor
                ? TryGetFloorCell(ray, definition, out cell)
                : TryGetWallCell(ray, definition, out cell);
        }

        private bool TryGetFloorCell(Ray ray, FurnitureDefinition definition, out Vector3Int cell)
        {
            cell = default;

            var plane = new Plane(Vector3.up, new Vector3(0f, grid.OriginCorner.y, 0f));
            if (!plane.Raycast(ray, out var distance)) return false;

            var hit = ray.GetPoint(distance);
            var hitCell = grid.WorldToCell(hit);

            var rotated = definition.GetRotatedSize(_rotationStep);

            // 마우스가 가리키는 칸이 가구의 중앙에 오도록 보정한다.
            // 크기가 짝수인 축은 정확한 중앙이 칸 경계라 한 칸 쪽으로 치우친다.
            cell = new Vector3Int(
                hitCell.x - (rotated.x - 1) / 2,
                0,
                hitCell.z - (rotated.z - 1) / 2);

            return true;
        }

        /// <summary>
        /// 벽 가구: 실제로 벽이 있는 면들과 레이를 교차시켜 가장 가까운 벽을 고른다.
        /// 고른 벽에 맞춰 회전 단계를 정하고(회전이 금지된 가구도 이 회전은 적용된다),
        /// 높이(y)는 마우스가 가리키는 벽면 높이를 칸 단위로 스냅한다.
        /// </summary>
        private bool TryGetWallCell(Ray ray, FurnitureDefinition definition, out Vector3Int cell)
        {
            cell = default;

            var origin = grid.OriginCorner;
            var size = grid.GridSize;
            var cs = grid.CellSize;

            var best = float.MaxValue;
            var found = false;
            Vector3Int bestCell = default;
            var bestStep = _rotationStep;

            foreach (RoomGrid.WallSide side in System.Enum.GetValues(typeof(RoomGrid.WallSide)))
            {
                if (!grid.HasWall(side)) continue;

                if (!TryPlane(ray, GetWallPlane(side, origin, size, cs), out var hit, out var dist))
                    continue;

                if (dist >= best) continue;

                // 벽마다 자세가 정해지므로 점유 크기도 그 회전 기준으로 계산해야 한다.
                var step = RoomGrid.GetWallRotationStep(side);
                var rotated = definition.GetRotatedSize(step);
                var c = grid.WorldToCell(hit);

                var y = Mathf.Clamp(c.y, 0, Mathf.Max(0, size.y - rotated.y));
                var x = Mathf.Clamp(c.x - (rotated.x - 1) / 2, 0, Mathf.Max(0, size.x - rotated.x));
                var z = Mathf.Clamp(c.z - (rotated.z - 1) / 2, 0, Mathf.Max(0, size.z - rotated.z));

                switch (side)
                {
                    case RoomGrid.WallSide.MinX: x = 0; break;
                    case RoomGrid.WallSide.MaxX: x = size.x - rotated.x; break;
                    case RoomGrid.WallSide.MinZ: z = 0; break;
                    case RoomGrid.WallSide.MaxZ: z = size.z - rotated.z; break;
                }

                best = dist;
                bestCell = new Vector3Int(x, y, z);
                bestStep = step;
                found = true;
            }

            if (!found) return false;

            // 벽 가구의 자세는 벽이 결정한다. R 키로 돌릴 수 있는 가구여도
            // 다른 벽으로 옮기면 그 벽에 맞춰 다시 정렬된다.
            _rotationStep = bestStep;
            cell = bestCell;
            return true;
        }

        private static Plane GetWallPlane(RoomGrid.WallSide side, Vector3 origin, Vector3Int size, float cs)
        {
            switch (side)
            {
                case RoomGrid.WallSide.MinX:
                    return new Plane(Vector3.right, origin);
                case RoomGrid.WallSide.MaxX:
                    return new Plane(Vector3.left, origin + new Vector3(size.x * cs, 0f, 0f));
                case RoomGrid.WallSide.MinZ:
                    return new Plane(Vector3.forward, origin);
                default:
                    return new Plane(Vector3.back, origin + new Vector3(0f, 0f, size.z * cs));
            }
        }

        private static bool TryPlane(Ray ray, Plane plane, out Vector3 hit, out float distance)
        {
            hit = default;
            if (!plane.Raycast(ray, out distance) || distance <= 0f) return false;
            hit = ray.GetPoint(distance);
            return true;
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        // ─────────────────────────────────────────────────────────
        // 설치
        // ─────────────────────────────────────────────────────────

        private void ConfirmPlacement()
        {
            if (_selected == null || !_currentValid) return;

            if (PlaceAt(_selected, _currentCell, _rotationStep) == null) return;

            // 설치 후에는 다음 가구를 고를 수 있게 인벤토리로 돌아간다.
            CancelPositioning();
        }

        /// <summary>
        /// 지정한 칸에 가구를 설치한다. 마우스 조작을 거치지 않으므로
        /// 서버에서 받은 레이아웃을 복원할 때도 이 메서드를 쓴다 (API문서.md 5.1 placements).
        /// 배치가 불가능하면 null 을 돌려준다.
        /// </summary>
        public PlacedFurniture PlaceAt(FurnitureDefinition definition, Vector3Int cell, int rotationStep)
        {
            if (definition == null || definition.prefab == null || grid == null) return null;

            if (grid.Validate(definition, cell, rotationStep) != RoomGrid.PlacementError.None)
                return null;

            var rotated = definition.GetRotatedSize(rotationStep);

            var go = Instantiate(definition.prefab, furnitureRoot);
            go.name = $"{definition.id}_{cell.x}_{cell.y}_{cell.z}";
            go.transform.position = grid.FootprintToWorld(cell, rotated);
            go.transform.rotation = Quaternion.Euler(0f, rotationStep * 90f, 0f);

            var placed = go.GetComponent<PlacedFurniture>();
            if (placed == null) placed = go.AddComponent<PlacedFurniture>();
            placed.Initialize(definition, cell, rotationStep);

            // 가구도 스프라이트라 깊이 버퍼로 가려지지 않는다.
            // 캐릭터와 같은 기준으로 sortingOrder 를 넣어야 앞뒤가 맞는다.
            IsoSorting.ApplyBaseOrder(go,
                IsoSorting.GetBaseOrder(targetCamera, go.transform.position, grid));

            if (!grid.Occupy(placed))
            {
                // 검사를 통과했는데 등록이 실패하면 상태가 어긋난 것이다. 흔적을 남기고 되돌린다.
                Debug.LogError($"[Placement] 점유 등록 실패: {definition.id} at {cell}");
                Destroy(go);
                return null;
            }

            FurniturePlaced?.Invoke(placed);
            return placed;
        }

        /// <summary>설치된 가구를 치운다.</summary>
        public void RemoveFurniture(PlacedFurniture furniture)
        {
            if (furniture == null || grid == null) return;
            grid.Release(furniture);
            Destroy(furniture.gameObject);
        }
    }
}
