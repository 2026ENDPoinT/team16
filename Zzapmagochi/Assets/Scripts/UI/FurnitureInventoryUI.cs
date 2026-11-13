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
    /// </summary>
    public class FurnitureInventoryUI : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private FurniturePlacementController placement;

        [Tooltip("배치 모드 진입 버튼 (화면 왼쪽 위)")]
        [SerializeField] private Button modeButton;
        [SerializeField] private TMP_Text modeButtonLabel;

        [Tooltip("인벤토리 패널 루트 (화면 오른쪽 절반)")]
        [SerializeField] private GameObject inventoryRoot;

        [Tooltip("가구 버튼이 들어갈 컨테이너")]
        [SerializeField] private Transform itemContainer;

        [Tooltip("복제해서 쓸 가구 버튼 원본. 비활성 상태로 둔다.")]
        [SerializeField] private Button itemButtonTemplate;

        [Tooltip("조작 안내 문구")]
        [SerializeField] private GameObject hintRoot;

        [Header("보유 가구")]
        [Tooltip("임시. 실제로는 서버의 인벤토리에서 받아온다 (API문서.md 3.1 inventory).")]
        [SerializeField] private List<FurnitureDefinition> ownedFurniture = new List<FurnitureDefinition>();

        private const string EnterLabel = "가구 배치 모드";
        private const string ExitLabel = "배치 모드 종료";

        private readonly List<Button> _spawned = new List<Button>();

        private void Awake()
        {
            if (modeButton != null)
                modeButton.onClick.AddListener(OnModeButtonClicked);

            if (itemButtonTemplate != null)
                itemButtonTemplate.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (placement != null)
                placement.ModeChanged += OnModeChanged;
        }

        private void OnDisable()
        {
            if (placement != null)
                placement.ModeChanged -= OnModeChanged;
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

        private void OnModeChanged(FurniturePlacementController.Mode mode)
        {
            var placing = mode != FurniturePlacementController.Mode.Off;

            if (inventoryRoot != null)
                inventoryRoot.SetActive(mode == FurniturePlacementController.Mode.Browsing);

            if (hintRoot != null)
                hintRoot.SetActive(mode == FurniturePlacementController.Mode.Positioning);

            if (modeButtonLabel != null)
                modeButtonLabel.text = placing ? ExitLabel : EnterLabel;
        }

        private void BuildItemButtons()
        {
            if (itemButtonTemplate == null || itemContainer == null) return;

            foreach (var button in _spawned)
            {
                if (button != null) Destroy(button.gameObject);
            }
            _spawned.Clear();

            foreach (var definition in ownedFurniture)
            {
                if (definition == null) continue;

                var button = Instantiate(itemButtonTemplate, itemContainer);
                button.gameObject.SetActive(true);
                button.name = $"Item_{definition.id}";

                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = $"{definition.DisplayNameOrId}\n" +
                                 $"<size=70%>{definition.size.x}×{definition.size.y}×{definition.size.z} · " +
                                 $"{(definition.surface == PlacementSurface.Floor ? "바닥" : "벽")}</size>";

                var captured = definition;
                button.onClick.AddListener(() => OnItemClicked(captured));

                _spawned.Add(button);
            }
        }

        private void OnItemClicked(FurnitureDefinition definition)
        {
            if (placement != null) placement.SelectFurniture(definition);
        }
    }
}
