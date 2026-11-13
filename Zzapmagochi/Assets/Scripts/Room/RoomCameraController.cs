using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// scene_room 카메라 이동 컨트롤러.
    /// - WASD: 위(W), 좌(A), 아래(S), 우(D) 방향 이동 (카메라 로컬 좌표 기준)
    /// - 마우스 휠 위/아래: 줌 인/아웃 (Orthographic Camera의 Size 값 조절)
    /// - 화면 클릭 + 드래그: 드래그한 만큼 화면 1:1 이동 (카메라 로컬 좌표 기준)
    /// - 스페이스바 (임시 구현): 카메라를 원점(1.88, 3.6, -1.88)으로 이동
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RoomCameraController : MonoBehaviour
    {
        [Header("원점 복귀 (스페이스바)")]
        [Tooltip("스페이스바를 눌렀을 때 돌아갈 카메라 원점 좌표")]
        [SerializeField] private Vector3 originPosition = new Vector3(1.88f, 3.6f, -1.88f);

        [Header("WASD 이동 (카메라 로컬 좌표 기준)")]
        [Tooltip("WASD 키보드 이동 속도")]
        [SerializeField] private float moveSpeed = 5f;

        [Header("마우스 휠 줌 (Orthographic Size)")]
        [Tooltip("마우스 휠 한 칸당 조절할 Orthographic Size 변화량")]
        [SerializeField] private float zoomStep = 0.4f;

        [Tooltip("최소 Orthographic Size (최대 줌 인)")]
        [SerializeField] private float minOrthoSize = 1.5f;

        [Tooltip("최대 Orthographic Size (최대 줌 아웃)")]
        [SerializeField] private float maxOrthoSize = 7.0f;

        private Camera _cam;
        private bool _isDragging;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
        }

        private void Update()
        {
            HandleSpacebarReset();
            HandleWasdMove();
            HandleMouseDrag();
            HandleMouseWheelZoom();
        }

        /// <summary>
        /// (임시 구현) 스페이스바: 카메라를 원점(1.88, 3.6, -1.88)으로 이동
        /// </summary>
        private void HandleSpacebarReset()
        {
            var kb = Keyboard.current;
            if (kb == null || IsTypingInUI()) return;

            if (kb.spaceKey.wasPressedThisFrame)
            {
                transform.position = originPosition;
            }
        }

        /// <summary>
        /// WASD로 위, 좌, 아래, 우 방향 이동 (카메라 로컬 좌표 기준)
        /// </summary>
        private void HandleWasdMove()
        {
            var kb = Keyboard.current;
            if (kb == null || IsTypingInUI()) return;

            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.aKey.isPressed) input.x -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;

            if (input.sqrMagnitude > 0.001f)
            {
                Vector3 localDelta = new Vector3(input.normalized.x, input.normalized.y, 0f) * (moveSpeed * Time.deltaTime);
                transform.Translate(localDelta, Space.Self);
            }
        }

        /// <summary>
        /// 화면 클릭 + 드래그: 드래그한 만큼 화면 이동 (카메라 로컬 XY 평면 1:1 이동)
        /// </summary>
        private void HandleMouseDrag()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (!IsPointerOverUI())
                    _isDragging = true;
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                _isDragging = false;
            }

            if (_isDragging && mouse.leftButton.isPressed)
            {
                Vector2 pixelDelta = mouse.delta.ReadValue();
                if (pixelDelta.sqrMagnitude > 0f && Screen.height > 0)
                {
                    // Orthographic 카메라에서 화면 세로 전체(Screen.height)는 월드 길이 (orthographicSize * 2)에 해당함.
                    // 픽셀 이동량을 월드(카메라 로컬) 거리로 환산해 정확히 드래그한 만큼 화면이 이동하도록 처리.
                    float unitsPerPixel = (_cam.orthographicSize * 2f) / Screen.height;
                    Vector3 localDelta = new Vector3(-pixelDelta.x * unitsPerPixel, -pixelDelta.y * unitsPerPixel, 0f);
                    transform.Translate(localDelta, Space.Self);
                }
            }
        }

        /// <summary>
        /// 마우스 휠 위/아래: 줌 인/아웃 (projection: orthographic의 size 값 조절)
        /// </summary>
        private void HandleMouseWheelZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null || IsPointerOverUI()) return;

            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                // 휠 위(+): 줌 인(Size 감소), 휠 아래(-): 줌 아웃(Size 증가)
                float direction = Mathf.Sign(scrollY);
                _cam.orthographicSize = Mathf.Clamp(
                    _cam.orthographicSize - direction * zoomStep,
                    minOrthoSize,
                    maxOrthoSize);
            }
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private static bool IsTypingInUI()
        {
            return EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null;
        }
    }
}
