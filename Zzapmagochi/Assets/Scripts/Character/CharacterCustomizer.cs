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

        /// <summary>선택된 캐릭터가 바뀔 때 알린다.</summary>
        public event Action<CharacterPresetDefinition> PresetChanged;

        public IReadOnlyDictionary<CharacterSlot, CharacterItemDefinition> Equipped => _equipped;

        /// <summary>현재 선택된 캐릭터. 항상 하나가 선택되어 있어야 한다.</summary>
        public CharacterPresetDefinition CurrentPreset { get; private set; }

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

        // ─────────────────────────────────────────────────────────
        // 캐릭터(프리셋) 전환
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// 캐릭터를 바꾼다. 고정 파츠를 프리셋 것으로 갈고,
        /// 의상은 그 캐릭터의 기본 프리셋으로 되돌린다.
        /// </summary>
        public void ApplyPreset(CharacterPresetDefinition preset)
        {
            if (preset == null) return;

            CurrentPreset = preset;

            // 1. 고정 파츠 교체
            foreach (var target in targets)
            {
                if (target == null) continue;

                foreach (var pair in preset.EnumerateFixedParts())
                    target.SetSprite(pair.Key, pair.Value);
            }

            // 2. 의상을 프리셋 기본값으로 재설정
            _equipped.Clear();
            foreach (var slot in CharacterSlots.All)
            {
                var item = preset.GetDefaultItem(slot);
                if (item != null) _equipped[slot] = item;
            }

            foreach (var slot in CharacterSlots.All)
                ApplySlot(slot);

            PresetChanged?.Invoke(preset);
        }

        /// <summary>
        /// 적용하지 않고 선택 상태만 맞춘다.
        /// 씬에 이미 그 캐릭터가 서 있을 때, 다시 덮어써서 깜빡이지 않도록 쓴다.
        /// </summary>
        public void SetPresetWithoutApplying(CharacterPresetDefinition preset)
        {
            CurrentPreset = preset;
            PresetChanged?.Invoke(preset);
        }

        /// <summary>현재 캐릭터의 고정 파츠와 일치하는 프리셋을 찾는다. 없으면 null.</summary>
        public CharacterPresetDefinition FindMatchingPreset(IEnumerable<CharacterPresetDefinition> presets)
        {
            if (presets == null || targets.Count == 0 || targets[0] == null) return null;

            var source = targets[0];

            foreach (var preset in presets)
            {
                if (preset == null) continue;

                var matches = true;
                foreach (var pair in preset.EnumerateFixedParts())
                {
                    // 프리셋이 비워 둔 파츠는 비교하지 않는다.
                    if (pair.Value == null) continue;

                    if (source.GetSprite(pair.Key) != pair.Value)
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches) return preset;
            }

            return null;
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
