using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Zzapmagochi.Core;

namespace Zzapmagochi.Net
{
    public class HttpApiClient : IApiClient
    {
        public string BaseUrl { get; set; }

        [Serializable]
        private class ErrorEnvelope
        {
            public ErrorDetail error;
        }

        [Serializable]
        private class ErrorDetail
        {
            public string code;
            public string message;
        }

        public HttpApiClient(
            string baseUrl = "http://127.0.0.1:8000/v1")
        {
            BaseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<AuthResponse> Login(
            string studentId,
            string password)
        {
            var body = new LoginRequest
            {
                studentId = studentId,
                password = password
            };

            var res = await SendJsonAsync<AuthResponse>(
                "POST",
                $"{BaseUrl}/auth/login",
                JsonUtility.ToJson(body));

            res.isNewUser = false;
            return res;
        }

        public async Task<AuthResponse> SignUp(
            string studentId,
            string password)
        {
            var body = new SignUpRequest
            {
                studentId = studentId,
                password = password
            };

            var res = await SendJsonAsync<AuthResponse>(
                "POST",
                $"{BaseUrl}/auth/signup",
                JsonUtility.ToJson(body));

            res.isNewUser = true;
            return res;
        }

        public async Task<IdAvailabilityResponse> CheckStudentId(
            string studentId)
        {
            var escaped = UnityWebRequest.EscapeURL(
                studentId ?? string.Empty);
            var url = $"{BaseUrl}/auth/check-id?studentId={escaped}";
            return await SendJsonAsync<IdAvailabilityResponse>(
                "GET",
                url,
                null);
        }

        public async Task<MeResponse> GetMe()
        {
            return await SendJsonAsync<MeResponse>(
                "GET",
                $"{BaseUrl}/me",
                null);
        }

        public async Task<RoomLayoutDto> GetMyRoom()
        {
            return await SendJsonAsync<RoomLayoutDto>(
                "GET",
                $"{BaseUrl}/room/me",
                null);
        }

        public async Task<RoomLayoutDto> UpdateRoom(
            RoomUpdateRequest request)
        {
            return await SendJsonAsync<RoomLayoutDto>(
                "PUT",
                $"{BaseUrl}/room/me",
                JsonUtility.ToJson(request));
        }

        public async Task<CharacterDataDto> UpdateCharacter(
            CharacterUpdateRequest request)
        {
            return await SendJsonAsync<CharacterDataDto>(
                "PUT",
                $"{BaseUrl}/me/character",
                JsonUtility.ToJson(request));
        }

        public async Task<GitHubStreakPayload> LinkGitHub(
            string githubUsername,
            string oauthToken = null)
        {
            return await GitHubStreakGenerator.FetchPayloadAsync(
                githubUsername,
                oauthToken,
                "student");
        }

        public async Task<GitHubStreakPayload> GetGitHubStreak(
            string studentId)
        {
            await Task.Yield();
            return GitHubStreakGenerator.CreateMockPayload(
                studentId,
                "zzap-coder");
        }

        public async Task<TodoListResponse> GetTodos()
        {
            return await SendJsonAsync<TodoListResponse>(
                "GET",
                $"{BaseUrl}/todos",
                null);
        }

        public async Task<TodoListResponse> SyncTodos(
            string cookieHeader = null,
            string eclassId = null,
            string eclassPassword = null,
            bool openBrowserLogin = false)
        {
            var body = new TodoSyncRequest
            {
                cookieHeader = cookieHeader,
                eclassId = eclassId,
                eclassPassword = eclassPassword,
                openBrowserLogin = openBrowserLogin,
                simulateCompleteOne = true
            };
            int timeoutSec = (openBrowserLogin || !string.IsNullOrEmpty(eclassId)) ? 95 : 15;
            return await SendJsonAsync<TodoListResponse>(
                "POST",
                $"{BaseUrl}/todos/sync",
                JsonUtility.ToJson(body),
                timeoutSec);
        }

        private static async Task<T> SendJsonAsync<T>(
            string method,
            string url,
            string jsonBody,
            int timeoutSeconds = 10)
        {
            using var req = new UnityWebRequest(url, method);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = timeoutSeconds;

            if (!string.IsNullOrEmpty(jsonBody))
            {
                var raw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.SetRequestHeader(
                    "Content-Type",
                    "application/json");
            }

            req.SetRequestHeader("Accept", "application/json");

            if (Session.IsAuthenticated)
            {
                req.SetRequestHeader(
                    "Authorization",
                    $"Bearer {Session.AccessToken}");
            }

            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            var responseText = req.downloadHandler != null
                ? req.downloadHandler.text
                : string.Empty;

            if (req.result == UnityWebRequest.Result.ConnectionError ||
                req.result == UnityWebRequest.Result.DataProcessingError)
            {
                throw new ApiException(
                    0,
                    ApiErrorCode.NetworkError,
                    "서버에 연결할 수 없습니다.");
            }

            if (req.responseCode >= 400)
            {
                string code = ApiErrorCode.InvalidRequest;
                string message = "요청 처리에 실패했습니다.";

                if (!string.IsNullOrEmpty(responseText))
                {
                    try
                    {
                        var env = JsonUtility.FromJson<ErrorEnvelope>(
                            responseText);
                        if (env != null && env.error != null)
                        {
                            if (!string.IsNullOrEmpty(env.error.code))
                                code = env.error.code;
                            if (!string.IsNullOrEmpty(env.error.message))
                                message = env.error.message;
                        }
                    }
                    catch
                    {
                    }
                }

                throw new ApiException(req.responseCode, code, message);
            }

            return JsonUtility.FromJson<T>(responseText);
        }
    }
}
