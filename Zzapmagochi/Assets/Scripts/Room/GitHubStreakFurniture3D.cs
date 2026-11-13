using System;
using System.Threading.Tasks;
using UnityEngine;
using Zzapmagochi.Core;
using Zzapmagochi.Net;

namespace Zzapmagochi.Room
{
    public class GitHubStreakFurniture3D : MonoBehaviour
    {
        [Header("3D 가구 화면 (비워두면 자식 'Canvas'를 자동 탐색)")]
        [SerializeField] private MeshRenderer screenMeshRenderer;
        [SerializeField] private SpriteRenderer screenSpriteRenderer;
        [SerializeField] private int screenMaterialIndex = 0;

        [Header("해상도 (가로 2 : 세로 1 유닛 = 400 x 200 px)")]
        [SerializeField] private int textureWidth = 400;
        [SerializeField] private int textureHeight = 200;

        [Tooltip("선택사항: 400x200 배경 이미지(Read/Write Enabled)를 넣으면 그 위에 잔디를 찍습니다.")]
        [SerializeField] private Texture2D customBackgroundTexture;

        [Header("3D 발광(Emission) 강도 설정")]
        [SerializeField] private bool enableEmission = true;
        [SerializeField, Range(0f, 5f)] private float emissionIntensity = 1.4f;

        [Header("테스트용 GitHub ID (비워두면 Session 학번 기반 Mock 로드)")]
        [SerializeField] private string testGithubUsername = "torvalds";

        private static GitHubStreakPayload _cachedPayload;
        private static string _cachedKey;

        private GitHubStreak3DBundle _bundle;
        private Sprite _generatedSprite;
        private MaterialPropertyBlock _mpb;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            AutoFindRenderers();
        }

        private async void Start()
        {
            await RefreshStreakBoardAsync(testGithubUsername);
        }

        private void AutoFindRenderers()
        {
            if (screenMeshRenderer == null)
            {
                var canvasChild = transform.Find("Canvas");
                if (canvasChild != null)
                    screenMeshRenderer = canvasChild.GetComponent<MeshRenderer>();

                if (screenMeshRenderer == null)
                    screenMeshRenderer = GetComponentInChildren<MeshRenderer>();
            }

            if (screenSpriteRenderer == null)
            {
                screenSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        public async Task RefreshStreakBoardAsync(string githubUsername = null, string oauthToken = null, bool forceReload = false)
        {
            try
            {
                string studentId = Session.StudentId ?? "20260001";
                string cacheKey = !string.IsNullOrWhiteSpace(githubUsername)
                    ? $"gh:{githubUsername}"
                    : $"st:{studentId}";

                if (forceReload || _cachedPayload == null || _cachedKey != cacheKey)
                {
                    if (!string.IsNullOrWhiteSpace(githubUsername))
                    {
                        try
                        {
                            _cachedPayload = await GitHubStreakGenerator.FetchPayloadAsync(
                                githubUsername, oauthToken, studentId);
                        }
                        catch
                        {
                            _cachedPayload = GitHubStreakGenerator.CreateMockPayload(studentId, githubUsername);
                        }
                    }
                    else
                    {
                        _cachedPayload = await Api.Current.GetGitHubStreak(studentId);
                    }

                    _cachedKey = cacheKey;
                }

                if (this == null) return;

                CleanupGeneratedAssets();

                _bundle = GitHubStreakGenerator.Build3DBundleFromPayload(
                    _cachedPayload,
                    textureWidth,
                    textureHeight,
                    customBackgroundTexture);

                ApplyBundleToRenderer(_bundle);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GitHubStreakFurniture3D] 스트릭 가구 로드 실패: {e.Message}");
            }
        }

        private void ApplyBundleToRenderer(GitHubStreak3DBundle bundle)
        {
            if (bundle == null) return;

            if (screenMeshRenderer != null)
            {
                screenMeshRenderer.GetPropertyBlock(_mpb, screenMaterialIndex);

                _mpb.SetTexture(BaseMapId, bundle.AlbedoTexture);
                _mpb.SetTexture(MainTexId, bundle.AlbedoTexture);

                if (enableEmission && bundle.EmissionTexture != null)
                {
                    _mpb.SetTexture(EmissionMapId, bundle.EmissionTexture);
                    _mpb.SetColor(EmissionColorId, Color.white * emissionIntensity);
                }
                else
                {
                    _mpb.SetColor(EmissionColorId, Color.black);
                }

                screenMeshRenderer.SetPropertyBlock(_mpb, screenMaterialIndex);
            }

            if (screenSpriteRenderer != null && bundle.AlbedoTexture != null)
            {
                _generatedSprite = Sprite.Create(
                    bundle.AlbedoTexture,
                    new Rect(0, 0, bundle.AlbedoTexture.width, bundle.AlbedoTexture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit: 200f);

                screenSpriteRenderer.sprite = _generatedSprite;
            }
        }

        private void CleanupGeneratedAssets()
        {
            if (_generatedSprite != null)
            {
                Destroy(_generatedSprite);
                _generatedSprite = null;
            }

            _bundle?.Dispose();
            _bundle = null;
        }

        private void OnDestroy()
        {
            CleanupGeneratedAssets();
        }
    }
}
