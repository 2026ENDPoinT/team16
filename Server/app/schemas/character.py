"""캐릭터 · 설문(페르소나) 요청·응답 JSON.

필드 이름은 Unity 클라이언트의 직렬화 이름과 1:1로 맞춘다.
바꿀 때는 API문서.md 를 먼저 고치고 양쪽에 알린다.
"""
from typing import Literal, Optional

from pydantic import BaseModel, Field

# 설문으로 판정되는 4가지 성격.
# Unity 쪽 MatePersonaType enum 과 문자열이 정확히 일치해야 한다.
Persona = Literal["Diligent", "Cheerleader", "Tsundere", "NightCoder"]

# 꾸미기로 바꿀 수 있는 5개 슬롯.
# 고정 파츠(머리·눈·입·팔·다리·몸)는 presetId 가 결정하므로 여기 없다.
CharacterSlot = Literal["outer", "accessories", "clothTop", "clothBottom", "shoe"]


class CharacterParts(BaseModel):
    """슬롯별로 착용한 아이템 ID. null 이면 그 슬롯은 벗은 상태다."""

    outer: Optional[str] = None
    accessories: Optional[str] = None
    clothTop: Optional[str] = None
    clothBottom: Optional[str] = None
    shoe: Optional[str] = None


class CharacterState(BaseModel):
    """캐릭터의 전체 상태. GET /v1/me 와 캐릭터 저장 응답이 공유한다."""

    nickname: str
    persona: Optional[Persona] = Field(
        default=None,
        description="설문 전에는 null. 설문을 마쳐야 값이 생긴다.",
    )
    catchphrase: str = Field(default="", description="설문 마지막에 입력하는 한 줄 대사")
    presetId: str = Field(
        ...,
        description="캐릭터 종류(고정 파츠 묶음). Unity 의 CharacterPresetDefinition.id",
    )
    parts: CharacterParts = Field(default_factory=CharacterParts)


class SurveyAnswer(BaseModel):
    """설문 한 문항의 답. 서버가 채점을 다시 할 수 있도록 원본을 받아 둔다."""

    questionIndex: int = Field(..., ge=0, description="0-indexed 문항 번호")
    optionIndex: int = Field(..., ge=0, description="0-indexed 선택지 번호")


class SurveySubmitRequest(BaseModel):
    """POST /v1/me/survey — scene_survey 완료 시 한 번 호출한다."""

    nickname: str = Field(..., min_length=1, max_length=20)
    catchphrase: str = Field(default="", max_length=60)
    persona: Persona = Field(..., description="클라이언트가 판정한 결과")
    answers: list[SurveyAnswer] = Field(
        default_factory=list,
        description="판정 근거. 서버는 검증·분석용으로만 저장하고 persona 를 덮어쓰지 않는다.",
    )


class SurveySubmitResponse(BaseModel):
    """설문 저장 결과. 클라이언트는 이 응답을 받은 뒤 scene_room 으로 넘어간다."""

    character: CharacterState
    onboarding: "OnboardingState"


class CharacterUpdateRequest(BaseModel):
    """PUT /v1/me/character — 꾸미기 확정 시 호출한다. 전체 교체(PUT)다."""

    nickname: Optional[str] = Field(default=None, min_length=1, max_length=20)
    presetId: str
    parts: CharacterParts = Field(default_factory=CharacterParts)


class OnboardingState(BaseModel):
    """
    로그인 직후 어느 씬으로 보낼지 정하는 데 쓴다.
    클라이언트가 조건을 조합해 판단하지 않도록 서버가 다음 씬을 직접 알려준다.
    """

    hasCharacter: bool = Field(..., description="캐릭터가 생성되었는지")
    hasPersona: bool = Field(..., description="설문을 마쳐 페르소나가 정해졌는지")
    nextScene: Literal["scene_survey", "scene_room"] = Field(
        ...,
        description="클라이언트가 바로 이동할 씬 이름",
    )


SurveySubmitResponse.model_rebuild()
