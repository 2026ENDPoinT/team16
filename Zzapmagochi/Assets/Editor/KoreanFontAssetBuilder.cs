using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Zzapmagochi.EditorTools
{
    /// <summary>
    /// Assets/Art/Fonts 의 한글 TTF/OTF 로 TMP 폰트 에셋을 만든다.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;경로&gt; \
    ///     -executeMethod Zzapmagochi.EditorTools.KoreanFontAssetBuilder.Build
    ///
    /// 아틀라스는 Dynamic 모드로 둔다. 한글은 글자 수가 많아 모든 글리프를 미리 구우면
    /// 아틀라스가 수십 MB로 불어난다. Dynamic 은 실제로 쓰인 글자만 런타임에 채운다.
    /// </summary>
    public static class KoreanFontAssetBuilder
    {
        private const string FontDir = "Assets/Art/Fonts";
        private const string SourceFont = FontDir + "/NotoSansKR-Regular.otf";
        private const string OutputAsset = FontDir + "/NotoSansKR SDF.asset";

        // 아틀라스 설정
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasWidth = 1024;
        private const int AtlasHeight = 1024;

        [MenuItem("Zzapmagochi/Build Scenes/Create Korean TMP Font Asset")]
        public static void Build()
        {
            if (!File.Exists(SourceFont))
            {
                Debug.LogError($"[KoreanFontAssetBuilder] 원본 폰트를 찾을 수 없습니다: {SourceFont}");
                return;
            }

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFont);
            if (source == null)
            {
                Debug.LogError($"[KoreanFontAssetBuilder] 폰트 임포트 실패: {SourceFont}");
                return;
            }

            if (File.Exists(OutputAsset))
            {
                AssetDatabase.DeleteAsset(OutputAsset);
                Debug.Log("[KoreanFontAssetBuilder] 기존 폰트 에셋 삭제 후 재생성");
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                source,
                SamplingPointSize,
                AtlasPadding,
                GlyphRenderMode.SDFAA,
                AtlasWidth,
                AtlasHeight,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError("[KoreanFontAssetBuilder] TMP_FontAsset 생성에 실패했습니다.");
                return;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(OutputAsset);
            AssetDatabase.CreateAsset(fontAsset, OutputAsset);

            // 아틀라스 텍스처와 머티리얼을 폰트 에셋의 하위 에셋으로 넣는다.
            // 이걸 빼먹으면 재생 시 글자가 보이지 않는다.
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
            {
                var atlas = fontAsset.atlasTextures[0];
                atlas.name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            TrySetAsDefault(fontAsset);

            Debug.Log($"[KoreanFontAssetBuilder] 생성 완료: {OutputAsset}");
        }

        /// <summary>
        /// TMP 기본 폰트로 지정한다. 앞으로 새로 만드는 TMP 컴포넌트가 이 폰트를 쓰게 된다.
        /// </summary>
        private static void TrySetAsDefault(TMP_FontAsset fontAsset)
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
            {
                Debug.LogWarning("[KoreanFontAssetBuilder] TMP Settings 가 없어 기본 폰트 지정을 건너뜁니다.");
                return;
            }

            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultFontAsset");
            if (prop == null)
            {
                Debug.LogWarning("[KoreanFontAssetBuilder] m_defaultFontAsset 프로퍼티를 찾지 못했습니다.");
                return;
            }

            prop.objectReferenceValue = fontAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log("[KoreanFontAssetBuilder] TMP 기본 폰트로 지정했습니다.");
        }
    }
}
