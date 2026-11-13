using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// 자취방의 벽지와 바닥지를 교체한다.
    /// 벽지는 room/wall_L 과 room/wall_R 두 장을 같이 바꾸고,
    /// 바닥지는 room/floor 한 장을 바꾼다.
    ///
    /// 벽지와 바닥은 각각 반드시 하나가 선택되어 있어야 하므로 해제는 없다.
    /// </summary>
    public class RoomSurfaceController : MonoBehaviour
    {
        [Header("대상 렌더러")]
        [Tooltip("room/wall_L")]
        [SerializeField] private SpriteRenderer wallLeft;

        [Tooltip("room/wall_R")]
        [SerializeField] private SpriteRenderer wallRight;

        [Tooltip("room/floor")]
        [SerializeField] private SpriteRenderer floor;

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

        private void Start()
        {
            if (defaultWallpaper != null) Apply(defaultWallpaper);
            if (defaultFloor != null) Apply(defaultFloor);
        }

        /// <summary>종류에 따라 알맞은 렌더러에 스프라이트를 넣는다.</summary>
        public void Apply(RoomSurfaceDefinition definition)
        {
            if (definition == null || definition.sprite == null) return;

            if (definition.kind == RoomSurfaceKind.Wallpaper)
            {
                SetSprite(wallLeft, definition.sprite);
                SetSprite(wallRight, definition.sprite);
                CurrentWallpaper = definition;
            }
            else
            {
                SetSprite(floor, definition.sprite);
                CurrentFloor = definition;
            }

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

            var wallSprite = wallLeft != null ? wallLeft.sprite : null;
            var floorSprite = floor != null ? floor.sprite : null;

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

        private static void SetSprite(SpriteRenderer renderer, Sprite sprite)
        {
            if (renderer != null) renderer.sprite = sprite;
        }
    }
}
