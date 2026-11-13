
using System;
using System.Threading.Tasks;

namespace Zzapmagochi.Net
{
    public class MockApiClient : IApiClient
    {
        public bool RejectEverything { get; set; }
        public int LatencyMs { get; set; } = 300;

        public async Task<AuthResponse> Login(
            string studentId,
            string password)
        {
            await Delay();
            if (RejectEverything)
            {
                throw new ApiException(
                    401,
                    ApiErrorCode.InvalidCredentials,
                    "학번 또는 비밀번호가 올바르지 않습니다.");
            }
            return NewSession(isNewUser: false);
        }

        public async Task<AuthResponse> SignUp(
            string studentId,
            string password)
        {
            await Delay();
            if (RejectEverything)
            {
                throw new ApiException(
                    409,
                    ApiErrorCode.StudentIdTaken,
                    "이미 사용 중인 학번입니다.");
            }
            return NewSession(isNewUser: true);
        }

        public async Task<IdAvailabilityResponse> CheckStudentId(
            string studentId)
        {
            await Delay();
            var taken = !string.IsNullOrEmpty(studentId) &&
                studentId.StartsWith(
                    "taken",
                    StringComparison.OrdinalIgnoreCase);
            return new IdAvailabilityResponse
            {
                studentId = studentId,
                isAvailable = !taken
            };
        }

        public async Task<MeResponse> GetMe()
        {
            await Delay();
            return new MeResponse();
        }

        public async Task<RoomLayoutDto> GetMyRoom()
        {
            await Delay();
            return new RoomLayoutDto();
        }

        public async Task<RoomLayoutDto> UpdateRoom(
            RoomUpdateRequest request)
        {
            await Delay();
            return new RoomLayoutDto
            {
                wallpaperId = request.wallpaperId,
                floorId = request.floorId,
                placements = request.placements
            };
        }

        public async Task<CharacterDataDto> UpdateCharacter(
            CharacterUpdateRequest request)
        {
            await Delay();
            return new CharacterDataDto
            {
                presetId = request.presetId,
                parts = request.parts
            };
        }

        public async Task<GitHubStreakPayload> LinkGitHub(
            string githubUsername,
            string oauthToken = null)
        {
            await Delay();
            if (RejectEverything)
            {
                throw new ApiException(
                    400,
                    ApiErrorCode.InvalidRequest,
                    "깃허브 연동에 실패했습니다.");
            }
            return await GitHubStreakGenerator.FetchPayloadAsync(
                githubUsername,
                oauthToken,
                "mock-student");
        }

        public async Task<GitHubStreakPayload> GetGitHubStreak(
            string studentId)
        {
            await Delay();
            return GitHubStreakGenerator.CreateMockPayload(
                studentId,
                "zzap-coder");
        }

        private Task Delay() =>
            LatencyMs > 0 ? Task.Delay(LatencyMs) : Task.CompletedTask;

        private static AuthResponse NewSession(bool isNewUser)
        {
            return new AuthResponse
            {
                accessToken = "mock-" + Guid.NewGuid().ToString("N"),
                refreshToken = "mock-" + Guid.NewGuid().ToString("N"),
                expiresIn = 3600,
                isNewUser = isNewUser,
                role = "user"
            };
        }
    }
}
