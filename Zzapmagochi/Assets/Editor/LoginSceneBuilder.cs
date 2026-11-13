using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zzapmagochi.UI;

namespace Zzapmagochi.EditorTools
{
    /// <summary>
    /// scene_login / scene_room 을 코드로 생성한다.
    /// 에디터 메뉴 또는 Unity CLI(-executeMethod)로 실행한다.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;경로&gt; \
    ///     -executeMethod Zzapmagochi.EditorTools.LoginSceneBuilder.BuildAll
    ///
    /// 씬을 다시 만들면 기존 내용은 전부 지워진다. 손으로 수정한 것이 있다면 먼저 백업할 것.
    /// </summary>
    public static class LoginSceneBuilder
    {
        private const string ScenesDir = "Assets/Scenes";
        private const string LoginScenePath = ScenesDir + "/scene_login.unity";
        private const string RoomScenePath = ScenesDir + "/scene_room.unity";

        private static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        // 색상 팔레트
        private static readonly Color Background = new Color32(0x1E, 0x22, 0x2B, 0xFF);
        private static readonly Color PanelColor = new Color32(0x2B, 0x31, 0x3D, 0xFF);
        private static readonly Color FieldColor = new Color32(0x3A, 0x41, 0x50, 0xFF);
        private static readonly Color PrimaryColor = new Color32(0x4C, 0x8B, 0xF5, 0xFF);
        private static readonly Color SecondaryColor = new Color32(0x4A, 0x52, 0x63, 0xFF);
        private static readonly Color TextColor = new Color32(0xEC, 0xEF, 0xF4, 0xFF);
        private static readonly Color MutedColor = new Color32(0x9A, 0xA3, 0xB2, 0xFF);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);

        private static TMP_FontAsset _font;

        // ─────────────────────────────────────────────────────────────
        // 진입점
        // ─────────────────────────────────────────────────────────────

        [MenuItem("Zzapmagochi/Build Scenes/scene_login + scene_room")]
        public static void BuildAll()
        {
            _font = ResolveFont();

            BuildRoomPlaceholderScene();
            BuildLoginScene();
            RegisterBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LoginSceneBuilder] scene_login / scene_room 생성 완료");
        }

        /// <summary>
        /// TMP 필수 리소스(기본 폰트 에셋 등)를 가져온다.
        /// BuildAll 보다 먼저, 별도의 CLI 호출로 한 번만 실행한다.
        /// </summary>
        [MenuItem("Zzapmagochi/Build Scenes/Import TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            if (Directory.Exists("Assets/TextMesh Pro"))
            {
                Debug.Log("[LoginSceneBuilder] TMP Essentials 이미 존재. 건너뜀");
                return;
            }

            // TMP_PackageResourceImporter.ImportResources 는 AssetDatabase.ImportPackage 를 쓰는데
            // 이것이 비동기라 배치모드에서는 import 가 끝나기 전에 Unity 가 종료된다.
            // 내부 API ImportPackageImmediately 를 리플렉션으로 호출해 동기 처리한다.
            var packagePath = FindTmpEssentialsPackage();
            if (packagePath == null)
            {
                Debug.LogError("[LoginSceneBuilder] TMP Essential Resources.unitypackage 를 찾지 못했습니다.");
                return;
            }

            var method = typeof(AssetDatabase).GetMethod(
                "ImportPackageImmediately",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            if (method != null)
            {
                method.Invoke(null, new object[] { packagePath });
            }
            else
            {
                Debug.LogWarning("[LoginSceneBuilder] ImportPackageImmediately 를 찾지 못해 비동기 import 로 대체합니다.");
                AssetDatabase.ImportPackage(packagePath, false);
            }

            AssetDatabase.Refresh();

            Debug.Log(Directory.Exists("Assets/TextMesh Pro")
                ? "[LoginSceneBuilder] TMP Essentials 가져오기 완료"
                : "[LoginSceneBuilder] TMP Essentials 가져오기 실패 — Assets/TextMesh Pro 가 생성되지 않았습니다.");
        }

        private static string FindTmpEssentialsPackage()
        {
            const string relative = "/Package Resources/TMP Essential Resources.unitypackage";

            foreach (var dir in Directory.GetDirectories("Library/PackageCache"))
            {
                if (!Path.GetFileName(dir).StartsWith("com.unity.ugui")) continue;
                var candidate = dir.Replace('\\', '/') + relative;
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }

            var builtIn = EditorApplication.applicationContentsPath +
                          "/Resources/PackageManager/BuiltInPackages/com.unity.ugui" + relative;
            return File.Exists(builtIn) ? builtIn : null;
        }

        // ─────────────────────────────────────────────────────────────
        // scene_login
        // ─────────────────────────────────────────────────────────────

        private static void BuildLoginScene()
        {
            var scene = NewScene();
            var canvas = CreateCanvas(out var canvasGo);

            CreateEventSystem();

            // 배경
            var bg = CreateImage("Background", canvasGo.transform, Background);
            Stretch(bg.rectTransform);

            // ── 로그인 패널 ──────────────────────────────────────────
            var panel = CreateImage("LoginPanel", canvasGo.transform, PanelColor);
            Center(panel.rectTransform, new Vector2(620, 620));

            var title = CreateText("Title", panel.transform, "짭마고치", 52, TextColor, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(520, 70));
            title.alignment = TextAlignmentOptions.Center;

            var subtitle = CreateText("Subtitle", panel.transform, "학번으로 로그인하세요", 22, MutedColor);
            Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -145), new Vector2(520, 36));
            subtitle.alignment = TextAlignmentOptions.Center;

            var idLabel = CreateText("IdLabel", panel.transform, "아이디", 22, MutedColor);
            Anchor(idLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -210), new Vector2(460, 32));

            var idInput = CreateInputField("IdInput", panel.transform, "학번을 입력하세요");
            Anchor(idInput.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -256), new Vector2(460, 64));

            var pwLabel = CreateText("PasswordLabel", panel.transform, "비밀번호", 22, MutedColor);
            Anchor(pwLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -320), new Vector2(460, 32));

            var pwInput = CreateInputField("PasswordInput", panel.transform, "비밀번호를 입력하세요");
            pwInput.contentType = TMP_InputField.ContentType.Password;
            Anchor(pwInput.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -366), new Vector2(460, 64));

            var loginButton = CreateButton("LoginButton", panel.transform, "로그인", PrimaryColor);
            Anchor(loginButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -452), new Vector2(460, 64));

            var signUpButton = CreateButton("SignUpButton", panel.transform, "회원가입", SecondaryColor);
            Anchor(signUpButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -528), new Vector2(460, 56));

            // ── 로그인 실패 팝업 ─────────────────────────────────────
            var failPopup = BuildMessagePopup(canvasGo.transform);

            // ── 회원가입 팝업 ────────────────────────────────────────
            var signUpPopup = BuildSignUpPopup(canvasGo.transform);

            // ── 컨트롤러 연결 ────────────────────────────────────────
            var controllerGo = new GameObject("LoginController");
            var controller = controllerGo.AddComponent<LoginController>();

            var so = new SerializedObject(controller);
            so.FindProperty("idInput").objectReferenceValue = idInput;
            so.FindProperty("passwordInput").objectReferenceValue = pwInput;
            so.FindProperty("loginButton").objectReferenceValue = loginButton;
            so.FindProperty("signUpButton").objectReferenceValue = signUpButton;
            so.FindProperty("failPopup").objectReferenceValue = failPopup;
            so.FindProperty("signUpPopup").objectReferenceValue = signUpPopup;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, LoginScenePath);
        }

        private static MessagePopup BuildMessagePopup(Transform parent)
        {
            var dim = CreateImage("FailPopup", parent, DimColor);
            Stretch(dim.rectTransform);

            var panel = CreateImage("Panel", dim.transform, PanelColor);
            Center(panel.rectTransform, new Vector2(520, 280));

            var title = CreateText("Title", panel.transform, "로그인 실패", 30, TextColor, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(440, 44));
            title.alignment = TextAlignmentOptions.Center;

            var message = CreateText("Message", panel.transform,
                "학번 또는 비밀번호가 올바르지 않습니다.", 22, MutedColor);
            Anchor(message.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(440, 70));
            message.alignment = TextAlignmentOptions.Center;
            message.textWrappingMode = TextWrappingModes.Normal;

            var confirm = CreateButton("ConfirmButton", panel.transform, "확인", PrimaryColor);
            Anchor(confirm.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0, 56), new Vector2(200, 56));

            var popup = dim.gameObject.AddComponent<MessagePopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("root").objectReferenceValue = dim.gameObject;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.FindProperty("confirmButton").objectReferenceValue = confirm;
            so.ApplyModifiedPropertiesWithoutUndo();

            dim.gameObject.SetActive(false);
            return popup;
        }

        private static SignUpPopup BuildSignUpPopup(Transform parent)
        {
            var dim = CreateImage("SignUpPopup", parent, DimColor);
            Stretch(dim.rectTransform);

            var panel = CreateImage("Panel", dim.transform, PanelColor);
            Center(panel.rectTransform, new Vector2(640, 720));

            var title = CreateText("Title", panel.transform, "회원가입", 34, TextColor, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(520, 48));
            title.alignment = TextAlignmentOptions.Center;

            // 아이디 + 중복확인
            var idLabel = CreateText("IdLabel", panel.transform, "아이디", 22, MutedColor);
            Anchor(idLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(480, 32));

            var idInput = CreateInputField("IdInput", panel.transform, "사용할 학번");
            Anchor(idInput.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-70, -176), new Vector2(340, 60));

            var checkButton = CreateButton("CheckIdButton", panel.transform, "중복확인", SecondaryColor, 20);
            Anchor(checkButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(160, -176), new Vector2(120, 60));

            var idStatus = CreateText("IdStatusText", panel.transform, string.Empty, 19, MutedColor);
            Anchor(idStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -218), new Vector2(480, 30));

            // 비밀번호
            var pwLabel = CreateText("PasswordLabel", panel.transform, "비밀번호", 22, MutedColor);
            Anchor(pwLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -266), new Vector2(480, 32));

            var pwInput = CreateInputField("PasswordInput", panel.transform, "비밀번호");
            pwInput.contentType = TMP_InputField.ContentType.Password;
            Anchor(pwInput.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -312), new Vector2(480, 60));

            var pwConfirmLabel = CreateText("PasswordConfirmLabel", panel.transform, "비밀번호 재입력", 22, MutedColor);
            Anchor(pwConfirmLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -368), new Vector2(480, 32));

            var pwConfirmInput = CreateInputField("PasswordConfirmInput", panel.transform, "비밀번호 재입력");
            pwConfirmInput.contentType = TMP_InputField.ContentType.Password;
            Anchor(pwConfirmInput.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -414), new Vector2(480, 60));

            var pwStatus = CreateText("PasswordStatusText", panel.transform, string.Empty, 19, MutedColor);
            Anchor(pwStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -456), new Vector2(480, 30));

            // 동작 버튼
            var submit = CreateButton("SubmitButton", panel.transform, "회원가입", PrimaryColor);
            Anchor(submit.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(480, 62));
            submit.interactable = false;

            var cancel = CreateButton("CancelButton", panel.transform, "취소", SecondaryColor);
            Anchor(cancel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0, 66), new Vector2(480, 54));

            var popup = dim.gameObject.AddComponent<SignUpPopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("root").objectReferenceValue = dim.gameObject;
            so.FindProperty("idInput").objectReferenceValue = idInput;
            so.FindProperty("checkIdButton").objectReferenceValue = checkButton;
            so.FindProperty("idStatusText").objectReferenceValue = idStatus;
            so.FindProperty("passwordInput").objectReferenceValue = pwInput;
            so.FindProperty("passwordConfirmInput").objectReferenceValue = pwConfirmInput;
            so.FindProperty("passwordStatusText").objectReferenceValue = pwStatus;
            so.FindProperty("submitButton").objectReferenceValue = submit;
            so.FindProperty("cancelButton").objectReferenceValue = cancel;
            so.ApplyModifiedPropertiesWithoutUndo();

            dim.gameObject.SetActive(false);
            return popup;
        }

        // ─────────────────────────────────────────────────────────────
        // scene_room (자리표시자)
        // ─────────────────────────────────────────────────────────────

        private static void BuildRoomPlaceholderScene()
        {
            if (File.Exists(RoomScenePath) && HasContent(RoomScenePath))
            {
                Debug.Log("[LoginSceneBuilder] scene_room 에 내용이 있어 건너뜀");
                return;
            }

            var scene = NewScene();
            CreateCanvas(out var canvasGo);
            CreateEventSystem();

            var bg = CreateImage("Background", canvasGo.transform, new Color32(0x25, 0x2C, 0x24, 0xFF));
            Stretch(bg.rectTransform);

            var text = CreateText("Placeholder", canvasGo.transform,
                "scene_room\n(자취방 씬은 아직 비어 있습니다)", 40, TextColor, FontStyles.Bold);
            Center(text.rectTransform, new Vector2(900, 200));
            text.alignment = TextAlignmentOptions.Center;

            EditorSceneManager.SaveScene(scene, RoomScenePath);
        }

        /// <summary>기본 카메라/라이트만 있는 빈 씬인지 대략 판별한다.</summary>
        private static bool HasContent(string path)
        {
            var text = File.ReadAllText(path);
            return text.Contains("Canvas:") || text.Contains("m_Name: Grid");
        }

        // ─────────────────────────────────────────────────────────────
        // 빌드 설정
        // ─────────────────────────────────────────────────────────────

        private static void RegisterBuildSettings()
        {
            var wanted = new[] { LoginScenePath, RoomScenePath };
            var list = new List<EditorBuildSettingsScene>();

            foreach (var path in wanted)
                list.Add(new EditorBuildSettingsScene(path, true));

            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (System.Array.IndexOf(wanted, existing.path) >= 0) continue;
                list.Add(existing);
            }

            EditorBuildSettings.scenes = list.ToArray();
        }

        // ─────────────────────────────────────────────────────────────
        // UI 생성 헬퍼
        // ─────────────────────────────────────────────────────────────

        private static Scene NewScene()
        {
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static Canvas CreateCanvas(out GameObject go)
        {
            go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static void CreateEventSystem()
        {
            // 이 프로젝트는 Input System(New) 전용이므로 StandaloneInputModule 은 동작하지 않는다.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string content,
            float size, Color color, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (_font != null) text.font = _font;

            return text;
        }

        private static TMP_InputField CreateInputField(string name, Transform parent, string placeholder)
        {
            var background = CreateImage(name, parent, FieldColor);
            var go = background.gameObject;

            var input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = background;

            // Text Area (뷰포트)
            var areaGo = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            areaGo.transform.SetParent(go.transform, false);
            var areaRect = areaGo.GetComponent<RectTransform>();
            Stretch(areaRect);
            areaRect.offsetMin = new Vector2(16, 8);
            areaRect.offsetMax = new Vector2(-16, -8);

            var placeholderText = CreateText("Placeholder", areaGo.transform, placeholder, 22, MutedColor,
                FontStyles.Italic);
            Stretch(placeholderText.rectTransform);
            placeholderText.alignment = TextAlignmentOptions.Left;

            var valueText = CreateText("Text", areaGo.transform, string.Empty, 22, TextColor);
            Stretch(valueText.rectTransform);
            valueText.alignment = TextAlignmentOptions.Left;

            input.textViewport = areaRect;
            input.textComponent = valueText;
            input.placeholder = placeholderText;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.fontAsset = _font;
            input.pointSize = 22;

            return input;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color,
            float fontSize = 24)
        {
            var background = CreateImage(name, parent, color);
            var go = background.gameObject;

            var button = go.AddComponent<Button>();
            button.targetGraphic = background;

            var colors = button.colors;
            colors.highlightedColor = Lighten(color, 0.12f);
            colors.pressedColor = Lighten(color, -0.12f);
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.35f);
            button.colors = colors;

            var text = CreateText("Label", go.transform, label, fontSize, TextColor, FontStyles.Bold);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;

            return button;
        }

        // ─────────────────────────────────────────────────────────────
        // RectTransform 헬퍼
        // ─────────────────────────────────────────────────────────────

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        /// <summary>앵커를 지정하고 그 기준으로 위치·크기를 잡는다.</summary>
        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, anchor.y >= 0.999f ? 1f : anchor.y <= 0.001f ? 0f : 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Color Lighten(Color c, float amount)
        {
            return new Color(
                Mathf.Clamp01(c.r + amount),
                Mathf.Clamp01(c.g + amount),
                Mathf.Clamp01(c.b + amount),
                c.a);
        }

        // ─────────────────────────────────────────────────────────────
        // 폰트
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Assets/Art/Fonts 안에 TMP 폰트 에셋이 있으면 그것을 쓴다(한글용).
        /// 없으면 TMP 기본 폰트(LiberationSans)를 쓰는데, 이 폰트에는 한글 글리프가 없어
        /// 한국어 라벨이 네모로 표시된다.
        /// </summary>
        private static TMP_FontAsset ResolveFont()
        {
            if (Directory.Exists("Assets/Art/Fonts"))
            {
                var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Art/Fonts" });
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                    if (font != null)
                    {
                        Debug.Log($"[LoginSceneBuilder] 폰트 사용: {path}");
                        return font;
                    }
                }
            }

            // TMP_Settings 가 아직 없으면 defaultFontAsset 접근이 NullReference 를 던진다.
            TMP_FontAsset fallback = null;
            try
            {
                if (TMP_Settings.instance != null)
                    fallback = TMP_Settings.defaultFontAsset;
            }
            catch (System.Exception)
            {
                fallback = null;
            }

            if (fallback == null)
                fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (fallback == null)
            {
                var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
                if (guids.Length > 0)
                    fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (fallback == null)
                Debug.LogError("[LoginSceneBuilder] TMP 폰트 에셋을 찾지 못했습니다. " +
                               "ImportTmpEssentials 를 먼저 실행하세요.");
            else
                Debug.LogWarning("[LoginSceneBuilder] 한글 폰트 에셋이 없어 TMP 기본 폰트를 사용합니다. " +
                                 "한국어 라벨이 네모로 보일 수 있습니다. " +
                                 "Assets/Art/Fonts 에 한글 TMP 폰트 에셋을 넣고 다시 실행하세요.");

            return fallback;
        }
    }
}
