using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zzapmagochi.Core;
using Zzapmagochi.Net;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// 벽걸이 '할 일(Todo) 보드' 가구 컴포넌트.
    /// - 수동 체크박스 없이 eClass 크롤링 내역을 보여주고 [할 일 리프레시] 버튼으로 재동기화한다.
    /// - 리프레시 후 남은 할 일 개수가 줄어들면(decreasedCount > 0),
    ///   방 안의 캐릭터 머리 위 말풍선과 Desktop Mate PiP 창에 축하 프리셋 대사를 띄운다.
    /// </summary>
    public class TodoBoardFurniture3D : MonoBehaviour
    {
        public static bool IsPopupOpen => _isPopupOpen;

        [Header("3D 보드 렌더러")]
        [SerializeField] private MeshRenderer screenMeshRenderer;
        [SerializeField] private int screenMaterialIndex = 0;
        [SerializeField] private int textureWidth = 256;
        [SerializeField] private int textureHeight = 256;

        [Header("상호작용")]
        [SerializeField] private bool highlightOnHover = true;
        [SerializeField] private Color hoverColor = new Color(1f, 0.96f, 0.82f, 1f);

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Camera _viewCamera;
        private FurniturePlacementController _placement;
        private Collider[] _colliders;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _hoverBlock;
        private Material _runtimeMaterial;
        private Texture2D _boardTexture;
        private bool _hovered;

        // 공유 팝업/데이터 상태 (가구가 여러 개이거나 재배치되어도 하나만 열리도록 관리)
        private static TodoBoardFurniture3D _activePopupOwner;
        private static bool _isPopupOpen;
        private static bool _isSyncing;
        private static List<TodoItemDto> _todos = new List<TodoItemDto>();
        private static string _lastSyncedAt = "-";
        private static string _statusBanner = string.Empty;
        private static float _statusBannerUntil;
        private static string _cookieInput = string.Empty;
        private static string _eclassIdInput = string.Empty;
        private static string _eclassPwInput = string.Empty;
        private static bool _showCookieField;
        private static Vector2 _scrollPos;
        private static bool _initialLoaded;

        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subStyle;
        private GUIStyle _itemBoxStyle;
        private GUIStyle _itemTitleStyle;
        private GUIStyle _itemMetaStyle;
        private GUIStyle _btnStyle;
        private GUIStyle _bannerStyle;
        private Texture2D _winBgTex;
        private Texture2D _itemBgTex;
        private Texture2D _btnBgTex;
        private Texture2D _bannerBgTex;

        private async void Start()
        {
            _viewCamera = Camera.main;
            _placement = FindFirstObjectByType<FurniturePlacementController>();
            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            _hoverBlock = new MaterialPropertyBlock();

            AutoFindScreenRenderer();
            RedrawBoardTexture(_todos.Count);

            if (!_initialLoaded)
            {
                _initialLoaded = true;
                await LoadInitialTodosAsync();
            }
            else
            {
                RedrawBoardTexture(_todos.Count);
            }
        }

        private void OnDestroy()
        {
            if (_activePopupOwner == this)
            {
                _activePopupOwner = null;
                _isPopupOpen = false;
            }

            if (_runtimeMaterial != null) Destroy(_runtimeMaterial);
            if (_boardTexture != null) Destroy(_boardTexture);
            if (_winBgTex != null) Destroy(_winBgTex);
            if (_itemBgTex != null) Destroy(_itemBgTex);
            if (_btnBgTex != null) Destroy(_btnBgTex);
            if (_bannerBgTex != null) Destroy(_bannerBgTex);
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
        }

        private bool CanInteract()
        {
            if (_isPopupOpen || GitHubStreakFurniture3D.IsPopupOpen) return false;
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

        private async Task LoadInitialTodosAsync()
        {
            if (Api.Current == null) return;
            try
            {
                var res = await Api.Current.GetTodos();
                if (res != null && res.items != null)
                {
                    _todos = new List<TodoItemDto>(res.items);
                    _lastSyncedAt = string.IsNullOrEmpty(res.syncedAt) ? "방금 전" : FormatTime(res.syncedAt);
                    RefreshAllBoardTextures();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TodoBoard] 초기 할 일 조회 실패: {e.Message}");
            }
        }

        private async void SyncTodosNow(bool openBrowserLogin = false, bool useAccountLogin = false)
        {
            if (_isSyncing || Api.Current == null) return;
            _isSyncing = true;

            int beforeCount = _todos != null ? _todos.Count : 0;

            if (openBrowserLogin)
            {
                _statusBanner = "🌐 eClass 로그인 창을 열었습니다! 브라우저에서 로그인하시면 자동으로 쿠키를 추출해 연동합니다.";
                _statusBannerUntil = Time.time + 90f;
            }
            else if (useAccountLogin)
            {
                _statusBanner = "⏳ 서버 에이전트가 eClass에 자동 로그인하여 쿠키와 할 일을 추출하고 있습니다...";
                _statusBannerUntil = Time.time + 30f;
            }

            try
            {
                var res = await Api.Current.SyncTodos(
                    cookieHeader: _cookieInput,
                    eclassId: useAccountLogin ? _eclassIdInput : null,
                    eclassPassword: useAccountLogin ? _eclassPwInput : null,
                    openBrowserLogin: openBrowserLogin);

                if (res != null)
                {
                    _todos = res.items != null ? new List<TodoItemDto>(res.items) : new List<TodoItemDto>();
                    _lastSyncedAt = string.IsNullOrEmpty(res.syncedAt) ? DateTime.Now.ToString("HH:mm:ss") : FormatTime(res.syncedAt);

                    int decreased = res.decreasedCount;
                    if (decreased <= 0 && beforeCount > _todos.Count)
                        decreased = beforeCount - _todos.Count;

                    RefreshAllBoardTextures();

                    if (decreased > 0)
                    {
                        _statusBanner = $"와! 해냈어! 할 일이 {decreased}개 줄어들었어요! (남은 할 일: {_todos.Count}개)";
                        _statusBannerUntil = Time.time + 6.5f;

                        if (DesktopMateController.Instance != null)
                        {
                            DesktopMateController.Instance.TriggerTodoCompletedCelebration(decreased);
                        }
                    }
                    else if (!string.IsNullOrEmpty(res.message))
                    {
                        _statusBanner = res.message;
                        _statusBannerUntil = Time.time + 5f;
                    }
                    else
                    {
                        _statusBanner = $"eClass 할 일 목록을 최신 상태로 리프레시했어요. (현재 {_todos.Count}개)";
                        _statusBannerUntil = Time.time + 3.5f;
                    }
                }
            }
            catch (Exception e)
            {
                _statusBanner = $"리프레시 중 오류가 발생했어요: {e.Message}";
                _statusBannerUntil = Time.time + 4.5f;
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private static void RefreshAllBoardTextures()
        {
            var boards = FindObjectsByType<TodoBoardFurniture3D>(FindObjectsSortMode.None);
            foreach (var b in boards)
            {
                if (b != null) b.RedrawBoardTexture(_todos.Count);
            }
        }

        private void AutoFindScreenRenderer()
        {
            if (screenMeshRenderer != null) return;
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                string n = r.gameObject.name.ToLowerInvariant();
                if (n.Contains("canvas") || n.Contains("screen") || n.Contains("board"))
                {
                    screenMeshRenderer = r;
                    return;
                }
            }
            if (renderers.Length > 0)
                screenMeshRenderer = renderers[renderers.Length - 1];
        }

        private void RedrawBoardTexture(int todoCount)
        {
            if (screenMeshRenderer == null) return;

            int w = Mathf.Max(64, textureWidth);
            int h = Mathf.Max(64, textureHeight);

            if (_boardTexture == null || _boardTexture.width != w || _boardTexture.height != h)
            {
                if (_boardTexture != null) Destroy(_boardTexture);
                _boardTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            Color corkBg = new Color(0.84f, 0.67f, 0.45f, 1f);
            Color paperBg = new Color(0.98f, 0.96f, 0.90f, 1f);
            Color headerBar = new Color(0.24f, 0.45f, 0.68f, 1f);
            Color pinColor = new Color(0.90f, 0.25f, 0.25f, 1f);
            Color activeRow = new Color(0.22f, 0.26f, 0.34f, 1f);
            Color badgeColor = todoCount > 0 ? new Color(0.92f, 0.42f, 0.20f, 1f) : new Color(0.24f, 0.72f, 0.42f, 1f);
            Color emptyRow = new Color(0.86f, 0.84f, 0.78f, 1f);

            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = corkBg;

            // 종이 메모지 영역
            FillRect(pixels, w, h, 18, 18, w - 36, h - 36, paperBg);
            // 상단 헤더 바
            FillRect(pixels, w, h, 18, h - 54, w - 36, 36, headerBar);
            // 압정 포인트
            FillRect(pixels, w, h, w / 2 - 8, h - 30, 16, 16, pinColor);

            // 할 일 줄 표시 (최대 5줄)
            int maxLines = 5;
            int startY = h - 82;
            int rowHeight = 22;
            int gap = 10;

            for (int i = 0; i < maxLines; i++)
            {
                int y = startY - i * (rowHeight + gap);
                if (y < 28) break;

                bool hasItem = i < todoCount;
                Color dotCol = hasItem ? badgeColor : emptyRow;
                Color lineCol = hasItem ? activeRow : emptyRow;

                // 불릿 아이콘
                FillRect(pixels, w, h, 34, y, 18, 18, dotCol);
                // 텍스트 바
                int barWidth = hasItem ? (w - 104 - (i % 2) * 24) : (w - 120);
                FillRect(pixels, w, h, 62, y + 3, barWidth, 12, lineCol);
            }

            _boardTexture.SetPixels(pixels);
            _boardTexture.Apply();

            if (_runtimeMaterial == null)
            {
                var mats = screenMeshRenderer.sharedMaterials;
                int idx = Mathf.Clamp(screenMaterialIndex, 0, mats.Length - 1);
                Shader shader = mats[idx] != null ? mats[idx].shader : Shader.Find("Universal Render Pipeline/Lit");
                _runtimeMaterial = new Material(shader);
                mats[idx] = _runtimeMaterial;
                screenMeshRenderer.materials = mats;
            }

            if (_runtimeMaterial.HasProperty(BaseMapId)) _runtimeMaterial.SetTexture(BaseMapId, _boardTexture);
            if (_runtimeMaterial.HasProperty(MainTexId)) _runtimeMaterial.SetTexture(MainTexId, _boardTexture);
            if (_runtimeMaterial.HasProperty(BaseColorId)) _runtimeMaterial.SetColor(BaseColorId, Color.white);
            if (_runtimeMaterial.HasProperty(ColorId)) _runtimeMaterial.SetColor(ColorId, Color.white);
        }

        private static void FillRect(Color[] pixels, int texW, int texH, int x, int y, int rw, int rh, Color col)
        {
            int xMax = Mathf.Min(texW, x + rw);
            int yMax = Mathf.Min(texH, y + rh);
            int xMin = Mathf.Max(0, x);
            int yMin = Mathf.Max(0, y);

            for (int py = yMin; py < yMax; py++)
            {
                int row = py * texW;
                for (int px = xMin; px < xMax; px++)
                {
                    pixels[row + px] = col;
                }
            }
        }

        private void EnsureGuiStyles()
        {
            if (_windowStyle != null) return;

            _winBgTex = MakeTex(2, 2, new Color(0.11f, 0.13f, 0.18f, 0.97f));
            _itemBgTex = MakeTex(2, 2, new Color(0.17f, 0.20f, 0.27f, 0.95f));
            _btnBgTex = MakeTex(2, 2, new Color(0.22f, 0.52f, 0.88f, 1f));
            _bannerBgTex = MakeTex(2, 2, new Color(0.18f, 0.42f, 0.28f, 0.95f));

            _windowStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _winBgTex, textColor = Color.white },
                padding = new RectOffset(18, 18, 16, 16)
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.96f, 0.97f, 1f) }
            };

            _subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color(0.72f, 0.78f, 0.88f) }
            };

            _itemBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _itemBgTex, textColor = Color.white },
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(0, 0, 4, 6)
            };

            _itemTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = new Color(0.98f, 0.95f, 0.84f) }
            };

            _itemMetaStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color(0.75f, 0.82f, 0.92f) }
            };

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { background = _btnBgTex, textColor = Color.white },
                hover = { background = _btnBgTex, textColor = new Color(1f, 0.95f, 0.7f) }
            };

            _bannerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { background = _bannerBgTex, textColor = new Color(0.95f, 1f, 0.9f) },
                padding = new RectOffset(10, 10, 8, 8)
            };
        }

        private void OnGUI()
        {
            if (!_isPopupOpen || _activePopupOwner != this) return;

            EnsureGuiStyles();

            float w = Mathf.Min(540f, Screen.width - 32f);
            float h = Mathf.Min(520f, Screen.height - 40f);
            Rect winRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.Box(winRect, GUIContent.none, _windowStyle);

            GUILayout.BeginArea(new Rect(winRect.x + 18f, winRect.y + 16f, winRect.width - 36f, winRect.height - 32f));

            // 헤더
            GUILayout.BeginHorizontal();
            GUILayout.Label($"📋 eClass 할 일 보드 ({_todos.Count}건)", _titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기 ✕", GUILayout.Width(76f), GUILayout.Height(30f)))
            {
                _isPopupOpen = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label($"마지막 동기화: {_lastSyncedAt} · 수동 체크 없이 eClass 크롤링 내역을 리프레시합니다.", _subStyle);

            // 축하/상태 배너
            if (!string.IsNullOrEmpty(_statusBanner) && Time.time < _statusBannerUntil)
            {
                GUILayout.Space(6f);
                GUILayout.Box(_statusBanner, _bannerStyle, GUILayout.ExpandWidth(true));
            }

            GUILayout.Space(8f);

            // 3개 버튼: '리프레시' / '웹에서 로그인' / 'ID나 쿠키등록' (잘리지 않게 여유 폭 배정)
            GUILayout.BeginHorizontal();
            GUI.enabled = !_isSyncing;
            string syncLabel = _isSyncing ? "동기화 중..." : "리프레시";
            if (GUILayout.Button(syncLabel, _btnStyle, GUILayout.ExpandWidth(true), GUILayout.MinWidth(130f), GUILayout.Height(38f)))
            {
                SyncTodosNow(openBrowserLogin: false, useAccountLogin: false);
            }

            GUILayout.Space(6f);

            if (GUILayout.Button("웹에서 로그인", _btnStyle, GUILayout.ExpandWidth(true), GUILayout.MinWidth(150f), GUILayout.Height(38f)))
            {
                SyncTodosNow(openBrowserLogin: true, useAccountLogin: false);
            }
            GUI.enabled = true;

            GUILayout.Space(6f);

            if (GUILayout.Button("ID나 쿠키등록", _btnStyle, GUILayout.ExpandWidth(true), GUILayout.MinWidth(150f), GUILayout.Height(38f)))
            {
                _showCookieField = !_showCookieField;
            }
            GUILayout.EndHorizontal();

            if (_showCookieField)
            {
                GUILayout.Space(6f);
                GUILayout.Label("방법 1) eClass 학번/비밀번호 입력 시 서버 에이전트가 백그라운드로 쿠키를 자동 추출합니다:", _subStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("학번:", _subStyle, GUILayout.Width(36f));
                _eclassIdInput = GUILayout.TextField(_eclassIdInput ?? string.Empty, GUILayout.Width(120f));
                GUILayout.Label("비번:", _subStyle, GUILayout.Width(36f));
                _eclassPwInput = GUILayout.PasswordField(_eclassPwInput ?? string.Empty, '*', GUILayout.Width(120f));
                GUI.enabled = !_isSyncing && !string.IsNullOrWhiteSpace(_eclassIdInput) && !string.IsNullOrWhiteSpace(_eclassPwInput);
                if (GUILayout.Button("백그라운드 연동", GUILayout.Height(24f)))
                {
                    SyncTodosNow(openBrowserLogin: false, useAccountLogin: true);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                GUILayout.Space(4f);
                GUILayout.Label("방법 2) 직접 Cookie 문자열 입력 (비워두면 자동추출 쿠키 또는 데모 시뮬레이션 사용):", _subStyle);
                _cookieInput = GUILayout.TextField(_cookieInput ?? string.Empty);
            }

            GUILayout.Space(10f);

            // 할 일 목록 스크롤 영역
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
            if (_todos == null || _todos.Count == 0)
            {
                GUILayout.Space(30f);
                GUILayout.Label("🎉 남아있는 할 일이 없습니다! 모든 과제를 완료했어요!", _itemTitleStyle);
            }
            else
            {
                for (int i = 0; i < _todos.Count; i++)
                {
                    var item = _todos[i];
                    GUILayout.BeginVertical(_itemBoxStyle);
                    string course = string.IsNullOrEmpty(item.courseName) ? "[eClass]" : $"[{item.courseName}]";
                    GUILayout.Label($"{course} {item.title}", _itemTitleStyle);

                    string due = string.IsNullOrEmpty(item.dueAt) ? "마감 기한 정보 없음" : $"마감: {item.dueAt}";
                    GUILayout.Label(due, _itemMetaStyle);
                    GUILayout.EndVertical();
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private static string FormatTime(string isoOrText)
        {
            if (DateTime.TryParse(isoOrText, out var dt))
                return dt.ToLocalTime().ToString("MM/dd HH:mm:ss");
            return isoOrText;
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
    }
}
