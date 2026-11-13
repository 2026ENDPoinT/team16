"""Me (current user profile, wallet, inventory, room) schemas."""

from typing import List, Literal, Optional

from pydantic import BaseModel, Field

from app.schemas.character import CharacterData
from app.schemas.room import RoomLayout


class OnboardingData(BaseModel):
    hasCharacter: bool
    hasPersona: bool
    nextScene: Literal["scene_survey", "scene_room"]


class WalletData(BaseModel):
    totalEarned: int = Field(0, ge=0, description="누적 획득 포인트 (집 문서 해금 기준)")
    balance: int = Field(0, ge=0, description="현재 사용 가능한 잔여 코인")


class MeResponse(BaseModel):
    studentId: str
    role: Literal["user", "admin"]
    onboarding: OnboardingData
    character: Optional[CharacterData] = None
    wallet: WalletData
    inventory: List[str] = Field(default_factory=list)
    room: Optional[RoomLayout] = None