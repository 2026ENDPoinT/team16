using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SeoulTechEclass
{
    /// <summary>
    /// 앱 자체 입력창(ID/PW)에서 로그인하는 경량 어댑터.
    /// 브라우저/Playwright는 필요 없다. 학교가 JS 암호화·SSO·2차 인증을 요구하면
    /// HTML 폼 POST만으로 인증할 수 없으므로 학교 로그인 요청 명세에 맞게 수정해야 한다.
    /// </summary>
    public sealed class EclassLoginService
    {
        private static readonly Uri Origin = new Uri("https://eclass.seoultech.ac.kr/");
        private static readonly Uri LoginPage = new Uri(Origin, "ilos/main/member/login_form.acl");
        private readonly EclassCrawler crawler;

        public EclassLoginService(EclassCrawler crawler)
        {
            this.crawler = crawler ?? throw new ArgumentNullException(nameof(crawler));
        }

        /// <summary>
        /// 로그인 성공을 홈 페이지의 로그아웃/할 일 UI로 확인한 뒤에만 쿠키 저장.
        /// 비밀번호는 파일로 저장하지 않는다.
        /// </summary>
        public async Task LoginAsync(string id, string password, CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("학번과 비밀번호를 입력하세요.");

            CookieContainer jar = new CookieContainer();
            using (HttpClientHandler handler = new HttpClientHandler())
            {
                handler.CookieContainer = jar;
                handler.UseCookies = true;
                // 비밀번호를 포함한 POST가 307/308로 외부 서버에 전달되는 것을 방지한다.
                handler.AllowAutoRedirect = false;

                using (HttpClient client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(25);
                    EclassCrawler.SetBrowserHeaders(client);

                    // 먼저 공식 메인 URL에 접속한다. 로그인하지 않은 경우 학교가
                    // 안내하는 로그인 화면/리다이렉트를 따라가므로 URL을 추측하지 않는다.
                    PageResponse landing = await GetPageAsync(client, new Uri(EclassCrawler.HomeUrl), token);
                    if (EclassCrawler.IsAuthenticatedHome(landing.Html))
                    {
                        Persist(jar);
                        return;
                    }

                    FormLogin form = ReadLoginForm(landing.Html, landing.Url);
                    if (form == null)
                    {
                        // 메인 페이지에 로그인 폼이 없을 때의 두 번째 공식 경로 후보.
                        // 경로가 없는 경우 실패를 그대로 알리고, 임의 URL로 비밀번호를 보내지 않는다.
                        landing = await GetPageAsync(client, LoginPage, token);
                        form = ReadLoginForm(landing.Html, landing.Url);
                    }
                    if (form == null)
                        throw new EclassLoginException(
                            "학교 로그인 폼을 찾지 못했습니다. 제공된 outerHTML은 로그인 후 화면입니다. " +
                            "로그인 전 HTML 또는 Network의 로그인 요청 방식 확인이 필요합니다.");

                    form.Values[form.IdField] = id.Trim();
                    form.Values[form.PasswordField] = password;
                    if (!form.Values.ContainsKey("encoding")) form.Values["encoding"] = "utf-8";

                    using (HttpRequestMessage post = new HttpRequestMessage(HttpMethod.Post, form.Action))
                    {
                        post.Content = new FormUrlEncodedContent(form.Values);
                        using (HttpResponseMessage response = await client.SendAsync(post, token))
                        {
                            if (IsRedirect(response.StatusCode))
                            {
                                Uri next = RedirectUrl(response, form.Action);
                                await GetPageAsync(client, next, token);
                            }
                            else response.EnsureSuccessStatusCode();
                        }
                    }

                    // POST가 HTTP 200인 것만으로 인증 성공이라고 판단하면 안 된다.
                    string home = (await GetPageAsync(client, new Uri(EclassCrawler.HomeUrl), token)).Html;
                    if (!EclassCrawler.IsAuthenticatedHome(home))
                        throw new EclassLoginException(
                            "학교 로그인이 확인되지 않았습니다. 아이디/비밀번호를 확인하세요. " +
                            "eClass에서 SSO·자바스크립트 인증을 요구한다면 로그인 요청에 맞는 추가 구현이 필요합니다.");

                    Persist(jar);
                }
            }
        }

        private sealed class PageResponse
        {
            public Uri Url;
            public string Html;
        }

        private static bool IsRedirect(HttpStatusCode status)
        {
            int code = (int)status;
            return code == 301 || code == 302 || code == 303 || code == 307 || code == 308;
        }

        private static Uri RedirectUrl(HttpResponseMessage response, Uri current)
        {
            if (response.Headers.Location == null)
                throw new EclassLoginException("로그인 리다이렉트에 Location 헤더가 없습니다.");
            Uri uri = new Uri(current, response.Headers.Location);
            EnsureSchoolOrigin(uri);
            return uri;
        }

        private static async Task<PageResponse> GetPageAsync(HttpClient client, Uri url, CancellationToken token)
        {
            for (int step = 0; step < 8; step++)
            {
                EnsureSchoolOrigin(url);
                using (HttpResponseMessage response = await client.GetAsync(url, token))
                {
                    if (IsRedirect(response.StatusCode))
                    {
                        url = RedirectUrl(response, url);
                        continue;
                    }
                    response.EnsureSuccessStatusCode();
                    return new PageResponse {
                        Url = url,
                        Html = await response.Content.ReadAsStringAsync()
                    };
                }
            }
            throw new EclassLoginException("로그인 리다이렉트가 너무 많습니다.");
        }

        private void Persist(CookieContainer jar)
        {
            string cookie = jar.GetCookieHeader(new Uri(EclassCrawler.HomeUrl));
            if (string.IsNullOrWhiteSpace(cookie))
                throw new EclassLoginException("로그인에는 성공했지만 eClass 세션 쿠키를 받지 못했습니다.");
            crawler.ImportCookieHeader(cookie);
        }

        private static void EnsureSchoolOrigin(Uri uri)
        {
            if (uri == null || uri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(uri.Host, Origin.Host, StringComparison.OrdinalIgnoreCase))
                throw new EclassLoginException(
                    "로그인이 학교의 외부 SSO 사이트로 이동했습니다. " +
                    "안전을 위해 로그인 정보를 전송하지 않았습니다. 실제 SSO 로그인 흐름 확인이 필요합니다.");
        }

        private sealed class FormLogin
        {
            public Uri Action;
            public string IdField;
            public string PasswordField;
            public Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private static FormLogin ReadLoginForm(string html, Uri pageUrl)
        {
            foreach (Match formMatch in Regex.Matches(html, @"<form\b(?<attrs>[^>]*)>(?<body>.*?)</form>",
                         RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                FormLogin candidate = MakeForm(
                    formMatch.Groups["attrs"].Value, formMatch.Groups["body"].Value, pageUrl);
                if (candidate != null) return candidate;
            }

            // 폼 외부의 JavaScript로 로그인하는 경우에는 임의의 제출 주소를
            // 가정하지 않는다. 실제 로그인 요청을 확인해야 안전하게 구현 가능하다.
            return null;
        }

        private static FormLogin MakeForm(string formAttributes, string body, Uri pageUrl)
        {
            Dictionary<string, string> formAttrs = Attributes(formAttributes);
            FormLogin result = new FormLogin();
            string possibleId = null;
            string possiblePassword = null;
            int bestIdScore = -1;
            foreach (Match input in Regex.Matches(body, @"<input\b(?<attrs>[^>]*)>", RegexOptions.IgnoreCase))
            {
                Dictionary<string, string> attrs = Attributes(input.Groups["attrs"].Value);
                string name = Get(attrs, "name");
                if (name.Length == 0) name = Get(attrs, "id");
                if (name.Length == 0) continue;
                string type = Get(attrs, "type").ToLowerInvariant();
                string lower = name.ToLowerInvariant();
                if (type == "hidden") result.Values[name] = Get(attrs, "value");
                if (type == "password") possiblePassword = name;
                if (type == "text" || type == "email" || type.Length == 0)
                {
                    // 학번 입력을 검색창이나 보안문자 입력보다 우선한다.
                    int score = lower == "usr_id" ? 5 :
                                lower.Contains("login") ? 4 :
                                lower.Contains("user") || lower.Contains("student") ? 3 :
                                lower == "id" ? 2 : 0;
                    if (score > bestIdScore)
                    {
                        bestIdScore = score;
                        possibleId = name;
                    }
                }
            }
            if (possibleId == null || possiblePassword == null) return null;
            result.IdField = possibleId;
            result.PasswordField = possiblePassword;

            string method = Get(formAttrs, "method");
            if (string.Equals(method, "get", StringComparison.OrdinalIgnoreCase))
                throw new EclassLoginException("학교 로그인 폼이 GET 방식입니다. 비밀번호 노출을 방지하기 위해 전송을 중단했습니다.");
            string action = Get(formAttrs, "action");
            Uri target;
            if (action.TrimStart().StartsWith("javascript", StringComparison.OrdinalIgnoreCase))
                throw new EclassLoginException("학교 로그인은 JavaScript 기반입니다. 실제 요청 명세가 필요합니다.");
            if (!Uri.TryCreate(pageUrl, WebUtility.HtmlDecode(action), out target))
                throw new EclassLoginException("로그인 URL이 유효하지 않습니다.");
            EnsureSchoolOrigin(target);
            result.Action = target;
            return result;
        }

        private static Dictionary<string, string> Attributes(string raw)
        {
            Dictionary<string, string> parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            const string pattern = @"(?<key>[a-zA-Z_:][\w:.-]*)\s*=\s*(?:""(?<val>[^""]*)""|'(?<val>[^']*)'|(?<val>[^\s>]+))";
            foreach (Match match in Regex.Matches(raw, pattern))
                parsed[match.Groups["key"].Value] = WebUtility.HtmlDecode(match.Groups["val"].Value);
            return parsed;
        }

        private static string Get(Dictionary<string, string> map, string key)
        {
            string value;
            return map.TryGetValue(key, out value) ? value : "";
        }
    }

    public sealed class EclassLoginException : Exception
    {
        public EclassLoginException(string message) : base(message) { }
    }
}
