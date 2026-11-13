using System;
using System.Collections.Generic;
using UnityEngine;

///GitHub API 응답 역직렬화 모델 및 가구 시스템이 참조할 최종 데이터(GitHubStreakResult: 총 커밋 수, 현재 연속 스트릭, 최대 스트릭, 일별 데이터 리스트, 직렬화된 JSON 스트림).
/// 
/// 
/// 1. GitHub GraphQL API v4 (공식, OAuth Access Token 또는 PAT 사용) 유저가 설정에서 깃허브 계정을 연동(OAuth 2.0)했을 때 발급받은 토큰으로 contributionsCollection 쿼리를 호출합니다.
/// 장점: 비공개 레포지토리(Private Repo) 커밋까지 포함된 정확한 잔디 데이터와 일별 커밋 수, 레벨(NONE ~ FOURTH_QUARTER)을 공식 스키마로 받아옵니다.
/// 공개 계정 기반 JSON 프록시 API (토큰 불필요, Username만 사용)

/// 오픈소스 API([https://github-contributions-api.jogruber.de/v4/](https://github-contributions-api.jogruber.de/v4/){username}?y=last)를 호출해 공개 기여도 데이터를 JSON으로 받아옵니다.
/// 장점: 개발 초기 단계에서 백엔드 OAuth 서버 구축 없이 깃허브 아이디 문자열만 입력하면 즉시 테스트할 수 있습니다.

namespace DormTamagotchi.GitHub
{
    /// <summary>
    /// 잔디 진하기 단계 (0: 없음 ~ 4: 매우 많음)
    /// </summary>
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
        public int count;                 // 커밋/기여 횟수
        public ContributionLevel level;   // 0 ~ 4 단계
    }

    /// <summary>
    /// 가구 오브젝트나 UI에서 자유롭게 꺼내 쓸 최종 스트릭 데이터 패키지
    /// (JSON 직렬화 가능 -> 서버 저장 및 데이터 스트림 전송용)
    /// </summary>
    [Serializable]
    public class GitHubStreakPayload
    {
        public string username;
        public string fetchedAtIso;
        public int totalContributions;
        public int currentStreak;         // 현재 연속 커밋 일수
        public int longestStreak;         // 최장 연속 커밋 일수
        public List<DayContribution> days = new List<DayContribution>();

        // 바이트 스트림이나 로컬 캐시 저장을 위한 직렬화 헬퍼
        public string ToJsonStream() => JsonUtility.ToJson(this);
        public static GitHubStreakPayload FromJsonStream(string json) => JsonUtility.FromJson<GitHubStreakPayload>(json);
    }

    /// <summary>
    /// 데이터 스트림과 유니티 텍스처/이미지 바이트를 모두 담는 컨테이너
    /// </summary>
    public class GitHubStreakBundle : IDisposable
    {
        public GitHubStreakPayload Data { get; private set; }
        public Texture2D GrassTexture { get; private set; }
        public Sprite GrassSprite { get; private set; }
        public byte[] PngImageStream { get; private set; }

        public GitHubStreakBundle(GitHubStreakPayload data, Texture2D texture, Sprite sprite, byte[] pngStream)
        {
            Data = data;
            GrassTexture = texture;
            GrassSprite = sprite;
            PngImageStream = pngStream;
        }

        public void Dispose()
        {
            if (GrassSprite != null) UnityEngine.Object.Destroy(GrassSprite);
            if (GrassTexture != null) UnityEngine.Object.Destroy(GrassTexture);
        }
    }

    #region Internal API Response DTOs (JsonUtility 호환용)

    // 1. GitHub 공식 GraphQL v4 응답 구조
    [Serializable] internal class GraphQLRequest { public string query; }
    [Serializable] internal class GraphQLRoot { public GraphQLData data; }
    [Serializable] internal class GraphQLData { public GraphQLUser user; }
    [Serializable] internal class GraphQLUser { public GraphQLCollection contributionsCollection; }
    [Serializable] internal class GraphQLCollection { public GraphQLCalendar contributionCalendar; }
    [Serializable] internal class GraphQLCalendar
    {
        public int totalContributions;
        public List<GraphQLWeek> weeks;
    }
    [Serializable] internal class GraphQLWeek { public List<GraphQLDay> contributionDays; }
    [Serializable] internal class GraphQLDay
    {
        public int contributionCount;
        public string date;
        public string contributionLevel;
    }

    // 2. 토큰 없는 공개 API (jogruber API) 응답 구조
    [Serializable] internal class PublicApiRoot
    {
        public List<PublicApiDay> contributions;
    }
    [Serializable] internal class PublicApiDay
    {
        public string date;
        public int count;
        public int level;
    }

    #endregion
}