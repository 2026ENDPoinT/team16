using System.Collections.Generic;
using UnityEngine;

namespace Zzapmagochi.Character
{
    /// <summary>
    /// 캐릭터 한 종류의 기본 프리셋.
    /// 꾸미기로 바꿀 수 없는 고정 파츠(몸·머리·눈·입·팔·다리·헤어)와
    /// 슬롯별 기본 의상을 한 에셋에 담는다.
    ///
    /// 만드는 법:
    ///   Project 창 우클릭 → Create → Zzapmagochi → Character Preset
    ///   또는 메뉴 Zzapmagochi → Character → Create Preset From Scene Character
    ///   (씬에 있는 캐릭터의 현재 파츠를 그대로 떠서 에셋으로 만든다)
    /// </summary>
    [CreateAssetMenu(menuName = "Zzapmagochi/Character Preset", fileName = "chara_new")]
    public class CharacterPresetDefinition : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("서버와 주고받는 고유 ID")]
        public string id;

        [Tooltip("선택 목록에 보이는 이름")]
        public string displayName;

        [Tooltip("선택 버튼 썸네일. 비우면 head 스프라이트를 쓴다.")]
        public Sprite portrait;

        [Header("고정 파츠 — 꾸미기로 바꿀 수 없는 부위")]
        public Sprite body;
        public Sprite head;
        public Sprite arm;
        public Sprite eye;
        public Sprite mouth;
        public Sprite hairFront;
        public Sprite hairBack;

        [Tooltip("대기 포즈 다리")]
        public Sprite legIdle;

        [Tooltip("걷기 포즈 다리")]
        public Sprite legWalk;

        [Header("기본 의상 — 슬롯별로 하나씩. 비우면 안 입은 상태로 시작한다")]
        public CharacterItemDefinition outer;
        public CharacterItemDefinition accessories;
        public CharacterItemDefinition clothTop;
        public CharacterItemDefinition clothBottom;
        public CharacterItemDefinition shoe;

        public Sprite Portrait => portrait != null ? portrait : head;
        public string DisplayNameOrId => string.IsNullOrEmpty(displayName) ? id : displayName;

        /// <summary>CharacterBody 필드 이름 → 이 프리셋이 넣을 스프라이트.</summary>
        public IEnumerable<KeyValuePair<string, Sprite>> EnumerateFixedParts()
        {
            yield return new KeyValuePair<string, Sprite>("body", body);
            yield return new KeyValuePair<string, Sprite>("head", head);
            yield return new KeyValuePair<string, Sprite>("arm", arm);
            yield return new KeyValuePair<string, Sprite>("eye", eye);
            yield return new KeyValuePair<string, Sprite>("mouth", mouth);
            yield return new KeyValuePair<string, Sprite>("hair_front", hairFront);
            yield return new KeyValuePair<string, Sprite>("hair_back", hairBack);
            yield return new KeyValuePair<string, Sprite>("leg_idle", legIdle);
            yield return new KeyValuePair<string, Sprite>("leg_walk", legWalk);
        }

        /// <summary>슬롯의 기본 의상. 없으면 null(=안 입은 상태).</summary>
        public CharacterItemDefinition GetDefaultItem(CharacterSlot slot)
        {
            switch (slot)
            {
                case CharacterSlot.Outer: return outer;
                case CharacterSlot.Accessories: return accessories;
                case CharacterSlot.ClothTop: return clothTop;
                case CharacterSlot.ClothBottom: return clothBottom;
                case CharacterSlot.Shoe: return shoe;
                default: return null;
            }
        }
    }
}
