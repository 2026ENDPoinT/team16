using System;
using System.Threading.Tasks;

namespace Zzapmagochi.Net
{
    /// <summary>
    /// 서버가 준비되기 전까지 쓰는 목 구현.
    /// 현재 정책: 아무 아이디/비밀번호나 입력해도 로그인·회원가입 모두 성공한다.
    ///
    /// 실패 흐름(로그인 실패 팝업)을 확인하려면 RejectEverything 를 true 로 두면 된다.
    /// 아이디가 "taken" 으로 시작하면 중복 아이디로 취급해 회원가입 팝업의 중복 표시를 확인할 수 있다.
    /// </summary>
    public class MockApiClient : IApiClient
    {
        /// <summary>실패 UI 확인용 스위치. 평소에는 false.</summary>
        public bool RejectEverything { get; set; }

        /// <summary>서버 왕복을 흉내 내는 지연(ms). 로딩 상태 UI가 보이도록 0 이상으로 둔다.</summary>
        public int LatencyMs { get; set; } = 300;

        public async Task<AuthResponse> Login(string studentId, string password)
        {
            await Delay();

            if (RejectEverything)
            {
                throw new ApiException(401, ApiErrorCode.InvalidCredentials,
                    "학번 또는 비밀번호가 올바르지 않습니다.");
            }

            // 서버가 없으므로 입력값 검증 없이 성공 처리한다.
            return NewSession(isNewUser: false);
        }

        public async Task<AuthResponse> SignUp(string studentId, string password)
        {
            await Delay();

            if (RejectEverything)
            {
                throw new ApiException(409, ApiErrorCode.StudentIdTaken,
                    "이미 사용 중인 학번입니다.");
            }

            return NewSession(isNewUser: true);
        }

        public async Task<IdAvailabilityResponse> CheckStudentId(string studentId)
        {
            await Delay();

            var taken = !string.IsNullOrEmpty(studentId) &&
                        studentId.StartsWith("taken", StringComparison.OrdinalIgnoreCase);

            return new IdAvailabilityResponse
            {
                studentId = studentId,
                isAvailable = !taken
            };
        }

        private Task Delay()
        {
            return LatencyMs > 0 ? Task.Delay(LatencyMs) : Task.CompletedTask;
        }

        private static AuthResponse NewSession(bool isNewUser)
        {
            return new AuthResponse
            {
                accessToken = "mock-access-" + Guid.NewGuid().ToString("N"),
                refreshToken = "mock-refresh-" + Guid.NewGuid().ToString("N"),
                expiresIn = 3600,
                isNewUser = isNewUser,
                role = "user"
            };
        }
        //  
        public async Task<GitHubStreakPayload> LinkGitHub(string githubUsername, string oauthToken = null)
        {
            await Delay();
            if (RejectEverything)
                throw new ApiException(400, ApiErrorCode.InvalidRequest, "깃허브 연동에 실패했습니다.");

            // 실제 깃허브 아이디가 입력되었으면 실제 공개 API를 찔러보고, 아니면 Mock 데이터 반환
            return await GitHubStreakGenerator.FetchPayloadAsync(githubUsername, oauthToken, "mock-student");
        }

        public async Task<GitHubStreakPayload> GetGitHubStreak(string studentId)
        {
            await Delay();
            return GitHubStreakGenerator.CreateMockPayload(studentId, "zzap-coder");
        }
            }
}
