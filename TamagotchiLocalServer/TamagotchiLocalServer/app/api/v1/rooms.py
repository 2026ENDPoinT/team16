from fastapi import APIRouter, Depends, status
from sqlalchemy.orm import Session

from app.db.session import get_db
from app.schemas.rooms import RoomCreate, RoomRead, StatePatch
from app.services.rooms import RoomService

router = APIRouter(prefix="/rooms", tags=["rooms"])


@router.post("", response_model=RoomRead, status_code=status.HTTP_201_CREATED)
def create_room(body: RoomCreate, db: Session = Depends(get_db)):
    return RoomService(db).create(body)


@router.get("/{room_id}", response_model=RoomRead)
def get_room(room_id: str, db: Session = Depends(get_db)):
    return RoomService(db).get_or_404(room_id)


@router.patch("/{room_id}/state", response_model=RoomRead)
def patch_room_state(room_id: str, body: StatePatch, db: Session = Depends(get_db)):
    return RoomService(db).patch_state(room_id, body)


@router.delete("/{room_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_room(room_id: str, db: Session = Depends(get_db)):
    RoomService(db).delete(room_id)
