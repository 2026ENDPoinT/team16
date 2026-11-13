using System;
using UnityEngine;
using Zzapmagochi.Core;
using Zzapmagochi.Net;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// 3D 자취방 벽면의 깃허브 스트릭 가구(액자/전광판) 컨트롤러
    /// </summary>
    public class GitHubStreakFurniture3D : MonoBehaviour
    {
        [Header("3D 가구 화면 MeshRenderer (Quad 또는 액자 내부 스크린 메쉬)")]
        [SerializeField] private MeshRenderer screenMeshRenderer;
        [SerializeField] private int screenMaterialIndex = 0;

        [Header("3D 발광(Emission) 강도 설정")]
        [SerializeField] private bool enableEmission = true;
        [SerializeField, Range(0f, 5f)] private float emissionIntensity = 1.4f;

        [Header("테스트용 GitHub ID (비워두면 Session 학번 기반 Mock 로드)")]
        [SerializeField] private string testGithubUsername = "torvalds";

        private GitHubStreak3DBundle _bundle;
        private MaterialPropertyBlock _mpb;

        // URP Lit / Unlit 공통 셰이더 프로퍼티 ID
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        private async void Start()
        {
            await RefreshStreakBoardAsync(testGithubUsername);
        }

        /// <summary>
        /// 외부(설정창 연동 완료 시점 또는 타 유저 방 입장 시점)에서 호출하여 3D 가구 화면을 갱신합니다.
        /// </summary>
        public async System.Threading.Tasks.Task RefreshStreakBoardAsync(string githubUsername = null, string oauthToken = null)
        {
            try
            {
                _bundle?.Dispose();

                if (!string.IsNullOrWhiteSpace(githubUsername))
                {
                    // 실제 GitHub 데이터 스트림 + 3D 텍스처 번들 생성
                    _bundle = await GitHubStreakGenerator.Fetch3DBundleAsync(
                        githubUsername, oauthToken, Session.StudentId);
                }
                else
                {
                    // 연동 정보가 없을 때는 Api.Current (MockApiClient 등)를 통해 학번 기반으로 로드
                    var payload = await Api.Current.GetGitHubStreak(Session.StudentId ?? "20260001");
                    _bundle = GitHubStreakGenerator.Build3DBundleFromPayload(payload);
                }

                ApplyBundleTo3DMesh(_bundle);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GitHubStreakFurniture3D] 스트릭 가구 로드 실패: {e.Message}");
            }
        }

        private void ApplyBundleTo3DMesh(GitHubStreak3DBundle bundle)
        {
            if (screenMeshRenderer == null || bundle == null) return;

            screenMeshRenderer.GetPropertyBlock(_mpb, screenMaterialIndex);

            // Albedo (기본 텍스처) 주입
            _mpb.SetTexture(BaseMapId, bundle.AlbedoTexture);
            _mpb.SetTexture(MainTexId, bundle.AlbedoTexture);

            // Emission (어두운 3D 방에서도 잔디가 빛나도록 발광 텍스처 주입)
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

        private void OnDestroy()
        {
            _bundle?.Dispose();
        }
    }
}