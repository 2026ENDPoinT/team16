using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zzapmagochi.Room;
using Zzapmagochi.UI;

namespace Zzapmagochi.EditorTools
{
    /// <summary>
    /// scene_room 의 출입문(enterance) 클릭 팝업을 구성한다.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;경로&gt; \
    ///     -executeMethod Zzapmagochi.EditorTools.RoomDoorSceneBuilder.BuildAll
    ///
    /// 기존 씬 내용은 건드리지 않고 DoorUI 하위만 다시 만든다.
    /// </summary>
    public static class RoomDoorSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/scene_room.unity";
        private const string UiRootName = "DoorUI";
        private const string DoorObjectName = "enterance";

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color PanelColor = new Color32(0x2B, 0x31, 0x3D, 0xFF);
        private static readonly Color PrimaryColor = new Color32(0x4C, 0x8B, 0xF5, 0xFF);
        private static readonly Color SecondaryColor = new Color32(0x4A, 0x52, 0x63, 0xFF);
        private static readonly Color TextColor = new Color32(0xEC, 0xEF, 0xF4, 0xFF);
        private static readonly Color MutedColor = new Color32(0x9A, 0xA3, 0xB2, 0xFF);

        private static TMP_FontAsset _font;

        [MenuItem("Zzapmagochi/Build Scenes/Room Door")]
        public static void BuildAll()
        {
            _font = ResolveFont();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var canvas = FindCanvas();
            if (canvas == null)
            {
                Debug.LogError("[RoomDoor] Canvas 를 찾지 못했습니다.");
                return;
            }

            var existing = canvas.transform.Find(UiRootName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(UiRootName, typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            Stretch(root.GetComponent<RectTransform>());

            var friendPopup = BuildFriendListPopup(root.transform);
            var exitPopup = BuildExitPopup(root.transform, friendPopup);

            WireDoor(exitPopup);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[RoomDoor] 구성 완료");
        }

        // ─────────────────────────────────────────────────────────
        // 출입문 팝업
        // ─────────────────────────────────────────────────────────

        private static RoomExitPopup BuildExitPopup(Transform parent, FriendListPopup friendPopup)
        {
            var dim = CreateImage("ExitPopup", parent, DimColor);
            Stretch(dim.rectTransform);

            var panel = CreateImage("Panel", dim.transform, PanelColor);
            Center(panel.rectTransform, new Vector2(560, 380));

            var title = CreateText("Title", panel.transform, "어디로 갈까요?", 32, TextColor, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -52), new Vector2(480, 46));
            title.alignment = TextAlignmentOptions.Center;

            var friendButton = CreateButton("FriendVisitButton", panel.transform, "친구 자취방 방문",
                PrimaryColor, 24);
            Anchor(friendButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0, -130), new Vector2(460, 64));

            var randomButton = CreateButton("RandomVisitButton", panel.transform, "무작위 자취방 방문",
                SecondaryColor, 24);
            Anchor(randomButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0, -206), new Vector2(460, 64));

            var notice = CreateText("Notice", panel.transform, string.Empty, 19, MutedColor);
            Anchor(notice.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -262), new Vector2(460, 30));
            notice.alignment = TextAlignmentOptions.Center;

            var close = CreateButton("CloseButton", panel.transform, "닫기", SecondaryColor, 20);
            Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0, 40), new Vector2(200, 52));

            var popup = dim.gameObject.AddComponent<RoomExitPopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("root").objectReferenceValue = dim.gameObject;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("friendVisitButton").objectReferenceValue = friendButton;
            so.FindProperty("randomVisitButton").objectReferenceValue = randomButton;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("friendListPopup").objectReferenceValue = friendPopup;
            so.FindProperty("noticeText").objectReferenceValue = notice;
            so.ApplyModifiedPropertiesWithoutUndo();

            dim.gameObject.SetActive(false);
            return popup;
        }

        // ─────────────────────────────────────────────────────────
        // 친구 목록 팝업 (자리만)
        // ─────────────────────────────────────────────────────────

        private static FriendListPopup BuildFriendListPopup(Transform parent)
        {
            var dim = CreateImage("FriendListPopup", parent, DimColor);
            Stretch(dim.rectTransform);

            var panel = CreateImage("Panel", dim.transform, PanelColor);
            Center(panel.rectTransform, new Vector2(600, 620));

            var title = CreateText("Title", panel.transform, "친구 목록", 32, TextColor, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -52), new Vector2(520, 46));
            title.alignment = TextAlignmentOptions.Center;

            // 친구 항목이 들어갈 자리. 지금은 비어 있다.
            var container = new GameObject("Items", typeof(RectTransform));
            container.transform.SetParent(panel.transform, false);
            var containerRect = container.GetComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(32f, 110f);
            containerRect.offsetMax = new Vector2(-32f, -100f);

            var layout = container.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 10f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var empty = CreateText("EmptyNotice", panel.transform,
                "아직 친구가 없습니다.\n<size=80%>친구 기능은 준비 중입니다.</size>", 22, MutedColor);
            Anchor(empty.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 120));
            empty.alignment = TextAlignmentOptions.Center;

            var close = CreateButton("CloseButton", panel.transform, "닫기", SecondaryColor, 22);
            Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0, 40), new Vector2(240, 56));

            var popup = dim.gameObject.AddComponent<FriendListPopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("root").objectReferenceValue = dim.gameObject;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("itemContainer").objectReferenceValue = container.transform;
            so.FindProperty("emptyNotice").objectReferenceValue = empty.gameObject;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();

            dim.gameObject.SetActive(false);
            return popup;
        }

        // ─────────────────────────────────────────────────────────
        // 문 연결
        // ─────────────────────────────────────────────────────────

        private static void WireDoor(RoomExitPopup exitPopup)
        {
            var door = GameObject.Find(DoorObjectName);
            if (door == null)
            {
                Debug.LogError($"[RoomDoor] 씬에서 {DoorObjectName} 오브젝트를 찾지 못했습니다.");
                return;
            }

            if (door.GetComponent<Collider>() == null)
            {
                Debug.LogError($"[RoomDoor] {DoorObjectName} 에 Collider 가 없어 클릭할 수 없습니다.");
                return;
            }

            var component = door.GetComponent<RoomDoor>() ?? door.AddComponent<RoomDoor>();

            var so = new SerializedObject(component);
            so.FindProperty("viewCamera").objectReferenceValue = Camera.main;
            so.FindProperty("exitPopup").objectReferenceValue = exitPopup;
            so.FindProperty("placement").objectReferenceValue =
                Object.FindFirstObjectByType<FurniturePlacementController>();
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[RoomDoor] {DoorObjectName} 연결 완료");
        }

        // ─────────────────────────────────────────────────────────
        // UI 헬퍼
        // ─────────────────────────────────────────────────────────

        private static Canvas FindCanvas()
        {
            var named = GameObject.Find("Canvas_overlay");
            if (named != null)
            {
                var c = named.GetComponent<Canvas>();
                if (c != null) return c;
            }

            return Object.FindFirstObjectByType<Canvas>();
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
            text.textWrappingMode = TextWrappingModes.Normal;
            if (_font != null) text.font = _font;

            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color,
            float fontSize)
        {
            var background = CreateImage(name, parent, color);
            var go = background.gameObject;

            var button = go.AddComponent<Button>();
            button.targetGraphic = background;

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
            if (!Directory.Exists("Assets/Art/Fonts")) return TMP_Settings.defaultFontAsset;

            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Art/Fonts" });
            if (guids.Length == 0) return TMP_Settings.defaultFontAsset;

            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
