"""Events are persistent messages visible to other clients in the same room."""

from sqlalchemy.orm import Session

from app.db.models import Event
from app.repositories.events import EventRepository
from app.schemas.events import EventCreate, EventList
from app.services.rooms import RoomService


class EventService:
    def __init__(self, db: Session):
        self.rooms = RoomService(db)
        self.events = EventRepository(db)

    def create(self, room_id: str, body: EventCreate) -> Event:
        self.rooms.get_or_404(room_id)
        return self.events.create(room_id, body.sender_id, body.event_type, body.payload)

    def list_after(self, room_id: str, after_id: int, limit: int) -> EventList:
        self.rooms.get_or_404(room_id)
        items = self.events.list_after(room_id, after_id, limit)
        next_after = items[-1].id if items else after_id
        return EventList(items=items, next_after_id=next_after)
