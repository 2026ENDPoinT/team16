using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zzapmagochi.Character;
using Zzapmagochi.Core;
using Zzapmagochi.Net;
using Zzapmagochi.UI;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// 다른 사용자의 자취방을 같은 scene_room 안에서 보여 준다.
    ///
    ///   출입문 → 무작위 방문
    ///     → GET /room/random/student-id 로 상대 학번을 받고
    ///     → GET /room/{studentId} 로 그 사람의 캐릭터와 방을 받아
    ///     → 지금 씬의 방과 캐릭터를 그 내용으로 바꿔 끼운다.
    ///
    /// 방문 중에는 가구 배치를 막는다. 남의 방을 고쳐 저장해 버리면 안 되기 때문이다.
    /// </summary>
    public class RoomVisitController : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private FurnitureInventoryUI inventoryUI;
        [SerializeField] private CharacterCustomizer customizer;
        [SerializeField] private CharacterParts sceneCharacter;
        [SerializeField] private RoomExitPopup exitPopup;

        [Header("방문 중 배너")]
        [SerializeField] private GameObject bannerRoot;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private Button returnButton;

        [Header("카탈로그")]
        [Tooltip("presetId 를 캐릭터 프리셋으로 바꾸는 데 쓴다.")]
        [SerializeField] private List<CharacterPresetDefinition> presets = new List<CharacterPresetDefinition>();

        [Tooltip("parts 의 아이템 ID 를 의상 에셋으로 바꾸는 데 쓴다.")]
        [SerializeField] private List<CharacterItemDefinition> items = new List<CharacterItemDefinition>();

        /// <summary>방문 중이면 상대 학번, 내 방이면 null.</summary>
        public string VisitingStudentId { get; private set; }

        public bool IsVisiting => !string.IsNullOrEmpty(VisitingStudentId);

        /// <summary>내 방으로 돌아갈 때 복원할 내용. 방문을 시작할 때 찍어 둔다.</summary>
        private RoomLayoutDto _myRoomSnapshot;
        private CharacterPresetDefinition _myPreset;
        private readonly Dictionary<CharacterSlot, CharacterItemDefinition> _myEquipped =
            new Dictionary<CharacterSlot, CharacterItemDefinition>();

        private bool _busy;

        private void Awake()
        {
            if (returnButton != null) returnButton.onClick.AddListener(ReturnToMyRoom);
            if (bannerRoot != null) bannerRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (exitPopup != null) exitPopup.RandomVisitRequested += OnRandomVisitRequested;
        }

        private void OnDisable()
        {
            if (exitPopup != null) exitPopup.RandomVisitRequested -= OnRandomVisitRequested;
        }

        private void OnRandomVisitRequested()
        {
            _ = VisitRandomAsync();
        }

        /// <summary>무작위 방문. 출입문 팝업의 두 번째 버튼이 부른다.</summary>
        public async Task VisitRandomAsync()
        {
            if (_busy) return;
            _busy = true;

            try
            {
                var pick = await Api.Current.GetRandomStudentId();
                if (pick == null || string.IsNullOrEmpty(pick.studentId))
                {
                    ShowBanner("방문할 수 있는 자취방이 없습니다.", showReturn: false);
                    return;
                }

                await VisitAsync(pick.studentId);
            }
            catch (ApiException e)
            {
                ShowBanner(e.Message, showReturn: IsVisiting);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RoomVisit] 실패: {e.Message}");
                ShowBanner("자취방을 불러오지 못했습니다.", showReturn: IsVisiting);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>학번을 지정해 방문한다. 친구 목록에서도 쓸 수 있다.</summary>
        public async Task VisitAsync(string studentId)
        {
            var visit = await Api.Current.GetUserRoom(studentId);
            if (visit == null || visit.room == null)
            {
                ShowBanner("자취방 정보를 받지 못했습니다.", showReturn: IsVisiting);
                return;
            }

            // 처음 나갈 때만 내 상태를 찍어 둔다. 방문 중 또 방문해도 원본은 유지된다.
            if (!IsVisiting) CaptureMyState();

            VisitingStudentId = studentId;

            if (exitPopup != null) exitPopup.Hide();

            ApplyCharacter(visit.character);
            if (inventoryUI != null)
            {
                inventoryUI.ApplyRoomLayout(visit.room);
                inventoryUI.SetEditingAllowed(false);
            }

            var who = visit.character != null && !string.IsNullOrEmpty(visit.character.nickname)
                ? visit.character.nickname
                : studentId;

            ShowBanner($"{who} 님의 자취방 ({studentId})", showReturn: true);
        }

        /// <summary>내 방으로 되돌린다.</summary>
        public void ReturnToMyRoom()
        {
            if (!IsVisiting) return;

            VisitingStudentId = null;

            if (_myPreset != null && customizer != null)
            {
                customizer.ApplyPreset(_myPreset);

                // ApplyPreset 은 의상을 프리셋 기본값으로 되돌리므로, 찍어 둔 착용 상태를 다시 입힌다.
                foreach (var slot in CharacterSlots.All)
                {
                    if (_myEquipped.TryGetValue(slot, out var item) && item != null)
                        customizer.Equip(item);
                    else
                        customizer.Unequip(slot);
                }
            }

            if (inventoryUI != null)
            {
                if (_myRoomSnapshot != null) inventoryUI.ApplyRoomLayout(_myRoomSnapshot);
                inventoryUI.SetEditingAllowed(true);
            }

            if (bannerRoot != null) bannerRoot.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────
        // 내부
        // ─────────────────────────────────────────────────────────

        private void CaptureMyState()
        {
            _myEquipped.Clear();

            if (customizer != null)
            {
                _myPreset = customizer.CurrentPreset;
                foreach (var pair in customizer.Equipped) _myEquipped[pair.Key] = pair.Value;
            }

            _myRoomSnapshot = BuildSnapshot();
        }

        /// <summary>지금 씬에 놓인 가구와 벽지·바닥을 그대로 DTO 로 담는다.</summary>
        private RoomLayoutDto BuildSnapshot()
        {
            var snapshot = new RoomLayoutDto
            {
                ownerStudentId = Session.StudentId,
                visibility = "public",
                placements = new List<RoomPlacementDto>()
            };

            var surfaces = FindFirstObjectByType<RoomSurfaceController>();
            snapshot.wallpaperId = surfaces != null && surfaces.CurrentWallpaper != null
                ? surfaces.CurrentWallpaper.id
                : "wall_0";
            snapshot.floorId = surfaces != null && surfaces.CurrentFloor != null
                ? surfaces.CurrentFloor.id
                : "floor_0";

            foreach (var placed in FindObjectsByType<PlacedFurniture>(FindObjectsSortMode.None))
            {
                if (placed == null || placed.Definition == null) continue;

                var p = placed.ToPlacement();
                if (string.IsNullOrEmpty(p.furnitureId)) continue;

                snapshot.placements.Add(new RoomPlacementDto
                {
                    furnitureId = p.furnitureId,
                    gridX = p.gridX,
                    gridY = p.gridY,
                    gridZ = p.gridZ,
                    rotation = p.rotation
                });
            }

            return snapshot;
        }

        /// <summary>서버의 캐릭터 데이터(ID 묶음)를 실제 에셋으로 바꿔 입힌다.</summary>
        private void ApplyCharacter(CharacterDataDto data)
        {
            if (data == null || customizer == null) return;

            var preset = presets.Find(x => x != null && x.id == data.presetId);
            if (preset != null) customizer.ApplyPreset(preset);
            else if (!string.IsNullOrEmpty(data.presetId))
                Debug.LogWarning($"[RoomVisit] 모르는 presetId: {data.presetId}");

            if (data.parts == null) return;

            ApplySlot(CharacterSlot.Outer, data.parts.outer);
            ApplySlot(CharacterSlot.Accessories, data.parts.accessories);
            ApplySlot(CharacterSlot.ClothTop, data.parts.clothTop);
            ApplySlot(CharacterSlot.ClothBottom, data.parts.clothBottom);
            ApplySlot(CharacterSlot.Shoe, data.parts.shoe);
        }

        private void ApplySlot(CharacterSlot slot, string itemId)
        {
            // 서버가 빈 값을 주면 "그 슬롯은 벗은 상태"라는 뜻이다.
            if (string.IsNullOrEmpty(itemId))
            {
                customizer.Unequip(slot);
                return;
            }

            var item = items.Find(x => x != null && x.id == itemId);
            if (item == null)
            {
                Debug.LogWarning($"[RoomVisit] 모르는 의상 ID: {itemId}");
                customizer.Unequip(slot);
                return;
            }

            customizer.Equip(item);
        }

        private void ShowBanner(string message, bool showReturn)
        {
            if (bannerText != null) bannerText.text = message;
            if (returnButton != null) returnButton.gameObject.SetActive(showReturn);
            if (bannerRoot != null) bannerRoot.SetActive(true);
        }
    }
}
