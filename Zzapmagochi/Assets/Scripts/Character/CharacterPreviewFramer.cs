using UnityEngine;

namespace Zzapmagochi.Character
{
    /// <summary>
    /// 미리보기 카메라를 캐릭터에 맞춰 자동으로 맞춘다.
    ///
    /// 아이소메트릭 각도(30°, -45°)에서는 카메라 위치를 손으로 계산하기 어렵다.
    /// 스프라이트가 비스듬히 서 있어 월드 바운즈가 X·Z 양쪽으로 퍼지기 때문이다.
    /// 그래서 파츠가 만들어진 뒤 실제 바운즈를 재서 화면 중앙에 오도록 옮긴다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CharacterPreviewFramer : MonoBehaviour
    {
        [SerializeField] private CharacterParts target;

        [Tooltip("연결해 두면 캐릭터가 바뀔 때 자동으로 다시 맞춘다.")]
        [SerializeField] private CharacterCustomizer customizer;

        [Tooltip("캐릭터 주위 여백 배수. 1이면 딱 맞게, 1.2면 20% 여유")]
        [SerializeField] private float padding = 1.2f;

        [Tooltip("화면 중앙보다 살짝 아래에 두고 싶을 때. 화면 높이 비율")]
        [SerializeField] private float verticalBias = 0.05f;

        private Camera _camera;
        private bool _framed;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (customizer != null) customizer.PresetChanged += OnPresetChanged;
        }

        private void OnDisable()
        {
            if (customizer != null) customizer.PresetChanged -= OnPresetChanged;
        }

        private void OnPresetChanged(CharacterPresetDefinition preset)
        {
            // 캐릭터가 바뀌면 덩치도 달라질 수 있으므로 다음 프레임에 다시 맞춘다.
            Invalidate();
        }

        private void LateUpdate()
        {
            // 파츠는 CharacterParts.Start 에서 만들어지므로 첫 프레임에는 없을 수 있다.
            if (!_framed) Reframe();
        }

        /// <summary>캐릭터 전체가 화면에 들어오도록 카메라를 다시 맞춘다.</summary>
        public void Reframe()
        {
            if (target == null || _camera == null) return;
            if (!TryGetBounds(out var bounds)) return;

            var right = transform.right;
            var up = transform.up;

            // 바운즈 중심이 화면 중앙에 오도록 카메라를 시야에 수직인 평면에서만 옮긴다.
            // 시선 방향으로는 움직여도 직교 카메라의 화면이 바뀌지 않는다.
            var delta = bounds.center - transform.position;
            transform.position += right * Vector3.Dot(delta, right) + up * Vector3.Dot(delta, up);

            // AABB 를 화면 축(right/up)에 투영한 반지름
            var e = bounds.extents;
            var halfUp = Mathf.Abs(e.x * up.x) + Mathf.Abs(e.y * up.y) + Mathf.Abs(e.z * up.z);
            var halfRight = Mathf.Abs(e.x * right.x) + Mathf.Abs(e.y * right.y) + Mathf.Abs(e.z * right.z);

            var aspect = _camera.aspect > 0.01f ? _camera.aspect : 1f;
            _camera.orthographicSize = Mathf.Max(halfUp, halfRight / aspect) * padding;

            // 머리 위 여백을 조금 더 주기 위해 아래로 살짝 민다.
            transform.position -= up * (_camera.orthographicSize * verticalBias);

            _framed = true;
        }

        /// <summary>다음 프레임에 다시 맞춘다. 파츠 구성이 크게 바뀌었을 때 호출한다.</summary>
        public void Invalidate()
        {
            _framed = false;
        }

        private bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            var found = false;

            foreach (var renderer in target.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null) continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }
    }
}
