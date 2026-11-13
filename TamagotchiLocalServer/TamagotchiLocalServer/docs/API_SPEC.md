# 로컬 서버 API 명세 (v1)

기본 주소: `http://127.0.0.1:8000`

모든 요청과 응답은 달리 명시하지 않는 한 `application/json`을 사용합니다. `{room_id}`는 `POST /rooms`가 반환하는 `id`입니다. **동일한 room_id를 쓰는 클라이언트는 같은 데이터에 접근합니다.**

## 1. 서버 확인

**GET** `/api/v1/health`

응답 `200 OK`:

```json
{"status": "ok"}
```

## 2. 새 방 만들기

**POST** `/api/v1/rooms`

요청 (각 필드는 선택 사항):

```json
{
  "name": "우리 팀 다마고치",
  "initial_state": {"hunger": 70, "mood": "happy"}
}
```

응답 `201 Created` (ID/시간은 예시):

```json
{
  "id": "25d056ce-070f-4aa1-98f6-54d18b1dd95b",
  "name": "우리 팀 다마고치",
  "state": {"hunger": 70, "mood": "happy"},
  "revision": 0,
  "created_at": "2026-11-13T14:00:00",
  "updated_at": "2026-11-13T14:00:00"
}
```

`id` 값을 Unity A와 Unity B 모두 알고 있어야 같은 방을 사용합니다. 현재는 ID를 전달할 로그인/초대 시스템을 구현하지 않았으므로 개발 중에는 직접 복사해 사용하세요.

## 3. 방 조회

**GET** `/api/v1/rooms/{room_id}`

응답 `200 OK`:

```json
{
  "id": "25d056ce-070f-4aa1-98f6-54d18b1dd95b",
  "name": "우리 팀 다마고치",
  "state": {"hunger": 70, "mood": "happy"},
  "revision": 0,
  "created_at": "2026-11-13T14:00:00",
  "updated_at": "2026-11-13T14:00:00"
}
```

`404`: 해당 방이 존재하지 않음.

## 4. 공유 상태 변경

**PATCH** `/api/v1/rooms/{room_id}/state`

요청:

```json
{
  "changes": {"hunger": 90},
  "expected_revision": 0
}
```

- `changes`는 한 개 이상의 Key를 가진 JSON 객체여야 합니다.
- 기존 상태에 최상위 Key를 **얕게 병합(shallow merge)**합니다. 이 예시는 `mood` 값을 보존하고 `hunger`만 변경합니다.
- Key에 `null`을 보내면 삭제가 아니라 **그 Key 값을 null로 저장**합니다.
- `expected_revision`은 생략할 수 있습니다. 입력하면 현재 `revision`과 같아야 갱신됩니다.
- 성공할 때마다 `revision`이 1 증가합니다. 다른 요청과 갱신이 충돌하면 `409 Conflict`를 반환합니다. 최신 상태를 다시 읽은 뒤 재시도해야 합니다.

응답 `200 OK`: 변경 후 전체 Room 객체.

예시:

```json
{
  "id": "25d056ce-070f-4aa1-98f6-54d18b1dd95b",
  "name": "우리 팀 다마고치",
  "state": {"hunger": 90, "mood": "happy"},
  "revision": 1,
  "created_at": "2026-11-13T14:00:00",
  "updated_at": "2026-11-13T14:01:00"
}
```

## 5. 이벤트 보내기

**POST** `/api/v1/rooms/{room_id}/events`

요청:

```json
{
  "sender_id": "unity_a",
  "event_type": "fed",
  "payload": {"food": "apple", "amount": 20}
}
```

- `sender_id`: 누가 보냈는지 표시하는 클라이언트 이름 (1~64자)
- `event_type`: 이벤트 분류, 추후 Unity 구현에서 정의 (1~64자)
- `payload`: 추가 JSON 데이터 객체 (생략 가능)
- 응답 `201 Created`: 자동 생성된 숫자 `id`, `room_id`, `sender_id`, `event_type`, `payload`, `created_at`을 포함한 이벤트 객체.

## 6. 새 이벤트 읽기

**GET** `/api/v1/rooms/{room_id}/events?after_id=0&limit=50`

- `after_id`: 마지막으로 처리한 이벤트 ID. 0이면 처음부터.
- `limit`: 반환할 최대 개수(1~100), 기본값 50.
- 이벤트는 ID 오름차순으로 반환됩니다.

응답 `200 OK`:

```json
{
  "items": [
    {
      "id": 1,
      "room_id": "25d056ce-070f-4aa1-98f6-54d18b1dd95b",
      "sender_id": "unity_a",
      "event_type": "fed",
      "payload": {"food": "apple", "amount": 20},
      "created_at": "2026-11-13T14:01:10"
    }
  ],
  "next_after_id": 1
}
```

이후에는 `after_id=1`로 다시 요청해야 이전 메시지 중복 처리를 방지할 수 있습니다. 이 방식은 **폴링**이고 자동 푸시가 아닙니다. `sender_id`를 확인해 자신이 보낸 이벤트를 무시할 수도 있습니다.

## 7. 방 삭제

**DELETE** `/api/v1/rooms/{room_id}`

성공하면 `204 No Content`를 반환합니다. 해당 방과 소속 이벤트들이 영구적으로 삭제됩니다. 되돌릴 수 없습니다.

## 8. 오류 응답

| 코드 | 의미 |
|---|---|
| 200 | 조회/갱신 성공 |
| 201 | 생성 성공 |
| 204 | 삭제 성공(본문 없음) |
| 404 | 방을 찾을 수 없음 |
| 409 | 상태 버전 충돌 |
| 422 | 요청 JSON 자료형/필수 필드/범위가 잘못됨 |
| 500 | 서버 내부 오류 |

오류 시 보통 `{ "detail": "..." }` 형식을 반환합니다. 입력 검증 오류(`422`)는 `detail`에 검증 결과 목록을 반환합니다.
