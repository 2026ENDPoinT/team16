"""할 일(Todo) 및 eClass 크롤링 동기화 JSON 스키마 (재화 및 수동 체크 제거 버전)."""

from typing import List, Optional
from pydantic import BaseModel, Field


class TodoItemData(BaseModel):
    id: str
    type: str = "assignment"
    title: str
    courseName: str = ""
    dueAt: str = ""


class TodoListResponse(BaseModel):
    items: List[TodoItemData] = Field(default_factory=list)
    total: int = 0
    previousTotal: int = 0
    decreasedCount: int = 0
    syncedAt: str = ""


class TodoSyncRequest(BaseModel):
    cookieHeader: Optional[str] = None
    simulateCompleteOne: bool = False
