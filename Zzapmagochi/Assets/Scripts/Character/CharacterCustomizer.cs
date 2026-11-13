using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zzapmagochi.Character
{
    /// <summary>
    /// 슬롯 단위로 아이템을 입히고 벗긴다.
    /// 같은 변경을 여러 CharacterParts 에 동시에 적용한다(씬의 캐릭터 + 꾸미기 미리보기).
    /// </summary>
    public class CharacterCustomizer : MonoBehaviour
    {
        [Tooltip("변경을 함께 적용할 캐릭터들. 보통 씬의 캐릭터와 미리보기 복제본.")]
        [SerializeField] private List<CharacterParts> targets = new List<CharacterParts>();

        /// <summary>슬롯별로 현재 장착한 아이템. 비어 있으면 벗은 상태.</summary>
        private readonly Dictionary<CharacterSlot, CharacterItemDefinition> _equipped =
            new Dictionary<CharacterSlot, CharacterItemDefinition>();

        /// <summary>장착 상태가 바뀔 때 알린다. UI 가 선택 표시를 갱신하는 데 쓴다.</summary>
        public event Action<CharacterSlot, CharacterItemDefinition> EquipChanged;

        public IReadOnlyDictionary<CharacterSlot, CharacterItemDefinition> Equipped => _equipped;

        public void AddTarget(CharacterParts parts)
        {
            if (parts == null || targets.Contains(parts)) return;
            targets.Add(parts);
            ApplyAll(parts);
        }

        public CharacterItemDefinition GetEquipped(CharacterSlot slot)
        {
            return _equipped.TryGetValue(slot, out var item) ? item : null;
        }

        public bool IsEquipped(CharacterItemDefinition item)
        {
            return item != null && GetEquipped(item.slot) == item;
        }

        /// <summary>
        /// 아이템을 장착한다. 같은 슬롯에 이미 있던 아이템은 자동으로 교체된다.
        /// 이미 장착한 아이템을 다시 넣으면 벗는다(토글).
        /// </summary>
        public void Toggle(CharacterItemDefinition item)
        {
            if (item == null) return;

            if (IsEquipped(item)) Unequip(item.slot);
            else Equip(item);
        }

        public void Equip(CharacterItemDefinition item)
        {
            if (item == null) return;

            _equipped[item.slot] = item;
            ApplySlot(item.slot);
            EquipChanged?.Invoke(item.slot, item);
        }

        public void Unequip(CharacterSlot slot)
        {
            _equipped.Remove(slot);
            ApplySlot(slot);
            EquipChanged?.Invoke(slot, null);
        }

        /// <summary>현재 캐릭터에 들어 있는 스프라이트를 그 슬롯의 기본값으로 읽어 온다.</summary>
        public void CaptureCurrent(IEnumerable<CharacterItemDefinition> catalog)
        {
            if (targets.Count == 0 || catalog == null) return;

            var source = targets[0];
            if (source == null) return;

            foreach (var item in catalog)
            {
                if (item == null) continue;

                var parts = CharacterSlots.GetParts(item.slot);
                if (string.IsNullOrEmpty(parts.Idle)) continue;

                var current = source.GetSprite(parts.Idle);
                if (current != null && current == item.idleSprite)
                    _equipped[item.slot] = item;
            }
        }

        private void ApplySlot(CharacterSlot slot)
        {
            foreach (var target in targets)
                ApplySlot(target, slot);
        }

        private void ApplyAll(CharacterParts target)
        {
            foreach (var slot in CharacterSlots.All)
                ApplySlot(target, slot);
        }

        private void ApplySlot(CharacterParts target, CharacterSlot slot)
        {
            if (target == null) return;

            var parts = CharacterSlots.GetParts(slot);
            if (string.IsNullOrEmpty(parts.Idle)) return;

            var item = GetEquipped(slot);

            // 벗은 상태는 스프라이트를 null 로 밀어 넣는다. CharacterParts 가 렌더러를 꺼 준다.
            target.SetSprite(parts.Idle, item != null ? item.GetSprite(false) : null);

            if (parts.HasWalk)
                target.SetSprite(parts.Walk, item != null ? item.GetSprite(true) : null);
        }
    }
}
