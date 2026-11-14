using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zzapmagochi.Core;
using Zzapmagochi.Net;

namespace Zzapmagochi.Room
{
    public class GitHubStreakFurniture3D : MonoBehaviour
    {
        public static bool IsPopupOpen => _isPopupOpen;

        [Header("3D 가구 화면 (비워두면 자식 'Canvas'를 자동 탐색)")]
        [SerializeField] private MeshRenderer screenMeshRenderer;
        [SerializeField] private SpriteRenderer screenSpriteRenderer;
        [SerializeField] private int screenMaterialIndex = 0;

        [Header("해상도 (가로 2 : 세로 1 유닛 = 400 x 200 px)")]
        [SerializeField] private int textureWidth = 400;
        [SerializeField] private int textureHeight = 200;

        [Tooltip("선택사항: 400x200 배경 이미지(Read/Write Enabled)를 넣으면 그 위에 잔디를 찍습니다.")]
        [SerializeField] private Texture2D customBackgroundTexture;

        [Header("3D 발광(Emission) 강도 설정")]
        [SerializeField] private bool enableEmission = true;
        [SerializeField, Range(0f, 5f)] private float emissionIntensity = 1.4f;

        [Header("테스트용 GitHub ID (비워두면 Session 학번 기반 Mock 로드)")]
        [SerializeField] private string testGithubUsername = "torvalds";

        [Header("상호작용")]
        [SerializeField] private bool highlightOnHover = true;
        [SerializeField] private Color hoverColor = new Color(0.85f, 1f, 0.88f, 1f);

        private const string PrefsGithubIdKey = "Zzap_CustomGithubUsername";

        private static GitHubStreakPayload _cachedPayload;
        private static string _cachedKey;
        private static GitHubStreakFurniture3D _activePopupOwner;
        private static bool _isPopupOpen;
        private static bool _isLoading;
        private static string _usernameInput = "";
        private static string _statusMessage = "";

        private Camera _viewCamera;
        private FurniturePlacementController _placement;
        private Collider[] _colliders;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _hoverBlock;
        private bool _hovered;

        private GitHubStreak3DBundle _bundle;
        private Sprite _generatedSprite;
        private MaterialPropertyBlock _mpb;

        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _btnStyle;
        private GUIStyle _statusStyle;
        private Texture2D _winBgTex;
        private Texture2D _btnBgTex;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _hoverBlock = new MaterialPropertyBlock();
            _viewCamera = Camera.main;
            _placement = FindFirstObjectByType<FurniturePlacementController>();
            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            AutoFindRenderers();
        }

        private async void Start()
        {
            string savedId = PlayerPrefs.GetString(PrefsGithubIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(savedId))
                testGithubUsername = savedId.Trim();

            if (string.IsNullOrWhiteSpace(_usernameInput))
                _usernameInput = testGithubUsername;

            await RefreshStreakBoardAsync(testGithubUsername);
        }

        private void Update()
        {
            if (_viewCamera == null) _viewCamera = Camera.main;
            if (_placement == null) _placement = FindFirstObjectByType<FurniturePlacementController>();

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

            bool pointing = IsPointerOnBoard(mouse.position.ReadValue());
            SetHovered(pointing);

            if (pointing && mouse.leftButton.wasPressedThisFrame)
            {
                OpenPopup();
            }
        }

        public void OpenPopup()
        {
            _activePopupOwner = this;
            _isPopupOpen = true;
            if (string.IsNullOrWhiteSpace(_usernameInput))
                _usernameInput = testGithubUsername;
            _statusMessage = $"현재 표시 중인 ID: {(string.IsNullOrWhiteSpace(testGithubUsername) ? "기본값" : testGithubUsername)}";
        }

        private bool CanInteract()
        {
            if (_isPopupOpen || TodoBoardFurniture3D.IsPopupOpen) return false;
            if (_placement != null && _placement.CurrentMode != FurniturePlacementController.Mode.Off)
                return false;
            return true;
        }

        private bool IsPointerOnBoard(Vector2 screenPos)
        {
            if (_viewCamera == null || _colliders == null) return false;
            Ray ray = _viewCamera.ScreenPointToRay(screenPos);
            foreach (var col in _colliders)
            {
                if (col != null && col.Raycast(ray, out _, 1000f))
                    return true;
            }
            return false;
        }

        private void SetHovered(bool hovered)
        {
            if (!highlightOnHover || _hovered == hovered) return;
            _hovered = hovered;

            if (_renderers == null) return;
            foreach (var r in _renderers)
            {
                if (r == null || r == screenMeshRenderer) continue;
                if (!hovered)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }
                r.GetPropertyBlock(_hoverBlock);
                _hoverBlock.SetColor(BaseColorId, hoverColor);
                _hoverBlock.SetColor(ColorId, hoverColor);
                r.SetPropertyBlock(_hoverBlock);
            }
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void AutoFindRenderers()
        {
            if (screenMeshRenderer == null)
            {
                var canvasChild = transform.Find("Canvas");
                if (canvasChild != null)
                    screenMeshRenderer = canvasChild.GetComponent<MeshRenderer>();

                if (screenMeshRenderer == null)
                    screenMeshRenderer = GetComponentInChildren<MeshRenderer>();
            }

            if (screenSpriteRenderer == null)
            {
                screenSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private async void ApplyEnteredGithubUsername()
        {
            if (_isLoading) return;
            string targetId = (_usernameInput ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(targetId))
            {
                _statusMessage = "GitHub 아이디를 입력해 주세요.";
                return;
            }

            _isLoading = true;
            _statusMessage = $"'{targetId}'의 최근 스트릭 정보를 불러오는 중...";

            try
            {
                PlayerPrefs.SetString(PrefsGithubIdKey, targetId);
                PlayerPrefs.Save();

                var allBoards = FindObjectsByType<GitHubStreakFurniture3D>(FindObjectsSortMode.None);
                foreach (var board in allBoards)
                {
                    if (board == null) continue;
                    board.testGithubUsername = targetId;
                    await board.RefreshStreakBoardAsync(targetId, null, forceReload: true);
                }

                int totalCommits = _cachedPayload != null ? _cachedPayload.totalContributions : 0;
                int currentStreak = _cachedPayload != null ? _cachedPayload.currentStreak : 0;
                _statusMessage = $"✅ '{targetId}' 스트릭 적용 완료! (현재 연속 {currentStreak}일 / 연간 {totalCommits}커밋)";
            }
            catch (Exception e)
            {
                _statusMessage = $"로드 중 오류 발생: {e.Message}";
            }
            finally
            {
                _isLoading = false;
            }
        }

        public async Task RefreshStreakBoardAsync(string githubUsername = null, string oauthToken = null, bool forceReload = false)
        {
            try
            {
                string studentId = Session.StudentId ?? "20260001";
                string cacheKey = !string.IsNullOrWhiteSpace(githubUsername)
                    ? $"gh:{githubUsername}"
                    : $"st:{studentId}";

                if (forceReload || _cachedPayload == null || _cachedKey != cacheKey)
                {
                    if (!string.IsNullOrWhiteSpace(githubUsername))
                    {
                        try
                        {
                            _cachedPayload = await GitHubStreakGenerator.FetchPayloadAsync(
                                githubUsername, oauthToken, studentId);
                        }
                        catch
                        {
                            _cachedPayload = GitHubStreakGenerator.CreateMockPayload(studentId, githubUsername);
                        }
                    }
                    else
                    {
                        _cachedPayload = await Api.Current.GetGitHubStreak(studentId);
                    }

                    _cachedKey = cacheKey;
                }

                if (this == null) return;

                CleanupGeneratedAssets();

                _bundle = GitHubStreakGenerator.Build3DBundleFromPayload(
                    _cachedPayload,
                    textureWidth,
                    textureHeight,
                    customBackgroundTexture);

                ApplyBundleToRenderer(_bundle);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GitHubStreakFurniture3D] 스트릭 가구 로드 실패: {e.Message}");
            }
        }

        private void ApplyBundleToRenderer(GitHubStreak3DBundle bundle)
        {
            if (bundle == null) return;

            if (screenMeshRenderer != null)
            {
                screenMeshRenderer.GetPropertyBlock(_mpb, screenMaterialIndex);

                _mpb.SetTexture(BaseMapId, bundle.AlbedoTexture);
                _mpb.SetTexture(MainTexId, bundle.AlbedoTexture);

                if (enableEmission && bundle.EmissionTexture != null)
                {
                    _mpb.SetTexture(EmissionMapId, bundle.EmissionTexture);
                    _mpb.SetColor(EmissionColorId, Color.white * emissionIntensity);
                }
                else
                {
                    _mpb.SetColor(EmissionColorId, Color.black);
                }

                screenMeshRenderer.SetPropertyBlock(_mpb, screenMaterialIndex);
            }

            if (screenSpriteRenderer != null && bundle.AlbedoTexture != null)
            {
                _generatedSprite = Sprite.Create(
                    bundle.AlbedoTexture,
                    new Rect(0, 0, bundle.AlbedoTexture.width, bundle.AlbedoTexture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit: 200f);

                screenSpriteRenderer.sprite = _generatedSprite;
            }
        }

        private void EnsureGuiStyles()
        {
            if (_windowStyle != null) return;

            _winBgTex = MakeTex(2, 2, new Color(0.09f, 0.12f, 0.16f, 0.97f));
            _btnBgTex = MakeTex(2, 2, new Color(0.18f, 0.64f, 0.34f, 1f));

            _windowStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _winBgTex, textColor = Color.white },
                padding = new RectOffset(18, 18, 16, 16)
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.98f, 0.94f) }
            };

            _subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color(0.72f, 0.80f, 0.76f) }
            };

            _inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 6, 6)
            };

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { background = _btnBgTex, textColor = Color.white },
                hover = { background = _btnBgTex, textColor = new Color(0.9f, 1f, 0.85f) }
            };

            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = new Color(0.55f, 0.95f, 0.68f) }
            };
        }

        private void OnGUI()
        {
            if (!_isPopupOpen || _activePopupOwner != this) return;

            EnsureGuiStyles();

            float w = Mathf.Min(420f, Screen.width - 32f);
            float h = 230f;
            Rect winRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.Box(winRect, GUIContent.none, _windowStyle);

            GUILayout.BeginArea(new Rect(winRect.x + 18f, winRect.y + 16f, winRect.width - 36f, winRect.height - 32f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("🌿 GitHub 스트릭 액자 설정", _titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기 ✕", GUILayout.Width(68f), GUILayout.Height(28f)))
            {
                _isPopupOpen = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("표시할 GitHub 아이디(Username)를 입력하세요. (본인 인증 없이 자유롭게 조회 가능)", _subStyle);

            GUILayout.Space(10f);
            _usernameInput = GUILayout.TextField(_usernameInput ?? string.Empty, _inputStyle, GUILayout.Height(34f));

            GUILayout.Space(10f);
            GUI.enabled = !_isLoading;
            string btnLabel = _isLoading ? "⏳ 스트릭 불러오는 중..." : "🔄 GitHub 스트릭 불러오기";
            if (GUILayout.Button(btnLabel, _btnStyle, GUILayout.Height(36f)))
            {
                ApplyEnteredGithubUsername();
            }
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUILayout.Space(8f);
                GUILayout.Label(_statusMessage, _statusStyle);
            }

            GUILayout.EndArea();
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private void CleanupGeneratedAssets()
        {
            if (_generatedSprite != null)
            {
                Destroy(_generatedSprite);
                _generatedSprite = null;
            }

            _bundle?.Dispose();
            _bundle = null;
        }

        private void OnDestroy()
        {
            if (_activePopupOwner == this)
            {
                _activePopupOwner = null;
                _isPopupOpen = false;
            }

            CleanupGeneratedAssets();
            if (_winBgTex != null) Destroy(_winBgTex);
            if (_btnBgTex != null) Destroy(_btnBgTex);
        }
    }
}
