using SeoulTechEclass;
using System;
using System.IO;
using System.Threading.Tasks;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // VS Code 테스트용. Unity에서는 Application.persistentDataPath를 사용.
        string folder = Directory.GetCurrentDirectory();
        EclassCrawler crawler = new EclassCrawler(folder);
        string command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

        try
        {
            switch (command)
            {
                case "login":
                    Console.WriteLine("[1] 브라우저에서 eClass 로그인: " + EclassCrawler.HomeUrl);
                    Console.WriteLine("[2] F12 > Network > 새로고침 > main_form.acl 선택");
                    Console.WriteLine("[3] Request Headers의 Cookie 값만 복사해 아래에 붙여넣기");
                    Console.WriteLine("※ 쿠키는 비밀번호처럼 비밀입니다. 채팅/깃허브에 붙여넣지 마세요.");
                    Console.Write("Cookie: ");
                    string? cookie = Console.ReadLine();
                    crawler.ImportCookieHeader(cookie ?? "");
                    Console.WriteLine("쿠키가 로컬에 저장되었습니다. 다음 명령: dotnet run -- crawl");
                    return 0;

                case "crawl":
                    EclassResult result = await crawler.FetchAsync();
                    string path = crawler.SaveResult(result);
                    Console.WriteLine("성공: 할 일 " + result.Todos.Count + "개, 공지사항 " + result.Notices.Count + "개");
                    Console.WriteLine("저장 위치: " + path);
                    return 0;

                case "sample":
                    if (args.Length < 2) { Console.WriteLine("사용법: dotnet run -- sample \"저장한_로그인후_HTML.txt\""); return 2; }
                    string html = File.ReadAllText(args[1]);
                    EclassResult sample = new EclassResult(EclassCrawler.ParseTodos(html), EclassCrawler.ParseNotices(html));
                    string samplePath = Path.Combine(folder, "sample_result.txt");
                    File.WriteAllText(samplePath, sample.ToText());
                    Console.WriteLine("HTML 파싱 테스트: 할 일 " + sample.Todos.Count + "개, 공지 " + sample.Notices.Count + "개");
                    Console.WriteLine("샘플 결과: " + samplePath);
                    return 0;

                case "logout":
                    crawler.ClearSession();
                    Console.WriteLine("저장된 쿠키를 삭제했습니다.");
                    return 0;

                default:
                    Console.WriteLine("사용법:");
                    Console.WriteLine("  dotnet run -- sample \"HTML파일.txt\"  (로그인 없이 파싱 테스트)");
                    Console.WriteLine("  dotnet run -- login              (브라우저 쿠키를 1회 저장)");
                    Console.WriteLine("  dotnet run -- crawl              (result.txt 생성)");
                    Console.WriteLine("  dotnet run -- logout             (쿠키 삭제)");
                    return 0;
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("실패: " + e.Message);
            return 1;
        }
    }
}
