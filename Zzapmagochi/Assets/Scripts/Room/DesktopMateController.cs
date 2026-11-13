using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zzapmagochi.Room
{
    public enum MatePersonaType
    {
        Diligent,    // 성실한 모범생
        Cheerleader, // 하이텐션 응원단장
        Tsundere,    // 툴툴대지만 챙겨주는 동기
        NightCoder   // 새벽 코딩 메이트
    }

    /// <summary>
    /// PC WebGL 환경에서 특정 버튼을 누르면 브라우저 Document Picture-in-Picture(항상 위 미니 창)로
    /// 데스크탑 메이트(시메지 스타일 배회 + 주기적/클릭 시 말풍선 대사)를 띄우는 컨트롤러.
    /// 유니티 에디터에서 실행할 때는 창 배경이나 버튼 없이 화면 하단에서 캐릭터만 걸어 다니고,
    /// 캐릭터를 클릭하거나 주기가 되면 머리 위에 말풍선만 깔끔하게 표시됩니다.
    /// </summary>
    public class DesktopMateController : MonoBehaviour
    {
        [Header("연결할 UI 버튼")]
        [Tooltip("클릭 시 데스크탑 메이트(PiP)를 켜거나 끌 버튼")]
        [SerializeField] private Button pipTriggerButton;

        [Header("캡처할 캐릭터 (비워두면 씬에서 자동 탐색)")]
        [Tooltip("CharacterParts가 붙어있거나 캐릭터 스프라이트들이 들어있는 최상위 오브젝트 (예: miku, CharacterTemplate)")]
        [SerializeField] private Transform characterRoot;

        [Header("커스텀 PNG 직접 지정 (선택 — 비워두면 씬 캐릭터 자동 캡처)")]
        [SerializeField] private Texture2D customIdleTexture;
        [SerializeField] private Texture2D customWalkTexture;

        [Header("캐릭터 정보 & 말풍선 주기")]
        [SerializeField] private string characterNickname = "짭이";
        [SerializeField] private MatePersonaType personaType = MatePersonaType.Diligent;

        [Tooltip("몇 초마다 한 번씩 말풍선 대사를 칠지 설정 (예: 180 = 3분, 시연/테스트용은 20초 권장)")]
        [SerializeField, Range(5f, 600f)] private float speechIntervalSeconds = 20f;

        [Tooltip("페르소나 기본 대사 외에 추가로 넣을 커스텀 대사 목록")]
        [SerializeField] private List<string> customLines = new List<string>();

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int ZzapDesktopMate_IsSupported();

        [DllImport("__Internal")]
        private static extern void ZzapDesktopMate_OpenPiP(string configJson, string idleDataUrl, string walkDataUrl);
#endif

        [Serializable]
        private class PiPConfigDto
        {
            public string nickname;
            public string persona;
            public float intervalSeconds;
            public List<string> lines;
        }

        // ── 에디터 오버레이 상태 (창 배경/버튼 없이 캐릭터+말풍선만 표시) ──
        private bool _editorMateActive;
        private Texture2D _cachedIdleTex;
        private Texture2D _cachedWalkTex;
        private Texture2D _bubbleBgTex;
        private float _matePosX;
        private float _mateTargetX;
        private bool _mateIsWalking = true;
        private bool _mateFacingRight = true;
        private bool _mateWalkFrameToggle;
        private float _mateFrameTimer;
        private float _mateStateTimer = 2f;
        private float _mateSpeechTimer;
        private float _mateBubbleVisibleTimer;
        private float _mateBounceTimer;
        private string _mateCurrentLine = "";
        private int _lastLineIndex = -1;
        private List<string> _activeLines = new List<string>();

        private void Awake()
        {
            ApplyStoredSurveyProfileIfAny();

            if (pipTriggerButton != null)
            {
                // WebGL에서는 브라우저 보안 정책(Transient User Activation) 때문에
                // 마우스 포인터를 누르는 즉시 PiP 요청이 나가야 차단되지 않음.
                var trigger = pipTriggerButton.gameObject.GetComponent<EventTrigger>()
                              ?? pipTriggerButton.gameObject.AddComponent<EventTrigger>();

                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                entry.callback.AddListener(_ => OpenDesktopMatePiP());
                trigger.triggers.Add(entry);
            }
        }

        private void ApplyStoredSurveyProfileIfAny()
        {
            if (!MateProfileStore.HasCustomProfile) return;

            characterNickname = MateProfileStore.Nickname;
            personaType = MateProfileStore.Persona;
        }

        // ── 향후 설정창(UI)에서 호출할 수 있는 공개 API ────────────
        public void SetNickname(string newNickname) => characterNickname = newNickname;
        public void SetPersona(MatePersonaType newPersona)
        {
            personaType = newPersona;
            _activeLines = BuildDialogueLines();
        }
        public void SetCustomTextures(Texture2D idle, Texture2D walk = null)
        {
            customIdleTexture = idle;
            customWalkTexture = walk != null ? walk : idle;
        }
        public void AddCustomLine(string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                customLines.Add(line.Trim());
                _activeLines = BuildDialogueLines();
            }
        }

        /// <summary>
        /// 버튼 클릭 시 호출되어 현재 캐릭터의 Idle/Walk 모습을 준비하고 데스크탑 메이트를 토글합니다.
        /// </summary>
        public void OpenDesktopMatePiP()
        {
            ApplyStoredSurveyProfileIfAny();
            EnsureCharacterRoot();

            _cachedIdleTex?.DestroyIfTemp();
            _cachedWalkTex?.DestroyIfTemp();

            if (customIdleTexture != null)
            {
                _cachedIdleTex = customIdleTexture;
                _cachedWalkTex = customWalkTexture != null ? customWalkTexture : customIdleTexture;
            }
            else
            {
                CaptureCharacterFrames(out _cachedIdleTex, out _cachedWalkTex);
            }

            _activeLines = BuildDialogueLines();
            string personaLabel = GetPersonaDisplayName(personaType);

#if UNITY_WEBGL && !UNITY_EDITOR
            string idleDataUrl = TextureToDataUrl(_cachedIdleTex);
            string walkDataUrl = TextureToDataUrl(_cachedWalkTex);

            var dto = new PiPConfigDto
            {
                nickname = characterNickname,
                persona = personaLabel,
                intervalSeconds = speechIntervalSeconds,
                lines = _activeLines
            };

            string json = JsonUtility.ToJson(dto);
            ZzapDesktopMate_OpenPiP(json, idleDataUrl, walkDataUrl);
#else
            // 유니티 에디터에서는 회색 창/버튼 없이 캐릭터만 화면 하단을 걸어 다니도록 토글
            _editorMateActive = !_editorMateActive;
            if (_editorMateActive)
            {
                EnsureBubbleBgTexture();
                _matePosX = Screen.width * 0.72f;
                _mateTargetX = Mathf.Clamp(_matePosX - 140f, 60f, Screen.width - 180f);
                _mateSpeechTimer = speechIntervalSeconds;
                TriggerRandomLine();
            }
#endif
        }

        private void EnsureCharacterRoot()
        {
            if (characterRoot != null) return;

            var parts = FindFirstObjectByType<CharacterParts>();
            if (parts != null)
            {
                characterRoot = parts.transform;
                return;
            }

            var miku = GameObject.Find("miku") ?? GameObject.Find("CharacterTemplate");
            if (miku != null)
                characterRoot = miku.transform;
        }

        /// <summary>
        /// 현재 씬에 있는 캐릭터의 Idle 프레임과 Walk 프레임을 투명 배경 PNG용 Texture2D로 캡처합니다.
        /// </summary>
        private void CaptureCharacterFrames(out Texture2D idleTex, out Texture2D walkTex)
        {
            const int width = 256;
            const int height = 340;
            const int tempLayer = 31;

            if (characterRoot == null)
            {
                idleTex = CreateFallbackTexture(width, height, false);
                walkTex = CreateFallbackTexture(width, height, true);
                return;
            }

            var renderers = characterRoot.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0)
            {
                idleTex = CreateFallbackTexture(width, height, false);
                walkTex = CreateFallbackTexture(width, height, true);
                return;
            }

            var originalLayers = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalLayers[i] = renderers[i].gameObject.layer;
                renderers[i].gameObject.layer = tempLayer;
            }

            var charParts = characterRoot.GetComponent<CharacterParts>();

            var camGo = new GameObject("__TempMateCaptureCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << tempLayer;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;

            try
            {
                Bounds bounds = new Bounds(characterRoot.position, Vector3.zero);
                bool hasBounds = false;
                foreach (var r in renderers)
                {
                    if (!r.enabled || r.sprite == null) continue;
                    if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                    else bounds.Encapsulate(r.bounds);
                }

                Quaternion charRot = characterRoot.rotation;
                Vector3 forward = charRot * Vector3.forward;
                camGo.transform.rotation = charRot;
                camGo.transform.position = bounds.center - forward * 5f;
                cam.orthographicSize = Mathf.Max(0.8f, bounds.extents.y * 1.15f);

                // 1) Idle 프레임 촬영
                if (charParts != null) charParts.SetWalking(false);
                idleTex = RenderCameraToTexture(cam, width, height);

                // 2) Walk 프레임 촬영
                if (charParts != null)
                {
                    charParts.SetWalking(true);
                    walkTex = RenderCameraToTexture(cam, width, height);
                }
                else
                {
                    characterRoot.localPosition += new Vector3(0f, 0.06f, 0f);
                    walkTex = RenderCameraToTexture(cam, width, height);
                    characterRoot.localPosition -= new Vector3(0f, 0.06f, 0f);
                }
            }
            finally
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        renderers[i].gameObject.layer = originalLayers[i];
                }
                Destroy(camGo);
            }
        }

        private static Texture2D RenderCameraToTexture(Camera cam, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var prevActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = null;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            return tex;
        }

        private static string TextureToDataUrl(Texture2D tex)
        {
            if (tex == null) return string.Empty;
            byte[] png = tex.EncodeToPNG();
            return "data:image/png;base64," + Convert.ToBase64String(png);
        }

        private static Texture2D CreateFallbackTexture(int w, int h, bool step)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var body = new Color32(110, 200, 220, 255);
            var pixels = new Color32[w * h];
            Array.Fill(pixels, clear);

            int yOffset = step ? 12 : 0;
            for (int y = 40 + yOffset; y < 220 + yOffset; y++)
            {
                for (int x = 88; x < 168; x++)
                    pixels[y * w + x] = body;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private List<string> BuildDialogueLines()
        {
            var list = new List<string>();

            switch (personaType)
            {
                case MatePersonaType.Diligent:
                    list.Add("오늘 eclass 할 일 목록 확인했어? 마감 전에 하나씩 해치우자!");
                    list.Add("지금 집중력 진짜 좋은데? 이 기세로 오늘 목표까지 쭉 가보자!");
                    list.Add("30분마다 스트레칭 한 번씩! 거북목 되면 안 된다구.");
                    list.Add("오늘도 깃허브 잔디 심는 거 잊지 않았지? 1커밋이라도 챙기자!");
                    list.Add("할 일 완료하고 받은 재화로 내 방에 새 가구 놔줄 거지? 기대할게!");
                    break;

                case MatePersonaType.Cheerleader:
                    list.Add("파이팅 파이팅!! 오늘 과제 다 부숴버리자구! 할 수 있다!!");
                    list.Add("우와, 아직도 책상 앞을 지키고 있다니 완전 멋져!! 최고야!");
                    list.Add("힘들 땐 시원한 물 한 잔 마시고 심호흡 크게! 내가 옆에서 응원할게!");
                    list.Add("과제 하나 끝낼 때마다 내가 박수 백 번 쳐줄게! 짝짝짝!!");
                    break;

                case MatePersonaType.Tsundere:
                    list.Add("흥, 딱히 네가 걱정돼서 나와 있는 건 아니거든? 그냥 방이 심심해서야.");
                    list.Add("야, 과제 마감 시간 다 돼가는데 딴짓하는 거 아니지? 지켜보고 있다.");
                    list.Add("...뭐야, 생각보다 열심히 하네. 끝나면 조금은 칭찬해 줄지도.");
                    list.Add("눈 피곤해 보이는데 잠깐 쉬었다 해. 쓰러지면 내 방은 누가 꾸며주냐고.");
                    break;

                case MatePersonaType.NightCoder:
                    list.Add("새벽 공기 + 코딩 조합... 오늘 버그 한 번에 잡힐 예감이 든다.");
                    list.Add("컴파일 에러 나도 당황하지 마. 세미콜론이랑 오타부터 천천히 보자.");
                    list.Add("오늘 깃허브 스트릭 끊기면 안 돼! 자기 전에 푸시 꼭 확인해.");
                    list.Add("카페인도 좋지만 물도 챙겨 마셔. 우리 끝까지 살아남자...");
                    break;
            }

            if (customLines != null)
            {
                foreach (var line in customLines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        list.Add(line.Trim());
                }
            }

            if (MateProfileStore.HasCustomProfile && !string.IsNullOrWhiteSpace(MateProfileStore.Catchphrase))
            {
                for (int i = 0; i < list.Count; i++)
                    list[i] = MateProfileStore.ApplyCatchphrase(list[i]);
            }

            return list;
        }

        private static string GetPersonaDisplayName(MatePersonaType type) => type switch
        {
            MatePersonaType.Diligent    => "성실형 메이트",
            MatePersonaType.Cheerleader => "응원단장 메이트",
            MatePersonaType.Tsundere    => "츤데레 메이트",
            MatePersonaType.NightCoder  => "새벽코딩 메이트",
            _                           => "메이트"
        };

        // ─────────────────────────────────────────────────────────
        // 에디터 시메지 오버레이 (반투명 회색 창/버튼 없이 캐릭터만 배회 + 클릭 시 말풍선)
        // ─────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_editorMateActive) return;

            float dt = Time.deltaTime;
            _mateSpeechTimer -= dt;
            if (_mateSpeechTimer <= 0f)
            {
                _mateSpeechTimer = speechIntervalSeconds;
                TriggerRandomLine();
            }

            if (_mateBubbleVisibleTimer > 0f)
                _mateBubbleVisibleTimer -= dt;

            if (_mateBounceTimer > 0f)
                _mateBounceTimer = Mathf.Max(0f, _mateBounceTimer - dt);

            _mateStateTimer -= dt;
            if (_mateIsWalking)
            {
                float dir = _mateTargetX > _matePosX ? 1f : -1f;
                _mateFacingRight = dir > 0f;
                _matePosX += dir * 65f * dt;

                _mateFrameTimer += dt;
                if (_mateFrameTimer >= 0.35f)
                {
                    _mateFrameTimer = 0f;
                    _mateWalkFrameToggle = !_mateWalkFrameToggle;
                }

                if (Mathf.Abs(_mateTargetX - _matePosX) < 4f || _mateStateTimer <= 0f)
                {
                    _mateIsWalking = false;
                    _mateStateTimer = UnityEngine.Random.Range(1.5f, 3.5f);
                    _mateWalkFrameToggle = false;
                }
            }
            else if (_mateStateTimer <= 0f)
            {
                _mateIsWalking = true;
                _mateStateTimer = UnityEngine.Random.Range(2.0f, 4.5f);
                float minX = 40f;
                float maxX = Mathf.Max(100f, Screen.width - 180f);
                _mateTargetX = UnityEngine.Random.Range(minX, maxX);
                _mateFrameTimer = 0f;
            }
        }

        private void TriggerRandomLine()
        {
            if (_activeLines == null || _activeLines.Count == 0) return;

            int idx = UnityEngine.Random.Range(0, _activeLines.Count);
            if (_activeLines.Count > 1 && idx == _lastLineIndex)
                idx = (idx + 1) % _activeLines.Count;

            _lastLineIndex = idx;
            _mateCurrentLine = _activeLines[idx];
            _mateBubbleVisibleTimer = 5.0f;
            _mateBounceTimer = 0.25f;
        }

        private void EnsureBubbleBgTexture()
        {
            if (_bubbleBgTex != null) return;
            _bubbleBgTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _bubbleBgTex.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.96f));
            _bubbleBgTex.Apply();
        }

        private void OnGUI()
        {
            if (!_editorMateActive) return;

            Texture2D curTex = (_mateIsWalking && _mateWalkFrameToggle && _cachedWalkTex != null)
                ? _cachedWalkTex
                : _cachedIdleTex;

            if (curTex == null) return;

            const float charW = 135f;
            const float charH = 175f;
            float bounceOffset = _mateBounceTimer > 0f
                ? Mathf.Sin((_mateBounceTimer / 0.25f) * Mathf.PI) * 14f
                : 0f;

            float charX = Mathf.Clamp(_matePosX, 10f, Mathf.Max(20f, Screen.width - charW - 10f));
            float charY = Screen.height - charH - 12f - bounceOffset;
            Rect charRect = new Rect(charX, charY, charW, charH);

            // 1. 캐릭터만 투명하게 그리기 (회색 창/테두리/버튼 일체 없음)
            Rect uvRect = _mateFacingRight ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
            GUI.DrawTextureWithTexCoords(charRect, curTex, uvRect, true);

            // 2. 캐릭터 클릭 감지 -> 클릭하면 즉시 말풍선 대사 출력
            if (GUI.Button(charRect, GUIContent.none, GUIStyle.none))
            {
                _mateSpeechTimer = speechIntervalSeconds;
                TriggerRandomLine();
            }

            // 3. 말풍선 표시 (캐릭터 머리 바로 위)
            if (_mateBubbleVisibleTimer > 0f && !string.IsNullOrEmpty(_mateCurrentLine))
            {
                EnsureBubbleBgTexture();

                var bubbleStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(14, 14, 10, 10)
                };
                bubbleStyle.normal.background = _bubbleBgTex;
                bubbleStyle.normal.textColor = new Color32(0x1E, 0x22, 0x2B, 0xFF);

                const float bubbleW = 250f;
                float contentH = bubbleStyle.CalcHeight(new GUIContent(_mateCurrentLine), bubbleW);
                float bubbleH = Mathf.Max(48f, contentH + 8f);
                float bubbleX = Mathf.Clamp(
                    charX + (charW - bubbleW) * 0.5f,
                    12f,
                    Mathf.Max(12f, Screen.width - bubbleW - 12f));
                float bubbleY = Mathf.Max(12f, charY - bubbleH - 8f);

                GUI.Label(new Rect(bubbleX, bubbleY, bubbleW, bubbleH), _mateCurrentLine, bubbleStyle);
            }
        }

        private void OnDestroy()
        {
            if (_cachedIdleTex != customIdleTexture)
                _cachedIdleTex?.DestroyIfTemp();
            if (_cachedWalkTex != customWalkTexture && _cachedWalkTex != customIdleTexture)
                _cachedWalkTex?.DestroyIfTemp();
            _bubbleBgTex?.DestroyIfTemp();
        }
    }

    internal static class TextureTempExtensions
    {
        public static void DestroyIfTemp(this Texture2D tex)
        {
            if (tex != null) UnityEngine.Object.Destroy(tex);
        }
    }
}
