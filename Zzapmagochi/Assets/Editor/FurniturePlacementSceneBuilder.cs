using System.Collections.Generic;
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
    /// scene_room 에 가구 배치 모드와 벽지·바닥 교체 기능을 구성한다.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;경로&gt; \
    ///     -executeMethod Zzapmagochi.EditorTools.FurniturePlacementSceneBuilder.BuildAll
    ///
    /// 기존 씬 내용(방, 카메라, 조명)은 건드리지 않고 필요한 오브젝트만 추가/교체한다.
    /// </summary>
    public static class FurniturePlacementSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/scene_room.unity";
        private const string PrefabDir = "Assets/Prefabs/Furniture";
        private const string DataDir = "Assets/Data/Furniture";
        private const string SurfaceDir = "Assets/Data/Surface";
        private const string SpriteDir = "Assets/Sprites";
        private const string MaterialDir = "Assets/Art/Materials";

        private const string GridObjectName = "RoomGrid";
        private const string ControllerObjectName = "FurniturePlacement";
        private const string UiRootName = "PlacementUI";
        private const string SurfaceObjectName = "RoomSurfaces";

        // 색상
        private static readonly Color PanelColor = new Color32(0x23, 0x28, 0x33, 0xF2);
        private static readonly Color ItemColor = new Color32(0x3A, 0x41, 0x50, 0xFF);
        private static readonly Color PrimaryColor = new Color32(0x4C, 0x8B, 0xF5, 0xFF);
        private static readonly Color TextColor = new Color32(0xEC, 0xEF, 0xF4, 0xFF);
        private static readonly Color MutedColor = new Color32(0x9A, 0xA3, 0xB2, 0xFF);

        private static TMP_FontAsset _font;

        [MenuItem("Zzapmagochi/Build Scenes/Furniture Placement")]
        public static void BuildAll()
        {
            _font = ResolveFont();

            EnsureFolders();

            var previewMaterial = CreatePreviewMaterial();

            // 에셋 경로만 들고 다닌다. 씬을 열기 전에 만든 ScriptableObject 인스턴스를
            // 씬 필드에 그대로 넣으면 null 로 저장되는 경우가 있다.
            var furniturePaths = new List<string>
            {
                CreateFridge(),
                CreateSofa(),
                CreatePictureFrame()
            };

            var wallpaperPaths = CreateSurfaces(RoomSurfaceKind.Wallpaper, "hous_wall_", "벽지");
            var floorPaths = CreateSurfaces(RoomSurfaceKind.Floor, "hous_ground_", "바닥");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var grid = EnsureGrid();
            var controller = EnsureController(grid, previewMaterial);
            var surfaces = EnsureSurfaceController();
            BuildUi(controller, surfaces, furniturePaths, wallpaperPaths, floorPaths);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FurniturePlacement] 구성 완료 "
                      + $"(가구 {furniturePaths.Count}종, 벽지 {wallpaperPaths.Count}종, 바닥 {floorPaths.Count}종)");
        }

        private static void EnsureFolders()
        {
            foreach (var dir in new[] { "Assets/Prefabs", PrefabDir, "Assets/Data", DataDir, SurfaceDir, "Assets/Art", MaterialDir })
            {
                if (Directory.Exists(dir)) continue;
                var parent = Path.GetDirectoryName(dir)?.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
            }
        }

        // ─────────────────────────────────────────────────────────
        // 머티리얼 / 프리팹 / 가구 정의
        // ─────────────────────────────────────────────────────────

        private static Material CreatePreviewMaterial()
        {
            const string path = MaterialDir + "/FurniturePreview.mat";

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "FurniturePreview" };

            // URP Lit 을 투명 모드로 바꾼다. 이 키워드/프로퍼티를 전부 맞춰야 실제로 비친다.
            material.SetFloat("_Surface", 1f);          // 0 = Opaque, 1 = Transparent
            material.SetFloat("_Blend", 0f);            // Alpha
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            // renderQueue 를 여기서 올려도 URP 가 머티리얼을 임포트할 때 3000 으로 되돌린다.
            // 방 스프라이트보다 뒤에 그리는 일은 FurniturePlacementController 가
            // 렌더러의 sortingOrder 로 처리한다.
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.color = new Color(0.3f, 0.9f, 0.4f, 0.45f);

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static Material CreateSolidMaterial(string name, Color color)
        {
            var path = $"{MaterialDir}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        /// <summary>냉장고: 큐브, 바닥 1×1, 높이 2유닛.</summary>
        private static string CreateFridge()
        {
            var material = CreateSolidMaterial("furn_fridge", new Color32(0xD7, 0xDD, 0xE5, 0xFF));
            var prefab = BuildPrimitivePrefab(
                "furn_fridge",
                PrimitiveType.Cube,
                size: new Vector3(1f, 2f, 1f),
                localPosition: new Vector3(0f, 1f, 0f),
                localRotation: Quaternion.identity,
                localScale: new Vector3(1f, 2f, 1f),
                material);

            return CreateDefinition(
                "furn_fridge", "냉장고",
                new Vector3Int(1, 2, 1),
                PlacementSurface.Floor,
                blocksPath: true,
                prefab);
        }

        /// <summary>쇼파: 누운 원통, 바닥 2×1, 높이 1유닛.</summary>
        private static string CreateSofa()
        {
            var material = CreateSolidMaterial("furn_sofa", new Color32(0xC2, 0x6B, 0x5A, 0xFF));

            // 유니티 기본 Cylinder 는 높이 2, 지름 1이다.
            // Z축으로 90도 눕히면 로컬 Y축이 월드 X축이 되므로
            // scale.y = 1 이면 X 방향 길이가 2유닛, 지름은 1유닛이 된다.
            var prefab = BuildPrimitivePrefab(
                "furn_sofa",
                PrimitiveType.Cylinder,
                size: new Vector3(2f, 1f, 1f),
                localPosition: new Vector3(0f, 0.5f, 0f),
                localRotation: Quaternion.Euler(0f, 0f, 90f),
                localScale: Vector3.one,
                material);

            return CreateDefinition(
                "furn_sofa", "쇼파",
                new Vector3Int(2, 1, 1),
                PlacementSurface.Floor,
                blocksPath: true,
                prefab);
        }

        /// <summary>
        /// 액자: 벽 전용. 두께 1 × 높이 2 × 너비 2유닛.
        /// 기준 자세(회전 0단계)는 X 최소쪽 벽에 붙어 +X 를 바라보는 모습이다.
        /// 다른 벽에서는 컨트롤러가 회전 단계를 바꿔 주므로 프리팹은 이 자세만 만들면 된다.
        /// </summary>
        private static string CreatePictureFrame()
        {
            const string path = PrefabDir + "/furn_frame.prefab";

            var frameMaterial = CreateSolidMaterial("furn_frame", new Color32(0x6B, 0x4A, 0x2F, 0xFF));
            var canvasMaterial = CreateSolidMaterial("furn_frame_canvas", new Color32(0xE8, 0xDF, 0xC8, 0xFF));

            var root = new GameObject("furn_frame");

            // 점유 칸의 벽쪽 면(-X)에 바짝 붙인다. 루트가 1칸 폭의 중앙에 있으므로 -0.5 가 벽면이다.
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Frame";
            frame.transform.SetParent(root.transform, false);
            frame.transform.localPosition = new Vector3(-0.44f, 1.0f, 0f);
            frame.transform.localScale = new Vector3(0.12f, 1.7f, 1.7f);
            frame.GetComponent<Renderer>().sharedMaterial = frameMaterial;
            frame.GetComponent<Collider>().isTrigger = true;

            var canvasPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            canvasPanel.name = "Canvas";
            canvasPanel.transform.SetParent(root.transform, false);
            canvasPanel.transform.localPosition = new Vector3(-0.37f, 1.0f, 0f);
            canvasPanel.transform.localScale = new Vector3(0.04f, 1.4f, 1.4f);
            canvasPanel.GetComponent<Renderer>().sharedMaterial = canvasMaterial;
            canvasPanel.GetComponent<Collider>().isTrigger = true;

            root.AddComponent<PlacedFurniture>();

            AssetDatabase.DeleteAsset(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            var definitionPath = CreateDefinition(
                "furn_frame", "액자",
                new Vector3Int(1, 2, 2),
                PlacementSurface.Wall,
                blocksPath: false,
                prefab);

            // 액자는 R 키로 돌리지 않는다. 닿는 벽이 자세를 정한다.
            var definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(definitionPath);
            if (definition != null)
            {
                definition.allowRotation = false;
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
            }

            return definitionPath;
        }

        /// <summary>
        /// 루트가 점유 영역의 바닥 중앙에 오는 프리팹을 만든다.
        /// RoomGrid.FootprintToWorld 가 이 기준으로 위치를 계산한다.
        /// </summary>
        private static GameObject BuildPrimitivePrefab(string name, PrimitiveType primitive,
            Vector3 size, Vector3 localPosition, Quaternion localRotation, Vector3 localScale,
            Material material)
        {
            var path = $"{PrefabDir}/{name}.prefab";

            var root = new GameObject(name);
            var visual = GameObject.CreatePrimitive(primitive);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = localRotation;
            visual.transform.localScale = localScale;

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;

            // 미리보기에서 꺼야 하므로 콜라이더는 남겨 두되, 배치 판정에는 쓰지 않는다.
            var collider = visual.GetComponent<Collider>();
            if (collider != null) collider.isTrigger = true;

            root.AddComponent<PlacedFurniture>();

            AssetDatabase.DeleteAsset(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            return prefab;
        }

        private static string CreateDefinition(string id, string displayName,
            Vector3Int size, PlacementSurface surface, bool blocksPath, GameObject prefab)
        {
            var path = $"{DataDir}/{id}.asset";

            var definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<FurnitureDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.id = id;
            definition.displayName = displayName;
            definition.size = size;
            definition.surface = surface;
            definition.blocksPath = blocksPath;
            definition.prefab = prefab;

            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            return path;
        }

        // ─────────────────────────────────────────────────────────
        // 벽지 / 바닥지
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Assets/Sprites 의 hous_wall_숫자 / hous_ground_숫자 스프라이트를 훑어
        /// 벽지·바닥지 정의 에셋을 만든다. 번호가 끊길 때까지 올라간다.
        /// </summary>
        private static List<string> CreateSurfaces(RoomSurfaceKind kind, string prefix, string label)
        {
            var paths = new List<string>();

            for (var index = 0; index < 64; index++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{prefix}{index}.png");
                if (sprite == null)
                {
                    // 0번이 없으면 아예 없는 것으로 보고, 중간이 비면 거기서 멈춘다.
                    if (index == 0) continue;
                    break;
                }

                var id = $"{(kind == RoomSurfaceKind.Wallpaper ? "wall" : "floor")}_{index}";
                var path = $"{SurfaceDir}/{id}.asset";

                var definition = AssetDatabase.LoadAssetAtPath<RoomSurfaceDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<RoomSurfaceDefinition>();
                    AssetDatabase.CreateAsset(definition, path);
                }

                definition.id = id;
                definition.displayName = $"{label} {index + 1}";
                definition.kind = kind;
                definition.sprite = sprite;

                EditorUtility.SetDirty(definition);
                paths.Add(path);
            }

            AssetDatabase.SaveAssets();
            return paths;
        }

        /// <summary>
        /// room 아래의 SpriteRenderer 를 벽/바닥으로 분류해 교체 컨트롤러에 연결한다.
        /// 이름을 하나씩 찾지 않는 이유: 방 구조가 벽 2장일 때도 4조각일 때도 동작해야 하고,
        /// 분류 규칙을 RoomAtmosphereController 와 한 곳에서 맞추기 위해서다.
        /// </summary>
        private static RoomSurfaceController EnsureSurfaceController()
        {
            var go = GameObject.Find(SurfaceObjectName) ?? new GameObject(SurfaceObjectName);
            var controller = go.GetComponent<RoomSurfaceController>()
                             ?? go.AddComponent<RoomSurfaceController>();

            var room = GameObject.Find("room");
            if (room == null)
            {
                Debug.LogError("[FurniturePlacement] 씬에서 room 오브젝트를 찾지 못했습니다.");
                return controller;
            }

            var walls = new List<SpriteRenderer>();
            var floors = new List<SpriteRenderer>();

            foreach (var renderer in room.GetComponentsInChildren<SpriteRenderer>(true))
            {
                // 3D 두께 껍데기는 MeshRenderer 라 여기 걸리지 않지만, 혹시 모를 경우를 위해 건너뛴다.
                if (renderer.transform.parent != null &&
                    renderer.transform.parent.name.StartsWith("__")) continue;

                if (RoomSurfaceController.IsFloorName(renderer.gameObject.name))
                    floors.Add(renderer);
                else
                    walls.Add(renderer);
            }

            var so = new SerializedObject(controller);
            FillComponentList(so.FindProperty("wallRenderers"), walls);
            FillComponentList(so.FindProperty("floorRenderers"), floors);
            so.FindProperty("atmosphere").objectReferenceValue =
                Object.FindFirstObjectByType<RoomAtmosphereController>();

            // 기본값은 비워 둔다. 씬에 이미 깔려 있는 스프라이트를 그대로 쓰고,
            // UI 가 열릴 때 그것과 일치하는 정의를 선택 상태로 잡는다.
            so.FindProperty("defaultWallpaper").objectReferenceValue = null;
            so.FindProperty("defaultFloor").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[FurniturePlacement] 벽 {walls.Count}장, 바닥 {floors.Count}장 연결");

            if (walls.Count == 0 || floors.Count == 0)
                Debug.LogError("[FurniturePlacement] 벽 또는 바닥 렌더러를 찾지 못했습니다. room 하위 구조를 확인하세요.");

            return controller;
        }

        private static void FillComponentList<T>(SerializedProperty property, List<T> items)
            where T : Object
        {
            if (property == null) return;

            property.arraySize = items.Count;
            for (var i = 0; i < items.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // ─────────────────────────────────────────────────────────
        // 씬 오브젝트
        // ─────────────────────────────────────────────────────────

        private static RoomGrid EnsureGrid()
        {
            var go = GameObject.Find(GridObjectName) ?? new GameObject(GridObjectName);
            var grid = go.GetComponent<RoomGrid>() ?? go.AddComponent<RoomGrid>();

            var so = new SerializedObject(grid);
            so.FindProperty("gridSize").vector3IntValue = new Vector3Int(5, 4, 5);
            so.FindProperty("originCorner").vector3Value = new Vector3(-2.5f, 0f, -2.5f);
            so.FindProperty("cellSize").floatValue = 1f;
            so.FindProperty("wallAtMinX").boolValue = true;
            so.FindProperty("wallAtMaxZ").boolValue = true;
            so.FindProperty("wallAtMaxX").boolValue = false;
            so.FindProperty("wallAtMinZ").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            return grid;
        }

        private static FurniturePlacementController EnsureController(RoomGrid grid, Material previewMaterial)
        {
            var go = GameObject.Find(ControllerObjectName) ?? new GameObject(ControllerObjectName);
            var controller = go.GetComponent<FurniturePlacementController>()
                             ?? go.AddComponent<FurniturePlacementController>();

            var furnitureRoot = GameObject.Find("PlacedFurniture");
            if (furnitureRoot == null)
            {
                furnitureRoot = new GameObject("PlacedFurniture");
                furnitureRoot.transform.position = Vector3.zero;
            }

            var so = new SerializedObject(controller);
            so.FindProperty("grid").objectReferenceValue = grid;
            so.FindProperty("targetCamera").objectReferenceValue = Camera.main;
            so.FindProperty("furnitureRoot").objectReferenceValue = furnitureRoot.transform;
            so.FindProperty("previewMaterial").objectReferenceValue = previewMaterial;
            so.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        // ─────────────────────────────────────────────────────────
        // UI
        // ─────────────────────────────────────────────────────────

        private static void BuildUi(FurniturePlacementController controller,
            RoomSurfaceController surfaces,
            List<string> furniturePaths, List<string> wallpaperPaths, List<string> floorPaths)
        {
            var canvas = FindCanvas();
            if (canvas == null)
            {
                Debug.LogError("[FurniturePlacement] Canvas 를 찾지 못했습니다.");
                return;
            }

            // 이전에 만든 UI 는 통째로 지우고 다시 만든다.
            var existing = canvas.transform.Find(UiRootName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(UiRootName, typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            Stretch(root.GetComponent<RectTransform>());

            // ── 왼쪽 위 모드 버튼 ──────────────────────────────
            var modeButton = CreateButton("ModeButton", root.transform, "가구 배치 모드", PrimaryColor, 22);
            var modeRect = modeButton.GetComponent<RectTransform>();
            modeRect.anchorMin = new Vector2(0f, 1f);
            modeRect.anchorMax = new Vector2(0f, 1f);
            modeRect.pivot = new Vector2(0f, 1f);
            modeRect.anchoredPosition = new Vector2(24f, -24f);
            modeRect.sizeDelta = new Vector2(220f, 56f);
            var modeLabel = modeButton.GetComponentInChildren<TMP_Text>(true);

            // ── 오른쪽 절반 인벤토리 ───────────────────────────
            var inventory = CreateImage("Inventory", root.transform, PanelColor);
            var invRect = inventory.rectTransform;
            invRect.anchorMin = new Vector2(0.5f, 0f);
            invRect.anchorMax = new Vector2(1f, 1f);
            invRect.offsetMin = Vector2.zero;
            invRect.offsetMax = Vector2.zero;

            var invTitle = CreateText("Title", inventory.transform, "가구 인벤토리", 28, TextColor, FontStyles.Bold);
            var titleRect = invTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -28f);
            titleRect.sizeDelta = new Vector2(-48f, 44f);
            invTitle.alignment = TextAlignmentOptions.Left;

            var invHint = CreateText("Hint", inventory.transform,
                "설치할 가구를 선택하세요", 18, MutedColor);
            var invHintRect = invHint.rectTransform;
            invHintRect.anchorMin = new Vector2(0f, 1f);
            invHintRect.anchorMax = new Vector2(1f, 1f);
            invHintRect.pivot = new Vector2(0.5f, 1f);
            invHintRect.anchoredPosition = new Vector2(0f, -74f);
            invHintRect.sizeDelta = new Vector2(-48f, 28f);
            invHint.alignment = TextAlignmentOptions.Left;

            // 아이템 컨테이너
            var container = new GameObject("Items", typeof(RectTransform));
            container.transform.SetParent(inventory.transform, false);
            var containerRect = container.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0f, 0f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.offsetMin = new Vector2(24f, 24f);
            containerRect.offsetMax = new Vector2(-24f, -110f);

            var layout = container.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 12f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            // 구역 제목 라벨 원본 (가구 / 벽지 / 바닥)
            var sectionLabel = CreateText("SectionTemplate", container.transform, "구역", 18,
                MutedColor, FontStyles.Bold);
            sectionLabel.rectTransform.sizeDelta = new Vector2(0f, 26f);
            var sectionLayout = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLayout.preferredHeight = 26f;
            sectionLabel.gameObject.SetActive(false);

            // 아이템 버튼 원본
            var template = CreateButton("ItemTemplate", container.transform, "가구", ItemColor, 20);
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.sizeDelta = new Vector2(0f, 72f);
            var templateLayout = template.gameObject.AddComponent<LayoutElement>();
            templateLayout.preferredHeight = 72f;

            // 벽지·바닥 썸네일 자리. 가구는 스프라이트가 없어 꺼진 채로 둔다.
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(template.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(12f, 0f);
            iconRect.sizeDelta = new Vector2(56f, 56f);

            var templateLabel = template.GetComponentInChildren<TMP_Text>(true);
            if (templateLabel != null)
            {
                var tl = templateLabel.rectTransform;
                tl.offsetMin = new Vector2(80f, 0f);
                tl.offsetMax = new Vector2(-12f, 0f);
                templateLabel.alignment = TextAlignmentOptions.Left;
            }

            template.gameObject.SetActive(false);

            // ── 배치 중 안내 ───────────────────────────────────
            var hint = CreateImage("PlacingHint", root.transform, new Color(0f, 0f, 0f, 0.55f));
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 32f);
            hintRect.sizeDelta = new Vector2(560f, 48f);

            var hintText = CreateText("Text", hint.transform,
                "좌클릭 설치   ·   우클릭 취소   ·   R 회전   ·   ESC 종료",
                20, TextColor);
            Stretch(hintText.rectTransform);
            hintText.alignment = TextAlignmentOptions.Center;

            // ── 컴포넌트 연결 ──────────────────────────────────
            var ui = root.AddComponent<FurnitureInventoryUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("placement").objectReferenceValue = controller;
            so.FindProperty("modeButton").objectReferenceValue = modeButton;
            so.FindProperty("modeButtonLabel").objectReferenceValue = modeLabel;
            so.FindProperty("inventoryRoot").objectReferenceValue = inventory.gameObject;
            so.FindProperty("itemContainer").objectReferenceValue = container.transform;
            so.FindProperty("itemButtonTemplate").objectReferenceValue = template;
            so.FindProperty("sectionLabelTemplate").objectReferenceValue = sectionLabel;
            so.FindProperty("hintRoot").objectReferenceValue = hint.gameObject;
            so.FindProperty("surfaces").objectReferenceValue = surfaces;

            FillAssetList<FurnitureDefinition>(so.FindProperty("ownedFurniture"), furniturePaths);
            FillAssetList<RoomSurfaceDefinition>(so.FindProperty("ownedWallpapers"), wallpaperPaths);
            FillAssetList<RoomSurfaceDefinition>(so.FindProperty("ownedFloors"), floorPaths);

            so.ApplyModifiedPropertiesWithoutUndo();

            inventory.gameObject.SetActive(false);
            hint.gameObject.SetActive(false);
        }

        /// <summary>
        /// 경로 목록을 씬 필드의 에셋 참조로 채운다.
        /// 씬을 연 뒤에 불러와야 참조가 null 로 저장되지 않는다.
        /// </summary>
        private static void FillAssetList<T>(SerializedProperty property, List<string> paths)
            where T : Object
        {
            if (property == null) return;

            property.arraySize = paths.Count;
            for (var i = 0; i < paths.Count; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(paths[i]);
                if (asset == null)
                    Debug.LogError($"[FurniturePlacement] 에셋을 불러오지 못했습니다: {paths[i]}");

                property.GetArrayElementAtIndex(i).objectReferenceValue = asset;
            }
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
