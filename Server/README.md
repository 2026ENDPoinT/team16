# Server — 짭마고치 FastAPI 기본 골격 (3-Tier)

팀장 `API문서.md`(v1, 2026-11-13)와 `개발문서.md`를 참고해 만든 **서버 폴더 구조**입니다.
**기능 구현은 아직 시작하지 않았습니다.** `GET /health`만 실제 작동합니다.

## 구조

- Tier 1: Unity WebGL / C# 클라이언트 (별도 프로젝트)
- Tier 2: FastAPI 서버 (`app/api/v1/` → `app/services/` → `app/repositories/`)
- Tier 3: SQLite (`data/tamagotchi.db`, **현재 미생성**)
- `app/models/`: 추후 SQLAlchemy 테이블 정의
- `app/schemas/`: 추후 Pydantic JSON 계약 정의
- `app/core/`: 환경설정 / 보안 / 예외 처리
- `app/db/`: DB 연결·세션 코드
- `tests/`: 기본 서버 테스트

## 실행 (Windows + VS Code)

1. **압축 해제 후 `Server` 폴더 자체를 VS Code로 엽니다.**
2. PowerShell 터미널에서 다음 명령어를 순서대로 실행합니다.

```powershell
py -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe -m uvicorn app.main:app --host 127.0.0.1 --port 8000 --reload
```

3. 브라우저 접속:
   - 서버 상태: http://127.0.0.1:8000/health
   - Swagger UI: http://127.0.0.1:8000/docs
   - OpenAPI JSON: http://127.0.0.1:8000/openapi.json

정상 응답 예시 (`serverTime`은 실제 UTC 시각으로 변경됨):

```json
{
  "status": "ok",
  "version": "0.1.0",
  "serverTime": "2026-11-14T00:00:00Z"
}
```

**현재 Swagger UI에 `/health`만 보이는 게 정상입니다.**
다른 API 파일은 빈 라우터 또는 기능별 자리 표시자이며, 실제 엔드포인트가 없습니다.

## 테스트

```powershell
.\.venv\Scripts\python.exe -m pytest -q
```

## 추후 구현 파일 안내

| 명세 기능 | Router | Service | Repository / Model / Schema |
|---|---|---|---|
| 학번 회원가입·로그인 | `api/v1/auth.py` | `services/auth_service.py` | `auth_*`, `user_*`, `models/user.py`, `schemas/auth.py` |
| 내 정보·캐릭터 | `api/v1/me.py` | `services/character_service.py` | `character_*`, `schemas/me.py` |
| 할 일·재화·eClass | `api/v1/tasks.py` | `services/task_service.py`, `sync_service.py`, `wallet_service.py` | `task_*`, `wallet_*` |
| 자취방 | `api/v1/room.py` | `services/room_service.py` | `room_*` |
| 상점·인벤토리 | `api/v1/shop.py` | `services/shop_service.py` | `shop_*`, `inventory_*` |
| GitHub 계정 연결 | `api/v1/external.py` | `services/external_service.py` | `external_*` |
| 친구·수상(선택) | `api/v1/friends.py`, `awards.py`, `admin.py` | `friend_service.py`, `award_service.py` | 기능별 모델·스키마 |

**API 계약**: 기능 구현 시 반드시 `/v1` 기본 경로, camelCase 필드, 문자열 ID, ISO 8601 UTC 및 통일된 에러 구조를 `API문서.md`에 맞춥니다.

## 주의

- `data/tamagotchi.db`는 DB 모델을 구현하여 실제로 DB를 사용할 때 생성됩니다. 지금은 파일이 없어도 정상입니다.
- `app/db/session.py`는 SQLAlchemy 연결을 위한 준비 코드일 뿐, 테이블을 자동 생성하지 않습니다.
- 실제 로그인, DB 저장, 게임 로직, 외부 연동은 아직 동작하지 않습니다.
- `.env`는 로컬 개발용으로, 공유하거나 Git에 업로드하지 마세요.
- 현재 로컬 개발 전용: `127.0.0.1`을 사용합니다. 외부 배포 시 인증, HTTPS, 접근 제어 및 CORS 등을 별도 구성해야 합니다.
