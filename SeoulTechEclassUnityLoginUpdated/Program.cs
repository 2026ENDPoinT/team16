using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SeoulTechEclass;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        string folder = Directory.GetCurrentDirectory();
        EclassCrawler crawler = new EclassCrawler(folder);
        string command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

        try
        {
            if (command == "sample")
            {
                if (args.Length < 2) throw new ArgumentException("HTML 파일 경로를 입력하세요.");
                string html = File.ReadAllText(args[1]);
                EclassResult sample = new EclassResult(EclassCrawler.ParseTodos(html), EclassCrawler.ParseNotices(html));
                string filename = Path.Combine(folder, "sample_result.txt");
                File.WriteAllText(filename, sample.ToText(), new UTF8Encoding(false));
                Console.WriteLine("샘플 성공: 할 일 " + sample.Todos.Count + "개, 공지 " + sample.Notices.Count + "개 / " + filename);
                return 0;
            }
            if (command == "login")
            {
                Console.Write("학교 학번: ");
                string id = Console.ReadLine() ?? "";
                Console.Write("학교 비밀번호: ");
                string password = ReadPassword();
                Console.WriteLine();
                await new EclassLoginService(crawler).LoginAsync(id, password);
                // 비밀번호를 파일/로그로 저장하지 않는다.
                Console.WriteLine("로그인 성공. 학교 쿠키를 저장했습니다.");
                await RunCrawl(crawler);
                return 0;
            }
            if (command == "crawl")
            {
                try
                {
                    await RunCrawl(crawler);
                    return 0;
                }
                catch (EclassSessionRequiredException)
                {
                    Console.WriteLine("로그인 쿠키가 없거나 만료되었습니다. 로그인 정보를 입력해 주세요.");
                    Console.Write("학교 학번: ");
                    string id = Console.ReadLine() ?? "";
                    Console.Write("학교 비밀번호: ");
                    string password = ReadPassword();
                    Console.WriteLine();
                    await new EclassLoginService(crawler).LoginAsync(id, password);
                    await RunCrawl(crawler);
                    return 0;
                }
            }
            if (command == "status")
            {
                Console.WriteLine(await crawler.HasValidSessionAsync() ? "세션 유효" : "세션 없음 / 만료됨");
                return 0;
            }
            if (command == "logout")
            {
                crawler.ClearSession();
                Console.WriteLine("저장된 쿠키를 삭제했습니다.");
                return 0;
            }
            Console.WriteLine("사용법:");
            Console.WriteLine("  dotnet run -- sample \".\\Samples\\sample.html\"");
            Console.WriteLine("  dotnet run -- crawl     (세션 없거나 만료 시 자동 로그인 입력)");
            Console.WriteLine("  dotnet run -- login     (강제로 다시 로그인)");
            Console.WriteLine("  dotnet run -- status    (쿠키 유효성 확인)");
            Console.WriteLine("  dotnet run -- logout   (쿠키 삭제)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("실패: " + ex.Message);
            return 1;
        }
    }

    private static async Task RunCrawl(EclassCrawler crawler)
    {
        EclassResult result = await crawler.FetchAsync();
        string path = crawler.SaveResult(result);
        Console.WriteLine("성공: 할 일 " + result.Todos.Count + "개, 공지 " + result.Notices.Count + "개");
        Console.WriteLine("저장 위치: " + path);
    }

    private static string ReadPassword()
    {
        StringBuilder buffer = new StringBuilder();
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace && buffer.Length > 0)
                buffer.Length--;
            else if (!char.IsControl(key.KeyChar))
                buffer.Append(key.KeyChar);
        }
        return buffer.ToString();
    }
}
