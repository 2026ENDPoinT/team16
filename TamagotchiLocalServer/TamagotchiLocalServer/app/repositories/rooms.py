"""Only database queries for Room live here."""

from datetime import datetime, timezone
from uuid import uuid4

from sqlalchemy import update
from sqlalchemy.orm import Session

from app.db.models import Room


class RoomRepository:
    def __init__(self, db: Session):
        self.db = db

    def create(self, name: str | None, initial_state: dict) -> Room:
        room = Room(id=str(uuid4()), name=name, state=initial_state, revision=0)
        self.db.add(room)
        self.db.commit()
        self.db.refresh(room)
        return room

    def get(self, room_id: str) -> Room | None:
        return self.db.get(Room, room_id)

    def patch_state(self, room: Room, changes: dict) -> Room | None:
        """Atomic compare-and-swap to avoid silently overwriting concurrent updates."""
        next_state = {**room.state, **changes}
        statement = (
            update(Room)
            .where(Room.id == room.id, Room.revision == room.revision)
            .values(
                state=next_state,
                revision=room.revision + 1,
                updated_at=datetime.now(timezone.utc),
            )
            .execution_options(synchronize_session=False)
        )
        result = self.db.execute(statement)
        if result.rowcount != 1:
            self.db.rollback()
            return None
        self.db.commit()
        self.db.expire_all()
        return self.get(room.id)

    def delete(self, room: Room) -> None:
        self.db.delete(room)
        self.db.commit()
