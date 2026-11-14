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

        Task<GitHubStreakPayload> LinkGitHub(string githubUsername, string oauthToken = null);
        Task<GitHubStreakPayload> GetGitHubStreak(string studentId);

        Task<TodoListResponse> GetTodos();
        Task<TodoListResponse> SyncTodos(
            string cookieHeader = null,
            string eclassId = null,
            string eclassPassword = null,
            bool openBrowserLogin = false);
    }
}
