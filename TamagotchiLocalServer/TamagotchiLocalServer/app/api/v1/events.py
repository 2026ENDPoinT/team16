from fastapi import APIRouter, Depends, Query, status
from sqlalchemy.orm import Session

from app.db.session import get_db
from app.schemas.events import EventCreate, EventList, EventRead
from app.services.events import EventService

router = APIRouter(prefix="/rooms/{room_id}/events", tags=["events"])


@router.post("", response_model=EventRead, status_code=status.HTTP_201_CREATED)
def send_event(room_id: str, body: EventCreate, db: Session = Depends(get_db)):
    return EventService(db).create(room_id, body)


@router.get("", response_model=EventList)
def read_events(
    room_id: str,
    after_id: int = Query(default=0, ge=0),
    limit: int = Query(default=50, ge=1, le=100),
    db: Session = Depends(get_db),
):
    return EventService(db).list_after(room_id, after_id, limit)
