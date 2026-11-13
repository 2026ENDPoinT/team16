using UnityEngine;

namespace Zzapmagochi.Character
{
    /// <summary>
    /// 캐릭터 꾸미기 아이템 하나. 서버는 id 만 주고받는다 (API문서.md 3.1 inventory).
    /// </summary>
    [CreateAssetMenu(menuName = "Zzapmagochi/Character Item", fileName = "cloth_new")]
    public class CharacterItemDefinition : ScriptableObject
    {
        [Header("식별")]
        public string id;
        public string displayName;

        [Header("슬롯")]
        public CharacterSlot slot = CharacterSlot.ClothTop;

        [Header("스프라이트")]
        [Tooltip("대기 포즈용 스프라이트. 포즈 구분이 없는 슬롯은 이것만 쓴다.")]
        public Sprite idleSprite;

        [Tooltip("걷기 포즈용 스프라이트. 비우면 대기용을 그대로 쓴다.")]
        public Sprite walkSprite;

        /// <summary>인벤토리 썸네일.</summary>
        public Sprite Icon => idleSprite != null ? idleSprite : walkSprite;

        public Sprite GetSprite(bool walking)
        {
            if (walking) return walkSprite != null ? walkSprite : idleSprite;
            return idleSprite != null ? idleSprite : walkSprite;
        }

        public string DisplayNameOrId => string.IsNullOrEmpty(displayName) ? id : displayName;
    }
}
