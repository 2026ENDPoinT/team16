using System.IO;
using UnityEngine;
using DormTamagotchi.GitHub;
/// <summary>
/// (인게임 가구 오브젝트 바인딩 예시)
가구 담당자가 위 모듈을 가져다 쓸 때 사용하는 컴포넌트 예시입니다.
/// </summary>
public class StreakFurnitureExample : MonoBehaviour
{
    [Header("GitHub 연동 설정")]
    [SerializeField] private string githubUsername = "torvalds";
    [SerializeField] private string optionalOAuthToken = ""; // 비워두면 공개 API로 즉시 동작

    [Header("가구 렌더러 연결")]
    [SerializeField] private SpriteRenderer wallBoardRenderer;

    private GitHubStreakBundle _currentBundle;

    private async void Start()
    {
        await LoadAndApplyStreakFurnitureAsync();
    }

    public async Awaitable LoadAndApplyStreakFurnitureAsync()
    {
        try
        {
            // 1. 스트릭 데이터 + 텍스처 + PNG 바이트 스트림 한 번에 받아오기
            _currentBundle?.Dispose();
            _currentBundle = await GitHubStreakService.FetchStreakBundleAsync(githubUsername, optionalOAuthToken);

            // 2. 가구 SpriteRenderer에 즉시 적용
            if (wallBoardRenderer != null)
            {
                wallBoardRenderer.sprite = _currentBundle.GrassSprite;
            }

            // 3. 스트릭 수치 및 데이터 스트림 활용 예시 (업적 해금, UI 툴팁, 로컬 캐싱)
            Debug.Log($"[GitHub 연동 완료] 유저: {_currentBundle.Data.username} | " +
                      $"총 기여: {_currentBundle.Data.totalContributions}회 | " +
                      $"현재 스트릭: {_currentBundle.Data.currentStreak}일 | " +
                      $"최장 스트릭: {_currentBundle.Data.longestStreak}일");

            // 필요 시 PNG 파일이나 JSON 캐시로 로컬 디스크/서버에 즉시 저장 가능
            string cachePngPath = Path.Combine(Application.persistentDataPath, $"{githubUsername}_streak.png");
            await File.WriteAllBytesAsync(cachePngPath, _currentBundle.PngImageStream);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"GitHub 스트릭 가구 로딩 실패: {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        // 동적 생성된 Texture2D 및 Sprite 언매니지드 메모리 해제
        _currentBundle?.Dispose();
    }
}