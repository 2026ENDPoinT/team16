
 GitHub GraphQL API v4 (공식, OAuth Access Token 또는 PAT 사용)

유저가 설정에서 깃허브 계정을 연동(OAuth 2.0)했을 때 발급받은 토큰으로 contributionsCollection 쿼리를 호출합니다.

장점: 비공개 레포지토리(Private Repo) 커밋까지 포함된 정확한 잔디 데이터와 일별 커밋 수, 레벨(NONE ~ FOURTH_QUARTER)을 공식 스키마로 받아옵니다.


2. 공개 계정 기반 JSON 프록시 API (토큰 불필요, Username만 사용)

오픈소스 API([https://github-contributions-api.jogruber.de/v4/](https://github-contributions-api.jogruber.de/v4/){username}?y=last)를 호출해 공개 기여도 데이터를 JSON으로 받아옵니다.

장점: 개발 초기 단계에서 백엔드 OAuth 서버 구축 없이 깃허브 아이디 문자열만 입력하면 즉시 테스트할 수 있습니다.

아키텍처 주의점 (다른 사람 자취방 놀러가기 기능 연동 시):

유저 A가 유저 B의 자취방에 방문할 때, 유저 A의 클라이언트에 유저 B의 깃허브 OAuth 토큰을 절대 내려보내면 안 됩니다.

해결책: 유저 B가 접속하거나 연동할 때 받아온 경량화된 JSON 데이터 스트림(약 1~2KB) 또는 생성된 PNG 바이트 스트림을 게임 DB(또는 서버)에 캐싱해 두고, 유저 A가 방문할 때는 캐싱된 스트림을 받아 렌더링하거나 유저 B의 githubUsername으로 공개 API를 호출하도록 분리해야 합니다.

# 모듈 구조 설계

가구 시스템과 결합도를 완전히 끊어내기 위해 3개의 계층으로 분리합니다.

1. GitHubStreakModels.cs (데이터 스트림 & DTO 계층)

GitHub API 응답 역직렬화 모델 및 가구 시스템이 참조할 최종 데이터(GitHubStreakResult: 총 커밋 수, 현재 연속 스트릭, 최대 스트릭, 일별 데이터 리스트, 직렬화된 JSON 스트림).

2. GitHubStreakRenderer.cs (절차적 텍스처 & 이미지 스트림 변환기)

스트릭 데이터를 받아 Texture2D, Sprite, byte[] (PNG 스트림)으로 변환합니다.

좌표계 보정: 웹(GitHub)은 좌상단 (0, 0)이 일요일이지만, 유니티 Texture2D의 UV 좌표계는 좌하단 (0, 0)이 기준이므로 Y축 반전(6 - dayOfWeek)을 수행하여 상하 반전을 방지합니다.


3. GitHubStreakProvider.cs (비동기 네트워크 통신 매니저)

유니티 6.3 LTS (6000.3.x)의 핵심 비동기 기능인 Awaitable과 UnityWebRequest를 사용하여 메인 스레드 블로킹 없이 데이터를 가져옵니다.

토큰이 있으면 공식 GraphQL API를, 토큰이 없으면(개발 테스트 또는 타 유저 단순 조회) Username 기반 공개 REST API를 자동으로 호출하는 Dual-Mode를 지원합니다.


# 잼미니의 조언

4. 가구 오브젝트 구현 시 연동 가이드 (유니티 2D 아이소메트릭 팁)
등각투영(Isometric) 우측 벽면 기울기(Skew) 처리 방법

첨부한 스케치처럼 우측 벽면에 비스듬히 걸린 액자 형태를 만들 때, 기본 SpriteRenderer의 Rotation만 돌리면 직사각형이 다이아몬드처럼 회전해 버려 수직 기둥 선이 벽면 모서리와 맞지 않습니다.   

해결 방법 A (Transform 계층 트릭 - 셰이더 불필요): 부모 빈 오브젝트(SkewParent)의 Scale을 (1, 0.5, 1)로 줄이고 Rotation Z를 -30도(또는 프로젝트 타일 비율인 26.565도, 즉 $\arctan(0.5)$)로 돌린 뒤, 자식 오브젝트(WallBoard)를 역회전시키면 2D 스프라이트에 완벽한 등각투영 기울기(Shear) 변환이 걸립니다.

해결 방법 B (Shader Graph UV Shear): URP 2D Sprite Unlit Shader Graph에서 UV의 $Y$ 좌표에 $X \times \tan(\theta)$ 만큼 오프셋을 더해주는 간단한 노드를 만들면 _currentBundle.GrassTexture를 꽂았을 때 벽면 각도에 딱 맞게 평행사변형으로 렌더링됩니다.

메모리 관리 (Dispose 패턴)유니티에서 런타임에 new Texture2D()와 Sprite.Create()로 만든 에셋은 가비지 컬렉터(GC)가 자동으로 회수하지 않으므로, 다른 사람의 자취방을 무작위로 여러 번 방문할 때 메모리 누수가 발생할 수 있습니다.

 예시 코드의 OnDestroy처럼 방을 나갈 때 반드시 _currentBundle.Dispose()를 호출하도록 가구 담당자에게 안내해 주면 됩니다.