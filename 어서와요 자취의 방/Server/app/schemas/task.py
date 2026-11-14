"""할 일(Todo) & eClass 크롤링 리프레시 스키마.

- 재화(Wallet) 삭제
- 수동 완료 체크 삭제
- 크롤링/리프레시 시 이전 할 일 개수(previousTotal)와 현재 할 일 개수(total)를 비교해
  줄어든 개수(decreasedCount)를 반환한다.
"""

from pydantic import BaseModel, Field


class TodoItemData(BaseModel):
    id: str
    type: str = "assignment"
    title: str
    courseName: str = ""
    dueAt: str = ""


class TodoListResponse(BaseModel):
    items: list[TodoItemData] = Field(default_factory=list)
    total: int = 0
    previousTotal: int = 0
    decreasedCount: int = 0
    syncedAt: str = ""
    source: str = "demo"
    message: str = ""


class TodoSyncRequest(BaseModel):
    cookieHeader: str | None = None
    eclassId: str | None = None
    eclassPassword: str | None = None
    openBrowserLogin: bool = False
    simulateCompleteOne: bool = True
