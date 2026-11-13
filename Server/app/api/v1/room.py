"""내 방 조회 및 가구/벽지/바닥 배치 저장 API (/v1/room)."""

from fastapi import APIRouter, Header

from app.api.v1.auth import authorized_session
from app.core.auth_errors import AuthRoute
from app.repositories import room_repository
from app.schemas.room import RoomLayout, RoomUpdateRequest


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
