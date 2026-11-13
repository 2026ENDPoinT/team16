# 짭마고치 API 명세

> ENDPoinT 해커톤 2026 — Team 16
> 버전 `v1` · 기준일 2026-11-13
> 이 문서가 **서버와 클라이언트의 계약**이다. 여기 적힌 필드 이름과 타입을 양쪽이 그대로 따른다.
> 변경이 필요하면 코드를 먼저 고치지 말고 이 문서를 먼저 고친 뒤 양쪽에 알린다.

---

## 1. 공통 규약

### 1.1 기본 정보

| 항목 | 값 |
|---|---|
| Base URL (개발) | `http://192.168.0.x:8000/v1` 또는 `http://xserver.iptime.org:8000/v1` |
| Base URL (배포) | `https://<도메인>/v1` |
| 포맷 | JSON (UTF-8) |
| 인증 | `Authorization: Bearer <accessToken>` |

### 1.2 명명 규칙

- 필드명은 **camelCase**
- 시간은 전부 **ISO 8601 UTC** 문자열 (`2026-11-13T04:30:00Z`)
- ID는 문자열. 숫자로 보이더라도 문자열로 주고받는다 (학번 `"20241234"`의 앞자리 0 손실 방지)
- 금액·수량은 정수

### 1.3 응답 형태

성공은 **데이터 객체를 그대로** 반환한다. 불필요한 래핑을 하지 않는다.

```json
{ "studentId": "20241234", "nickname": "짭이" }
```

목록은 `items` 와 `total` 로 감싼다.

```json
{ "items": [ ... ], "total": 42 }
```

### 1.4 에러 형태

모든 에러는 동일한 형태로 반환한다.

```json
{
  "error": {
    "code": "INSUFFICIENT_BALANCE",
    "message": "잔액이 부족합니다.",
    "detail": { "required": 120, "balance": 45 }
  }
}
```

| HTTP | 의미 | 대표 `code` |
|---|---|---|
| 400 | 요청 형식 오류 | `INVALID_REQUEST` |
| 401 | 인증 실패 · 토큰 만료 | `UNAUTHORIZED`, `TOKEN_EXPIRED` |
| 403 | 권한 없음 | `FORBIDDEN`, `ROOM_PRIVATE` |
| 404 | 대상 없음 | `NOT_FOUND` |
| 409 | 상태 충돌 | `ALREADY_COMPLETED`, `ALREADY_OWNED`, `CELL_OCCUPIED` |
| 422 | 비즈니스 규칙 위반 | `INSUFFICIENT_BALANCE`, `ITEM_LOCKED` |
| 429 | 요청 과다 | `RATE_LIMITED` |
| 500 | 서버 오류 | `INTERNAL_ERROR` |

클라이언트는 **`code`로 분기**한다. `message`는 사용자에게 그대로 보여줄 수 있는 한국어 문구이고, 분기 조건으로 쓰지 않는다.

### 1.5 클라이언트 공통 처리

- 401 `TOKEN_EXPIRED` → `/auth/refresh` 1회 시도 → 실패 시 로그인 화면
- 네트워크 오류·5xx → 재시도 버튼을 띄운다. 자동 무한 재시도 금지
- 타임아웃 10초

---

## 2. 인증

### 2.1 로그인

```
POST /auth/login
```

**요청**

```json
{
  "studentId": "20241234",
  "password": "********"
}
```

**응답 200**

```json
{
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "f3c1a9...",
  "expiresIn": 3600,
  "isNewUser": false,
  "role": "user"
}
```

| 필드 | 타입 | 설명 |
|---|---|---|
| `expiresIn` | int | accessToken 유효 시간(초) |
| `isNewUser` | bool | `true`면 클라이언트는 `CharacterCreate` 씬으로 보낸다 |
| `role` | enum | `user` / `admin` |

**에러**

| HTTP | code | 상황 |
|---|---|---|
| 401 | `INVALID_CREDENTIALS` | 학번 또는 비밀번호 불일치 |
| 429 | `RATE_LIMITED` | 로그인 시도 과다 |

> 로그인 실패 시 "학번이 없음"과 "비밀번호 틀림"을 구분해서 알려주지 않는다. 학번 존재 여부가 노출된다.

### 2.2 학번 중복 확인

```
GET /auth/check-id?studentId=20241234
```

인증 불필요. 회원가입 팝업의 "중복확인" 버튼이 호출한다.

**응답 200**

```json
{ "studentId": "20241234", "isAvailable": true }
```

**에러**: 400 `INVALID_REQUEST` — 학번 형식 오류, 429 `RATE_LIMITED` — 호출 과다

> 이 엔드포인트는 학번의 가입 여부를 외부에 노출한다. 반드시 **레이트 리밋을 건다.** 없으면 전체 학번을 훑어 가입자 명단을 만들 수 있다.

### 2.3 회원가입

```
POST /auth/signup
```

인증 불필요.

```json
{
  "studentId": "20241234",
  "password": "********"
}
```

**응답 201** — 2.1과 동일한 형태. `isNewUser`는 항상 `true`.

```json
{
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "f3c1a9...",
  "expiresIn": 3600,
  "isNewUser": true,
  "role": "user"
}
```

**에러**

| HTTP | code | 상황 |
|---|---|---|
| 409 | `STUDENT_ID_TAKEN` | 이미 가입된 학번 |
| 400 | `INVALID_REQUEST` | 학번 형식 오류 |
| 422 | `WEAK_PASSWORD` | 비밀번호 규칙 미달 |

> 가입 직후 바로 로그인된 상태가 되므로, 클라이언트는 응답의 토큰을 저장하고 다음 화면으로 넘어간다. 별도 로그인 호출을 하지 않는다.
> 비밀번호 재입력 일치 검사는 **클라이언트에서만** 한다. 서버는 `password` 하나만 받는다.

### 2.4 토큰 갱신

```
POST /auth/refresh
```

```json
{ "refreshToken": "f3c1a9..." }
```

**응답 200** — 2.1과 동일한 형태. `refreshToken`도 새 값으로 교체한다(회전).

### 2.5 로그아웃

```
POST /auth/logout
```

**응답 204** — 본문 없음. 서버는 해당 refreshToken을 폐기한다.

---

## 3. 내 정보

### 3.1 전체 조회

```
GET /me
```

로그인 직후와 메인 씬 진입 시 호출한다. 캐릭터·지갑·인벤토리를 한 번에 받는다.

**응답 200**

```json
{
  "studentId": "20241234",
  "role": "user",
  "character": {
    "nickname": "짭이",
    "persona": "성실형",
    "parts": {
      "body": "body_01",
      "hair": "hair_03",
      "top": "top_02",
      "bottom": "bottom_01",
      "shoes": "shoes_01",
      "accessory": null
    }
  },
  "wallet": {
    "totalEarned": 1240,
    "balance": 380
  },
  "inventory": [
    "furn_bed_01", "furn_desk_01", "cloth_top_02", "wall_01", "floor_03"
  ],
  "createdAt": "2026-11-01T09:12:00Z"
}
```

- `persona`, `accessory`는 `null` 가능
- `inventory`는 보유 아이템 ID 배열. 가구·의상·벽지·바닥지를 구분 없이 담는다. 분류는 클라이언트가 ScriptableObject 정의로 판단한다

### 3.2 캐릭터 생성

```
POST /me/character
```

최초 1회만 호출된다. 이미 캐릭터가 있으면 409.

```json
{
  "nickname": "짭이",
  "persona": "성실형",
  "parts": {
    "body": "body_01", "hair": "hair_03", "top": "top_02",
    "bottom": "bottom_01", "shoes": "shoes_01", "accessory": null
  }
}
```

**응답 201** — 3.1의 `character` 객체

**에러**: 409 `CHARACTER_EXISTS`, 400 `INVALID_NICKNAME`

### 3.3 캐릭터 외형 저장

```
PUT /me/character
```

꾸미기 확정 시 호출한다. 전체 교체(PUT)이며 부분 수정이 아니다.

```json
{
  "nickname": "짭이",
  "parts": {
    "body": "body_01", "hair": "hair_05", "top": "top_07",
    "bottom": "bottom_01", "shoes": "shoes_01", "accessory": "acc_02"
  }
}
```

**응답 200** — 저장된 `character` 객체

**에러**: 403 `ITEM_NOT_OWNED` — 보유하지 않은 아이템을 착용하려 한 경우. 서버가 인벤토리와 대조해 검증한다

---

## 4. 할 일

### 4.1 목록 조회

```
GET /todos?status=pending&limit=50
```

| 쿼리 | 기본값 | 설명 |
|---|---|---|
| `status` | `all` | `all` / `pending` / `completed` |
| `limit` | 50 | 최대 100 |

**응답 200**

```json
{
  "items": [
    {
      "id": "todo_8f21",
      "source": "eclass",
      "type": "assignment",
      "title": "자료구조 과제 3",
      "courseName": "자료구조",
      "dueAt": "2026-11-15T14:59:00Z",
      "isCompleted": false,
      "completedAt": null,
      "reward": 10
    },
    {
      "id": "todo_a301",
      "source": "manual",
      "type": "custom",
      "title": "포트폴리오 정리",
      "courseName": null,
      "dueAt": null,
      "isCompleted": true,
      "completedAt": "2026-11-12T08:20:00Z",
      "reward": 5
    }
  ],
  "total": 2,
  "syncedAt": "2026-11-13T04:00:00Z"
}
```

| 필드 | 값 |
|---|---|
| `source` | `eclass` / `manual` |
| `type` | `assignment` / `quiz` / `lecture` / `custom` |
| `reward` | 완료 시 받을 재화. 서버가 계산해 내려준다 |
| `syncedAt` | eclass 마지막 동기화 시각. 클라이언트가 "N분 전 동기화" 표시에 쓴다 |

> `reward` 값을 클라이언트가 계산하지 않는다. 지급 규칙이 바뀌어도 서버만 고치면 되게 한다.

### 4.2 완료 처리

```
POST /todos/{id}/complete
```

**요청 본문 없음**

**응답 200**

```json
{
  "todo": {
    "id": "todo_8f21",
    "isCompleted": true,
    "completedAt": "2026-11-13T05:01:00Z"
  },
  "rewarded": 10,
  "wallet": {
    "totalEarned": 1250,
    "balance": 390
  },
  "unlockedItems": ["furn_sofa_02"]
}
```

| 필드 | 설명 |
|---|---|
| `rewarded` | 이번에 지급된 재화. 중복 완료 시 `0` |
| `wallet` | 갱신된 지갑. 클라이언트는 이 값으로 덮어쓴다 |
| `unlockedItems` | 이번 누적 재화 증가로 **새로 해금된** 아이템 ID. 없으면 빈 배열 |

`unlockedItems`가 비어 있지 않으면 "새 아이템 해금!" 연출을 띄운다. 클라이언트가 해금 조건을 직접 비교할 필요가 없다.

**에러**: 409 `ALREADY_COMPLETED`, 404 `NOT_FOUND`

### 4.3 수동 할 일 추가

```
POST /todos
```

```json
{ "title": "포트폴리오 정리", "dueAt": null }
```

**응답 201** — 4.1의 항목 객체

### 4.4 eclass 수동 동기화

```
POST /todos/sync
```

서버가 eclass에서 할 일을 다시 수집한다. 자동 동기화와 별개로 사용자가 새로고침할 때 호출한다.

**응답 200**

```json
{ "added": 3, "updated": 1, "syncedAt": "2026-11-13T05:10:00Z" }
```

**에러**

| HTTP | code | 상황 |
|---|---|---|
| 409 | `SYNC_IN_PROGRESS` | 이미 동기화 중 |
| 429 | `RATE_LIMITED` | 호출 과다. 최소 1분 간격 |
| 503 | `ECLASS_UNAVAILABLE` | eclass 접속 실패 |

> **eclass 연동 방식이 확정되기 전까지 이 엔드포인트는 목 데이터를 반환한다.** 클라이언트는 연동 완료 여부와 무관하게 이 계약대로 구현하면 된다.

---

## 5. 자취방

### 5.1 레이아웃 공통 형태

모든 자취방 응답은 아래 구조를 쓴다.

```json
{
  "studentId": "20241234",
  "ownerNickname": "짭이",
  "visibility": "public",
  "grid": { "width": 5, "depth": 5, "height": 3 },
  "wallpaperId": "wall_01",
  "floorId": "floor_03",
  "placements": [
    { "furnitureId": "furn_bed_01", "gridX": 0, "gridY": 3, "rotation": 0 },
    { "furnitureId": "furn_desk_01", "gridX": 2, "gridY": 0, "rotation": 90 }
  ],
  "character": {
    "nickname": "짭이",
    "parts": { "body": "body_01", "hair": "hair_03", "top": "top_02",
               "bottom": "bottom_01", "shoes": "shoes_01", "accessory": null }
  },
  "updatedAt": "2026-11-12T23:40:00Z"
}
```

| 필드 | 설명 |
|---|---|
| `grid` | 방 크기. **현재 5×5×3이지만 서버가 내려준 값을 쓴다.** 하드코딩 금지 |
| `gridX`, `gridY` | 좌상단 기준 0-indexed 셀 좌표 |
| `rotation` | `0` / `90` / `180` / `270` 만 허용 |
| `character` | 방문 모드에서 방 주인 캐릭터를 렌더링하는 데 쓴다 |

> 방문 모드에서도 같은 구조를 받으므로, `Room` 씬은 본인 방과 타인 방을 **동일한 파싱 코드**로 처리한다.

### 5.2 내 자취방 조회

```
GET /room/me
```

**응답 200** — 5.1 형태

### 5.3 내 자취방 저장

```
PUT /room/me
```

꾸미기 확정 시 호출한다. 전체 교체다.

```json
{
  "wallpaperId": "wall_01",
  "floorId": "floor_03",
  "placements": [
    { "furnitureId": "furn_bed_01", "gridX": 0, "gridY": 3, "rotation": 0 },
    { "furnitureId": "furn_desk_01", "gridX": 2, "gridY": 0, "rotation": 90 }
  ]
}
```

**응답 200** — 5.1 형태

**서버 검증 항목** (클라이언트도 UI 단계에서 동일하게 검증하되, 최종 판정은 서버)

| 검증 | 에러 code |
|---|---|
| 보유하지 않은 아이템 | 403 `ITEM_NOT_OWNED` |
| 그리드 범위를 벗어남 | 400 `OUT_OF_BOUNDS` |
| 가구끼리 셀 충돌 | 409 `CELL_OCCUPIED` |
| 벽 전용 가구를 바닥에 배치 | 400 `INVALID_PLACEMENT` |
| `rotation`이 0/90/180/270이 아님 | 400 `INVALID_REQUEST` |

`detail`에 문제가 된 항목을 담는다.

```json
{
  "error": {
    "code": "CELL_OCCUPIED",
    "message": "가구가 겹칩니다.",
    "detail": { "gridX": 2, "gridY": 0, "conflictWith": "furn_bed_01" }
  }
}
```

### 5.4 공개 범위 변경

```
PUT /room/me/visibility
```

```json
{ "visibility": "friends" }
```

`public` / `friends` / `private` 중 하나.

**응답 200** — `{ "visibility": "friends" }`

### 5.5 타인 자취방 조회

```
GET /room/{studentId}
```

**응답 200** — 5.1 형태 (단, 방문 모드이므로 편집 불가는 클라이언트가 처리)

**에러**

| HTTP | code | 상황 |
|---|---|---|
| 403 | `ROOM_PRIVATE` | 비공개 방 |
| 403 | `ROOM_FRIENDS_ONLY` | 친구 공개인데 친구가 아님 |
| 404 | `NOT_FOUND` | 없는 학번 |

> 상대가 **오프라인이어도 조회된다.** 저장된 레이아웃을 내려주는 방식이기 때문이다.

### 5.6 랜덤 방문

```
GET /room/random
```

공개 상태인 방 중 하나를 무작위로 반환한다. 본인 방은 제외한다.

**응답 200** — 5.1 형태
**에러**: 404 `NO_PUBLIC_ROOM` — 공개된 방이 하나도 없음

---

## 6. 상점

### 6.1 목록 조회

```
GET /shop/items?category=furniture
```

| 쿼리 | 설명 |
|---|---|
| `category` | `furniture` / `clothing` / `wallpaper` / `floor` / 생략 시 전체 |

**응답 200**

```json
{
  "items": [
    {
      "itemId": "furn_sofa_02",
      "category": "furniture",
      "price": 150,
      "unlockAtTotal": 1000,
      "isUnlocked": true,
      "isOwned": false
    },
    {
      "itemId": "furn_fridge_01",
      "category": "furniture",
      "price": 400,
      "unlockAtTotal": 3000,
      "isUnlocked": false,
      "isOwned": false
    }
  ],
  "total": 2,
  "wallet": { "totalEarned": 1240, "balance": 380 }
}
```

- `isUnlocked` · `isOwned`는 **서버가 계산해서 내려준다.** 클라이언트가 `totalEarned`와 비교하지 않는다
- 잠긴 아이템도 목록에 포함한다. 잠금 상태로 노출해 목표를 보여주는 것이 기획 의도다
- 이름·아이콘·설명은 응답에 없다. 클라이언트의 ScriptableObject에서 `itemId`로 조회한다

### 6.2 구매

```
POST /shop/purchase
```

```json
{ "itemId": "furn_sofa_02" }
```

**응답 200**

```json
{
  "itemId": "furn_sofa_02",
  "paid": 150,
  "wallet": { "totalEarned": 1240, "balance": 230 },
  "inventory": ["furn_bed_01", "furn_desk_01", "furn_sofa_02", "cloth_top_02"]
}
```

**에러**

| HTTP | code | 상황 |
|---|---|---|
| 422 | `INSUFFICIENT_BALANCE` | 잔액 부족 |
| 422 | `ITEM_LOCKED` | 누적 재화 조건 미달 |
| 409 | `ALREADY_OWNED` | 이미 보유 |
| 404 | `NOT_FOUND` | 없는 아이템 |

> 클라이언트는 **구매 전에 미리 차감하지 않는다.** 응답의 `wallet`·`inventory`로 덮어쓴다. 낙관적 업데이트를 하면 서버 거절 시 롤백 처리가 필요해지고, 재화 표시가 어긋나는 버그의 원인이 된다.

---

## 7. 외부 연동

> 클라이언트는 외부 서비스를 직접 호출하지 않는다. 아래 엔드포인트만 쓴다.

### 7.1 연결된 계정 목록

```
GET /external/accounts
```

```json
{
  "items": [
    { "provider": "github", "accountName": "hyuni02", "connectedAt": "2026-11-05T12:00:00Z" }
  ],
  "total": 1
}
```

### 7.2 GitHub 연결 시작

```
POST /external/github/connect
```

**응답 200**

```json
{ "authorizeUrl": "https://github.com/login/oauth/authorize?client_id=...&state=..." }
```

클라이언트는 이 URL을 브라우저로 연다. WebGL은 `Application.OpenURL`, 네이티브는 외부 브라우저. 인증 완료 후 서버가 콜백을 처리하므로 클라이언트는 7.1을 다시 호출해 연결 여부를 확인한다.

> **액세스 토큰은 어떤 응답에도 포함되지 않는다.** 서버가 암호화해서 보관한다.

### 7.3 GitHub 연결 해제

```
DELETE /external/github
```

**응답 204**

### 7.4 스트릭 조회

```
GET /external/github/streak
```

GitHub 스트릭 액자가 호출한다.

**응답 200**

```json
{
  "accountName": "hyuni02",
  "currentStreak": 12,
  "longestStreak": 41,
  "totalContributions": 832,
  "lastContributedAt": "2026-11-13T00:00:00Z",
  "weeks": [
    { "days": [0, 2, 5, 1, 0, 0, 3] },
    { "days": [4, 4, 0, 0, 7, 2, 1] }
  ],
  "cachedAt": "2026-11-13T03:00:00Z"
}
```

| 필드 | 설명 |
|---|---|
| `weeks` | 최근 53주. 각 주는 일요일부터 7일. 값은 그날의 기여 수 |
| `cachedAt` | 서버 수집 시각. 매 호출마다 GitHub를 때리지 않는다 |

**에러**: 404 `NOT_CONNECTED` — GitHub 계정 미연결. 액자는 "연결하기" 상태로 표시한다

---

## 8. 친구 (선택 기능)

### 8.1 목록

```
GET /friends
```

```json
{
  "items": [
    { "studentId": "20245678", "nickname": "강모씨", "roomVisibility": "public" }
  ],
  "total": 1
}
```

### 8.2 학번 검색

```
GET /friends/search?studentId=20245678
```

```json
{ "studentId": "20245678", "nickname": "강모씨", "isFriend": false, "requestSent": false }
```

**에러**: 404 `NOT_FOUND`

### 8.3 친구 요청

```
POST /friends/request
```

```json
{ "studentId": "20245678" }
```

**응답 201** — `{ "status": "pending" }`
**에러**: 409 `ALREADY_FRIEND`, 409 `REQUEST_EXISTS`, 400 `CANNOT_ADD_SELF`

### 8.4 요청 수락 · 거절

```
POST /friends/requests/{studentId}/accept
POST /friends/requests/{studentId}/reject
```

**응답 204**

### 8.5 받은 요청 목록

```
GET /friends/requests
```

```json
{ "items": [ { "studentId": "20249999", "nickname": "아무개", "requestedAt": "..." } ], "total": 1 }
```

---

## 9. 수상 경력

### 9.1 제출

```
POST /awards
```

```json
{
  "contestName": "ENDPoinT 해커톤 2026",
  "awardName": "대상",
  "awardedAt": "2026-11-15",
  "evidenceUrl": "https://..."
}
```

**응답 201**

```json
{ "id": "award_3f91", "status": "pending", "submittedAt": "2026-11-16T10:00:00Z" }
```

### 9.2 내 제출 목록

```
GET /awards/me
```

```json
{
  "items": [
    {
      "id": "award_3f91",
      "contestName": "ENDPoinT 해커톤 2026",
      "awardName": "대상",
      "status": "approved",
      "grantedItemId": "furn_trophy_01",
      "reviewedAt": "2026-11-17T09:00:00Z",
      "rejectReason": null
    }
  ],
  "total": 1
}
```

`status`: `pending` / `approved` / `rejected`
승인 시 `grantedItemId`가 인벤토리에 추가된다.

### 9.3 관리자 — 대기 목록

```
GET /admin/awards?status=pending
```

`role: admin` 전용. 아니면 403 `FORBIDDEN`.

```json
{
  "items": [
    {
      "id": "award_3f91",
      "studentId": "20241234",
      "nickname": "짭이",
      "contestName": "ENDPoinT 해커톤 2026",
      "awardName": "대상",
      "awardedAt": "2026-11-15",
      "evidenceUrl": "https://...",
      "submittedAt": "2026-11-16T10:00:00Z"
    }
  ],
  "total": 1
}
```

### 9.4 관리자 — 승인 · 반려

```
POST /admin/awards/{id}/approve
```

```json
{ "grantItemId": "furn_trophy_01" }
```

```
POST /admin/awards/{id}/reject
```

```json
{ "reason": "증빙 자료를 확인할 수 없습니다." }
```

**응답 200** — 9.2의 항목 객체

---

## 10. 헬스 체크

```
GET /health
```

인증 불필요. 서버 구동 확인과 클라이언트의 연결 상태 표시에 쓴다.

```json
{ "status": "ok", "version": "0.1.0", "serverTime": "2026-11-13T05:30:00Z" }
```

---

## 11. 구현 순서

서버·클라이언트 모두 아래 순서로 붙이면 중간중간 동작하는 상태를 유지할 수 있다.

| 순서 | 엔드포인트 | 비고 |
|---|---|---|
| 1 | `/health` | 연결 확인용. 가장 먼저 |
| 2 | `/auth/login`, `/auth/signup`, `/auth/check-id`, `/auth/refresh`, `/me` | 로그인·회원가입 흐름 완성 |
| 3 | `/me/character` (POST, PUT) | 캐릭터 생성·꾸미기 |
| 4 | `/room/me` (GET, PUT) | **자취방 저장·로드. 핵심** |
| 5 | `/todos`, `/todos/{id}/complete` | 재화 획득 루프 |
| 6 | `/shop/items`, `/shop/purchase` | 소비 루프 완성 |
| 7 | `/external/github/*` | 스트릭 액자 |
| 8 | `/todos/sync` 실연동 | eclass. 그 전까지 목 데이터 |
| 9 | `/friends/*`, `/room/{id}`, `/room/random` | 선택 기능 |
| 10 | `/awards/*`, `/admin/awards/*` | 선택 기능 |

**4번까지 되면 데모의 뼈대가 서고, 6번까지 되면 기획의 핵심 루프가 전부 보인다.**

---

## 12. 미확정 항목

- [ ] 로그인 비밀번호를 학교 계정과 연동할지, 앱 자체 비밀번호를 둘지
- [ ] `accessToken` 만료 시간 (현재 명세는 3600초 가정)
- [ ] eclass 동기화 주기
- [ ] `reward` 지급량 — 개발문서.md 6.2의 임시값
- [ ] 아이템 ID 네이밍 규칙 확정 (`furn_` / `cloth_` / `wall_` / `floor_` / `acc_` 접두사 제안)
