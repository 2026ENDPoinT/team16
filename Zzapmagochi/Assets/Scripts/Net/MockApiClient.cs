
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

        private static readonly System.Collections.Generic.List<TodoItemDto> _mockTodos =
            new System.Collections.Generic.List<TodoItemDto>
            {
                new TodoItemDto { id = "t1", type = "assignment", title = "[과제] 트리 및 그래프 탐색 구현 과제", courseName = "자료구조", dueAt = "11월 18일 오후 11:59" },
                new TodoItemDto { id = "t2", type = "lecture", title = "[영상] 11주차 REST API 설계와 비동기 통신", courseName = "웹프로그래밍", dueAt = "11월 19일 오후 11:59" },
                new TodoItemDto { id = "t3", type = "assignment", title = "[과제] 동적 계획법(DP) 연습문제 풀이 제출", courseName = "알고리즘", dueAt = "11월 20일 오후 11:59" },
                new TodoItemDto { id = "t4", type = "quiz", title = "[퀴즈] 프로세스 동기화 및 데드락 주간 퀴즈", courseName = "운영체제", dueAt = "11월 21일 오후 6:00" },
                new TodoItemDto { id = "t5", type = "assignment", title = "[과제] ENDPoinT 해커톤 최종 발표 자료 제출", courseName = "캡스톤디자인", dueAt = "11월 22일 오전 9:00" },
            };

        private static int _mockTodoRemaining = 5;

        public async Task<TodoListResponse> GetTodos()
        {
            await Delay();
            var list = _mockTodos.GetRange(0, _mockTodoRemaining);
            return new TodoListResponse
            {
                items = list,
                total = list.Count,
                previousTotal = list.Count,
                decreasedCount = 0,
                syncedAt = DateTime.UtcNow.ToString("o")
            };
        }

        public async Task<TodoListResponse> SyncTodos(
            string cookieHeader = null,
            string eclassId = null,
            string eclassPassword = null,
            bool openBrowserLogin = false)
        {
            await Delay();
            int prev = _mockTodoRemaining;
            if (_mockTodoRemaining > 0)
                _mockTodoRemaining--;
            else
                _mockTodoRemaining = _mockTodos.Count;

            var list = _mockTodos.GetRange(0, _mockTodoRemaining);
            return new TodoListResponse
            {
                items = list,
                total = list.Count,
                previousTotal = prev,
                decreasedCount = Math.Max(0, prev - list.Count),
                syncedAt = DateTime.UtcNow.ToString("o"),
                source = "demo",
                message = "목(Mock) 모드 할 일 리프레시 완료"
            };
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
