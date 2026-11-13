using System;
using System.IO;
using SeoulTechEclass;
using UnityEngine;

/// <summary>
/// EclassCrawler.cs와 이 파일을 Unity Assets/Scripts/에 함께 넣으세요.
/// 별도 브라우저 엔진, Playwright, NuGet 패키지는 필요하지 않습니다.
/// Unity WebGL은 지원 대상이 아닙니다.
/// </summary>
public class EclassUnityBridge : MonoBehaviour
{
    public string ResultFilePath { get { return Path.Combine(Application.persistentDataPath, "result.txt"); } }

    // 유저가 처음 학교 로그인하도록 기본 브라우저 열기
    public void OpenLoginPage()
    {
        Application.OpenURL(EclassCrawler.HomeUrl);
    }

    // 로그인 후 개발자도구에서 복사한 Cookie 요청 헤더를 UI 입력창에서 전달
    public void ImportSessionCookie(string cookieHeader)
    {
        try
        {
            new EclassCrawler(Application.persistentDataPath).ImportCookieHeader(cookieHeader);
            Debug.Log("eClass 쿠키 저장 완료 (내용은 로그에 표시하지 않음)");
        }
        catch (Exception ex)
        {
            Debug.LogError("쿠키 저장 실패: " + ex.Message);
        }
    }

    // 버튼 이벤트나 다마고치 데이터 갱신 시 호출
    public async void FetchAndSave()
    {
        try
        {
            EclassCrawler crawler = new EclassCrawler(Application.persistentDataPath);
            EclassResult data = await crawler.FetchAsync();
            crawler.SaveResult(data);
            Debug.Log("eClass 완료: 할 일 " + data.Todos.Count + "개, 공지 " + data.Notices.Count + "개 / " + ResultFilePath);
            // Unity 팀원이 필요하면 data.Todos / data.Notices를 바로 UI에 전달해도 됩니다.
        }
        catch (Exception ex)
        {
            Debug.LogError("eClass 갱신 실패: " + ex.Message);
        }
    }

    public void ClearSession()
    {
        new EclassCrawler(Application.persistentDataPath).ClearSession();
        Debug.Log("eClass 쿠키 삭제 완료");
    }
}
