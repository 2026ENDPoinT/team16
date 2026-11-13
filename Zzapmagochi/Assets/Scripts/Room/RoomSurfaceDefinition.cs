using UnityEngine;

namespace Zzapmagochi.Room
{
    /// <summary>벽지인지 바닥지인지.</summary>
    public enum RoomSurfaceKind
    {
        Wallpaper,
        Floor
    }

    /// <summary>
    /// 벽지 / 바닥지 한 종류. 서버는 id 만 주고받는다
    /// (API문서.md 5.1 의 wallpaperId / floorId).
    /// </summary>
    [CreateAssetMenu(menuName = "Zzapmagochi/Room Surface", fileName = "wall_new")]
    public class RoomSurfaceDefinition : ScriptableObject
    {
        [Header("식별")]
        public string id;
        public string displayName;

        [Header("종류")]
        public RoomSurfaceKind kind = RoomSurfaceKind.Wallpaper;

        [Header("스프라이트")]
        [Tooltip("벽지는 wall_L / wall_R 에, 바닥지는 floor 에 들어간다.")]
        public Sprite sprite;

        public string DisplayNameOrId => string.IsNullOrEmpty(displayName) ? id : displayName;
    }
}
