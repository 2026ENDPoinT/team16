using System.Threading.Tasks;

namespace Zzapmagochi.Net
{
    /// <summary>
    /// 서버 API 계약. 구현은 MockApiClient(목) / HttpApiClient(실서버) 두 가지.
    /// </summary>
    public interface IApiClient
    {
        Task<AuthResponse> Login(string studentId, string password);
        Task<AuthResponse> SignUp(string studentId, string password);
        Task<IdAvailabilityResponse> CheckStudentId(string studentId);

        Task<MeResponse> GetMe();
        Task<RoomLayoutDto> GetMyRoom();
        Task<RoomLayoutDto> UpdateRoom(RoomUpdateRequest request);
        Task<CharacterDataDto> UpdateCharacter(CharacterUpdateRequest request);

        /// <summary>GET /room/random/student-id — 방문할 다른 학생을 무작위로 고른다.</summary>
        Task<RandomStudentIdResponse> GetRandomStudentId();

        /// <summary>GET /room/{studentId} — 그 학생의 캐릭터와 방을 가져온다.</summary>
        Task<RoomVisitResponse> GetUserRoom(string studentId);

        Task<GitHubStreakPayload> LinkGitHub(string githubUsername, string oauthToken = null);
        Task<GitHubStreakPayload> GetGitHubStreak(string studentId);
    }
}
