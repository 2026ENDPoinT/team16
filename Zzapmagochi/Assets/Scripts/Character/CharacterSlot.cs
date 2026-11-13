using System.Collections.Generic;

namespace Zzapmagochi.Character
{
    /// <summary>
    /// 꾸미기로 바꿀 수 있는 슬롯.
    /// hair_front / hair_back / mouth / eye / leg / arm / head / body 는 고정 파츠라 여기 없다.
    /// </summary>
    public enum CharacterSlot
    {
        Outer,
        Accessories,
        ClothTop,
        ClothBottom,
        Shoe
    }

    /// <summary>
    /// 슬롯과 CharacterBody 필드 이름의 대응표.
    /// 신발과 하의는 걷기/대기 두 장을 함께 갈아끼워야 하므로 한 슬롯이 두 파츠를 가진다.
    /// </summary>
    public static class CharacterSlots
    {
        /// <summary>슬롯 하나가 건드리는 파츠 이름들.</summary>
        public readonly struct SlotParts
        {
            /// <summary>대기 포즈 파츠 이름. 포즈 구분이 없는 슬롯은 이것만 쓴다.</summary>
            public readonly string Idle;

            /// <summary>걷기 포즈 파츠 이름. 없으면 null.</summary>
            public readonly string Walk;

            public SlotParts(string idle, string walk = null)
            {
                Idle = idle;
                Walk = walk;
            }

            public bool HasWalk => !string.IsNullOrEmpty(Walk);
        }

        private static readonly Dictionary<CharacterSlot, SlotParts> Map =
            new Dictionary<CharacterSlot, SlotParts>
            {
                { CharacterSlot.Outer,       new SlotParts("outer") },
                { CharacterSlot.Accessories, new SlotParts("accessories") },
                { CharacterSlot.ClothTop,    new SlotParts("cloth_top") },
                { CharacterSlot.ClothBottom, new SlotParts("cloth_bottom_idle", "cloth_bottom_walk") },
                { CharacterSlot.Shoe,        new SlotParts("shoe_idle", "shoe_walk") },
            };

        public static SlotParts GetParts(CharacterSlot slot)
        {
            return Map.TryGetValue(slot, out var parts) ? parts : default;
        }

        public static IEnumerable<CharacterSlot> All => Map.Keys;

        public static string GetDisplayName(CharacterSlot slot)
        {
            switch (slot)
            {
                case CharacterSlot.Outer: return "외투";
                case CharacterSlot.Accessories: return "액세서리";
                case CharacterSlot.ClothTop: return "상의";
                case CharacterSlot.ClothBottom: return "하의";
                case CharacterSlot.Shoe: return "신발";
                default: return slot.ToString();
            }
        }
    }
}
