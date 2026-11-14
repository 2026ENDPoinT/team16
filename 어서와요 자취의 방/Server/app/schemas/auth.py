"""Auth API requests and responses: field names match API문서.md exactly."""
from typing import Literal
from pydantic import BaseModel, Field


class LoginRequest(BaseModel):
    studentId: str = Field(..., description="앱 자체 학번 (eClass 로그인과 무관)")
    password: str


class SignupRequest(BaseModel):
    studentId: str = Field(..., description="앱 자체 학번 (eClass 로그인과 무관)")
    password: str


class StudentIdAvailabilityResponse(BaseModel):
    studentId: str
    isAvailable: bool


class RefreshRequest(BaseModel):
    refreshToken: str


class AuthResponse(BaseModel):
    accessToken: str
    refreshToken: str
    expiresIn: int = 3600
    isNewUser: bool
    role: Literal["user", "admin"]
