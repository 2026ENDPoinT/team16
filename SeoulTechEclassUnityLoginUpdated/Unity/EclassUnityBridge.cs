using System;
using System.IO;
using System.Threading.Tasks;
using SeoulTechEclass;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Unity 에디터 / 데스크톱 / 모바일용 in-app 로그인 UI.
/// Button.onClick에 OnSyncClicked, OnLoginClicked를 연결한다.
/// Unity WebGL에서는 학교 사이트의 CORS 및 HttpClient 제약으로 직접 실행할 수 없다.
/// WebGL은 기존 FastAPI 서버에서 학교 로그인/크롤링을 대행하도록 분리해야 한다.
/// </summary>
public sealed class EclassUnityBridge : MonoBehaviour
{
    [Header("UI 연결 (Inspector)")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private InputField studentIdInput;
    [SerializeField] private InputField passwordInput;
    [SerializeField] private Text statusText;
    [SerializeField] private Button syncButton;
    [SerializeField] private Button loginButton;

    private bool busy;
    private EclassCrawler crawler;

    public string ResultFilePath { get { return Path.Combine(Application.persistentDataPath, "result.txt"); } }

    private void Awake()
    {
        crawler = new EclassCrawler(Application.persistentDataPath);
        if (loginPanel != null) loginPanel.SetActive(false);
        if (passwordInput != null) passwordInput.contentType = InputField.ContentType.Password;
    }

    // 메인 화면의 'eClass 동기화' 버튼
    public async void OnSyncClicked()
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            SetStatus("eClass 세션 확인 및 동기화 중...");
            EclassResult result = await crawler.FetchAsync();
            string path = crawler.SaveResult(result);
            if (loginPanel != null) loginPanel.SetActive(false);
            SetStatus("완료: 할 일 " + result.Todos.Count + "개 / 공지 " + result.Notices.Count + "개");
            Debug.Log("eClass result.txt 저장: " + path);
        }
        catch (EclassSessionRequiredException)
        {
            crawler.ClearSession();
            if (loginPanel != null) loginPanel.SetActive(true);
            SetStatus("로그인이 필요합니다. 학교 계정으로 로그인하세요.");
        }
        catch (Exception ex)
        {
            SetStatus("동기화 오류: " + ex.Message);
        }
        finally { SetBusy(false); }
    }

    // 로그인 패널의 '로그인' 버튼. 성공하면 즉시 기존 크롤러를 실행한다.
    public async void OnLoginClicked()
    {
        if (busy) return;
        if (studentIdInput == null || passwordInput == null)
        {
            SetStatus("Unity Inspector에서 학번/비밀번호 입력창을 연결하세요.");
            return;
        }

        string id = studentIdInput.text;
        string password = passwordInput.text;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("학번과 비밀번호를 입력하세요.");
            return;
        }
        SetBusy(true);
        try
        {
            SetStatus("학교 로그인 중...");
            await new EclassLoginService(crawler).LoginAsync(id, password);
            passwordInput.text = "";
            SetStatus("로그인 성공. 할 일과 공지사항을 불러오는 중...");
            EclassResult result = await crawler.FetchAsync();
            string path = crawler.SaveResult(result);
            if (loginPanel != null) loginPanel.SetActive(false);
            SetStatus("완료: 할 일 " + result.Todos.Count + "개 / 공지 " + result.Notices.Count + "개");
            Debug.Log("eClass result.txt 저장: " + path);
        }
        catch (Exception ex)
        {
            SetStatus("로그인/동기화 실패: " + ex.Message);
            if (loginPanel != null) loginPanel.SetActive(true);
        }
        finally
        {
            // 오류가 나도 비밀번호를 화면에 계속 남기지 않는다.
            passwordInput.text = "";
            SetBusy(false);
        }
    }

    public void OnLogoutClicked()
    {
        crawler.ClearSession();
        if (loginPanel != null) loginPanel.SetActive(true);
        SetStatus("학교 로그인 정보가 삭제되었습니다.");
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log("[eClass] " + message);
    }

    private void SetBusy(bool value)
    {
        busy = value;
        if (syncButton != null) syncButton.interactable = !value;
        if (loginButton != null) loginButton.interactable = !value;
    }
}
