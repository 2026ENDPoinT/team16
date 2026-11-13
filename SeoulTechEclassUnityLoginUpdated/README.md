# SeoulTech eClass Unity Login — 앱 내부 로그인 개편판

이 프로젝트는 이전 `SeoulTechEclassUnityLite`를 수정한 버전입니다. **브라우저 개발자도구에서 Cookie 헤더를 복사하는 단계가 없습니다.**

## 변경된 실행 흐름

```text
Unity의 '동기화' 버튼
    ↓
EclassCrawler.FetchAsync()
    ├─ 저장 쿠키 없음 → EclassSessionRequiredException → 로그인 패널
    ├─ 쿠키 만료 → 저장 쿠키 삭제 → 로그인 패널
    └─ 쿠키 유효 → 할 일 / 공지 크롤링 → result.txt

로그인 패널에서 학교 ID/PW 입력
    ↓
EclassLoginService.LoginAsync(id, password)
    ↓
학교 메인 URL GET → 로그인 폼 탐색 → 인증 정보 POST → 학교 메인 화면 재검증
    ├─ 인증 실패 → 오류 표시 (비밀번호 미저장)
    └─ 인증 성공 → 세션 쿠키 저장 → 기존 FetchAsync() → result.txt
```

## 필요한 프로그램 및 지원 범위

- VS Code 테스트: .NET 10 SDK (기존 환경과 동일)
- Unity Editor 및 PC/모바일 네이티브 빌드: `Shared` 코드와 `Unity/EclassUnityBridge.cs`, Unity 기본 `uGUI` (`InputField`, `Text`, `Button`)
- **Playwright, Chromium, WebView 플러그인, 별도 NuGet 패키지는 필요하지 않습니다.**
- **Unity WebGL 직접 실행은 지원하지 않습니다.** Unity WebGL에서 학교 서버로 직접 로그인/크롤링할 수 없는 CORS 및 런타임 제약이 있어, WebGL 대상이라면 이미 구축 중인 FastAPI 게임 서버를 통해 로그인과 세션을 대행해야 합니다. `result.txt`도 서버 또는 브라우저 저장 영역으로 처리해야 합니다.

## 중요한 미검증 사항

**사용자가 제공한 HTML은 로그인 성공 이후의 메인 화면**입니다. `main_todo_list.acl`, `main_notice_list.acl`의 AJAX 호출 방식은 그 HTML에서 확인했지만, 로그인 전 로그인 화면의 요청 필드, 암호화, SSO 방식은 검증하지 못했습니다.

이 코드는 학교 메인 URL에 접근해 로그인 폼이 있다면 학번/비밀번호 및 hidden 입력값을 추출해 폼의 `action`으로 POST합니다. 메인 화면에서 폼을 찾지 못하면 `/ilos/main/member/login_form.acl`을 두 번째 후보로 조회합니다. **주소를 추측하여 학번과 비밀번호를 보내지 않습니다.** 학교에서 SSO·JavaScript 암호화·다중 단계 인증을 사용한다면 `EclassLoginService.cs`를 실제 로그인 요청 흐름에 맞게 보완해야 합니다. 로그인 실패를 성공으로 표시하거나 쿠키를 저장하지 않습니다.

## 전달된 로그인 후 outerHTML로 확인한 근거

- `id="header_todo"` 및 `/ilos/lo/logout.acl`: 로그인 완료 후 메인 화면 식별
- `/ilos/main/main_todo_list.acl`: 할 일 목록을 불러오는 AJAX POST 주소
- `/ilos/main/main_notice_list.acl`: 공지사항을 불러오는 AJAX POST 주소
- 본 HTML의 HTML 요소 기준: 할 일 5건, 공지사항 3건의 제목이 추출되는 선택자 확인
- **로그인 전 입력폼/실제 인증 요청은 이 HTML에 포함되지 않음** (실제 학교 계정 로그인 테스트 필요)

## VS Code 테스트

압축 해제 후 이 프로젝트 루트에서 터미널을 실행합니다.

```powershell
dotnet restore
dotnet build
dotnet run -- sample ".\Samples\sample.html"
```

정상적으로 HTML 파싱과 저장이 되면 `sample_result.txt`가 생성됩니다.

앱 내부 로그인과 동일한 흐름을 콘솔에서 테스트하려면:

```powershell
dotnet run -- crawl
```

저장한 세션이 없다면 학번/비밀번호 입력을 요구합니다. 비밀번호는 콘솔에 별표로 표시하지 않으며 타이핑도 화면에 표시되지 않습니다. 성공하면 `session_cookies.txt`와 `result.txt`를 저장합니다.

```powershell
dotnet run -- status  # 저장한 쿠키를 실제 서버에 보내 검증
dotnet run -- crawl   # 재로그인 없이 재사용
dotnet run -- logout  # 쿠키 삭제
dotnet run -- login   # 강제로 학교 로그인 다시 시도
```

`status` 명령에서 네트워크 실패를 세션 만료로 처리하지 않습니다. 네트워크 연결부터 점검하세요.

## Unity 연동

1. `Shared/EclassCrawler.cs` + `Shared/EclassLoginService.cs` + `Unity/EclassUnityBridge.cs`를 Unity 프로젝트의 `Assets/Scripts/Eclass/` 폴더로 복사합니다.
2. Canvas를 만들고 `동기화 버튼`, `로그인 패널`, 패널 안의 `학번 InputField`, `비밀번호 InputField`, `로그인 버튼`, 선택적으로 `상태 Text`를 만듭니다. Unity 기본 **uGUI 컴포넌트**로 만들어야 합니다 (TextMeshPro 필드 아님).
3. 빈 GameObject를 만들고 `EclassUnityBridge` 컴포넌트를 추가합니다.
4. Inspector에서 `Login Panel`, `Student Id Input`, `Password Input`, `Status Text`, `Sync Button`, `Login Button`을 연결합니다.
5. 동기화 버튼의 OnClick에 `EclassUnityBridge.OnSyncClicked()`, 로그인 버튼의 OnClick에 `EclassUnityBridge.OnLoginClicked()`를 연결합니다.
6. 실행 후 동기화 버튼을 누르면 쿠키를 확인합니다. 없거나 만료되면 로그인 패널이 나타납니다. 패널에 계정을 입력하면 로그인 후 자동으로 크롤링합니다.
7. 저장 경로는 **`Application.persistentDataPath/result.txt`**입니다. Unity Console에 경로를 남깁니다.
8. 선택적으로 로그아웃 버튼을 만들고 `OnLogoutClicked()`를 연결하면 저장 세션을 삭제합니다.

> 프로젝트가 TextMeshPro로 UI를 구성했다면 `Unity/EclassUnityBridge.cs`의 `InputField` → `TMP_InputField`, `Text` → `TMP_Text`로 바꾸고 `using TMPro;`를 추가하세요.

## 코드별 역할

- `Shared/EclassCrawler.cs`: 세션 쿠키 읽기·검증·삭제, 할 일과 공지 HTML 조회, 제목 파싱, `result.txt` 저장 (기존 크롤러 유지)
- `Shared/EclassLoginService.cs`: 학교 로그인 폼 조회, 학번/비밀번호 인증 요청, 로그인 성공 검증, 쿠키 저장
- `Unity/EclassUnityBridge.cs`: Unity 버튼 및 로그인 패널 상태 제어
- `Program.cs`: Unity 없이 VS Code에서 테스트하는 콘솔 엔트리포인트

## 보안 주의

- 학교 ID/PW는 **파일로 저장하지 않습니다**. 로그인할 때만 메모리에서 사용합니다.
- `session_cookies.txt`는 **평문 인증 쿠키 파일**입니다. 실제 서비스 배포 전에 OS 보안 저장소 또는 서버 측 암호화 저장소 사용을 권장합니다. 절대 Git에 커밋하거나 공유하지 마세요.
- 모든 로그인 요청은 `https://eclass.seoultech.ac.kr` 호스트에만 보내도록 제한했습니다. 외부 SSO 도메인으로 이동할 때는 비밀번호 전송 없이 오류로 멈춥니다.
- 네트워크 오류·사이트 다운은 인증 만료와 별도로 처리합니다. 인증이 확인되지 않은 경우 `result.txt`를 새로 덮어쓰지 않습니다.

## 현재 기능 검증 범위

- 기존 제공 HTML의 할 일 / 공지 CSS 선택자와 결과 저장 포맷 유지
- 설치 없이 빌드할 수 있도록 Unity 전용 파일은 .NET 콘솔 빌드에서 제외 (`csproj`)
- 여기서는 .NET SDK와 학교 로그인 테스트 계정이 없어 C# 빌드·실제 로그인은 실행 검증하지 못했습니다. Python으로 제공된 로그인 후 HTML 선택자를 검증해 할 일 5건, 공지사항 3건을 확인했습니다.
- 원래 `import cookie` 코드는 호환을 위해 `EclassCrawler` 내부에 남아 있지만 신규 Unity UI/콘솔은 호출하지 않습니다.

## 테스트 권장 순서

1. `dotnet build` 성공 확인
2. `dotnet run -- sample ".\Samples\sample.html"`로 정적 파싱 확인
3. `dotnet run -- crawl`로 저장 세션이 없을 때 로그인 입력으로 넘어가는지 확인
4. 학교 계정으로 로그인 성공 시 `session_cookies.txt`와 `result.txt` 생성 확인
5. `dotnet run -- status`로 세션 검증, `dotnet run -- crawl`로 재사용 확인
6. `dotnet run -- logout` 후 `dotnet run -- crawl`로 재로그인 요청 확인

**로그인 전에 실패하는 경우**: 처음에 로그인 전 웹 화면의 HTML을 제공하거나 개발자도구 Network에서 실제 인증 요청 URL/파라미터 이름(값은 제거)을 확인하여 `EclassLoginService.cs`의 로그인 어댑터를 수정해야 합니다. 인증 쿠키, 학번, 비밀번호는 공유하면 안 됩니다.
