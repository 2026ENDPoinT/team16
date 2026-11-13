using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

///비동기 네트워크 통신 매니저)

/// 유니티 6.3 LTS (6000.3.x)의 핵심 비동기 기능인 Awaitable과 UnityWebRequest를 사용하여 메인 스레드 블로킹 없이 데이터를 가져옵니다.

/// 토큰이 있으면 공식 GraphQL API를, 토큰이 없으면(개발 테스트 또는 타 유저 단순 조회) Username 기반 공개 REST API를 자동으로 호출하는 Dual-Mode를 지원합니다.

namespace DormTamagotchi.GitHub
{
    public static class GitHubStreakService
    {
        private const string GraphQLEndpoint = "https://api.github.com/graphql";
        private const string PublicFallbackEndpoint = "https://github-contributions-api.jogruber.de/v4/{0}?y=last";

        /// <summary>
        /// GitHub 계정으로부터 스트릭 데이터와 텍스처 번들을 비동기로 가져옵니다.
        /// </summary>
        /// <param name="username">GitHub 유저 아이디</param>
        /// <param name="oauthToken">연동된 OAuth 토큰 또는 PAT (비워두면 공개 API로 자동 전환)</param>
        public static async Awaitable<GitHubStreakBundle> FetchStreakBundleAsync(
            string username,
            string oauthToken = null,
            GrassThemePalette? customPalette = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("GitHub username이 비어 있습니다.");

            GitHubStreakPayload payload = string.IsNullOrWhiteSpace(oauthToken)
                ? await FetchViaPublicApiAsync(username)
                : await FetchViaGraphQLAsync(username, oauthToken);

            CalculateStreakMetrics(payload);

            GrassThemePalette palette = customPalette ?? GrassThemePalette.GitHubDark;
            return GitHubStreakRenderer.CreateBundle(payload, palette);
        }

        /// <summary>
        /// 1. 공식 GitHub GraphQL API v4 호출 (OAuth 연동 유저용 - Private 기여도 포함 가능)
        /// </summary>
        private static async Awaitable<GitHubStreakPayload> FetchViaGraphQLAsync(string username, string token)
        {
            string query = $@"
            query {{
              user(login: ""{username}"") {{
                contributionsCollection {{
                  contributionCalendar {{
                    totalContributions
                    weeks {{
                      contributionDays {{
                        contributionCount
                        date
                        contributionLevel
                      }}
                    }}
                  }}
                }}
              }}
            }}";

            string requestBody = JsonUtility.ToJson(new GraphQLRequest { query = query });
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);

            using UnityWebRequest request = new UnityWebRequest(GraphQLEndpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.SetRequestHeader("User-Agent", "Unity-DormTamagotchi-Client");

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception($"[GitHub GraphQL Error] {request.error}: {request.downloadHandler.text}");

            GraphQLRoot root = JsonUtility.FromJson<GraphQLRoot>(request.downloadHandler.text);
            var calendar = root?.data?.user?.contributionsCollection?.contributionCalendar;
            if (calendar == null)
                throw new Exception("GitHub GraphQL 응답에서 contributionCalendar를 찾을 수 없습니다.");

            GitHubStreakPayload payload = new GitHubStreakPayload
            {
                username = username,
                fetchedAtIso = DateTime.UtcNow.ToString("o"),
                totalContributions = calendar.totalContributions
            };

            foreach (var week in calendar.weeks)
            {
                foreach (var day in week.contributionDays)
                {
                    payload.days.Add(new DayContribution
                    {
                        date = day.date,
                        count = day.contributionCount,
                        level = ParseGraphQLLevel(day.contributionLevel)
                    });
                }
            }

            return payload;
        }

        /// <summary>
        /// 2. 토큰 없이 Username만으로 호출 가능한 공개 API (빠른 프로토타이핑 및 타 유저 조회용)
        /// </summary>
        private static async Awaitable<GitHubStreakPayload> FetchViaPublicApiAsync(string username)
        {
            string url = string.Format(PublicFallbackEndpoint, UnityWebRequest.EscapeURL(username));

            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.SetRequestHeader("User-Agent", "Unity-DormTamagotchi-Client");

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception($"[GitHub Public API Error] {request.error}");

            PublicApiRoot root = JsonUtility.FromJson<PublicApiRoot>(request.downloadHandler.text);
            if (root?.contributions == null)
                throw new Exception("공개 API 응답 파싱에 실패했습니다.");

            GitHubStreakPayload payload = new GitHubStreakPayload
            {
                username = username,
                fetchedAtIso = DateTime.UtcNow.ToString("o")
            };

            int total = 0;
            foreach (var day in root.contributions)
            {
                total += day.count;
                payload.days.Add(new DayContribution
                {
                    date = day.date,
                    count = day.count,
                    level = (ContributionLevel)Mathf.Clamp(day.level, 0, 4)
                });
            }
            payload.totalContributions = total;

            return payload;
        }

        /// <summary>
        /// 일별 배열을 순회하며 최장 스트릭(longestStreak)과 현재 스트릭(currentStreak)을 계산합니다.
        /// (오늘 아직 커밋하지 않았더라도 어제까지 커밋했다면 현재 스트릭이 끊기지 않은 것으로 판정)
        /// </summary>
        public static void CalculateStreakMetrics(GitHubStreakPayload payload)
        {
            if (payload.days == null || payload.days.Count == 0) return;

            int longest = 0;
            int tempStreak = 0;

            for (int i = 0; i < payload.days.Count; i++)
            {
                if (payload.days[i].count > 0)
                {
                    tempStreak++;
                    if (tempStreak > longest) longest = tempStreak;
                }
                else
                {
                    tempStreak = 0;
                }
            }

            // 현재 스트릭 계산 (맨 끝 날짜부터 역순 탐색)
            int current = 0;
            int lastIdx = payload.days.Count - 1;

            // 오늘(마지막 인덱스) 아직 커밋이 0개라면 어제(lastIdx - 1)부터 확인
            int startIdx = (payload.days[lastIdx].count > 0) ? lastIdx : lastIdx - 1;

            for (int i = startIdx; i >= 0; i--)
            {
                if (payload.days[i].count > 0)
                    current++;
                else
                    break;
            }

            payload.longestStreak = longest;
            payload.currentStreak = current;
        }

        private static ContributionLevel ParseGraphQLLevel(string levelStr) => levelStr switch
        {
            "FIRST_QUARTER"  => ContributionLevel.FirstQuarter,
            "SECOND_QUARTER" => ContributionLevel.SecondQuarter,
            "THIRD_QUARTER"  => ContributionLevel.ThirdQuarter,
            "FOURTH_QUARTER" => ContributionLevel.FourthQuarter,
            _                => ContributionLevel.None
        };
    }
}