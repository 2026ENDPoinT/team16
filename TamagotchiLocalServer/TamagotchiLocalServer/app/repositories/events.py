"""Only database queries for Event live here."""

from sqlalchemy import select
from sqlalchemy.orm import Session

from app.db.models import Event


class EventRepository:
    def __init__(self, db: Session):
        self.db = db

    def create(self, room_id: str, sender_id: str, event_type: str, payload: dict) -> Event:
        event = Event(
            room_id=room_id,
            sender_id=sender_id,
            event_type=event_type,
            payload=payload,
        )
        self.db.add(event)
        self.db.commit()
        self.db.refresh(event)
        return event

    def list_after(self, room_id: str, after_id: int, limit: int) -> list[Event]:
        query = (
            select(Event)
            .where(Event.room_id == room_id, Event.id > after_id)
            .order_by(Event.id.asc())
            .limit(limit)
        )
        return list(self.db.scalars(query).all())
