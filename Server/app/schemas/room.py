"""Room layout and furniture placement schemas."""

from typing import List, Literal

from pydantic import BaseModel, Field


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