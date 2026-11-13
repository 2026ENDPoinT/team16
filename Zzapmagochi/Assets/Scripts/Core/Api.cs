using Zzapmagochi.Net;

namespace Zzapmagochi.Core
{
    /// <summary>
    /// API 클라이언트 진입점. 실서버가 준비되면 Current 대입 한 줄만 HttpApiClient 로 바꾼다.
    /// </summary>
    public static class Api
    {
        private static IApiClient _current;

        public static IApiClient Current
        {
            get => _current ??= new HttpApiClient("http://127.0.0.1:8000/v1");
            set => _current = value;
        }

        /// <summary>목 구현일 때만 값을 돌려준다. 실패 UI 테스트용.</summary>
        public static MockApiClient AsMock => Current as MockApiClient;
    }
}
