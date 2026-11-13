using System.IO;
using UnityEditor;
using UnityEngine;
using Zzapmagochi.Character;

namespace Zzapmagochi.EditorTools
{
    /// <summary>
    /// 캐릭터 프리셋을 손으로 만들 때 쓰는 보조 도구.
    ///
    /// - 빈 프리셋부터 만들고 싶으면: Project 창 우클릭 → Create → Zzapmagochi → Character Preset
    /// - 씬에 세워 둔 캐릭터를 그대로 뜨고 싶으면: 아래 메뉴를 쓴다.
    /// </summary>
    public static class CharacterPresetTools
    {
        private const string PresetDir = "Assets/Data/Character/Presets";

        /// <summary>
        /// 하이어라키에서 CharacterParts 를 가진 오브젝트를 고른 뒤 실행하면
        /// 그 캐릭터의 현재 파츠 구성을 프리셋 에셋으로 저장한다.
        /// 파츠를 손으로 맞춰 본 뒤 에셋으로 굳힐 때 쓴다.
        /// </summary>
        [MenuItem("Zzapmagochi/Character/Create Preset From Selected Character")]
        public static void CreateFromSelection()
        {
            var parts = GetSelectedCharacterParts();
            if (parts == null)
            {
                EditorUtility.DisplayDialog("캐릭터 프리셋",
                    "하이어라키에서 CharacterParts 가 붙은 오브젝트를 선택한 뒤 다시 실행하세요.", "확인");
                return;
            }

            var preset = CreatePresetFrom(parts, parts.gameObject.name.ToLowerInvariant());
            if (preset == null) return;

            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
        }

        [MenuItem("Zzapmagochi/Character/Create Preset From Selected Character", true)]
        private static bool CreateFromSelectionValidate()
        {
            return GetSelectedCharacterParts() != null;
        }

        private static CharacterParts GetSelectedCharacterParts()
        {
            var go = Selection.activeGameObject;
            return go != null ? go.GetComponent<CharacterParts>() : null;
        }

        /// <summary>
        /// CharacterParts 의 characterBody 에 들어 있는 스프라이트로 프리셋을 만든다.
        /// 플레이 중이 아니어도 동작하도록 런타임 렌더러가 아니라 직렬화된 값을 읽는다.
        /// </summary>
        public static CharacterPresetDefinition CreatePresetFrom(CharacterParts parts, string id)
        {
            if (parts == null) return null;

            EnsureFolder();

            var path = AssetDatabase.GenerateUniqueAssetPath($"{PresetDir}/chara_{id}.asset");
            var preset = ScriptableObject.CreateInstance<CharacterPresetDefinition>();

            var body = parts.characterBody;
            preset.id = $"chara_{id}";
            preset.displayName = parts.gameObject.name;
            preset.body = body.body.parts;
            preset.head = body.head.parts;
            preset.arm = body.arm.parts;
            preset.eye = body.eye.parts;
            preset.mouth = body.mouth.parts;
            preset.hairFront = body.hair_front.parts;
            preset.hairBack = body.hair_back.parts;
            preset.legIdle = body.leg_idle.parts;
            preset.legWalk = body.leg_walk.parts;

            // 의상은 스프라이트가 아니라 아이템 에셋을 참조해야 하므로 ID 로 연결한다.
            preset.outer = FindItemBySprite(body.outer.parts);
            preset.accessories = FindItemBySprite(body.accessories.parts);
            preset.clothTop = FindItemBySprite(body.cloth_top.parts);
            preset.clothBottom = FindItemBySprite(body.cloth_bottom_idle.parts);
            preset.shoe = FindItemBySprite(body.shoe_idle.parts);

            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CharacterPreset] 생성: {path}");
            return preset;
        }

        /// <summary>그 스프라이트를 쓰는 의상 아이템을 찾는다. 없으면 null.</summary>
        public static CharacterItemDefinition FindItemBySprite(Sprite sprite)
        {
            if (sprite == null) return null;

            foreach (var guid in AssetDatabase.FindAssets("t:CharacterItemDefinition"))
            {
                var item = AssetDatabase.LoadAssetAtPath<CharacterItemDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (item == null) continue;
                if (item.idleSprite == sprite || item.walkSprite == sprite) return item;
            }

            return null;
        }

        public static void EnsureFolder()
        {
            foreach (var dir in new[] { "Assets/Data", "Assets/Data/Character", PresetDir })
            {
                if (Directory.Exists(dir)) continue;
                var parent = Path.GetDirectoryName(dir)?.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
            }
        }

        public static string Dir => PresetDir;
    }
}
