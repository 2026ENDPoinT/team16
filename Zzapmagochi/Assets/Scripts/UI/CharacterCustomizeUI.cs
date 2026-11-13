using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zzapmagochi.Character;

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

        private bool _open;

        private void Awake()
        {
            if (modeButton != null) modeButton.onClick.AddListener(Toggle);
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            if (itemButtonTemplate != null) itemButtonTemplate.gameObject.SetActive(false);
            if (slotLabelTemplate != null) slotLabelTemplate.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (customizer != null) customizer.EquipChanged += OnEquipChanged;
        }

        private void OnDisable()
        {
            if (customizer != null) customizer.EquipChanged -= OnEquipChanged;
        }

        private bool _captured;

        private void Start()
        {
            BuildItemList();
            SetOpen(false);
        }

        public void Toggle() => SetOpen(!_open);
        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);

        private void SetOpen(bool open)
        {
            _open = open;

            if (customRoot != null) customRoot.SetActive(open);
            if (modeButtonLabel != null) modeButtonLabel.text = open ? ExitLabel : EnterLabel;

            if (!open) return;

            // 캐릭터에 이미 입혀져 있는 옷을 장착 상태로 잡아 둔다.
            // Start 에서 하면 CharacterParts.Start 보다 먼저 돌 수 있어 파츠가 아직 없다.
            // 처음 열 때 한 번만 읽는다.
            if (!_captured && customizer != null)
            {
                customizer.CaptureCurrent(ownedItems);
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
        }

        private void OnEquipChanged(CharacterSlot slot, CharacterItemDefinition item)
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
        }
    }
}
