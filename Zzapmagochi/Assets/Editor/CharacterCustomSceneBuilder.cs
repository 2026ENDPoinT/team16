using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zzapmagochi.Character;
using Zzapmagochi.UI;

namespace Zzapmagochi.EditorTools
{
    /// <summary>
    /// dev_CharacterCustom 씬에 캐릭터 꾸미기 모드를 구성한다.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;경로&gt; \
    ///     -executeMethod Zzapmagochi.EditorTools.CharacterCustomSceneBuilder.BuildAll
    ///
    /// 기존 씬 내용(방, 카메라, 캐릭터, 가구 배치 UI)은 건드리지 않고 필요한 것만 추가한다.
    /// </summary>
    public static class CharacterCustomSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/dev_CharacterCustom.unity";
        private const string SpriteDir = "Assets/Sprites";
        private const string DataDir = "Assets/Data/Character";
        private const string RtDir = "Assets/Art/RenderTextures";
        private const string RtPath = RtDir + "/CharacterPreview.renderTexture";

        private const string UiRootName = "CustomizeUI";
        private const string StageName = "CharacterPreviewStage";
        private const string CustomizerName = "CharacterCustomizer";

        /// <summary>미리보기 무대는 방에서 멀리 떨어뜨려 둔다. 다른 것이 같이 찍히지 않도록.</summary>
        private static readonly Vector3 StagePosition = new Vector3(500f, 0f, 500f);

        /// <summary>메인 카메라가 원점을 보는 것과 같은 상대 위치.</summary>
        private static readonly Vector3 CameraOffset = new Vector3(1.88f, 3.6f, -1.88f);
        private static readonly Vector3 CameraEuler = new Vector3(30f, -45f, 0f);

        private static readonly Color PanelColor = new Color32(0x23, 0x28, 0x33, 0xFF);
        private static readonly Color StageColor = new Color32(0x2E, 0x33, 0x40, 0xFF);
        private static readonly Color ItemColor = new Color32(0x3A, 0x41, 0x50, 0xFF);
        private static readonly Color AccentColor = new Color32(0xAD, 0x5C, 0xD6, 0xFF);
        private static readonly Color TextColor = new Color32(0xEC, 0xEF, 0xF4, 0xFF);
        private static readonly Color MutedColor = new Color32(0x9A, 0xA3, 0xB2, 0xFF);

        private static TMP_FontAsset _font;

        [MenuItem("Zzapmagochi/Build Scenes/Character Custom")]
        public static void BuildAll()
        {
            _font = ResolveFont();
            EnsureFolders();

            var items = CreateItems();
            var renderTexture = CreateRenderTexture();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var sceneCharacter = FindSceneCharacter();
            if (sceneCharacter == null)
            {
                Debug.LogError("[CharacterCustom] 씬에서 CharacterParts 를 찾지 못했습니다.");
                return;
            }

            var preview = BuildPreviewStage(sceneCharacter, renderTexture);
            var customizer = EnsureCustomizer(sceneCharacter, preview);
            BuildUi(customizer, items, renderTexture);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CharacterCustom] 구성 완료 (의상 {items.Count}종)");
        }

        private static void EnsureFolders()
        {
            foreach (var dir in new[] { "Assets/Data", DataDir, "Assets/Art", RtDir })
            {
                if (Directory.Exists(dir)) continue;
                var parent = Path.GetDirectoryName(dir)?.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
            }
        }

        // ─────────────────────────────────────────────────────────
        // 의상 에셋
        // ─────────────────────────────────────────────────────────

        private static List<CharacterItemDefinition> CreateItems()
        {
            var items = new List<CharacterItemDefinition>
            {
                Define("cloth_outer_0", "외투 A", CharacterSlot.Outer, "out_out_0"),
                Define("cloth_outer_1", "외투 B", CharacterSlot.Outer, "out_out_1"),
                Define("cloth_acc_0", "액세서리 A", CharacterSlot.Accessories, "out_acc_0"),
                Define("cloth_top_0", "상의 A", CharacterSlot.ClothTop, "out_up_0"),
                Define("cloth_bottom_0", "하의 A", CharacterSlot.ClothBottom, "out_bott_0"),
                Define("cloth_shoe_0", "신발 A", CharacterSlot.Shoe, "out_ishoe_0", "out_wshoe_0"),
            };

            items.RemoveAll(i => i == null);
            return items;
        }

        private static CharacterItemDefinition Define(string id, string displayName,
            CharacterSlot slot, string idleSpriteName, string walkSpriteName = null)
        {
            var idle = LoadSprite(idleSpriteName);
            if (idle == null)
            {
                Debug.LogWarning($"[CharacterCustom] 스프라이트를 찾지 못해 건너뜀: {idleSpriteName}");
                return null;
            }

            var path = $"{DataDir}/{id}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<CharacterItemDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CharacterItemDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.id = id;
            definition.displayName = displayName;
            definition.slot = slot;
            definition.idleSprite = idle;
            definition.walkSprite = walkSpriteName != null ? LoadSprite(walkSpriteName) : null;

            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static Sprite LoadSprite(string fileName)
        {
            foreach (var ext in new[] { ".png", ".jpg", ".asset" })
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{fileName}{ext}");
                if (sprite != null) return sprite;
            }

            return null;
        }

        private static RenderTexture CreateRenderTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(RtPath);
            if (existing != null) return existing;

            var rt = new RenderTexture(640, 720, 24, RenderTextureFormat.ARGB32)
            {
                name = "CharacterPreview",
                antiAliasing = 2
            };

            AssetDatabase.CreateAsset(rt, RtPath);
            return AssetDatabase.LoadAssetAtPath<RenderTexture>(RtPath);
        }

        // ─────────────────────────────────────────────────────────
        // 미리보기 무대
        // ─────────────────────────────────────────────────────────

        private static CharacterParts FindSceneCharacter()
        {
            foreach (var parts in Object.FindObjectsByType<CharacterParts>(FindObjectsSortMode.None))
            {
                // 무대에 만들어 둔 복제본은 제외한다.
                if (parts.transform.position.x > 100f) continue;
                return parts;
            }

            return null;
        }

        /// <summary>
        /// 캐릭터 복제본과 전용 카메라를 방에서 멀리 떨어진 곳에 세우고
        /// 그 화면을 RenderTexture 로 뽑는다. 방이 같이 찍히지 않아 미리보기가 깔끔하다.
        /// </summary>
        private static CharacterParts BuildPreviewStage(CharacterParts sceneCharacter, RenderTexture rt)
        {
            var old = GameObject.Find(StageName);
            if (old != null) Object.DestroyImmediate(old);

            var stage = new GameObject(StageName);
            stage.transform.position = StagePosition;

            var clone = Object.Instantiate(sceneCharacter.gameObject, stage.transform);
            clone.name = "PreviewCharacter";
            clone.transform.position = StagePosition;
            clone.transform.rotation = sceneCharacter.transform.rotation;

            var cloneParts = clone.GetComponent<CharacterParts>();

            // 미리보기는 대기 포즈로 고정한다. 걷기 루프가 돌면 옷을 고르는 동안 깜빡인다.
            var so = new SerializedObject(cloneParts);
            so.FindProperty("autoWalkLoop").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            var cameraGo = new GameObject("PreviewCamera");
            cameraGo.transform.SetParent(stage.transform, false);
            cameraGo.transform.position = StagePosition + CameraOffset;
            cameraGo.transform.rotation = Quaternion.Euler(CameraEuler);

            var cam = cameraGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = StageColor;
            cam.targetTexture = rt;
            cam.depth = -10;

            // 정확한 위치와 줌은 실행 중에 캐릭터 바운즈를 재서 맞춘다.
            var framer = cameraGo.AddComponent<CharacterPreviewFramer>();
            var framerSo = new SerializedObject(framer);
            framerSo.FindProperty("target").objectReferenceValue = cloneParts;
            framerSo.ApplyModifiedPropertiesWithoutUndo();

            return cloneParts;
        }

        private static CharacterCustomizer EnsureCustomizer(CharacterParts sceneCharacter,
            CharacterParts preview)
        {
            var go = GameObject.Find(CustomizerName) ?? new GameObject(CustomizerName);
            var customizer = go.GetComponent<CharacterCustomizer>()
                             ?? go.AddComponent<CharacterCustomizer>();

            var so = new SerializedObject(customizer);
            var list = so.FindProperty("targets");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = sceneCharacter;
            list.GetArrayElementAtIndex(1).objectReferenceValue = preview;
            so.ApplyModifiedPropertiesWithoutUndo();

            return customizer;
        }

        // ─────────────────────────────────────────────────────────
        // UI
        // ─────────────────────────────────────────────────────────

        private static void BuildUi(CharacterCustomizer customizer,
            List<CharacterItemDefinition> items, RenderTexture rt)
        {
            var canvas = FindCanvas();
            if (canvas == null)
            {
                Debug.LogError("[CharacterCustom] Canvas 를 찾지 못했습니다.");
                return;
            }

            var existing = canvas.transform.Find(UiRootName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(UiRootName, typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            Stretch(root.GetComponent<RectTransform>());

            // ── 왼쪽 위 모드 버튼 ──────────────────────────────
            // 가구 배치 버튼이 이미 (24, -24) 에 있으므로 그 아래에 둔다.
            var modeButton = CreateButton("CustomModeButton", root.transform, "캐릭터 커스텀", AccentColor, 22);
            var modeRect = modeButton.GetComponent<RectTransform>();
            modeRect.anchorMin = new Vector2(0f, 1f);
            modeRect.anchorMax = new Vector2(0f, 1f);
            modeRect.pivot = new Vector2(0f, 1f);
            modeRect.anchoredPosition = new Vector2(24f, -92f);
            modeRect.sizeDelta = new Vector2(220f, 56f);
            var modeLabel = modeButton.GetComponentInChildren<TMP_Text>(true);

            // ── 꾸미기 모드 루트 ───────────────────────────────
            var customRoot = new GameObject("CustomRoot", typeof(RectTransform));
            customRoot.transform.SetParent(root.transform, false);
            Stretch(customRoot.GetComponent<RectTransform>());

            // 왼쪽 절반: 캐릭터 미리보기
            var previewPanel = CreateImage("PreviewPanel", customRoot.transform, StageColor);
            var previewRect = previewPanel.rectTransform;
            previewRect.anchorMin = new Vector2(0f, 0f);
            previewRect.anchorMax = new Vector2(0.5f, 1f);
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;

            var rawGo = new GameObject("PreviewImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            rawGo.transform.SetParent(previewPanel.transform, false);
            Stretch(rawGo.GetComponent<RectTransform>());
            var raw = rawGo.GetComponent<RawImage>();
            raw.texture = rt;

            var previewTitle = CreateText("PreviewTitle", previewPanel.transform, "현재 캐릭터", 26, TextColor, FontStyles.Bold);
            var ptRect = previewTitle.rectTransform;
            ptRect.anchorMin = new Vector2(0f, 1f);
            ptRect.anchorMax = new Vector2(1f, 1f);
            ptRect.pivot = new Vector2(0.5f, 1f);
            ptRect.anchoredPosition = new Vector2(0f, -28f);
            ptRect.sizeDelta = new Vector2(-48f, 40f);
            previewTitle.alignment = TextAlignmentOptions.Center;

            // 오른쪽 절반: 의상 목록
            var listPanel = CreateImage("ItemPanel", customRoot.transform, PanelColor);
            var listRect = listPanel.rectTransform;
            listRect.anchorMin = new Vector2(0.5f, 0f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.offsetMin = Vector2.zero;
            listRect.offsetMax = Vector2.zero;

            var listTitle = CreateText("Title", listPanel.transform, "의상", 28, TextColor, FontStyles.Bold);
            var ltRect = listTitle.rectTransform;
            ltRect.anchorMin = new Vector2(0f, 1f);
            ltRect.anchorMax = new Vector2(1f, 1f);
            ltRect.pivot = new Vector2(0.5f, 1f);
            ltRect.anchoredPosition = new Vector2(-40f, -28f);
            ltRect.sizeDelta = new Vector2(-48f, 40f);
            listTitle.alignment = TextAlignmentOptions.Left;

            var hint = CreateText("Hint", listPanel.transform,
                "같은 옷을 다시 누르면 벗습니다", 17, MutedColor);
            var hRect = hint.rectTransform;
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(-40f, -66f);
            hRect.sizeDelta = new Vector2(-48f, 26f);
            hint.alignment = TextAlignmentOptions.Left;

            var closeButton = CreateButton("CloseButton", listPanel.transform, "✕", ItemColor, 24);
            var cbRect = closeButton.GetComponent<RectTransform>();
            cbRect.anchorMin = new Vector2(1f, 1f);
            cbRect.anchorMax = new Vector2(1f, 1f);
            cbRect.pivot = new Vector2(1f, 1f);
            cbRect.anchoredPosition = new Vector2(-24f, -24f);
            cbRect.sizeDelta = new Vector2(48f, 48f);

            var container = new GameObject("Items", typeof(RectTransform));
            container.transform.SetParent(listPanel.transform, false);
            var containerRect = container.GetComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(24f, 24f);
            containerRect.offsetMax = new Vector2(-24f, -100f);

            var layout = container.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            // 슬롯 라벨 원본
            var slotLabel = CreateText("SlotTemplate", container.transform, "슬롯", 18, MutedColor, FontStyles.Bold);
            slotLabel.rectTransform.sizeDelta = new Vector2(0f, 26f);
            var slotLayout = slotLabel.gameObject.AddComponent<LayoutElement>();
            slotLayout.preferredHeight = 26f;
            slotLabel.gameObject.SetActive(false);

            // 아이템 버튼 원본 (아이콘 + 이름)
            var template = CreateButton("ItemTemplate", container.transform, "의상", ItemColor, 20);
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.sizeDelta = new Vector2(0f, 64f);
            var templateLayout = template.gameObject.AddComponent<LayoutElement>();
            templateLayout.preferredHeight = 64f;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(template.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(12f, 0f);
            iconRect.sizeDelta = new Vector2(48f, 48f);

            // 이름은 아이콘을 피해 오른쪽으로 민다.
            var templateLabel = template.GetComponentInChildren<TMP_Text>(true);
            if (templateLabel != null)
            {
                var tl = templateLabel.rectTransform;
                tl.offsetMin = new Vector2(72f, 0f);
                tl.offsetMax = new Vector2(-12f, 0f);
                templateLabel.alignment = TextAlignmentOptions.Left;
            }

            template.gameObject.SetActive(false);

            // ── 컴포넌트 연결 ──────────────────────────────────
            var ui = root.AddComponent<CharacterCustomizeUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("customizer").objectReferenceValue = customizer;
            so.FindProperty("modeButton").objectReferenceValue = modeButton;
            so.FindProperty("modeButtonLabel").objectReferenceValue = modeLabel;
            so.FindProperty("customRoot").objectReferenceValue = customRoot;
            so.FindProperty("itemContainer").objectReferenceValue = container.transform;
            so.FindProperty("itemButtonTemplate").objectReferenceValue = template;
            so.FindProperty("slotLabelTemplate").objectReferenceValue = slotLabel;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;

            var list = so.FindProperty("ownedItems");
            list.arraySize = items.Count;
            for (var i = 0; i < items.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            // 모드 버튼은 꾸미기 패널보다 위에 그려져야 한다.
            // CustomRoot 가 화면 전체를 덮기 때문에 순서를 바꾸지 않으면 버튼이 가려진다.
            modeButton.transform.SetAsLastSibling();

            customRoot.SetActive(false);
        }

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

        // ─────────────────────────────────────────────────────────
        // UI 헬퍼
        // ─────────────────────────────────────────────────────────

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
