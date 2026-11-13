using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SeoulTechEclass
{
    /// <summary>
    /// Unity / .NET 공통 코드. 별도 NuGet 패키지 및 브라우저 자동화 불필요.
    /// 로그인 쿠키 저장 및 재사용을 담당한다. 로그인 요청은 EclassLoginService에 분리했다.
    /// 쿠키는 인증 자격 증명이다. 외부 유출에 주의하고 버전 관리에 포함하지 말 것.
    /// </summary>
    public sealed class EclassCrawler
    {
        public const string HomeUrl = "https://eclass.seoultech.ac.kr/ilos/main/main_form.acl";
        private static readonly Uri BaseUri = new Uri("https://eclass.seoultech.ac.kr/");
        private const string TodoPath = "ilos/main/main_todo_list.acl";
        private const string NoticePath = "ilos/main/main_notice_list.acl";
        private const int PageSize = 5; // 업로드된 HTML의 AJAX 요청 값과 동일
        private const int MaxPages = 8; // 실수로 무한 크롤링하지 않도록 제한
        private readonly string dataFolder;
        private readonly string cookieFile;

        public EclassCrawler(string dataFolder)
        {
            if (string.IsNullOrWhiteSpace(dataFolder)) throw new ArgumentException("저장 폴더가 필요합니다.");
            this.dataFolder = dataFolder;
            Directory.CreateDirectory(dataFolder);
            cookieFile = Path.Combine(dataFolder, "session_cookies.txt");
        }

        public string CookieFilePath { get { return cookieFile; } }
        public string ResultFilePath { get { return Path.Combine(dataFolder, "result.txt"); } }
        public bool HasSavedSession { get { return File.Exists(cookieFile) && new FileInfo(cookieFile).Length > 0; } }

        /// <summary>
        /// 브라우저 개발자도구 Network > main_form.acl > Request Headers > Cookie 값을 전달한다.
        /// 이 함수는 학교 비밀번호를 받지 않는다. 쿠키는 로컬에 평문으로 저장된다.
        /// </summary>
        public void ImportCookieHeader(string cookieHeader)
        {
            if (cookieHeader == null) throw new ArgumentNullException(nameof(cookieHeader));
            cookieHeader = cookieHeader.Trim();
            if (cookieHeader.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
                cookieHeader = cookieHeader.Substring(7).Trim();
            if (cookieHeader.Length == 0 || cookieHeader.IndexOf('\r') >= 0 || cookieHeader.IndexOf('\n') >= 0)
                throw new ArgumentException("쿠키 값이 비었거나 잘못된 형식입니다.");

            // 잘못된 값을 저장하기 전에 파싱 가능한지 확인한다.
            CookieContainer validate = new CookieContainer();
            AddBrowserCookies(validate, cookieHeader);
            if (string.IsNullOrWhiteSpace(validate.GetCookieHeader(new Uri(HomeUrl))))
                throw new ArgumentException("쿠키가 없습니다. 'Cookie' 요청 헤더의 값을 복사하세요.");

            Directory.CreateDirectory(dataFolder);
            File.WriteAllText(cookieFile, cookieHeader, new UTF8Encoding(false));
        }

        public void ClearSession()
        {
            if (File.Exists(cookieFile)) File.Delete(cookieFile);
        }

        /// <summary>
        /// 파일 존재만으로 로그인 여부를 판단하지 않는다. 실제 메인 페이지를 요청해서 확인한다.
        /// 네트워크 오류는 세션 만료와 구분하여 호출자에게 전달한다.
        /// </summary>
        public async Task<bool> HasValidSessionAsync(CancellationToken token = default(CancellationToken))
        {
            if (!HasSavedSession) return false;
            CookieContainer cookies = new CookieContainer();
            try
            {
                AddBrowserCookies(cookies, File.ReadAllText(cookieFile, Encoding.UTF8));
            }
            catch (ArgumentException)
            {
                ClearSession();
                return false;
            }

            using (HttpClientHandler handler = new HttpClientHandler())
            {
                handler.UseCookies = true;
                handler.CookieContainer = cookies;
                using (HttpClient client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    SetBrowserHeaders(client);
                    string page = await GetTextAsync(client, new Uri(HomeUrl), token);
                    if (IsLoggedInHome(page)) return true;
                    ClearSession();
                    return false;
                }
            }
        }

        internal static void SetBrowserHeaders(HttpClient client)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (compatible; EclassTamagochi/1.0)");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "ko-KR,ko;q=0.9");
            client.DefaultRequestHeaders.Referrer = new Uri(HomeUrl);
        }

        internal static bool IsAuthenticatedHome(string html) { return IsLoggedInHome(html); }

        public async Task<EclassResult> FetchAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!HasSavedSession)
                throw new EclassSessionRequiredException("저장된 eClass 로그인 세션이 없습니다.");

            CookieContainer cookies = new CookieContainer();
            AddBrowserCookies(cookies, File.ReadAllText(cookieFile, Encoding.UTF8));

            using (HttpClientHandler handler = new HttpClientHandler())
            {
                handler.UseCookies = true;
                handler.CookieContainer = cookies;
                handler.AllowAutoRedirect = true;

                using (HttpClient client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(25);
                    SetBrowserHeaders(client);

                    // 로그인 화면으로 리다이렉트됐는지 확인. 내용 없는 결과 파일을 만들지 않는다.
                    string home = await GetTextAsync(client, new Uri(HomeUrl), cancellationToken);
                    if (!IsLoggedInHome(home))
                    {
                        ClearSession();
                        throw new EclassSessionRequiredException("로그인 세션이 만료되었습니다. 앱에서 다시 로그인하세요.");
                    }

                    List<string> todos = new List<string>();
                    List<string> notices = new List<string>();
                    HashSet<string> todoSeen = new HashSet<string>(StringComparer.Ordinal);
                    HashSet<string> noticeSeen = new HashSet<string>(StringComparer.Ordinal);

                    for (int page = 0; page < MaxPages; page++)
                    {
                        string html = await PostListAsync(client, TodoPath, page * PageSize, cancellationToken);
                        List<string> items = ParseTodos(html);
                        int added = AddUnique(todos, todoSeen, items);
                        if (items.Count < PageSize || added == 0) break;
                    }

                    for (int page = 0; page < MaxPages; page++)
                    {
                        // 학교의 JS에서 공지사항 start는 1부터 시작
                        string html = await PostListAsync(client, NoticePath, 1 + page * PageSize, cancellationToken);
                        List<string> items = ParseNotices(html);
                        int added = AddUnique(notices, noticeSeen, items);
                        if (items.Count < PageSize || added == 0) break;
                    }

                    // 서버가 Set-Cookie로 변경한 세션 값을 가능한 범위에서 갱신해둔다.
                    string refreshed = cookies.GetCookieHeader(new Uri(HomeUrl));
                    if (!string.IsNullOrWhiteSpace(refreshed))
                        File.WriteAllText(cookieFile, refreshed, new UTF8Encoding(false));

                    return new EclassResult(todos, notices);
                }
            }
        }

        public string SaveResult(EclassResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            Directory.CreateDirectory(dataFolder);
            File.WriteAllText(ResultFilePath, result.ToText(), new UTF8Encoding(false));
            return ResultFilePath;
        }

        private static async Task<string> GetTextAsync(HttpClient client, Uri url, CancellationToken token)
        {
            using (HttpResponseMessage response = await client.GetAsync(url, token))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        }

        private static async Task<string> PostListAsync(HttpClient client, string path, int start, CancellationToken token)
        {
            KeyValuePair<string, string>[] fields = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("start", start.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("display", PageSize.ToString()),
                new KeyValuePair<string, string>("encoding", "utf-8")
            };
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri, path)))
            {
                request.Content = new FormUrlEncodedContent(fields);
                request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
                using (HttpResponseMessage response = await client.SendAsync(request, token))
                {
                    response.EnsureSuccessStatusCode();
                    string html = await response.Content.ReadAsStringAsync();
                    // 인증 실패 페이지/포털 로그인 화면을 목록으로 오인하지 않도록 한다.
                    if (html.IndexOf("type=\"password\"", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        html.IndexOf("aside_list", StringComparison.OrdinalIgnoreCase) < 0)
                        throw new EclassSessionRequiredException("목록 요청 중 로그인 세션이 만료되었습니다.");
                    return html;
                }
            }
        }

        private static bool IsLoggedInHome(string html)
        {
            // 제공된 실제 로그인 후 HTML에 존재하는 요소를 모두 검사한다.
            return html.IndexOf("id=\"header_todo\"", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   html.IndexOf("/ilos/lo/logout.acl", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddBrowserCookies(CookieContainer container, string header)
        {
            foreach (string part in header.Split(';'))
            {
                string value = part.Trim();
                int eq = value.IndexOf('=');
                if (eq <= 0) continue;
                string name = value.Substring(0, eq).Trim();
                string content = value.Substring(eq + 1).Trim();
                if (name.Length == 0) continue;
                try
                {
                    container.Add(new Cookie(name, content, "/", BaseUri.Host));
                }
                catch (CookieException ex)
                {
                    throw new ArgumentException("쿠키 항목 '" + name + "'을 읽지 못했습니다: " + ex.Message, ex);
                }
            }
        }

        private static int AddUnique(List<string> target, HashSet<string> seen, List<string> items)
        {
            int count = 0;
            foreach (string item in items)
                if (seen.Add(item)) { target.Add(item); count++; }
            return count;
        }

        public static List<string> ParseTodos(string html)
        {
            List<string> output = new List<string>();
            foreach (Match item in Regex.Matches(html, @"<a\b[^>]*class\s*=\s*""[^""]*\baside_list_todo_item\b[^""]*""[^>]*>.*?</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                string title = FindClassText(item.Value, "aside_list_title");
                if (title.Length == 0) continue;
                string course = FindClassText(item.Value, "aside_list_info");
                string due = FindClassText(item.Value, "aside_list_date");
                string kind = FindClassText(item.Value, "aside_list_top");
                kind = Regex.Replace(kind, @"\bD-\d+\b", "").Trim();

                string text = (kind.Length > 0 ? "[" + kind + "] " : "") + title;
                if (course.Length > 0) text += " / " + course;
                if (due.Length > 0) text += " / 마감: " + due;
                output.Add(text);
            }
            return output;
        }

        public static List<string> ParseNotices(string html)
        {
            List<string> output = new List<string>();
            foreach (Match item in Regex.Matches(html, @"<button\b[^>]*class\s*=\s*""[^""]*\bnotice_list\b[^""]*""[^>]*>.*?</button>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                string title = FindClassText(item.Value, "notice_title");
                if (title.Length == 0) continue;
                string date = FindClassText(item.Value, "notice_date");
                output.Add(title + (date.Length > 0 ? " / " + date : ""));
            }
            return output;
        }

        private static string FindClassText(string html, string className)
        {
            string pattern = @"<div\b[^>]*class\s*=\s*""[^""]*\b" + Regex.Escape(className) + @"\b[^""]*""[^>]*>(.*?)</div>";
            Match match = Regex.Match(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            return match.Success ? NormalizeText(match.Groups[1].Value) : "";
        }

        private static string NormalizeText(string html)
        {
            string text = Regex.Replace(html, @"<[^>]+>", " ");
            text = WebUtility.HtmlDecode(text);
            return Regex.Replace(text, @"\s+", " ").Trim();
        }
    }

    public sealed class EclassSessionRequiredException : Exception
    {
        public EclassSessionRequiredException(string message) : base(message) { }
    }

    public sealed class EclassResult
    {
        public IReadOnlyList<string> Todos { get; private set; }
        public IReadOnlyList<string> Notices { get; private set; }

        public EclassResult(IReadOnlyList<string> todos, IReadOnlyList<string> notices)
        {
            Todos = todos;
            Notices = notices;
        }

        public string ToText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[todo]");
            foreach (string item in Todos) sb.AppendLine(item);
            sb.AppendLine("[note]");
            foreach (string item in Notices) sb.AppendLine(item);
            return sb.ToString();
        }
    }
}
