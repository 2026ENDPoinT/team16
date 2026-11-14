"""Admin user-list response. Camel case aligns with the existing Unity client."""

from typing import Literal

from pydantic import BaseModel


class AdminUserItem(BaseModel):
    studentId: str
    role: Literal["user", "admin"]
    createdAt: str  # ISO 8601 UTC: YYYY-MM-DDTHH:MM:SSZ


class AdminUsersResponse(BaseModel):
    items: list[AdminUserItem]
    total: int
