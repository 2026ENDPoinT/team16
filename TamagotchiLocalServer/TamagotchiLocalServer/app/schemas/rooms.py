"""HTTP request/response contracts for rooms and shared state."""

from datetime import datetime
from typing import Any

from pydantic import BaseModel, ConfigDict, Field


class RoomCreate(BaseModel):
    name: str | None = Field(default=None, max_length=80)
    initial_state: dict[str, Any] = Field(default_factory=dict)


class RoomRead(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: str
    name: str | None
    state: dict[str, Any]
    revision: int
    created_at: datetime
    updated_at: datetime


class StatePatch(BaseModel):
    """Top-level shallow merge; send null to set a key to null."""

    changes: dict[str, Any] = Field(min_length=1)
    expected_revision: int | None = Field(default=None, ge=0)
