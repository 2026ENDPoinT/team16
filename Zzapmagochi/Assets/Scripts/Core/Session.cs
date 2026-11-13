using Zzapmagochi.Net;

namespace Zzapmagochi.Core
{
    /// <summary>
    /// 로그인 세션. 지금은 메모리에만 들고 있다.
    /// 로그인 유지 기능을 붙일 때 refreshToken 저장소를 여기에 추가한다.
    /// PlayerPrefs 는 평문이므로 refreshToken 저장용으로 쓰지 않는다.
    /// </summary>
    public static class Session
    {
        public static string StudentId { get; private set; }
        public static string AccessToken { get; private set; }
        public static string RefreshToken { get; private set; }
        public static string Role { get; private set; } = "user";
        public static bool IsNewUser { get; private set; }

        public static bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);
        public static bool IsAdmin => Role == "admin";

        public static void Set(string studentId, AuthResponse auth)
        {
            StudentId = studentId;
            AccessToken = auth.accessToken;
            RefreshToken = auth.refreshToken;
            Role = string.IsNullOrEmpty(auth.role) ? "user" : auth.role;
            IsNewUser = auth.isNewUser;
        }

        public static void Clear()
        {
            StudentId = null;
            AccessToken = null;
            RefreshToken = null;
            Role = "user";
            IsNewUser = false;
        }
    }
}
