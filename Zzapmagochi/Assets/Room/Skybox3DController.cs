using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Zzapmagochi.Room
{
    [Serializable]
    public struct Skybox3DTheme
    {
        public string themeName;
        public Color centerColor;
        public Color edgeColor;
        public Color starColor;
        public Vector2 scrollSpeed;
    }

    public class Skybox3DController : MonoBehaviour
    {
        [SerializeField] private Material skyboxAsset;
        [SerializeField] private Skybox3DTheme[] themes =
        {
            new Skybox3DTheme
            {
                themeName = "Default Purple (스케치 기본)",
                centerColor = new Color32(115, 97, 133, 255),
                edgeColor   = new Color32(82, 64, 102, 255),
                starColor   = new Color32(158, 138, 184, 140),
                scrollSpeed = new Vector2(0.03f, 0.02f)
            },
            new Skybox3DTheme
            {
                themeName = "Panic Room Mint-Pink",
                centerColor = new Color32(232, 245, 248, 255),
                edgeColor   = new Color32(205, 228, 235, 255),
                starColor   = new Color32(170, 210, 225, 160),
                scrollSpeed = new Vector2(0.02f, 0.015f)
            }
        };

        private Material _runtimeSkybox;

        private static readonly int BgCenterId = Shader.PropertyToID("_BgColorCenter");
        private static readonly int BgEdgeId = Shader.PropertyToID("_BgColorEdge");
        private static readonly int PatternColorId = Shader.PropertyToID("_PatternColor");
        private static readonly int ScrollVelId = Shader.PropertyToID("_ScrollVelocity");

        private void Awake()
        {
            var sourceMat = skyboxAsset != null ? skyboxAsset : RenderSettings.skybox;
            if (sourceMat != null)
            {
                // 원본 에셋 파일이 수정되지 않도록 런타임 복사본 생성 후 RenderSettings에 등록
                _runtimeSkybox = new Material(sourceMat);
                RenderSettings.skybox = _runtimeSkybox;
            }
        }

        private void Start()
        {
            if (themes != null && themes.Length > 0)
                ApplyTheme(themes[0]);
        }

        public void ApplyTheme(Skybox3DTheme theme)
        {
            if (_runtimeSkybox == null) return;

            _runtimeSkybox.SetColor(BgCenterId, theme.centerColor);
            _runtimeSkybox.SetColor(BgEdgeId, theme.edgeColor);
            _runtimeSkybox.SetColor(PatternColorId, theme.starColor);
            _runtimeSkybox.SetVector(ScrollVelId, theme.scrollSpeed);
        }

        public async Task TransitionThemeAsync(int index, float duration = 0.5f)
        {
            if (_runtimeSkybox == null || index < 0 || index >= themes.Length) return;

            var target = themes[index];
            Color startCenter = _runtimeSkybox.GetColor(BgCenterId);
            Color startEdge = _runtimeSkybox.GetColor(BgEdgeId);
            Color startStar = _runtimeSkybox.GetColor(PatternColorId);

            float elapsed = 0f;
            while (elapsed < duration && _runtimeSkybox != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                _runtimeSkybox.SetColor(BgCenterId, Color.Lerp(startCenter, target.centerColor, t));
                _runtimeSkybox.SetColor(BgEdgeId, Color.Lerp(startEdge, target.edgeColor, t));
                _runtimeSkybox.SetColor(PatternColorId, Color.Lerp(startStar, target.starColor, t));

                await Task.Yield();
            }

            if (_runtimeSkybox != null)
                ApplyTheme(target);
        }

        private void OnDestroy()
        {
            if (_runtimeSkybox != null)
                Destroy(_runtimeSkybox);
        }
    }
}