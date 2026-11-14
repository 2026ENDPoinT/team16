"""Character and Survey schemas."""

from typing import List, Literal, Optional

from pydantic import BaseModel, Field


PersonaType = Literal["backend", "frontend", "ai", "game", "security", "pm"]


class CharacterPartsData(BaseModel):
    outer: Optional[str] = None
    accessories: Optional[str] = None
    clothTop: Optional[str] = None
    clothBottom: Optional[str] = None
    shoe: Optional[str] = None


class CharacterData(BaseModel):
    nickname: str = Field(..., min_length=1, max_length=12)
    persona: PersonaType
    catchphrase: str = Field("", max_length=40)
    presetId: str = "preset_default_1"
    parts: CharacterPartsData = Field(default_factory=CharacterPartsData)


class CharacterUpdateRequest(BaseModel):
    presetId: str
    parts: CharacterPartsData


class SurveyAnswerItem(BaseModel):
    questionIndex: int = Field(..., ge=0)
    optionIndex: int = Field(..., ge=0)


class SurveySubmitRequest(BaseModel):
    nickname: str = Field(..., min_length=1, max_length=12)
    catchphrase: str = Field("", max_length=40)
    persona: PersonaType
    answers: List[SurveyAnswerItem]


class SurveySubmitResponse(BaseModel):
    character: CharacterData
    nextScene: str = "scene_room"