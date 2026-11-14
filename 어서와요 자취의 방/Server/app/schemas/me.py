"""Me (current user profile, wallet, inventory, room) schemas."""

from typing import List, Literal, Optional

from pydantic import BaseModel, Field

from app.schemas.character import CharacterData
from app.schemas.room import RoomLayout


class OnboardingData(BaseModel):
    hasCharacter: bool
    hasPersona: bool
    nextScene: Literal["scene_survey", "scene_room"]


class MeResponse(BaseModel):
    studentId: str
    role: Literal["user", "admin"]
    onboarding: OnboardingData
    character: Optional[CharacterData] = None
    inventory: List[str] = Field(default_factory=list)
    room: Optional[RoomLayout] = None