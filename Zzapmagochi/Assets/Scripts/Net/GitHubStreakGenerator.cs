using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Zzapmagochi.Net
{
    public static class GitHubStreakGenerator
    {
        private const string GraphQLEndpoint = "https://api.github.com/graphql";
        private const string PublicFallbackEndpoint = "https://github-contributions-api.jogruber.de/v4/{0}?y=last";

        [Serializable] private class GraphQLRequest { public string query; }
        [Serializable] private class GraphQLRoot { public GraphQLData data; }
        [Serializable] private class GraphQLData { public GraphQLUser user; }
        [Serializable] private class GraphQLUser { public GraphQLCollection contributionsCollection; }
        [Serializable] private class GraphQLCollection { public GraphQLCalendar contributionCalendar; }
        [Serializable] private class GraphQLCalendar { public int totalContributions; public List<GraphQLWeek> weeks; }
        [Serializable] private class GraphQLWeek { public List<GraphQLDay> contributionDays; }
        [Serializable] private class GraphQLDay { public int contributionCount; public string date; public string contributionLevel; }

        [Serializable] private class PublicApiRoot { public List<PublicApiDay> contributions; }
        [Serializable] private class PublicApiDay { public string date; public int count; public int level; }

        /// <summary>
        /// GitHub에서 스트릭 데이터를 가져와 3D 가구용 텍스처(Albedo + Emission) 및 PNG 스트림 번들로 반환합니다.
        /// </summary>
        public static async Task<GitHubStreak3DBundle> Fetch3DBundleAsync(
            string githubUsername,
            string oauthToken = null,
            string studentId = null)
        {
            var payload = await FetchPayloadAsync(githubUsername, oauthToken, studentId);
            return Build3DBundleFromPayload(payload);
        }

        /// <summary>
        /// 원시 스트릭 데이터(JSON DTO)만 가져옵니다.
        /// </summary>
        public static async Task<GitHubStreakPayload> FetchPayloadAsync(
            string githubUsername,
            string oauthToken = null,
            string studentId = null)
        {
            if (string.IsNullOrWhiteSpace(githubUsername))
                throw new ApiException(400, ApiErrorCode.InvalidRequest, "GitHub 아이디를 입력해주세요.");

            GitHubStreakPayload payload = string.IsNullOrWhiteSpace(oauthToken)
                ? await FetchPublicAsync(githubUsername)
                : await FetchGraphQLAsync(githubUsername, oauthToken);

            payload.studentId = studentId;
            CalculateStreaks(payload);
            return payload;
        }

        /// <summary>
        /// 이미 보유한 데이터 스트림(Payload)이나 PNG 바이트를 3D 텍스처 번들로 변환합니다.
        /// </summary>
        public static GitHubStreak3DBundle Build3DBundleFromPayload(
            GitHubStreakPayload payload,
            int cellSize = 8,
            int gap = 2,
            int padding = 8)
        {
            int firstDayOffset = 0;
            if (payload.days.Count > 0 && DateTime.TryParse(payload.days[0].date, out DateTime firstDate))
                firstDayOffset = (int)firstDate.DayOfWeek;

            int totalCells = firstDayOffset + payload.days.Count;
            int totalWeeks = Mathf.CeilToInt(totalCells / 7f);
            const int daysPerWeek = 7;

            int width = padding * 2 + totalWeeks * cellSize + Mathf.Max(0, totalWeeks - 1) * gap;
            int height = padding * 2 + daysPerWeek * cellSize + (daysPerWeek - 1) * gap;

            // 3D 메쉬 위에서 도트 각이 또렷하게 살아나도록 Point 필터 및 AnisoLevel 설정
            Texture2D albedoTex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4
            };

            Texture2D emissionTex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32 bgDark = new Color32(13, 17, 23, 255);      // #0d1117 보드 배경색
            Color32 emptySlot = new Color32(22, 27, 34, 255);   // #161b22 빈 칸(Level 0)
            Color32 black = new Color32(0, 0, 0, 255);          // Emission 없음

            Color32[] albedoPixels = new Color32[width * height];
            Color32[] emissionPixels = new Color32[width * height];

            Array.Fill(albedoPixels, bgDark);
            Array.Fill(emissionPixels, black);

            for (int i = 0; i < payload.days.Count; i++)
            {
                int gridIndex = firstDayOffset + i;
                int weekIndex = gridIndex / 7;
                int dayOfWeek = gridIndex % 7;
                int invertedY = (daysPerWeek - 1) - dayOfWeek; // 일요일이 상단에 오도록 Y축 보정

                int startX = padding + weekIndex * (cellSize + gap);
                int startY = padding + invertedY * (cellSize + gap);

                ContributionLevel lv = payload.days[i].level;
                Color32 cellAlbedo = GetLevelColor(lv, emptySlot);
                // 커밋이 있는 칸(Level 1~4)만 3D 공간에서 자체 발광하도록 Emission 픽셀 기록
                Color32 cellEmission = (lv == ContributionLevel.None) ? black : cellAlbedo;

                for (int y = 0; y < cellSize; y++)
                {
                    int rowOffset = (startY + y) * width;
                    for (int x = 0; x < cellSize; x++)
                    {
                        int idx = rowOffset + startX + x;
                        albedoPixels[idx] = cellAlbedo;
                        emissionPixels[idx] = cellEmission;
                    }
                }
            }

            albedoTex.SetPixels32(albedoPixels);
            albedoTex.Apply(false, false);

            emissionTex.SetPixels32(emissionPixels);
            emissionTex.Apply(false, false);

            byte[] pngStream = albedoTex.EncodeToPNG();
            return new GitHubStreak3DBundle(payload, albedoTex, emissionTex, pngStream);
        }

        private static Color32 GetLevelColor(ContributionLevel level, Color32 emptyColor) => level switch
        {
            ContributionLevel.FirstQuarter  => new Color32(14, 68, 41, 255),   // #0e4429
            ContributionLevel.SecondQuarter => new Color32(0, 109, 50, 255),   // #006d32
            ContributionLevel.ThirdQuarter  => new Color32(38, 166, 65, 255),  // #26a641
            ContributionLevel.FourthQuarter => new Color32(57, 211, 83, 255),  // #39d353
            _                               => emptyColor
        };

        private static async Task<GitHubStreakPayload> FetchPublicAsync(string username)
        {
            string url = string.Format(PublicFallbackEndpoint, UnityWebRequest.EscapeURL(username));
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("User-Agent", "Zzapmagochi-Unity3D");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new ApiException(req.responseCode, ApiErrorCode.NetworkError, "깃허브 정보를 불러오지 못했습니다.");

            var root = JsonUtility.FromJson<PublicApiRoot>(req.downloadHandler.text);
            var payload = new GitHubStreakPayload
            {
                githubUsername = username,
                fetchedAtIso = DateTime.UtcNow.ToString("o")
            };

            int total = 0;
            if (root?.contributions != null)
            {
                foreach (var d in root.contributions)
                {
                    total += d.count;
                    payload.days.Add(new DayContribution
                    {
                        date = d.date,
                        count = d.count,
                        level = (ContributionLevel)Mathf.Clamp(d.level, 0, 4)
                    });
                }
            }
            payload.totalContributions = total;
            return payload;
        }

        private static async Task<GitHubStreakPayload> FetchGraphQLAsync(string username, string token)
        {
            string query = $@"query {{ user(login: ""{username}"") {{ contributionsCollection {{ contributionCalendar {{ totalContributions weeks {{ contributionDays {{ contributionCount date contributionLevel }} }} }} }} }} }}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new GraphQLRequest { query = query }));

            using var req = new UnityWebRequest(GraphQLEndpoint, "POST")
            {
                uploadHandler = new UploadHandlerRaw(bodyRaw),
                downloadHandler = new DownloadHandlerBuffer()
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            req.SetRequestHeader("User-Agent", "Zzapmagochi-Unity3D");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new ApiException(req.responseCode, ApiErrorCode.NetworkError, "GitHub GraphQL 통신 실패");

            var root = JsonUtility.FromJson<GraphQLRoot>(req.downloadHandler.text);
            var cal = root?.data?.user?.contributionsCollection?.contributionCalendar
                      ?? throw new ApiException(404, ApiErrorCode.InvalidRequest, "GitHub 사용자를 찾을 수 없습니다.");

            var payload = new GitHubStreakPayload
            {
                githubUsername = username,
                fetchedAtIso = DateTime.UtcNow.ToString("o"),
                totalContributions = cal.totalContributions
            };

            foreach (var week in cal.weeks)
            {
                foreach (var day in week.contributionDays)
                {
                    payload.days.Add(new DayContribution
                    {
                        date = day.date,
                        count = day.contributionCount,
                        level = day.contributionLevel switch
                        {
                            "FIRST_QUARTER"  => ContributionLevel.FirstQuarter,
                            "SECOND_QUARTER" => ContributionLevel.SecondQuarter,
                            "THIRD_QUARTER"  => ContributionLevel.ThirdQuarter,
                            "FOURTH_QUARTER" => ContributionLevel.FourthQuarter,
                            _                => ContributionLevel.None
                        }
                    });
                }
            }
            return payload;
        }

        public static void CalculateStreaks(GitHubStreakPayload payload)
        {
            if (payload.days == null || payload.days.Count == 0) return;

            int longest = 0, temp = 0;
            foreach (var d in payload.days)
            {
                if (d.count > 0) { temp++; if (temp > longest) longest = temp; }
                else temp = 0;
            }

            int current = 0;
            int lastIdx = payload.days.Count - 1;
            int startIdx = payload.days[lastIdx].count > 0 ? lastIdx : lastIdx - 1;
            for (int i = startIdx; i >= 0; i--)
            {
                if (payload.days[i].count > 0) current++;
                else break;
            }

            payload.longestStreak = longest;
            payload.currentStreak = current;
        }

        /// <summary>오프라인/Mock 테스트용 365일 스트릭 생성</summary>
        public static GitHubStreakPayload CreateMockPayload(string studentId, string githubUsername)
        {
            var payload = new GitHubStreakPayload
            {
                studentId = studentId,
                githubUsername = githubUsername,
                fetchedAtIso = DateTime.UtcNow.ToString("o")
            };

            var rng = new System.Random(studentId != null ? studentId.GetHashCode() : 42);
            DateTime startDate = DateTime.Today.AddDays(-364);
            int total = 0;

            for (int i = 0; i < 365; i++)
            {
                int count = rng.NextDouble() > 0.35 ? rng.Next(1, 12) : 0;
                total += count;
                ContributionLevel lv = count == 0 ? ContributionLevel.None
                    : count < 3 ? ContributionLevel.FirstQuarter
                    : count < 6 ? ContributionLevel.SecondQuarter
                    : count < 9 ? ContributionLevel.ThirdQuarter
                    : ContributionLevel.FourthQuarter;

                payload.days.Add(new DayContribution
                {
                    date = startDate.AddDays(i).ToString("yyyy-MM-dd"),
                    count = count,
                    level = lv
                });
            }
            payload.totalContributions = total;
            CalculateStreaks(payload);
            return payload;
        }
    }
}