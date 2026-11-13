# Tamagotchi Local Server (FastAPI)

Unity 애플리케이션 사이에서 **공유 상태**와 **메시지(이벤트)**를 주고받기 위한 로컬 REST API 서버입니다.

- **Python + FastAPI + Uvicorn**
- 서버 주소: `http://127.0.0.1:8000` (localhost 전용)
- **SQLite 영구 저장**: 서버를 재시작해도 방/상태/이벤트 유지
- **확장 가능한 계층형 구조**: API / 데이터 스키마 / 서비스 / DB 접근 / 설정 분리
- **버전 지정 API**: `/api/v1/...`
- **자동 API 문서**: `http://127.0.0.1:8000/docs`
- **자동 테스트**: `pytest`

> **현재 목적:** 같은 컴퓨터에서 실행하는 두 Unity 프로그램의 통신을 확인하기 위한 개발용 서버. 다른 컴퓨터에 연결하거나 인터넷에 공개하는 서비스가 아닙니다. 아직 사용자 인증이나 접근 제어가 없습니다.

## 1. 준비 사항

- Windows 10/11, VS Code, Python **3.10 이상**
- 프로젝트 폴더를 VS Code에서 열고 **터미널 → 새 터미널**에서 아래 명령을 실행합니다.

```powershell
# Python 설치 확인
py --version

# 프로젝트 전용 가상환경 만들기
py -m venv .venv

# 가상환경 안에 패키지 설치 (PowerShell 활성화 필요 없음)
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
```

## 2. 로컬 서버 실행

프로젝트 최상위 폴더(`run_server.py`가 있는 폴더)에서:

```powershell
.\.venv\Scripts\python.exe run_server.py
```

브라우저에서 확인:

- `http://127.0.0.1:8000/` → 서버 안내
- `http://127.0.0.1:8000/api/v1/health` → `{"status":"ok"}`
- `http://127.0.0.1:8000/docs` → 인터랙티브 API 테스트 화면

터미널에서 `Ctrl + C`를 눌러 종료합니다. 실행하는 동안 터미널 창을 유지하세요.

## 3. Unity A → 서버 → Unity B 시연

서버 실행 상태에서 **새 PowerShell 터미널**을 열어:

```powershell
.\.venv\Scripts\python.exe examples\demo_client.py
```

이 예제는 두 Unity 앱의 역할을 Python으로 대신 수행합니다.

1. Unity A 역할: 방 생성 → 포만감 70 → 90으로 수정 → `fed` 이벤트 전송
2. Unity B 역할: 동일한 방 상태 조회 → `fed` 이벤트 수신
3. SQLite 파일 `data/tamagotchi.db`에 해당 데이터 저장

실제 Unity C# 프로젝트에서는 같은 HTTP 주소와 JSON 규격을 사용하면 됩니다. 서버를 재시작해도 저장된 데이터는 남습니다.

## 4. 테스트

```powershell
.\.venv\Scripts\python.exe -m pytest -q
```

현재 테스트: 상태 공유, 이벤트 전송/조회, 상태 버전 충돌, 오류 및 입력 검증, 삭제, 재시작 후 저장 유지.

## 5. 주요 API

| 메서드 | 주소 | 기능 |
|---|---|---|
| GET | `/api/v1/health` | 서버 상태 확인 |
| POST | `/api/v1/rooms` | 공유 방 생성 |
| GET | `/api/v1/rooms/{room_id}` | 방과 공유 상태 조회 |
| PATCH | `/api/v1/rooms/{room_id}/state` | 상태 일부 변경 |
| POST | `/api/v1/rooms/{room_id}/events` | 메시지(이벤트) 추가 |
| GET | `/api/v1/rooms/{room_id}/events?after_id=0` | 새 이벤트 조회 |
| DELETE | `/api/v1/rooms/{room_id}` | 방과 이벤트 삭제 |

자세한 요청 JSON, 응답, 오류 코드는 **`docs/API_SPEC.md`** 참조.

## 6. 구조

```text
TamagotchiLocalServer/
├── app/
│   ├── main.py                # FastAPI 객체 생성, 라우터 연결, 시작/종료
│   ├── api/v1/               # HTTP 주소(GET, POST, PATCH, DELETE)
│   ├── schemas/              # Unity와 통신할 JSON 형식(Pydantic)
│   ├── services/             # 상태 변경 및 이벤트 업무 규칙
│   ├── repositories/         # 데이터베이스 읽기/쓰기
│   ├── db/                   # SQLite 연결, 데이터 테이블
│   └── core/config.py        # 호스트, 포트, DB 설정
├── data/                     # 실제 SQLite 파일(최초 실행 시 생성)
├── docs/                     # API 명세, 설계/수정 안내
├── examples/demo_client.py   # 서버 통신 시연 스크립트
├── tests/                    # 자동 테스트
├── run_server.py             # 서버 시작 명령 파일
├── requirements.txt          # 설치할 Python 패키지
└── .env.example              # 설정 예시
```

자세한 유지보수 설명은 **`docs/ARCHITECTURE.md`** 참조.

## 7. 나중에 수정할 때

- **Unity에서 전송할 필드가 바뀔 때:** `app/schemas/`에서 요청/응답 모델 조정
- **게임 규칙(포만감, 경험치 등) 추가:** `app/services/`에 기능 추가
- **API URL 추가:** `app/api/v1/`에 엔드포인트 추가
- **DB 저장 내용이 바뀔 때:** `app/db/models.py`와 `app/repositories/` 수정 (DB 스키마 마이그레이션 필요)
- **로컬 접속 주소/포트/DB 파일 변경:** `.env.example`을 `.env`로 복사해 설정

## 8. 제한 사항

- `127.0.0.1`은 **서버가 실행되는 컴퓨터 자신**입니다. 다른 컴퓨터의 Unity에서 `127.0.0.1`을 사용하면 그 다른 컴퓨터를 가리킵니다.
- 상태 변경은 자동 푸시가 아니라 **조회(GET)** 방식입니다. 이벤트 역시 **폴링(GET)**으로 받습니다. 실시간 푸시가 필요하면 추후 WebSocket을 추가할 수 있습니다.
- 현재 방 ID를 알고 있으면 같은 PC의 프로그램이 접근할 수 있습니다. **인증이 없으므로 실제 사용자 데이터나 비밀번호를 넣지 마세요.**
- 데이터베이스 테이블은 최초 실행 시 자동 생성되지만 **기존 테이블 구조 변경(마이그레이션)은 지원하지 않습니다.** 구조를 바꿀 때 Alembic 등을 추가해야 합니다.
- Unity WebGL(브라우저)과 연결하는 경우에는 브라우저의 CORS 설정이 추가로 필요합니다. Windows Unity 앱에서는 보통 필요 없습니다.
- 규모가 크거나 인터넷 외부에 공개하는 서버로 운영하기 전에는 인증, HTTPS, 백업, 로깅 및 배포 설정을 보강해야 합니다.
