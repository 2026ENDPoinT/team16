"""Business rules for rooms. Extend here when Unity's data model is settled."""

from fastapi import HTTPException, status
from sqlalchemy.orm import Session

from app.db.models import Room
from app.repositories.rooms import RoomRepository
from app.schemas.rooms import RoomCreate, StatePatch


class RoomService:
    def __init__(self, db: Session):
        self.repo = RoomRepository(db)

    def create(self, body: RoomCreate) -> Room:
        return self.repo.create(body.name, body.initial_state)

    def get_or_404(self, room_id: str) -> Room:
        room = self.repo.get(room_id)
        if room is None:
            raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Room not found")
        return room

    def patch_state(self, room_id: str, body: StatePatch) -> Room:
        room = self.get_or_404(room_id)
        if body.expected_revision is not None and body.expected_revision != room.revision:
            raise HTTPException(status_code=409, detail="State revision conflict; read room and retry")
        result = self.repo.patch_state(room, body.changes)
        if result is None:
            raise HTTPException(status_code=409, detail="State changed concurrently; read room and retry")
        return result

    def delete(self, room_id: str) -> None:
        room = self.get_or_404(room_id)
        self.repo.delete(room)
