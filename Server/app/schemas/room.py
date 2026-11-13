"""자취방 그리드 · 가구 배치 · 벽지/바닥 JSON.

좌표계는 Unity 의 RoomGrid 와 같다.
  - 칸 (0,0,0) 은 방의 최소 모서리
  - gridX = 가로, gridY = 높이(층), gridZ = 세로
  - 1칸 = 1유닛 = 200px
"""
from typing import Literal, Optional

from pydantic import BaseModel, Field

RoomVisibility = Literal["public", "friends", "private"]

# 90도 단위만 허용한다. Unity 는 내부적으로 0~3 단계로 들고 있고 저장할 때 도(°)로 바꾼다.
Rotation = Literal[0, 90, 180, 270]


class RoomGridSize(BaseModel):
    """
    방 크기. 클라이언트는 이 값을 하드코딩하지 않고 서버가 준 값을 쓴다.
    방을 넓히는 변경을 서버만 고쳐서 적용할 수 있게 하기 위해서다.
    """

    width: int = Field(default=5, ge=1, description="X축 칸 수")
    height: int = Field(default=4, ge=1, description="Y축(높이) 칸 수")
    depth: int = Field(default=5, ge=1, description="Z축 칸 수")


class FurniturePlacement(BaseModel):
    """가구 한 개의 배치. 점유 영역의 최소 모서리 칸과 회전만 저장한다."""

    furnitureId: str = Field(..., description="FurnitureDefinition.id")
    gridX: int = Field(..., ge=0)
    gridY: int = Field(..., ge=0, description="층. 바닥 가구는 항상 0")
    gridZ: int = Field(..., ge=0)
    rotation: Rotation = 0


class RoomLayout(BaseModel):
    """
    자취방 전체 상태.
    내 방 조회·저장과 타인 방 방문이 모두 이 구조를 쓴다.
    같은 구조라서 Unity 의 Room 씬이 파싱 코드 하나로 두 모드를 처리한다.
    """

    studentId: str
    ownerNickname: str = ""
    visibility: RoomVisibility = "public"
    grid: RoomGridSize = Field(default_factory=RoomGridSize)
    wallpaperId: str = Field(..., description="RoomSurfaceDefinition.id (벽지). 항상 하나가 선택된다.")
    floorId: str = Field(..., description="RoomSurfaceDefinition.id (바닥). 항상 하나가 선택된다.")
    placements: list[FurniturePlacement] = Field(default_factory=list)
    updatedAt: Optional[str] = Field(default=None, description="ISO 8601 UTC")


class RoomSaveRequest(BaseModel):
    """
    PUT /v1/room/me — 꾸미기 확정 시 호출한다. 전체 교체다.
    studentId 는 토큰에서 읽으므로 본문에 넣지 않는다.
    """

    wallpaperId: str
    floorId: str
    placements: list[FurniturePlacement] = Field(default_factory=list)


class RoomVisibilityRequest(BaseModel):
    """PUT /v1/room/me/visibility"""

    visibility: RoomVisibility


class RoomVisibilityResponse(BaseModel):
    visibility: RoomVisibility
