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
    /// 1) scene_room 안에서 방에 서 있는 캐릭터 머리 위에 종종 말풍선을 띄우고,
    /// 2) PC WebGL / 에디터 데스크탑 메이트(PiP) 창에서도 말풍선을 띄우며,
    /// 3) eClass 할 일(Todo) 리프레시 시 할 일 개수가 줄어들면 "와! 해냈어!" 등 축하 프리셋 대사를
    ///    룸 캐릭터 머리 위와 PiP 창 양쪽에 동시에 띄워주는 컨트롤러.
    /// </summary>
    public class DesktopMateController : MonoBehaviour
    {
        public static DesktopMateController Instance { get; private set; }

        [Header("연결할 UI 버튼")]
        [Tooltip("클릭 시 데스크탑 메이트(PiP)를 켜거나 끌 버튼")]
        [SerializeField] private Button pipTriggerButton;

        [Header("캡처할 캐릭터 (비워두면 씬에서 자동 탐색)")]
        [Tooltip("CharacterParts가 붙어있거나 캐릭터 스프라이트들이 들어있는 최상위 오브젝트")]
        [SerializeField] private Transform characterRoot;

        [Header("커스텀 PNG 직접 지정 (선택 — 비워두면 씬 캐릭터 자동 캡처)")]
        [SerializeField] private Texture2D customIdleTexture;
        [SerializeField] private Texture2D customWalkTexture;

        [Header("캐릭터 정보 & 말풍선 주기")]
        [SerializeField] private string characterNickname = "짭이";
        [SerializeField] private MatePersonaType personaType = MatePersonaType.Diligent;

        [Tooltip("룸 씬 & PiP에서 몇 초마다 한 번씩 말풍선 대사를 칠지 설정")]
        [SerializeField, Range(5f, 600f)] private float speechIntervalSeconds = 16f;

        [Tooltip("페르소나 기본 대사 외에 추가로 넣을 커스텀 대사 목록")]
        [SerializeField] private List<string> customLines = new List<string>();

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int ZzapDesktopMate_IsSupported();

        [DllImport("__Internal")]
        private static extern void ZzapDesktopMate_OpenPiP(string configJson, string idleDataUrl, string walkDataUrl);

        [DllImport("__Internal")]
        private static extern void ZzapDesktopMate_ShowLine(string lineText);
#endif

        [Serializable]
        private class PiPConfigDto
        {
            public string nickname;
            public string persona;
            public float intervalSeconds;
            public List<string> lines;
        }

        // 할 일(Todo) 개수가 줄어들었을 때 띄우는 축하 프리셋 대사 목록
        private static readonly string[] CelebrationPresetLines =
        {
            "와! 해냈어!! 할 일이 줄어들었잖아, 진짜 최고야! 🎉",
            "와! 해냈어! 과제 하나 끝냈구나, 너무 고생 많았어!! ✨",
            "오~ 할 일 목록이 줄었네! 와, 해냈어! 이 기세로 쭉 가자!",
            "와! 해냈어!! 역시 해낼 줄 알았다구, 잠깐 기지개라도 펴!",
            "할 일 클리어 확인! 와! 해냈어! 오늘 집중력 완전 대박인데? 🔥"
        };

        // ── 룸 씬(scene_room) 캐릭터 머리 위 말풍선 상태 ──
        private float _roomSpeechTimer = 6f;
        private float _roomBubbleVisibleTimer;
        private string _roomCurrentLine = "";

        // ── 에디터 PiP 오버레이 상태 ──
        private bool _editorMateActive;
        private Texture2D _cachedIdleTex;
        private Texture2D _cachedWalkTex;
        private Texture2D _bubbleBgTex;
        private Texture2D _celebrateBgTex;
        private bool _isCelebrationBubble;
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
            Instance = this;
            ApplyStoredSurveyProfileIfAny();
            _activeLines = BuildDialogueLines();

            // 방에 들어오면 5~8초 뒤에 첫 말풍선이 자연스럽게 뜨도록 설정
            _roomSpeechTimer = UnityEngine.Random.Range(5f, 8f);

            if (pipTriggerButton != null)
            {
                var trigger = pipTriggerButton.gameObject.GetComponent<EventTrigger>()
                              ?? pipTriggerButton.gameObject.AddComponent<EventTrigger>();

                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                entry.callback.AddListener(_ => OpenDesktopMatePiP());
                trigger.triggers.Add(entry);
            }
        }

        private void Start()
        {
            EnsureCharacterRoot();
            EnsureBubbleBgTexture();
        }

        private void ApplyStoredSurveyProfileIfAny()
        {
            if (!MateProfileStore.HasCustomProfile) return;

            characterNickname = MateProfileStore.Nickname;
            personaType = MateProfileStore.Persona;
        }

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
        /// 할 일(Todo) 리프레시 결과 할 일 숫자가 줄어들었을 때 호출합니다.
        /// 룸 캐릭터 머리 위와 PiP 창 양쪽에 "와! 해냈어!" 프리셋 대사를 즉시 띄웁니다.
        /// </summary>
        public void TriggerTodoCompletedCelebration(int decreasedCount = 1)
        {
            string baseLine = CelebrationPresetLines[
                UnityEngine.Random.Range(0, CelebrationPresetLines.Length)];

            if (decreasedCount > 1)
                baseLine = $"와! 해냈어!! 할 일이 무려 {decreasedCount}개나 줄었어! 대단해! 🎉";

            if (MateProfileStore.HasCustomProfile && !string.IsNullOrWhiteSpace(MateProfileStore.Catchphrase))
                baseLine = MateProfileStore.ApplyCatchphrase(baseLine);

            ShowImmediateLineInRoomAndPiP(baseLine, isCelebration: true, duration: 6.5f);
        }

        /// <summary>
        /// 특정 대사를 룸 캐릭터 머리 위 말풍선과 PiP 창에 동시에 즉시 띄웁니다.
        /// </summary>
        public void ShowImmediateLineInRoomAndPiP(string line, bool isCelebration = false, float duration = 5.5f)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            _isCelebrationBubble = isCelebration;

            // 1) 룸 씬 캐릭터 머리 위 말풍선 갱신
            _roomCurrentLine = line;
            _roomBubbleVisibleTimer = duration;
            _roomSpeechTimer = Mathf.Max(12f, speechIntervalSeconds);

            // 2) 에디터 PiP 오버레이 말풍선 갱신
            _mateCurrentLine = line;
            _mateBubbleVisibleTimer = duration;
            _mateBounceTimer = 0.3f;
            _mateSpeechTimer = Mathf.Max(12f, speechIntervalSeconds);

#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                ZzapDesktopMate_ShowLine(line);
            }
            catch
            {
            }
#endif
        }

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

                if (charParts != null) charParts.SetWalking(false);
                idleTex = RenderCameraToTexture(cam, width, height);

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
                    list.Add("오늘 eClass 할 일 보드 확인했어? 마감 전에 하나씩 해치우자!");
                    list.Add("지금 집중력 진짜 좋은데? 이 기세로 오늘 목표까지 쭉 가보자!");
                    list.Add("30분마다 스트레칭 한 번씩! 거북목 되면 안 된다구.");
                    list.Add("오늘도 깃허브 잔디 심는 거 잊지 않았지? 1커밋이라도 챙기자!");
                    list.Add("과제 끝나고 할 일 보드 새로고침할 때가 제일 뿌듯하더라!");
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
                    list.Add("눈 피곤해 보이는데 잠깐 쉬었다 해. 쓰러지면 누가 내 방 꾸며주냐고.");
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

        private void Update()
        {
            float dt = Time.deltaTime;

            // 1. 룸 씬(scene_room) 캐릭터 머리 위 말풍선 타이머 (항상 동작하여 종종 말풍선 표시)
            if (_roomBubbleVisibleTimer > 0f)
            {
                _roomBubbleVisibleTimer -= dt;
                if (_roomBubbleVisibleTimer <= 0f)
                    _isCelebrationBubble = false;
            }

            _roomSpeechTimer -= dt;
            if (_roomSpeechTimer <= 0f)
            {
                _roomSpeechTimer = UnityEngine.Random.Range(
                    Mathf.Max(8f, speechIntervalSeconds * 0.75f),
                    Mathf.Max(14f, speechIntervalSeconds * 1.25f));
                TriggerRandomRoomLine();
            }

            // 2. 에디터 데스크탑 메이트 오버레이 타이머
            if (!_editorMateActive) return;

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

        private string PickNextLine()
        {
            if (_activeLines == null || _activeLines.Count == 0)
                _activeLines = BuildDialogueLines();
            if (_activeLines.Count == 0) return string.Empty;

            int idx = UnityEngine.Random.Range(0, _activeLines.Count);
            if (_activeLines.Count > 1 && idx == _lastLineIndex)
                idx = (idx + 1) % _activeLines.Count;

            _lastLineIndex = idx;
            return _activeLines[idx];
        }

        private void TriggerRandomRoomLine()
        {
            string line = PickNextLine();
            if (string.IsNullOrEmpty(line)) return;

            _isCelebrationBubble = false;
            _roomCurrentLine = line;
            _roomBubbleVisibleTimer = 5.0f;
        }

        private void TriggerRandomLine()
        {
            string line = PickNextLine();
            if (string.IsNullOrEmpty(line)) return;

            _isCelebrationBubble = false;
            _mateCurrentLine = line;
            _mateBubbleVisibleTimer = 5.0f;
            _mateBounceTimer = 0.25f;
        }

        private void EnsureBubbleBgTexture()
        {
            if (_bubbleBgTex == null)
            {
                _bubbleBgTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _bubbleBgTex.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.96f));
                _bubbleBgTex.Apply();
            }

            if (_celebrateBgTex == null)
            {
                _celebrateBgTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _celebrateBgTex.SetPixel(0, 0, new Color32(0xFF, 0xF6, 0xD6, 0xFA));
                _celebrateBgTex.Apply();
            }
        }

        private void OnGUI()
        {
            // 1. 룸 씬(scene_room) 캐릭터 머리 위 말풍선 그리기
            DrawRoomCharacterSpeechBubble();

            // 2. 에디터 PiP 오버레이 그리기
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

            Rect uvRect = _mateFacingRight ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
            GUI.DrawTextureWithTexCoords(charRect, curTex, uvRect, true);

            if (GUI.Button(charRect, GUIContent.none, GUIStyle.none))
            {
                _mateSpeechTimer = speechIntervalSeconds;
                TriggerRandomLine();
            }

            if (_mateBubbleVisibleTimer > 0f && !string.IsNullOrEmpty(_mateCurrentLine))
            {
                DrawSpeechBubbleBox(
                    charX + charW * 0.5f,
                    charY - 8f,
                    _mateCurrentLine,
                    _isCelebrationBubble);
            }
        }

        private void DrawRoomCharacterSpeechBubble()
        {
            if (_roomBubbleVisibleTimer <= 0f || string.IsNullOrEmpty(_roomCurrentLine))
                return;

            EnsureCharacterRoot();
            var cam = Camera.main;
            if (characterRoot == null || cam == null) return;

            // 캐릭터 머리 위 월드 좌표를 화면 좌표로 변환
            Vector3 headWorldPos = characterRoot.position + new Vector3(0f, 1.55f, 0f);
            Vector3 screenPos = cam.WorldToScreenPoint(headWorldPos);
            if (screenPos.z <= 0f) return;

            float guiX = screenPos.x;
            float guiY = Screen.height - screenPos.y;

            DrawSpeechBubbleBox(guiX, guiY, _roomCurrentLine, _isCelebrationBubble);
        }

        private void DrawSpeechBubbleBox(float centerX, float bottomY, string text, bool isCelebration)
        {
            EnsureBubbleBgTexture();

            var bubbleStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = isCelebration ? 15 : 14,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(16, 16, 10, 10)
            };
            bubbleStyle.normal.background = isCelebration ? _celebrateBgTex : _bubbleBgTex;
            bubbleStyle.normal.textColor = isCelebration
                ? new Color32(0x7A, 0x3E, 0x00, 0xFF)
                : new Color32(0x1E, 0x22, 0x2B, 0xFF);

            const float bubbleW = 260f;
            float contentH = bubbleStyle.CalcHeight(new GUIContent(text), bubbleW);
            float bubbleH = Mathf.Max(46f, contentH + 8f);
            float bubbleX = Mathf.Clamp(
                centerX - bubbleW * 0.5f,
                12f,
                Mathf.Max(12f, Screen.width - bubbleW - 12f));
            float bubbleY = Mathf.Max(12f, bottomY - bubbleH);

            GUI.Label(new Rect(bubbleX, bubbleY, bubbleW, bubbleH), text, bubbleStyle);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (_cachedIdleTex != customIdleTexture)
                _cachedIdleTex?.DestroyIfTemp();
            if (_cachedWalkTex != customWalkTexture && _cachedWalkTex != customIdleTexture)
                _cachedWalkTex?.DestroyIfTemp();
            _bubbleBgTex?.DestroyIfTemp();
            _celebrateBgTex?.DestroyIfTemp();
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
