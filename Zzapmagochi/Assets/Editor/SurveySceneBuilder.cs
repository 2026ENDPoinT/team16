using System.Collections.Generic;
using System.IO;
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
    /// scene_survey (회원가입 직후 룸메이트 성격 설문 씬)을 코드로 자동 생성하고 Build Settings에 등록한다.
    /// 기존 회원가입 관련 파일(LoginController, SignUpPopup, LoginSceneBuilder 등)은 일절 수정하지 않는다.
    /// </summary>
    public static class SurveySceneBuilder
    {
        private const string SurveyScenePath = "Assets/Scenes/scene_survey.unity";
        private static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        private static readonly Color BackgroundColor = new Color32(0x1E, 0x22, 0x2B, 0xFF);
        private static readonly Color PanelColor = new Color32(0x2B, 0x31, 0x3D, 0xFF);
        private static readonly Color CardColor = new Color32(0x36, 0x3E, 0x4D, 0xFF);
        private static readonly Color FieldColor = new Color32(0x23, 0x28, 0x32, 0xFF);
        private static readonly Color PrimaryColor = new Color32(0x4C, 0x8B, 0xF5, 0xFF);
        private static readonly Color AccentColor = new Color32(0x6C, 0x5C, 0xE7, 0xFF);
        private static readonly Color TextColor = new Color32(0xEC, 0xEF, 0xF4, 0xFF);
        private static readonly Color MutedColor = new Color32(0x9A, 0xA3, 0xB2, 0xFF);

        private static TMP_FontAsset _font;

        [MenuItem("Zzapmagochi/Build Scenes/Build scene_survey")]
        public static void BuildSurveyScene()
        {
            _font = ResolveFont();

            if (!Directory.Exists("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateCanvas(out var canvasGo);
            CreateEventSystem();

            var bg = CreateImage("Background", canvasGo.transform, BackgroundColor);
            Stretch(bg.rectTransform);

            // 1단계: 인트로 패널
            var introPanel = BuildIntroPanel(canvasGo.transform, out var startSurveyBtn);

            // 2단계: 8문항 질문 패널
            var questionPanel = BuildQuestionPanel(
                canvasGo.transform,
                out var progressText,
                out var questionText,
                out var optionButtons,
                out var optionLabels);

            // 3단계: 결과 & 이름/말버릇 지정 패널
            var resultPanel = BuildResultPanel(
                canvasGo.transform,
                out var personaTitleText,
                out var personaDescText,
                out var nicknameInput,
                out var catchphraseInput,
                out var enterRoomBtn);

            // SurveyController 연결
            var ctrlGo = new GameObject("SurveyController");
            var ctrl = ctrlGo.AddComponent<SurveyController>();

            var so = new SerializedObject(ctrl);
            so.FindProperty("introPanel").objectReferenceValue = introPanel;
            so.FindProperty("startSurveyButton").objectReferenceValue = startSurveyBtn;

            so.FindProperty("questionPanel").objectReferenceValue = questionPanel;
            so.FindProperty("progressText").objectReferenceValue = progressText;
            so.FindProperty("questionText").objectReferenceValue = questionText;

            var btnArr = so.FindProperty("optionButtons");
            btnArr.arraySize = optionButtons.Length;
            for (int i = 0; i < optionButtons.Length; i++)
                btnArr.GetArrayElementAtIndex(i).objectReferenceValue = optionButtons[i];

            var lblArr = so.FindProperty("optionLabels");
            lblArr.arraySize = optionLabels.Length;
            for (int i = 0; i < optionLabels.Length; i++)
                lblArr.GetArrayElementAtIndex(i).objectReferenceValue = optionLabels[i];

            so.FindProperty("resultPanel").objectReferenceValue = resultPanel;
            so.FindProperty("resultPersonaTitleText").objectReferenceValue = personaTitleText;
            so.FindProperty("resultPersonaDescText").objectReferenceValue = personaDescText;
            so.FindProperty("nicknameInput").objectReferenceValue = nicknameInput;
            so.FindProperty("catchphraseInput").objectReferenceValue = catchphraseInput;
            so.FindProperty("enterRoomButton").objectReferenceValue = enterRoomBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            questionPanel.SetActive(false);
            resultPanel.SetActive(false);

            EditorSceneManager.SaveScene(scene, SurveyScenePath);
            RegisterSceneInBuildSettings(SurveyScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SurveySceneBuilder] scene_survey 생성 및 Build Settings 등록 완료!");
        }

        private static GameObject BuildIntroPanel(Transform parent, out Button startButton)
        {
            var panel = CreateImage("IntroPanel", parent, PanelColor);
            Center(panel.rectTransform, new Vector2(880, 520));

            var badge = CreateText("Badge", panel.transform, "놀러와요 자취의 방", 20, PrimaryColor, FontStyles.Bold);
            Anchor(badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(760, 34));
            badge.alignment = TextAlignmentOptions.Center;

            var mainCopy = CreateText("MainCopy", panel.transform,
                "작은 자취방에서 대학생활을 시작해봐요", 42, TextColor, FontStyles.Bold);
            Anchor(mainCopy.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -145), new Vector2(800, 64));
            mainCopy.alignment = TextAlignmentOptions.Center;

            var subCopy = CreateText("SubCopy", panel.transform,
                "룸메이트는 어떤 성격일까요?\n8개의 간단한 질문에 답하고 나와 함께할 단짝 메이트를 만나보세요.",
                24, MutedColor);
            Anchor(subCopy.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -245), new Vector2(760, 90));
            subCopy.alignment = TextAlignmentOptions.Center;

            startButton = CreateButton("StartSurveyButton", panel.transform, "룸메이트 성격 알아보기", PrimaryColor, 26);
            Anchor(startButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0, 85), new Vector2(420, 72));

            return panel.gameObject;
        }

        private static GameObject BuildQuestionPanel(
            Transform parent,
            out TMP_Text progressText,
            out TMP_Text questionText,
            out Button[] optionButtons,
            out TMP_Text[] optionLabels)
        {
            var panel = CreateImage("QuestionPanel", parent, PanelColor);
            Center(panel.rectTransform, new Vector2(1020, 740));

            progressText = CreateText("ProgressText", panel.transform, "질문 1 / 8", 22, PrimaryColor, FontStyles.Bold);
            Anchor(progressText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -52), new Vector2(880, 36));
            progressText.alignment = TextAlignmentOptions.Center;

            questionText = CreateText("QuestionText", panel.transform,
                "Q1. 새 학기 첫날 아침! 알람이 울릴 때 룸메이트의 모습은?", 32, TextColor, FontStyles.Bold);
            Anchor(questionText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(900, 84));
            questionText.alignment = TextAlignmentOptions.Center;

            optionButtons = new Button[4];
            optionLabels = new TMP_Text[4];

            float startY = -235f;
            float gapY = 112f;

            for (int i = 0; i < 4; i++)
            {
                var btn = CreateButton($"OptionButton_{i}", panel.transform, $"선택지 {i + 1}", CardColor, 22);
                Anchor(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, startY - i * gapY), new Vector2(880, 92));

                var lbl = btn.GetComponentInChildren<TMP_Text>(true);
                lbl.alignment = TextAlignmentOptions.MidlineLeft;
                var lblRect = lbl.rectTransform;
                lblRect.offsetMin = new Vector2(28, 8);
                lblRect.offsetMax = new Vector2(-28, -8);

                optionButtons[i] = btn;
                optionLabels[i] = lbl;
            }

            return panel.gameObject;
        }

        private static GameObject BuildResultPanel(
            Transform parent,
            out TMP_Text personaTitleText,
            out TMP_Text personaDescText,
            out TMP_InputField nicknameInput,
            out TMP_InputField catchphraseInput,
            out Button enterRoomButton)
        {
            var panel = CreateImage("ResultPanel", parent, PanelColor);
            Center(panel.rectTransform, new Vector2(880, 780));

            var header = CreateText("Header", panel.transform, "나와 함께할 룸메이트는...", 22, MutedColor);
            Anchor(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -54), new Vector2(720, 32));
            header.alignment = TextAlignmentOptions.Center;

            personaTitleText = CreateText("PersonaTitleText", panel.transform, "📋 성실형 메이트", 42, TextColor, FontStyles.Bold);
            Anchor(personaTitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -102), new Vector2(760, 58));
            personaTitleText.alignment = TextAlignmentOptions.Center;

            personaDescText = CreateText("PersonaDescText", panel.transform,
                "계획표와 과제 마감을 꼼꼼하게 챙겨주는 든든한 모범생 룸메이트예요!", 21, MutedColor);
            Anchor(personaDescText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -182), new Vector2(760, 76));
            personaDescText.alignment = TextAlignmentOptions.Center;

            // 이름 입력
            var nameLabel = CreateText("NicknameLabel", panel.transform, "룸메이트 이름", 22, TextColor, FontStyles.Bold);
            Anchor(nameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -295), new Vector2(640, 32));

            nicknameInput = CreateInputField("NicknameInput", panel.transform, "이름을 입력하세요 (기본값: 짭이)");
            nicknameInput.text = "짭이";
            Anchor(nicknameInput.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -342), new Vector2(640, 62));

            // 말버릇 입력 (옵션)
            var phraseLabel = CreateText("CatchphraseLabel", panel.transform, "말버릇 (옵션 — 비워두셔도 됩니다)", 22, TextColor, FontStyles.Bold);
            Anchor(phraseLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -435), new Vector2(640, 32));

            catchphraseInput = CreateInputField("CatchphraseInput", panel.transform, "예: ~다구!, ~냥, 완전 럭키잖아! (생략 가능)");
            Anchor(catchphraseInput.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -482), new Vector2(640, 62));

            // 자취방 이동 버튼
            enterRoomButton = CreateButton("EnterRoomButton", panel.transform, "자취방으로 이동하기", AccentColor, 26);
            Anchor(enterRoomButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0, 76), new Vector2(640, 70));

            return panel.gameObject;
        }

        private static void RegisterSceneInBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
            {
                if (s.path == scenePath)
                {
                    s.enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void CreateCamera()
        {
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
            cam.orthographic = true;
            camGo.transform.position = new Vector3(0, 0, -10);
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
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
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
            text.textWrappingMode = TextWrappingModes.Normal;
            if (_font != null) text.font = _font;

            return text;
        }

        private static TMP_InputField CreateInputField(string name, Transform parent, string placeholder)
        {
            var background = CreateImage(name, parent, FieldColor);
            var go = background.gameObject;

            var input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = background;

            var areaGo = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            areaGo.transform.SetParent(go.transform, false);
            var areaRect = areaGo.GetComponent<RectTransform>();
            Stretch(areaRect);
            areaRect.offsetMin = new Vector2(18, 8);
            areaRect.offsetMax = new Vector2(-18, -8);

            var placeholderText = CreateText("Placeholder", areaGo.transform, placeholder, 20, MutedColor, FontStyles.Italic);
            Stretch(placeholderText.rectTransform);
            placeholderText.alignment = TextAlignmentOptions.MidlineLeft;

            var valueText = CreateText("Text", areaGo.transform, string.Empty, 22, TextColor);
            Stretch(valueText.rectTransform);
            valueText.alignment = TextAlignmentOptions.MidlineLeft;

            input.textViewport = areaRect;
            input.textComponent = valueText;
            input.placeholder = placeholderText;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.fontAsset = _font;
            input.pointSize = 22;

            return input;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color, float fontSize = 24)
        {
            var background = CreateImage(name, parent, color);
            var go = background.gameObject;

            var button = go.AddComponent<Button>();
            button.targetGraphic = background;

            var colors = button.colors;
            colors.highlightedColor = new Color(
                Mathf.Clamp01(color.r + 0.1f),
                Mathf.Clamp01(color.g + 0.1f),
                Mathf.Clamp01(color.b + 0.1f),
                color.a);
            colors.pressedColor = new Color(
                Mathf.Clamp01(color.r - 0.1f),
                Mathf.Clamp01(color.g - 0.1f),
                Mathf.Clamp01(color.b - 0.1f),
                color.a);
            button.colors = colors;

            var text = CreateText("Label", go.transform, label, fontSize, TextColor, FontStyles.Bold);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;

            return button;
        }

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

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, anchor.y >= 0.999f ? 1f : anchor.y <= 0.001f ? 0f : 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static TMP_FontAsset ResolveFont()
        {
            if (Directory.Exists("Assets/Art/Fonts"))
            {
                var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Art/Fonts" });
                if (guids.Length > 0)
                {
                    var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    if (font != null) return font;
                }
            }
            return TMP_Settings.defaultFontAsset;
        }
    }
}
