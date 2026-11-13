using System.Threading.Tasks;

namespace Zzapmagochi.Net
{
    /// <summary>
    /// 서버 API 계약. 구현은 MockApiClient(목) / HttpApiClient(실서버) 두 가지.
    /// 목을 쓰더라도 반드시 비동기 시그니처를 유지한다. 동기로 만들면 실서버를 붙일 때 호출부를 전부 고쳐야 한다.
    /// </summary>
    public interface IApiClient
    {
        /// <summary>POST /auth/login</summary>
        Task<AuthResponse> Login(string studentId, string password);

        /// <summary>POST /auth/signup</summary>
        Task<AuthResponse> SignUp(string studentId, string password);

        /// <summary>GET /auth/check-id?studentId=...</summary>
        Task<IdAvailabilityResponse> CheckStudentId(string studentId);

        // [1] Net/IApiClient.cs 내부에 추가
        Task<GitHubStreakPayload> LinkGitHub(string githubUsername, string oauthToken = null);
        Task<GitHubStreakPayload> GetGitHubStreak(string studentId);
    }
}
