using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// scene_room 의 아이소메트릭 카메라 조작 컨트롤러 (New Input System 기반).
    /// - WASD / 방향키 / 마우스 드래그: 시점 이동 (Pan) + 범위 제한(Clamp)
    /// - 마우스 휠 / 모바일 핀치: 줌 인·아웃 (Zoom)
    /// - 우클릭 드래그 또는 Q/E (선택): 시점 미세 회전 (Skybox 3D 패럴랙스 연동)
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RoomCameraController : MonoBehaviour
    {
        [Header("시점 이동 (Pan)")]
        [Tooltip("키보드 WASD / 방향키 이동 속도")]
        [SerializeField] private float keyboardPanSpeed = 5f;

        [Tooltip("마우스/터치 드래그 이동 감도")]
        [SerializeField] private float dragPanSensitivity = 0.01f;

        [Tooltip("초기 위치 기준 카메라 화면 이동 최대 범위 (가로, 세로)")]
        [SerializeField] private Vector2 panLimit = new Vector2(3.5f, 2.5f);

        [Tooltip("이동 부드러움 (값이 클수록 즉각 반응)")]
        [SerializeField] private float panSmoothSpeed = 12f;

        [Header("줌 (Zoom)")]
        [Tooltip("마우스 휠 줌 감도")]
        [SerializeField] private float scrollZoomSpeed = 0.5f;

        [Tooltip("모바일 핀치 줌 감도")]
        [SerializeField] private float pinchZoomSpeed = 0.01f;

        [Tooltip("Orthographic Size 최소(확대) / 최대(축소) 값")]
        [SerializeField] private float minOrthoSize = 2.0f;
        [SerializeField] private float maxOrthoSize = 6.0f;

        [Tooltip("줌 부드러움")]
        [SerializeField] private float zoomSmoothSpeed = 10f;

        [Header("시점 회전 (Orbit - 선택)")]
        [Tooltip("우클릭 드래그 또는 Q/E로 살짝 시점을 돌릴지 여부 (스카이박스 입체감 확인용)")]
        [SerializeField] private bool enableOrbit = true;

        [Tooltip("방 중심 피벗 좌표 (회전 기준점)")]
        [SerializeField] private Vector3 roomPivot = new Vector3(0f, 1f, 0f);

        [Tooltip("우클릭 회전 감도")]
        [SerializeField] private float orbitSensitivity = 0.2f;

        [Tooltip("기본 각도(-45도)에서 좌우로 돌릴 수 있는 최대 각도")]
        [SerializeField] private float maxYawOffset = 25f;

        private Camera _cam;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private Vector2 _targetPanOffset;
        private Vector2 _currentPanOffset;
        private float _targetOrthoSize;
        private float _targetYawOffset;
        private float _currentYawOffset;
        private bool _isDraggingPan;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            _targetOrthoSize = _cam.orthographic ? _cam.orthographicSize : 3.77f;
        }

        private void Update()
        {
            HandleKeyboardPan();
            HandlePointerDragAndOrbit();
            HandleZoom();
            ApplyTransform();
        }

        private void HandleKeyboardPan()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // UI 입력창(TMP_InputField 등) 타이핑 중이거나 포커스가 있을 때는 무시
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                return;

            Vector2 move = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;

            if (move.sqrMagnitude > 0.001f)
            {
                _targetPanOffset += move.normalized * (keyboardPanSpeed * Time.deltaTime);
                ClampPan();
            }

            if (enableOrbit)
            {
                float yawInput = 0f;
                if (kb.qKey.isPressed) yawInput -= 1f;
                if (kb.eKey.isPressed) yawInput += 1f;
                if (Mathf.Abs(yawInput) > 0.001f)
                {
                    _targetYawOffset = Mathf.Clamp(
                        _targetYawOffset + yawInput * 35f * Time.deltaTime,
                        -maxYawOffset,
                        maxYawOffset);
                }
            }
        }

        private void HandlePointerDragAndOrbit()
        {
            // 모바일 2손가락 터치 중이면 드래그 팬 대신 핀치 줌에서 처리
            if (Touchscreen.current != null && GetActiveTouchCount() >= 2)
            {
                _isDraggingPan = false;
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                // 좌클릭 또는 휠클릭 드래그 -> 시점 이동(Pan)
                if (mouse.leftButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                {
                    if (!IsPointerOverUI())
                        _isDraggingPan = true;
                }

                if (mouse.leftButton.wasReleasedThisFrame || mouse.middleButton.wasReleasedThisFrame)
                {
                    _isDraggingPan = false;
                }

                if (_isDraggingPan && (mouse.leftButton.isPressed || mouse.middleButton.isPressed))
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    float zoomScale = _cam.orthographic ? (_cam.orthographicSize / 3.77f) : 1f;
                    _targetPanOffset -= delta * (dragPanSensitivity * zoomScale);
                    ClampPan();
                }

                // 우클릭 드래그 -> 시점 미세 회전(Orbit)
                if (enableOrbit && mouse.rightButton.isPressed && !IsPointerOverUI())
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    _targetYawOffset = Mathf.Clamp(
                        _targetYawOffset + delta.x * orbitSensitivity,
                        -maxYawOffset,
                        maxYawOffset);
                }
            }

            // 모바일 1손가락 드래그
            if (Touchscreen.current != null && GetActiveTouchCount() == 1)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame && !IsPointerOverUI())
                    _isDraggingPan = true;
                else if (touch.press.wasReleasedThisFrame)
                    _isDraggingPan = false;

                if (_isDraggingPan && touch.press.isPressed)
                {
                    Vector2 delta = touch.delta.ReadValue();
                    float zoomScale = _cam.orthographic ? (_cam.orthographicSize / 3.77f) : 1f;
                    _targetPanOffset -= delta * (dragPanSensitivity * zoomScale);
                    ClampPan();
                }
            }
        }

        private void HandleZoom()
        {
            // 마우스 휠
            var mouse = Mouse.current;
            if (mouse != null && !IsPointerOverUI())
            {
                float scrollY = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    // New Input System에서 마우스 휠 1칸은 보통 120 또는 1 단위로 들어옴
                    float normalizedScroll = Mathf.Sign(scrollY);
                    _targetOrthoSize = Mathf.Clamp(
                        _targetOrthoSize - normalizedScroll * scrollZoomSpeed,
                        minOrthoSize,
                        maxOrthoSize);
                }
            }

            // 모바일 두 손가락 핀치 줌
            if (Touchscreen.current != null && GetActiveTouchCount() >= 2)
            {
                var t0 = Touchscreen.current.touches[0];
                var t1 = Touchscreen.current.touches[1];

                Vector2 p0 = t0.position.ReadValue();
                Vector2 p1 = t1.position.ReadValue();
                Vector2 prevP0 = p0 - t0.delta.ReadValue();
                Vector2 prevP1 = p1 - t1.delta.ReadValue();

                float prevDist = Vector2.Distance(prevP0, prevP1);
                float currDist = Vector2.Distance(p0, p1);
                float diff = currDist - prevDist;

                _targetOrthoSize = Mathf.Clamp(
                    _targetOrthoSize - diff * pinchZoomSpeed,
                    minOrthoSize,
                    maxOrthoSize);
            }
        }

        private void ApplyTransform()
        {
            _currentPanOffset = Vector2.Lerp(_currentPanOffset, _targetPanOffset, Time.deltaTime * panSmoothSpeed);
            _currentYawOffset = Mathf.Lerp(_currentYawOffset, _targetYawOffset, Time.deltaTime * panSmoothSpeed);

            // 1. 기본 위치에서 Yaw 회전 적용
            Quaternion yawRot = Quaternion.Euler(0f, _currentYawOffset, 0f);
            Quaternion finalRot = yawRot * _initialRotation;
            Vector3 orbitedBasePos = roomPivot + yawRot * (_initialPosition - roomPivot);

            // 2. 카메라의 로컬 Right / Up 축을 따라 화면 평면 이동(Pan) 적용
            Vector3 right = finalRot * Vector3.right;
            Vector3 up = finalRot * Vector3.up;

            transform.rotation = finalRot;
            transform.position = orbitedBasePos + right * _currentPanOffset.x + up * _currentPanOffset.y;

            // 3. 줌 적용
            if (_cam.orthographic)
            {
                _cam.orthographicSize = Mathf.Lerp(
                    _cam.orthographicSize,
                    _targetOrthoSize,
                    Time.deltaTime * zoomSmoothSpeed);
            }
        }

        private void ClampPan()
        {
            _targetPanOffset.x = Mathf.Clamp(_targetPanOffset.x, -panLimit.x, panLimit.x);
            _targetPanOffset.y = Mathf.Clamp(_targetPanOffset.y, -panLimit.y, panLimit.y);
        }

        /// <summary>카메라 위치를 초기 상태로 리셋합니다.</summary>
        public void ResetCamera()
        {
            _targetPanOffset = Vector2.zero;
            _targetYawOffset = 0f;
            _targetOrthoSize = 3.77f;
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private static int GetActiveTouchCount()
        {
            if (Touchscreen.current == null) return 0;
            int count = 0;
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.press.isPressed) count++;
            }
            return count;
        }
    }
}
