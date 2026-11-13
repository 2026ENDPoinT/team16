using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zzapmagochi.UI;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// 자취방 출입문. 클릭하면 방문 팝업을 띄운다.
    ///
    /// 가구 배치·제거 중에는 반응하지 않는다.
    /// 그 상태의 클릭은 설치나 철거를 뜻하므로, 문까지 같이 열리면 안 된다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RoomDoor : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("클릭 판정에 쓸 카메라. 비우면 Camera.main 을 쓴다.")]
        [SerializeField] private Camera viewCamera;

        [SerializeField] private RoomExitPopup exitPopup;

        [Tooltip("배치 모드 중에는 문 클릭을 무시하기 위해 연결한다.")]
        [SerializeField] private FurniturePlacementController placement;

        [Header("강조")]
        [Tooltip("마우스를 올렸을 때 살짝 밝게 표시한다.")]
        [SerializeField] private bool highlightOnHover = true;

        [SerializeField] private Color hoverColor = new Color(1f, 0.95f, 0.75f, 1f);

        private Collider _collider;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private bool _hovered;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();

            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void Update()
        {
            if (!CanInteract())
            {
                SetHovered(false);
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            if (IsPointerOverUI())
            {
                SetHovered(false);
                return;
            }

            var pointingAtDoor = IsPointerOnDoor();
            SetHovered(pointingAtDoor);

            if (pointingAtDoor && mouse.leftButton.wasPressedThisFrame)
                Open();
        }

        /// <summary>문 팝업을 연다. 버튼 등에서 직접 부를 수도 있다.</summary>
        public void Open()
        {
            if (exitPopup != null) exitPopup.Show();
            else Debug.LogWarning("[RoomDoor] 출입문 팝업이 연결되어 있지 않습니다.");
        }

        /// <summary>배치·제거 중이거나 팝업이 이미 열려 있으면 반응하지 않는다.</summary>
        private bool CanInteract()
        {
            if (exitPopup != null && exitPopup.IsOpen) return false;

            if (placement != null &&
                placement.CurrentMode != FurniturePlacementController.Mode.Off)
                return false;

            return true;
        }

        private bool IsPointerOnDoor()
        {
            if (viewCamera == null || _collider == null) return false;

            var mouse = Mouse.current;
            if (mouse == null) return false;

            var ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());

            // 문만 맞으면 되므로 콜라이더에 직접 쏜다.
            // 방 전체를 레이캐스트하면 가구 뒤에 가려졌는지까지 따져야 해서 번거롭다.
            return _collider.Raycast(ray, out _, 1000f);
        }

        private void SetHovered(bool hovered)
        {
            if (!highlightOnHover || _hovered == hovered) return;

            _hovered = hovered;

            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;

                if (!hovered)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }

                // 머티리얼은 에셋이라 직접 바꾸지 않고 렌더러 단위로만 덮어쓴다.
                renderer.GetPropertyBlock(_block);
                _block.SetColor("_BaseColor", hoverColor);
                _block.SetColor("_Color", hoverColor);
                renderer.SetPropertyBlock(_block);
            }
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
