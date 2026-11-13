using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zzapmagochi.Net
{
    public enum ContributionLevel : byte
    {
        None = 0,
        FirstQuarter = 1,
        SecondQuarter = 2,
        ThirdQuarter = 3,
        FourthQuarter = 4
    }

    [Serializable]
    public struct DayContribution
    {
        public string date;               // "YYYY-MM-DD"
        public int count;                 // 커밋 횟수
        public ContributionLevel level;   // 0 ~ 4 단계
    }

    /// <summary>
    /// 서버 DB 캐싱 및 타 유저 자취방 방문 시 주고받는 원시 스트릭 데이터 스트림(DTO)
    /// </summary>
    [Serializable]
    public class GitHubStreakPayload
    {
        public string studentId;
        public string githubUsername;
        public string fetchedAtIso;
        public int totalContributions;
        public int currentStreak;
        public int longestStreak;
        public List<DayContribution> days = new List<DayContribution>();

        public string ToJsonStream() => JsonUtility.ToJson(this);
        public static GitHubStreakPayload FromJsonStream(string json) => JsonUtility.FromJson<GitHubStreakPayload>(json);
    }

    /// <summary>
    /// 3D 가구 오브젝트(MeshRenderer / 입체 타일)에서 즉시 사용할 데이터 + 텍스처 + PNG 스트림 컨테이너
    /// </summary>
    public class GitHubStreak3DBundle : IDisposable
    {
        public GitHubStreakPayload Data { get; }
        public Texture2D AlbedoTexture { get; }     // 3D 머티리얼 _BaseMap 용
        public Texture2D EmissionTexture { get; }   // 3D 머티리얼 _EmissionMap (네온/LED 발광) 용
        public byte[] PngImageStream { get; }       // 파일 저장 또는 네트워크 전송용 PNG 바이트 스트림

        public GitHubStreak3DBundle(
            GitHubStreakPayload data,
            Texture2D albedoTex,
            Texture2D emissionTex,
            byte[] pngStream)
        {
            Data = data;
            AlbedoTexture = albedoTex;
            EmissionTexture = emissionTex;
            PngImageStream = pngStream;
        }

        public void Dispose()
        {
            if (AlbedoTexture != null) UnityEngine.Object.Destroy(AlbedoTexture);
            if (EmissionTexture != null) UnityEngine.Object.Destroy(EmissionTexture);
        }
    }
}