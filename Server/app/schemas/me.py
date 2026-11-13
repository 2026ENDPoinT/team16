"""GET /v1/me 전체 응답 JSON.

로그인 직후와 scene_room 진입 시 한 번 호출해서
캐릭터·지갑·인벤토리·자취방을 한꺼번에 받는다.
왕복을 줄이려고 묶어 둔 것이므로, 화면마다 쪼개 부르지 않는다.
"""
from typing import Literal, Optional

from pydantic import BaseModel, Field

from app.schemas.character import CharacterState, OnboardingState
from app.schemas.room import RoomLayout


class Wallet(BaseModel):
    """
    누적 획득 재화와 사용 가능 재화를 분리해 관리한다.
    totalEarned 는 상점 해금 조건에 쓰이므로 소모되지 않는다.
    """

    totalEarned: int = Field(default=0, ge=0, description="누적 획득 재화. 절대 감소하지 않는다.")
    balance: int = Field(default=0, ge=0, description="사용 가능 재화. 구매 시 감소한다.")


class MeResponse(BaseModel):
    """
    로그인 후 클라이언트가 가장 먼저 받는 묶음.

    room 을 함께 내려주는 이유:
    "다음에 로그인하면 바로 scene_room 으로 가서 이전에 꾸민 상태를 불러온다"는
    요구를 한 번의 요청으로 끝내기 위해서다.
    """

    studentId: str
    role: Literal["user", "admin"] = "user"
    onboarding: OnboardingState
    character: Optional[CharacterState] = Field(
        default=None,
        description="캐릭터 생성 전에는 null",
    )
    wallet: Wallet = Field(default_factory=Wallet)
    inventory: list[str] = Field(
        default_factory=list,
        description="보유 아이템 ID 목록. 가구·의상·벽지·바닥지를 구분 없이 담는다.",
    )
    room: Optional[RoomLayout] = Field(
        default=None,
        description="자취방 상태. 아직 만들어지지 않았으면 null",
    )
    createdAt: Optional[str] = Field(default=None, description="ISO 8601 UTC")
    lastLoginAt: Optional[str] = Field(default=None, description="ISO 8601 UTC")
