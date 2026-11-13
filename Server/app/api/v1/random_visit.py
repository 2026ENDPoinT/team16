"""Authenticated random visit candidate: GET /v1/room/random/student-id.

This path is distinct from the API contract's GET /v1/room/random (full room),
and will not shadow a future GET /v1/room/{studentId} route.
"""

from fastapi import APIRouter, Header

from app.api.v1.auth import authorized_session
from app.core.auth_errors import AuthRoute
from app.schemas.random_visit import RandomStudentIdResponse
from app.services import random_visit_service

router = APIRouter(prefix="/v1/room", tags=["Room"], route_class=AuthRoute)


@router.get("/random/student-id", response_model=RandomStudentIdResponse, status_code=200)
def get_random_student_id(authorization: str | None = Header(default=None)):
    requester_id, _session_id = authorized_session(authorization)
    return random_visit_service.pick_random_student_for_visit(requester_id)
