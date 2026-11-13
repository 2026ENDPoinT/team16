using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zzapmagochi.Room;

namespace Zzapmagochi.UI
{
    /// <summary>
    /// 화면 오른쪽 절반에 뜨는 가구 인벤토리.
    /// 배치 모드로 들어가면 열리고, 가구를 고르면 닫힌다.
    /// 벽지와 바닥지도 같은 패널에서 고른다(이쪽은 고르는 즉시 적용되고 패널은 닫히지 않는다).
    /// </summary>
    public class FurnitureInventoryUI : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private FurniturePlacementController placement;

        [Tooltip("벽지·바닥 교체 담당. 없으면 해당 구역을 만들지 않는다.")]
        [SerializeField] private RoomSurfaceController surfaces;

        [Tooltip("배치 모드 진입 버튼 (화면 왼쪽 위)")]
        [SerializeField] private Button modeButton;
        [SerializeField] private TMP_Text modeButtonLabel;

        [Tooltip("인벤토리 패널 루트 (화면 오른쪽 절반)")]
        [SerializeField] private GameObject inventoryRoot;

        [Tooltip("가구 버튼이 들어갈 컨테이너")]
        [SerializeField] private Transform itemContainer;

        [Tooltip("복제해서 쓸 가구 버튼 원본. 비활성 상태로 둔다.")]
        [SerializeField] private Button itemButtonTemplate;

        [Tooltip("구역 제목 라벨 원본. 비활성 상태로 둔다.")]
        [SerializeField] private TMP_Text sectionLabelTemplate;

        [Tooltip("조작 안내 문구")]
        [SerializeField] private GameObject hintRoot;

        [Tooltip("안내 문구 텍스트. 모드에 따라 내용을 바꾼다.")]
        [SerializeField] private TMP_Text hintText;

        [Tooltip("인벤토리 최상단의 '가구 제거' 버튼")]
        [SerializeField] private Button removeButton;

        [Header("보유 가구")]
        [Tooltip("임시. 실제로는 서버의 인벤토리에서 받아온다 (API문서.md 3.1 inventory).")]
        [SerializeField] private List<FurnitureDefinition> ownedFurniture = new List<FurnitureDefinition>();

        [Header("보유 벽지 · 바닥")]
        [Tooltip("벽지 목록. 반드시 하나가 선택된 상태로 유지된다.")]
        [SerializeField] private List<RoomSurfaceDefinition> ownedWallpapers = new List<RoomSurfaceDefinition>();

        [Tooltip("바닥지 목록. 반드시 하나가 선택된 상태로 유지된다.")]
        [SerializeField] private List<RoomSurfaceDefinition> ownedFloors = new List<RoomSurfaceDefinition>();

        [Header("선택 표시")]
        [SerializeField] private Color normalColor = new Color32(0x3A, 0x41, 0x50, 0xFF);
        [SerializeField] private Color selectedColor = new Color32(0x4C, 0x8B, 0xF5, 0xFF);

        private const string EnterLabel = "가구 배치 모드";
        private const string ExitLabel = "배치 모드 종료";

        private const string PlacingHint = "좌클릭 설치   ·   우클릭 취소   ·   R 회전   ·   ESC 종료";
        private const string RemovingHint = "치울 가구를 클릭하세요   ·   우클릭 취소   ·   ESC 종료";

        private readonly List<Button> _spawned = new List<Button>();

        /// <summary>생성한 버튼 ↔ 벽지·바닥지. 선택 표시를 갱신할 때 쓴다.</summary>
        private readonly Dictionary<RoomSurfaceDefinition, Image> _surfaceButtons =
            new Dictionary<RoomSurfaceDefinition, Image>();

        private bool _captured;

        private void Awake()
        {
            if (modeButton != null)
                modeButton.onClick.AddListener(OnModeButtonClicked);

            if (removeButton != null)
                removeButton.onClick.AddListener(OnRemoveButtonClicked);

            if (itemButtonTemplate != null)
                itemButtonTemplate.gameObject.SetActive(false);

            if (sectionLabelTemplate != null)
                sectionLabelTemplate.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (placement != null)
                placement.ModeChanged += OnModeChanged;

            if (surfaces != null)
                surfaces.SurfaceChanged += OnSurfaceChanged;
        }

        private void OnDisable()
        {
            if (placement != null)
                placement.ModeChanged -= OnModeChanged;

            if (surfaces != null)
                surfaces.SurfaceChanged -= OnSurfaceChanged;
        }

        private void Start()
        {
            BuildItemButtons();
            OnModeChanged(placement != null
                ? placement.CurrentMode
                : FurniturePlacementController.Mode.Off);
        }

        private void OnModeButtonClicked()
        {
            if (placement != null) placement.TogglePlacementMode();
        }

        private void OnRemoveButtonClicked()
        {
            // 제거 모드로 들어가면 ModeChanged 가 돌아와 인벤토리가 닫힌다.
            if (placement != null) placement.EnterRemoveMode();
        }

        private void OnModeChanged(FurniturePlacementController.Mode mode)
        {
            var placing = mode != FurniturePlacementController.Mode.Off;

            if (inventoryRoot != null)
                inventoryRoot.SetActive(mode == FurniturePlacementController.Mode.Browsing);

            // 미리보기 중이거나 제거 중일 때 안내를 띄운다.
            var showHint = mode == FurniturePlacementController.Mode.Positioning
                           || mode == FurniturePlacementController.Mode.Removing;

            if (hintRoot != null) hintRoot.SetActive(showHint);

            if (hintText != null && showHint)
                hintText.text = mode == FurniturePlacementController.Mode.Removing
                    ? RemovingHint
                    : PlacingHint;

            if (modeButtonLabel != null)
                modeButtonLabel.text = placing ? ExitLabel : EnterLabel;

            if (mode != FurniturePlacementController.Mode.Browsing) return;

            // 씬에 이미 깔려 있는 벽지·바닥을 선택 상태로 잡아 둔다.
            // Start 에서 하면 RoomSurfaceController.Start 보다 먼저 돌 수 있다.
            if (!_captured && surfaces != null)
            {
                var catalog = new List<RoomSurfaceDefinition>(ownedWallpapers);
                catalog.AddRange(ownedFloors);

                surfaces.CaptureCurrent(catalog);
                surfaces.EnsureSelection(catalog);
                _captured = true;
            }

            RefreshSurfaceSelection();
        }

        private void BuildItemButtons()
        {
            if (itemButtonTemplate == null || itemContainer == null) return;

            foreach (var button in _spawned)
            {
                if (button != null) Destroy(button.gameObject);
            }
            _spawned.Clear();
            _surfaceButtons.Clear();

            CreateSectionLabel("가구", ownedFurniture.Count > 0);

            foreach (var definition in ownedFurniture)
            {
                if (definition == null) continue;
                CreateFurnitureButton(definition);
            }

            if (surfaces == null) return;

            CreateSurfaceSection("벽지", ownedWallpapers);
            CreateSurfaceSection("바닥", ownedFloors);
        }

        private void CreateSectionLabel(string title, bool condition = true)
        {
            if (!condition || sectionLabelTemplate == null) return;

            var label = Instantiate(sectionLabelTemplate, itemContainer);
            label.gameObject.SetActive(true);
            label.name = $"Section_{title}";
            label.text = title;
        }

        private void CreateFurnitureButton(FurnitureDefinition definition)
        {
            var button = Instantiate(itemButtonTemplate, itemContainer);
            button.gameObject.SetActive(true);
            button.name = $"Item_{definition.id}";

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = $"{definition.DisplayNameOrId}\n" +
                             $"<size=70%>{definition.size.x}×{definition.size.y}×{definition.size.z} · " +
                             $"{(definition.surface == PlacementSurface.Floor ? "바닥" : "벽")}</size>";

            SetIcon(button, null);

            var captured = definition;
            button.onClick.AddListener(() => OnItemClicked(captured));

            _spawned.Add(button);
        }

        private void CreateSurfaceSection(string title, List<RoomSurfaceDefinition> definitions)
        {
            var any = false;
            foreach (var definition in definitions)
            {
                if (definition != null) { any = true; break; }
            }

            if (!any) return;

            CreateSectionLabel(title);

            foreach (var definition in definitions)
            {
                if (definition == null) continue;

                var button = Instantiate(itemButtonTemplate, itemContainer);
                button.gameObject.SetActive(true);
                button.name = $"Surface_{definition.id}";

                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = definition.DisplayNameOrId;

                SetIcon(button, definition.sprite);

                var captured = definition;
                button.onClick.AddListener(() => OnSurfaceClicked(captured));

                var background = button.GetComponent<Image>();
                if (background != null) _surfaceButtons[definition] = background;

                _spawned.Add(button);
            }
        }

        /// <summary>버튼 자식 중 "Icon" 이름의 Image 에 썸네일을 넣는다.</summary>
        private static void SetIcon(Button button, Sprite sprite)
        {
            var icon = button.transform.Find("Icon")?.GetComponent<Image>();
            if (icon == null) return;

            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
        }

        private void OnItemClicked(FurnitureDefinition definition)
        {
            if (placement != null) placement.SelectFurniture(definition);
        }

        private void OnSurfaceClicked(RoomSurfaceDefinition definition)
        {
            if (surfaces == null) return;

            // 벽지·바닥은 반드시 하나가 선택되어 있어야 하므로 해제는 없다.
            // 이미 선택된 것을 다시 눌러도 그대로 둔다.
            if (surfaces.IsSelected(definition)) return;

            surfaces.Apply(definition);
        }

        private void OnSurfaceChanged(RoomSurfaceKind kind, RoomSurfaceDefinition definition)
        {
            RefreshSurfaceSelection();
        }

        private void RefreshSurfaceSelection()
        {
            if (surfaces == null) return;

            foreach (var pair in _surfaceButtons)
            {
                if (pair.Value == null) continue;
                pair.Value.color = surfaces.IsSelected(pair.Key) ? selectedColor : normalColor;
            }
        }
    }
}
