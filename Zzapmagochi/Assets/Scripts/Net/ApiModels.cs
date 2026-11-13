using System;

namespace Zzapmagochi.Net
{
    // API문서.md 2장 — 인증
    // 필드 이름은 API 명세와 1:1로 맞춘다. 바꿀 때는 문서를 먼저 고친다.

    [Serializable]
    public class LoginRequest
    {
        public string studentId;
        public string password;
    }

    [Serializable]
    public class SignUpRequest
    {
        public string studentId;
        public string password;
    }

    [Serializable]
    public class AuthResponse
    {
        public string accessToken;
        public string refreshToken;
        public int expiresIn;
        public bool isNewUser;
        public string role;
    }

    [Serializable]
    public class IdAvailabilityResponse
    {
        public string studentId;
        public bool isAvailable;
    }

    /// <summary>API문서.md 1.4 — 에러 응답. 분기는 Code로 한다. Message는 사용자에게 그대로 보여주는 문구.</summary>
    public class ApiException : Exception
    {
        public long StatusCode { get; }
        public string Code { get; }

        public ApiException(long statusCode, string code, string message) : base(message)
        {
            StatusCode = statusCode;
            Code = code;
        }
    }

    public static class ApiErrorCode
    {
        public const string InvalidCredentials = "INVALID_CREDENTIALS";
        public const string InvalidRequest = "INVALID_REQUEST";
        public const string RateLimited = "RATE_LIMITED";
        public const string StudentIdTaken = "STUDENT_ID_TAKEN";
        public const string NetworkError = "NETWORK_ERROR";
    }
}
