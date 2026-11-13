"""내 방 조회 및 가구/벽지/바닥 배치 저장 API (/v1/room)."""

from fastapi import APIRouter, Header

from app.api.v1.auth import authorized_session
from app.core.auth_errors import AuthError, AuthRoute
from app.repositories import room_repository
from app.repositories import auth_repository
from app.schemas.room import RoomLayout, RoomUpdateRequest, RoomVisitResponse


router = APIRouter(
    prefix="/room",
    tags=["room"],
    route_class=AuthRoute,
)


@router.get("/me", response_model=RoomLayout)
def get_my_room(authorization: str | None = Header(default=None)):
    student_id, _ = authorized_session(authorization)
    return room_repository.get_room(student_id)


@router.put("/me", response_model=RoomLayout)
def update_my_room(
    payload: RoomUpdateRequest,
    authorization: str | None = Header(default=None),
):
    student_id, _ = authorized_session(authorization)
    return room_repository.update_room(student_id, payload)


# 주의: 이 경로는 반드시 /me 보다 뒤에 선언해야 한다.
# 먼저 선언하면 "/room/me" 가 student_id="me" 로 잡힌다.
@router.get("/{student_id}", response_model=RoomVisitResponse)
def get_user_room(
    student_id: str,
    authorization: str | None = Header(default=None),
):
    """다른 사용자의 캐릭터와 자취방을 함께 돌려준다(무작위 방문·친구 방문용)."""
    authorized_session(authorization)

    # 없는 학번으로 부르면 기본값이 새로 만들어져 유령 계정처럼 보인다.
    # 가입된 계정인지 먼저 확인한다.
    if auth_repository.get_user(student_id) is None:
        raise AuthError(404, "NOT_FOUND", "해당 학번의 사용자를 찾을 수 없습니다.")

    return RoomVisitResponse(
        studentId=student_id,
        character=room_repository.get_character(student_id),
        room=room_repository.get_room(student_id),
    )
