using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// 자취방의 벽지와 바닥지를 교체한다.
    ///
    /// 대상 렌더러는 이름이 아니라 "구역"으로 묶어 둔다.
    /// 방 구조가 벽 2장(wall_L / wall_R)일 때도, 4조각(hous_wall_0_0 …)일 때도 같이 동작해야 하기 때문이다.
    /// 분류 규칙은 RoomAtmosphereController 와 똑같이 맞춘다 —
    /// 이름에 "ground" 또는 "floor" 가 들어가면 바닥, 나머지는 벽.
    ///
    /// 벽지와 바닥은 각각 반드시 하나가 선택되어 있어야 하므로 해제는 없다.
    /// </summary>
    public class RoomSurfaceController : MonoBehaviour
    {
        [Header("대상 렌더러")]
        [Tooltip("벽지를 입힐 SpriteRenderer 들. 벽이 여러 조각이면 전부 넣는다.")]
        [SerializeField] private List<SpriteRenderer> wallRenderers = new List<SpriteRenderer>();

        [Tooltip("바닥지를 입힐 SpriteRenderer 들.")]
        [SerializeField] private List<SpriteRenderer> floorRenderers = new List<SpriteRenderer>();

        [Header("연동")]
        [Tooltip("벽지·바닥을 바꾼 뒤 3D 두께 톤을 다시 계산하게 한다. 없으면 생략한다.")]
        [SerializeField] private RoomAtmosphereController atmosphere;

        [Header("기본값")]
        [Tooltip("비워 두면 씬에 이미 들어 있는 스프라이트를 그대로 쓴다.")]
        [SerializeField] private RoomSurfaceDefinition defaultWallpaper;
        [SerializeField] private RoomSurfaceDefinition defaultFloor;

        /// <summary>현재 선택된 벽지. 항상 하나가 선택되어 있어야 한다.</summary>
        public RoomSurfaceDefinition CurrentWallpaper { get; private set; }

        /// <summary>현재 선택된 바닥지. 항상 하나가 선택되어 있어야 한다.</summary>
        public RoomSurfaceDefinition CurrentFloor { get; private set; }

        /// <summary>벽지나 바닥지가 바뀔 때 알린다. UI 가 선택 표시를 갱신하는 데 쓴다.</summary>
        public event Action<RoomSurfaceKind, RoomSurfaceDefinition> SurfaceChanged;

        /// <summary>이름이 바닥을 가리키는지. RoomAtmosphereController 와 같은 규칙이다.</summary>
        public static bool IsFloorName(string name)
        {
            return name.Contains("ground") || name.Contains("floor");
        }

        private void Start()
        {
            if (defaultWallpaper != null) Apply(defaultWallpaper);
            if (defaultFloor != null) Apply(defaultFloor);
        }

        /// <summary>종류에 따라 알맞은 렌더러들에 스프라이트를 넣는다.</summary>
        public void Apply(RoomSurfaceDefinition definition)
        {
            if (definition == null || definition.sprite == null) return;

            if (definition.kind == RoomSurfaceKind.Wallpaper)
            {
                SetSprites(wallRenderers, definition.sprite);
                CurrentWallpaper = definition;
            }
            else
            {
                SetSprites(floorRenderers, definition.sprite);
                CurrentFloor = definition;
            }

            // 벽/바닥 스프라이트가 바뀌면 3D 두께 부분의 색도 다시 맞춰야 한다.
            // 이걸 빼먹으면 벽면만 바뀌고 모서리 두께는 이전 색으로 남는다.
            if (atmosphere != null) atmosphere.RefreshAll();

            SurfaceChanged?.Invoke(definition.kind, definition);
        }

        public RoomSurfaceDefinition GetCurrent(RoomSurfaceKind kind)
        {
            return kind == RoomSurfaceKind.Wallpaper ? CurrentWallpaper : CurrentFloor;
        }

        public bool IsSelected(RoomSurfaceDefinition definition)
        {
            return definition != null && GetCurrent(definition.kind) == definition;
        }

        /// <summary>
        /// 씬에 이미 들어 있는 스프라이트와 일치하는 정의를 찾아 선택 상태로 잡는다.
        /// 열자마자 방이 바뀌어 보이지 않도록, 적용은 하지 않고 선택만 맞춘다.
        /// </summary>
        public void CaptureCurrent(IEnumerable<RoomSurfaceDefinition> catalog)
        {
            if (catalog == null) return;

            var wallSprite = FirstSprite(wallRenderers);
            var floorSprite = FirstSprite(floorRenderers);

            foreach (var definition in catalog)
            {
                if (definition == null || definition.sprite == null) continue;

                if (definition.kind == RoomSurfaceKind.Wallpaper)
                {
                    if (CurrentWallpaper == null && definition.sprite == wallSprite)
                        CurrentWallpaper = definition;
                }
                else
                {
                    if (CurrentFloor == null && definition.sprite == floorSprite)
                        CurrentFloor = definition;
                }
            }
        }

        /// <summary>
        /// 아직 선택된 것이 없으면 목록의 첫 번째를 적용한다.
        /// "반드시 하나가 선택되어야 한다"를 보장하는 마지막 안전장치다.
        /// </summary>
        public void EnsureSelection(IEnumerable<RoomSurfaceDefinition> catalog)
        {
            if (catalog == null) return;

            foreach (var definition in catalog)
            {
                if (definition == null) continue;

                if (definition.kind == RoomSurfaceKind.Wallpaper && CurrentWallpaper == null)
                    Apply(definition);
                else if (definition.kind == RoomSurfaceKind.Floor && CurrentFloor == null)
                    Apply(definition);
            }
        }

        private static Sprite FirstSprite(List<SpriteRenderer> renderers)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.sprite != null) return renderer.sprite;
            }

            return null;
        }

        private static void SetSprites(List<SpriteRenderer> renderers, Sprite sprite)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null) renderer.sprite = sprite;
            }
        }
    }
}
