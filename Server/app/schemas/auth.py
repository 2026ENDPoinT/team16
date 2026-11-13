"""Request/response field names match API문서.md (camelCase)."""
from typing import Literal

from pydantic import BaseModel, Field


class LoginRequest(BaseModel):
    studentId: str = Field(..., description="앱 자체 계정 학번 (eClass 계정과 별개)")
    password: str


class RefreshRequest(BaseModel):
    refreshToken: str


class AuthResponse(BaseModel):
    accessToken: str
    refreshToken: str
    expiresIn: int = 3600
    isNewUser: bool
    role: Literal["user", "admin"]
