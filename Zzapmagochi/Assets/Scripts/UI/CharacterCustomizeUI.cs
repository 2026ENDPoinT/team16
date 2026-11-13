using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zzapmagochi.Character;
using Zzapmagochi.Core;
using Zzapmagochi.Net;

namespace Zzapmagochi.UI
{
    /// <summary>
    /// 캐릭터 꾸미기 모드 UI.
    /// 왼쪽 절반: 현재 캐릭터 미리보기 / 오른쪽 절반: 보유한 의상 목록.
    /// </summary>
    public class CharacterCustomizeUI : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private CharacterCustomizer customizer;

        [Tooltip("화면 왼쪽 위 모드 진입 버튼")]
        [SerializeField] private Button modeButton;
        [SerializeField] private TMP_Text modeButtonLabel;

        [Tooltip("꾸미기 모드 전체 루트")]
        [SerializeField] private GameObject customRoot;

        [Tooltip("오른쪽 의상 목록의 항목이 들어갈 컨테이너")]
        [SerializeField] private Transform itemContainer;

        [Tooltip("복제해서 쓸 항목 버튼 원본. 비활성 상태로 둔다.")]
        [SerializeField] private Button itemButtonTemplate;

        [Tooltip("슬롯 구분 라벨 원본. 비활성 상태로 둔다.")]
        [SerializeField] private TMP_Text slotLabelTemplate;

        [SerializeField] private Button closeButton;

        [Header("보유 캐릭터")]
        [Tooltip("선택할 수 있는 캐릭터 프리셋. 목록의 맨 위에 표시되고, 항상 하나가 선택된다.")]
        [SerializeField] private List<CharacterPresetDefinition> ownedCharacters = new List<CharacterPresetDefinition>();

        [Header("보유 의상")]
        [Tooltip("임시. 실제로는 서버 인벤토리에서 받아온다 (API문서.md 3.1).")]
        [SerializeField] private List<CharacterItemDefinition> ownedItems = new List<CharacterItemDefinition>();

        [Header("선택 표시")]
        [SerializeField] private Color normalColor = new Color32(0x3A, 0x41, 0x50, 0xFF);
        [SerializeField] private Color selectedColor = new Color32(0x4C, 0x8B, 0xF5, 0xFF);

        private const string EnterLabel = "캐릭터 커스텀";
        private const string ExitLabel = "커스텀 종료";

        /// <summary>생성한 버튼 ↔ 아이템. 선택 표시를 갱신할 때 쓴다.</summary>
        private readonly Dictionary<CharacterItemDefinition, Image> _itemButtons =
            new Dictionary<CharacterItemDefinition, Image>();

        /// <summary>생성한 버튼 ↔ 캐릭터 프리셋.</summary>
        private readonly Dictionary<CharacterPresetDefinition, Image> _characterButtons =
            new Dictionary<CharacterPresetDefinition, Image>();

        private bool _open;
        private bool _captured;
        private bool _isRestoring;

        private void Awake()
        {
            if (modeButton != null) modeButton.onClick.AddListener(Toggle);
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            if (itemButtonTemplate != null) itemButtonTemplate.gameObject.SetActive(false);
            if (slotLabelTemplate != null) slotLabelTemplate.gameObject.SetActive(false);

            EnsureScrollableList();
        }

        /// <summary>
        /// 의상 목록(itemContainer)도 가구 인벤토리처럼 마우스 휠과 드래그로 스크롤되게 만듭니다.
        /// </summary>
        private void EnsureScrollableList()
        {
            if (itemContainer == null) return;
            if (itemContainer.GetComponentInParent<ScrollRect>() != null) return;

            var contentRect = itemContainer as RectTransform;
            var parentRect = contentRect != null ? contentRect.parent as RectTransform : null;
            if (contentRect == null || parentRect == null) return;

            var viewportGo = new GameObject(
                "ItemsScrollViewport",
                typeof(RectTransform),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect));

            var viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.SetParent(parentRect, false);
            viewportRect.anchorMin = contentRect.anchorMin;
            viewportRect.anchorMax = contentRect.anchorMax;
            viewportRect.anchoredPosition = contentRect.anchoredPosition;
            viewportRect.sizeDelta = contentRect.sizeDelta;
            viewportRect.pivot = contentRect.pivot;
            viewportRect.SetSiblingIndex(contentRect.GetSiblingIndex());

            var bgImage = viewportGo.GetComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.005f);
            bgImage.raycastTarget = true;

            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            var fitter = contentRect.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
        }

        private void OnEnable()
        {
            if (customizer == null) return;
            customizer.EquipChanged += OnEquipChanged;
            customizer.PresetChanged += OnPresetChanged;
        }

        private void OnDisable()
        {
            if (customizer == null) return;
            customizer.EquipChanged -= OnEquipChanged;
            customizer.PresetChanged -= OnPresetChanged;
        }

        private void Start()
        {
            BuildItemList();
            SetOpen(false);

            _ = LoadCharacterFromServerAsync();
        }

        public void Toggle() => SetOpen(!_open);
        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);

        private void SetOpen(bool open)
        {
            var wasOpen = _open;
            _open = open;

            if (customRoot != null) customRoot.SetActive(open);
            if (modeButtonLabel != null) modeButtonLabel.text = open ? ExitLabel : EnterLabel;

            // 커스텀 창을 닫는 시점에 가구 배치 모드 종료와 동일하게 서버에 자동 저장
            if (wasOpen && !open)
            {
                _ = SaveCharacterToServerAsync();
            }

            if (!open) return;

            // 캐릭터에 이미 입혀져 있는 옷과 캐릭터 종류를 선택 상태로 잡아 둔다.
            // Start 에서 하면 CharacterParts.Start 보다 먼저 돌 수 있어 파츠가 아직 없다.
            // 처음 열 때 한 번만 읽는다.
            if (!_captured && customizer != null)
            {
                customizer.CaptureCurrent(ownedItems);
                SelectInitialCharacter();
                _captured = true;
            }

            RefreshSelection();
        }

        /// <summary>
        /// 슬롯별로 묶어서 목록을 만든다.
        /// 변경 가능한 슬롯만 도는 것으로 "변경 가능 파츠만 표시" 요구를 만족한다.
        /// </summary>
        private void BuildItemList()
        {
            if (itemContainer == null || itemButtonTemplate == null) return;

            _itemButtons.Clear();
            _characterButtons.Clear();

            // 캐릭터 선택은 의상(외투)보다 위에 둔다.
            BuildCharacterSection();

            foreach (var slot in CharacterSlots.All)
            {
                var itemsInSlot = new List<CharacterItemDefinition>();
                foreach (var item in ownedItems)
                {
                    if (item != null && item.slot == slot) itemsInSlot.Add(item);
                }

                if (itemsInSlot.Count == 0) continue;

                if (slotLabelTemplate != null)
                {
                    var label = Instantiate(slotLabelTemplate, itemContainer);
                    label.gameObject.SetActive(true);
                    label.name = $"Slot_{slot}";
                    label.text = CharacterSlots.GetDisplayName(slot);
                }

                foreach (var item in itemsInSlot)
                    CreateItemButton(item);
            }
        }

        /// <summary>목록 맨 위의 캐릭터 선택 구역을 만든다.</summary>
        private void BuildCharacterSection()
        {
            var available = new List<CharacterPresetDefinition>();
            foreach (var preset in ownedCharacters)
            {
                if (preset != null) available.Add(preset);
            }

            if (available.Count == 0) return;

            if (slotLabelTemplate != null)
            {
                var label = Instantiate(slotLabelTemplate, itemContainer);
                label.gameObject.SetActive(true);
                label.name = "Slot_Character";
                label.text = "캐릭터";
            }

            foreach (var preset in available)
            {
                var button = Instantiate(itemButtonTemplate, itemContainer);
                button.gameObject.SetActive(true);
                button.name = $"Character_{preset.id}";

                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = preset.DisplayNameOrId;

                var icon = button.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null)
                {
                    icon.sprite = preset.Portrait;
                    icon.enabled = preset.Portrait != null;
                    icon.preserveAspect = true;
                }

                var captured = preset;
                button.onClick.AddListener(() => OnCharacterClicked(captured));

                var background = button.GetComponent<Image>();
                if (background != null) _characterButtons[preset] = background;
            }
        }

        /// <summary>
        /// 처음 열 때 선택 상태를 정한다.
        /// 씬에 서 있는 캐릭터와 같은 프리셋이 있으면 그것을 고르고 덮어쓰지 않는다.
        /// 일치하는 것이 없으면 첫 번째 프리셋을 적용한다 — 항상 하나는 선택되어야 하므로.
        /// </summary>
        private void SelectInitialCharacter()
        {
            if (customizer == null || ownedCharacters.Count == 0) return;
            if (customizer.CurrentPreset != null) return;

            var matching = customizer.FindMatchingPreset(ownedCharacters);
            if (matching != null)
            {
                customizer.SetPresetWithoutApplying(matching);
                return;
            }

            foreach (var preset in ownedCharacters)
            {
                if (preset == null) continue;
                customizer.ApplyPreset(preset);
                return;
            }
        }

        private void OnCharacterClicked(CharacterPresetDefinition preset)
        {
            if (customizer == null) return;

            // 캐릭터는 반드시 하나가 선택되어 있어야 하므로 선택 해제는 없다.
            // 이미 선택된 캐릭터를 다시 눌러도 프리셋을 다시 불러오지 않는다(꾸민 옷이 날아가므로).
            if (customizer.CurrentPreset == preset) return;

            customizer.ApplyPreset(preset);
            _ = SaveCharacterToServerAsync();
        }

        private void CreateItemButton(CharacterItemDefinition item)
        {
            var button = Instantiate(itemButtonTemplate, itemContainer);
            button.gameObject.SetActive(true);
            button.name = $"Item_{item.id}";

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = item.DisplayNameOrId;

            // 썸네일: 자식 중 "Icon" 이름의 Image 를 쓴다.
            var icon = button.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = item.Icon;
                icon.enabled = item.Icon != null;
                icon.preserveAspect = true;
            }

            var captured = item;
            button.onClick.AddListener(() => OnItemClicked(captured));

            var background = button.GetComponent<Image>();
            if (background != null) _itemButtons[item] = background;
        }

        private void OnItemClicked(CharacterItemDefinition item)
        {
            // 같은 아이템을 다시 누르면 벗는다. 다른 아이템이면 같은 슬롯이 교체된다.
            if (customizer != null) customizer.Toggle(item);
            _ = SaveCharacterToServerAsync();
        }

        private async Task LoadCharacterFromServerAsync()
        {
            if (!Session.IsAuthenticated || customizer == null) return;

            // CharacterParts.Start()에서 자식 SpriteRenderer들이 생성될 때까지 1프레임 대기
            await Task.Yield();

            try
            {
                var me = await Api.Current.GetMe();
                if (me == null || me.character == null) return;

                _isRestoring = true;

                // 1. 캐릭터 프리셋(몸체/헤어/눈/입 등 고정 파츠) 복원
                var preset = ownedCharacters.Find(x => x != null && x.id == me.character.presetId);
                if (preset == null && ownedCharacters.Count > 0)
                    preset = ownedCharacters[0];

                if (preset != null)
                    customizer.ApplyPreset(preset);

                // 2. 슬롯별 장착 의상 복원 (비어 있으면 탈의 상태 적용)
                if (me.character.parts != null)
                {
                    ApplySlotFromId(CharacterSlot.Outer, me.character.parts.outer);
                    ApplySlotFromId(CharacterSlot.Accessories, me.character.parts.accessories);
                    ApplySlotFromId(CharacterSlot.ClothTop, me.character.parts.clothTop);
                    ApplySlotFromId(CharacterSlot.ClothBottom, me.character.parts.clothBottom);
                    ApplySlotFromId(CharacterSlot.Shoe, me.character.parts.shoe);
                }

                _captured = true;
                RefreshSelection();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CharacterSync] Load failed: {e.Message}");
            }
            finally
            {
                _isRestoring = false;
            }
        }

        private void ApplySlotFromId(CharacterSlot slot, string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                customizer.Unequip(slot);
                return;
            }

            var item = ownedItems.Find(x => x != null && x.slot == slot && x.id == itemId);
            if (item != null)
                customizer.Equip(item);
            else
                customizer.Unequip(slot);
        }

        private async Task SaveCharacterToServerAsync()
        {
            if (!Session.IsAuthenticated || _isRestoring || customizer == null) return;

            var presetId = customizer.CurrentPreset != null
                ? customizer.CurrentPreset.id
                : (ownedCharacters.Count > 0 && ownedCharacters[0] != null ? ownedCharacters[0].id : "chara_0");

            var req = new CharacterUpdateRequest
            {
                presetId = presetId,
                parts = new CharacterPartsDto
                {
                    outer = GetEquippedItemId(CharacterSlot.Outer),
                    accessories = GetEquippedItemId(CharacterSlot.Accessories),
                    clothTop = GetEquippedItemId(CharacterSlot.ClothTop),
                    clothBottom = GetEquippedItemId(CharacterSlot.ClothBottom),
                    shoe = GetEquippedItemId(CharacterSlot.Shoe)
                }
            };

            try
            {
                await Api.Current.UpdateCharacter(req);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CharacterSync] Save failed: {e.Message}");
            }
        }

        private string GetEquippedItemId(CharacterSlot slot)
        {
            var item = customizer != null ? customizer.GetEquipped(slot) : null;
            return item != null ? item.id : null;
        }

        private void OnEquipChanged(CharacterSlot slot, CharacterItemDefinition item)
        {
            RefreshSelection();
        }

        private void OnPresetChanged(CharacterPresetDefinition preset)
        {
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (customizer == null) return;

            foreach (var pair in _itemButtons)
            {
                if (pair.Value == null) continue;
                pair.Value.color = customizer.IsEquipped(pair.Key) ? selectedColor : normalColor;
            }

            foreach (var pair in _characterButtons)
            {
                if (pair.Value == null) continue;
                pair.Value.color = customizer.CurrentPreset == pair.Key ? selectedColor : normalColor;
            }
        }
    }
}
