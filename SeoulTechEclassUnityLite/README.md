# SeoulTech eClass Unity Lite

## 목적 / 설치 부담

- 서울과학기술대학교 로그인 후 메인 화면의 *할 일*과 *공지사항*을 읽어 `result.txt`에 기록합니다.
- Playwright, 브라우저 엔진 다운로드, NuGet 패키지, 별도 크롤러 서버가 필요 없습니다.
- VS Code 테스트는 `.NET 10 SDK`가 필요하지만, **Unity에 소스를 직접 넣을 경우 별도 .NET 10 런타임은 필요하지 않습니다**.
- 외부 의존성: PC/휴대폰의 **기존 웹 브라우저**(최초 학교 로그인 및 쿠키 복사용).
- Unity의 `System.Net.Http`를 이용합니다. Unity PC/모바일 빌드는 플랫폼별 테스트가 필요하며 **WebGL은 미지원**입니다.

## 확인된 사이트 구조 (사용자가 제공한 2026-11-13 로그인 후 HTML)

- `POST /ilos/main/main_todo_list.acl`: `start=0`, `display=5`, `encoding=utf-8`
- `POST /ilos/main/main_notice_list.acl`: `start=1`, `display=5`, `encoding=utf-8`
- 할 일: `.aside_list_todo_item` 내부 `.aside_list_title`, `.aside_list_info`, `.aside_list_date`, `.aside_list_top`
- 공지: `.notice_list` 내부 `.notice_title`, `.notice_date`
- 홈에서만 보이는 공지사항입니다. **과목별 강의실 공지를 전체 수집하는 기능은 포함하지 않습니다.**
- 기본 최대 8페이지(각 5개)를 요청하며 내용이 없거나 반복되면 중지합니다. 학교 사이트 변경/응답 제한 시 누락될 수 있습니다.

## VS Code 실행

터미널에서 프로젝트 루트로 이동합니다.

```powershell
dotnet build
dotnet run -- sample ".\Samples\sample.html"
```

`sample_result.txt` 파일을 확인하세요. 별도 인증 없이 HTML 선택자가 정상 동작하는지 먼저 검증합니다.

### 1. 최초 로그인/쿠키 가져오기

1. 평소 사용하는 Chrome/Edge에서 `https://eclass.seoultech.ac.kr/ilos/main/main_form.acl` 접속 → 본인 계정 로그인.
2. 로그인한 **eClass 탭**에서 `F12`를 눌러 개발자도구를 열고 `Network` 탭 선택.
3. `Ctrl+R` 새로고침하여 `main_form.acl` 요청 선택. 요청이 없다면 `Doc` 필터를 선택해 로그인 후 기본 화면의 요청을 찾습니다.
4. `Headers` → `Request Headers` → `Cookie` 항목에서 **값만** 복사합니다. `Set-Cookie` 응답 헤더가 아닙니다. 여러 개의 쿠키는 `name=value; name2=value`처럼 붙어 있습니다.
5. 터미널에서 `dotnet run -- login` 명령을 실행하고 쿠키 헤더 값을 붙여넣습니다. **절대로 채팅이나 GitHub에 쿠키 값을 공유하지 마세요.**

이 과정에서 사용자의 **학교 비밀번호는 코드에 저장하지 않습니다.**

### 2. 크롤링 실행

```powershell
dotnet run -- crawl
```

현재 폴더에 `result.txt` 생성. 샘플 형식:

```text
[todo]
[과제] 과제 1 & 복습 / 알고리즘 / 마감: 11월 20일 오후 11:59
[영상] 3주차 강의 / 웹프로그래밍 / 마감: 11월 21일
[note]
시험 일정 안내 / 11월 10일
프로젝트 발표 공지 / 11월 11일
```

`dotnet run -- logout`을 실행하면 저장 쿠키를 삭제할 수 있습니다.

## 이미 저장한 outerHTML로 파싱 시험

```powershell
dotnet run -- sample "C:\\...\\outerHTML.txt"
```

이 명령은 네트워크 접속을 하지 않고 `sample_result.txt`로 저장합니다.

## Unity 통합

1. `Shared/EclassCrawler.cs`와 `Unity/EclassUnityBridge.cs` **두 파일**을 Unity 프로젝트 `Assets/Scripts`에 복사.
2. 빈 GameObject를 만들고 `EclassUnityBridge`를 컴포넌트로 추가.
3. 로그인 버튼에서 `OpenLoginPage()` 호출 → 브라우저에서 로그인.
4. 사용자에게 쿠키 헤더 문자열을 입력받아 `ImportSessionCookie(string)`으로 전달할 수 있는 입력 UI를 연결. 민감한 값이므로 화면에 오래 노출하거나 로그로 출력하지 말 것.
5. 갱신 버튼에서 `FetchAndSave()`를 호출. 결과 파일은 `Application.persistentDataPath/result.txt`.
6. 또는 Unity 로직에서 `new EclassCrawler(Application.persistentDataPath).FetchAsync()`로 데이터 직접 접근.

## 제약 / 보안

- 최초 **자동 ID/PW 로그인**은 구현하지 않았습니다. 제공된 HTML은 로그인 후의 홈 화면이며 실제 SSO 인증 양식을 확인할 수 없었습니다. 처음에는 사용자 본인이 브라우저에서 로그인하고 **요청 Cookie 헤더를 한 번 복사**해야 합니다.
- 쿠키는 사용자의 세션 접근 권한입니다. `session_cookies.txt`는 **평문 저장**되므로 기기 사용자만 접근 가능한 저장 위치를 사용하고, 공유·업로드·버전관리하지 마세요. 상용 서비스에서는 OS 보안 저장소 도입을 권장합니다.
- 쿠키는 영구 인증이 아닙니다. **세션 만료 또는 서버 정책 변경 시 수동 재로그인 후 갱신**해야 합니다.
- SSO 계정, 다중 도메인, JS 요청 토큰이 추가되면 현재 방식이 더 이상 동작하지 않을 수 있습니다.
- 서버가 비정상적으로 응답하거나 로그인 세션이 만료된 경우 `result.txt`는 새로 덮어쓰지 않습니다.
- Unity WebGL은 `System.Net.Http` 소켓 제약과 CORS 때문에 지원하지 않습니다. 모바일 빌드는 쿠키 가져오기 UX와 실제 HTTP 동작을 기기에서 테스트해야 합니다.
- 로그인 쿠키를 읽는 실제 사용자 계정으로 사이트에 접속해 테스트하지 못했으므로 실서버 크롤링 성공은 확인되지 않았습니다.
