"""Flexible event format for future Unity app-to-app communication."""

from datetime import datetime
from typing import Any

from pydantic import BaseModel, ConfigDict, Field


class EventCreate(BaseModel):
    sender_id: str = Field(min_length=1, max_length=64)
    event_type: str = Field(min_length=1, max_length=64)
    payload: dict[str, Any] = Field(default_factory=dict)


class EventRead(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: int
    room_id: str
    sender_id: str
    event_type: str
    payload: dict[str, Any]
    created_at: datetime


class EventList(BaseModel):
    items: list[EventRead]
    next_after_id: int
