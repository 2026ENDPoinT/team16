"""Room layout and furniture placement schemas."""

from typing import List, Literal

from pydantic import BaseModel, Field
from app.schemas.character import CharacterData


RoomVisibility = Literal["public", "friends", "private"]
RotationStep = Literal[0, 90, 180, 270]


class RoomGridSize(BaseModel):
    width: int = 5
    height: int = 5
    depth: int = 5


class RoomPlacementData(BaseModel):
    furnitureId: str
    gridX: int = Field(..., ge=0, lt=5)
    gridY: int = Field(..., ge=0, lt=5)
    gridZ: int = Field(..., ge=0, lt=5)
    rotation: RotationStep = 0


class RoomLayout(BaseModel):
    ownerStudentId: str
    visibility: RoomVisibility = "friends"
    wallpaperId: str = "wall_basic"
    floorId: str = "floor_wood"
    gridSize: RoomGridSize = Field(default_factory=RoomGridSize)
    placements: List[RoomPlacementData] = Field(default_factory=list)
    updatedAt: str


class RoomUpdateRequest(BaseModel):
    visibility: RoomVisibility = "friends"
    wallpaperId: str
    floorId: str
    placements: List[RoomPlacementData]

class RoomVisitResponse(BaseModel):
    """
    다른 사용자의 자취방을 방문할 때 한 번에 받는 묶음.

    캐릭터와 방을 따로 부르면 왕복이 두 번이고, 그 사이에 상대가 바뀌면
    캐릭터와 방이 서로 다른 시점의 것이 섞인다.
    """

    studentId: str
    character: "CharacterData"
    room: RoomLayout


RoomVisitResponse.model_rebuild()
