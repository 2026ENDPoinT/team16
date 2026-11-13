"""공통 에러 응답 JSON.

모든 에러는 같은 형태로 내려간다. 클라이언트는 code 로 분기하고,
message 는 사용자에게 그대로 보여주는 한국어 문구로 쓴다.
"""
from typing import Any, Optional

from pydantic import BaseModel, Field


class ErrorBody(BaseModel):
    code: str = Field(..., description="분기용 코드. 예: INSUFFICIENT_BALANCE")
    message: str = Field(..., description="사용자에게 보여줄 한국어 문구")
    detail: Optional[dict[str, Any]] = Field(
        default=None,
        description="문제가 된 값. 예: {\"gridX\": 2, \"gridZ\": 0}",
    )


class ErrorResponse(BaseModel):
    error: ErrorBody


class ErrorCode:
    """자주 쓰는 코드 모음. 문자열을 코드 곳곳에 흩어두지 않는다."""

    # 공통
    INVALID_REQUEST = "INVALID_REQUEST"
    UNAUTHORIZED = "UNAUTHORIZED"
    TOKEN_EXPIRED = "TOKEN_EXPIRED"
    FORBIDDEN = "FORBIDDEN"
    NOT_FOUND = "NOT_FOUND"
    RATE_LIMITED = "RATE_LIMITED"
    INTERNAL_ERROR = "INTERNAL_ERROR"

    # 인증
    INVALID_CREDENTIALS = "INVALID_CREDENTIALS"
    STUDENT_ID_TAKEN = "STUDENT_ID_TAKEN"
    WEAK_PASSWORD = "WEAK_PASSWORD"

    # 캐릭터 · 설문
    CHARACTER_EXISTS = "CHARACTER_EXISTS"
    SURVEY_ALREADY_DONE = "SURVEY_ALREADY_DONE"
    INVALID_NICKNAME = "INVALID_NICKNAME"
    ITEM_NOT_OWNED = "ITEM_NOT_OWNED"
    INVALID_PRESET = "INVALID_PRESET"
    SLOT_MISMATCH = "SLOT_MISMATCH"

    # 자취방
    ROOM_PRIVATE = "ROOM_PRIVATE"
    ROOM_FRIENDS_ONLY = "ROOM_FRIENDS_ONLY"
    NO_PUBLIC_ROOM = "NO_PUBLIC_ROOM"
    OUT_OF_BOUNDS = "OUT_OF_BOUNDS"
    CELL_OCCUPIED = "CELL_OCCUPIED"
    NO_SUPPORT = "NO_SUPPORT"
    INVALID_PLACEMENT = "INVALID_PLACEMENT"

    # 상점 · 재화
    INSUFFICIENT_BALANCE = "INSUFFICIENT_BALANCE"
    ITEM_LOCKED = "ITEM_LOCKED"
    ALREADY_OWNED = "ALREADY_OWNED"
