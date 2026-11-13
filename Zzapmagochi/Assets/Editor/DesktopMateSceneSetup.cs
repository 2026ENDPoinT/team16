using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Zzapmagochi.Room;

namespace Zzapmagochi.EditorTools
{
    /// <summary>
    /// 현재 열린 씬(scene_room 등)에 '데스크탑 메이트 (PiP)' 버튼과 DesktopMateController를 한 번에 생성·연결해주는 에디터 도구.
    /// </summary>
    public static class DesktopMateSceneSetup
    {
        private const string ButtonName = "DesktopMatePipButton";
        private const string ControllerObjectName = "DesktopMateController";

        [MenuItem("Zzapmagochi/Build Scenes/Add Desktop Mate PiP Button to Active Scene")]
        public static void SetupInActiveScene()
        {
            var canvas = FindOrCreateOverlayCanvas();
            EnsureEventSystem();

            // 기존 버튼이 있으면 재사용/갱신
            var existingBtnTf = canvas.transform.Find(ButtonName);
            if (existingBtnTf != null)
                Object.DestroyImmediate(existingBtnTf.gameObject);

            var btn = CreatePipButton(canvas.transform);

            // 컨트롤러 오브젝트 생성 및 연결
            var ctrlGo = GameObject.Find(ControllerObjectName) ?? new GameObject(ControllerObjectName);
            var ctrl = ctrlGo.GetComponent<DesktopMateController>() ?? ctrlGo.AddComponent<DesktopMateController>();

            // 캐릭터 루트 자동 탐색
            Transform charRoot = null;
            var charParts = Object.FindFirstObjectByType<CharacterParts>();
            if (charParts != null)
            {
                charRoot = charParts.transform;
            }
            else
            {
                var miku = GameObject.Find("miku") ?? GameObject.Find("CharacterTemplate");
                if (miku != null) charRoot = miku.transform;
            }

            var so = new SerializedObject(ctrl);
            so.FindProperty("pipTriggerButton").objectReferenceValue = btn;
            if (charRoot != null)
                so.FindProperty("characterRoot").objectReferenceValue = charRoot;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(ctrlGo.scene);
            Selection.activeGameObject = ctrlGo;

            Debug.Log("[DesktopMateSceneSetup] '데스크탑 메이트 (PiP)' 버튼 및 DesktopMateController 설정 완료!");
        }

        private static Canvas FindOrCreateOverlayCanvas()
        {
            var named = GameObject.Find("Canvas_overlay");
            if (named != null)
            {
                named.SetActive(true);
                // 예전 Placeholder Background/Text가 남아있다면 비활성화
                var oldBg = named.transform.Find("Background");
                if (oldBg != null) oldBg.gameObject.SetActive(false);
                var oldPh = named.transform.Find("Placeholder");
                if (oldPh != null) oldPh.gameObject.SetActive(false);

                var c = named.GetComponent<Canvas>();
                if (c != null) return c;
            }

            var existing = Object.FindFirstObjectByType<Canvas>();
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return existing;
            }

            var go = new GameObject("Canvas_overlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static Button CreatePipButton(Transform parent)
        {
            var go = new GameObject(ButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            // 화면 우측 상단 배치 (왼쪽 위 가구 배치 버튼과 겹치지 않도록)
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(250f, 56f);

            var img = go.GetComponent<Image>();
            img.color = new Color32(0x5E, 0x4B, 0x8B, 0xFF);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "데스크탑 메이트 (PiP)";
            tmp.fontSize = 21;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color32(0xEC, 0xEF, 0xF4, 0xFF);
            tmp.alignment = TextAlignmentOptions.Center;

            var font = ResolveKoreanFont();
            if (font != null) tmp.font = font;

            return btn;
        }

        private static TMP_FontAsset ResolveKoreanFont()
        {
            if (!Directory.Exists("Assets/Art/Fonts")) return TMP_Settings.defaultFontAsset;
            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Art/Fonts" });
            if (guids.Length == 0) return TMP_Settings.defaultFontAsset;
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
