"""내 정보와 내 캐릭터 API (/v1/me)."""

from fastapi import APIRouter, Header

from app.api.v1.auth import authorized_session
from app.core.auth_errors import AuthRoute
from app.repositories import room_repository
from app.schemas.character import (
    CharacterData,
    CharacterUpdateRequest,
    SurveySubmitRequest,
    SurveySubmitResponse,
)
from app.schemas.me import MeResponse

router = APIRouter(prefix="/me", tags=["me"], route_class=AuthRoute)


@router.get("", response_model=MeResponse)
def get_me(authorization: str | None = Header(default=None)):
    student_id, _ = authorized_session(authorization)
    return room_repository.get_me(student_id)


@router.post("/survey", response_model=SurveySubmitResponse)
def submit_survey(
    payload: SurveySubmitRequest,
    authorization: str | None = Header(default=None),
):
    student_id, _ = authorized_session(authorization)
    character = room_repository.save_survey(student_id, payload)
    return SurveySubmitResponse(character=character, nextScene="scene_room")


@router.put("/character", response_model=CharacterData)
def update_my_character(
    payload: CharacterUpdateRequest,
    authorization: str | None = Header(default=None),
):
    student_id, _ = authorized_session(authorization)
    return room_repository.update_character(student_id, payload)