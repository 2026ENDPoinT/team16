"""App authentication routes, exposed at /v1/auth/* (see API문서.md)."""
from fastapi import APIRouter, Header, Request, status

from app.core.auth_errors import AuthError, AuthRoute
from app.core.auth_security import decode_access_token
from app.repositories import auth_repository as repository
from app.schemas.auth import AuthResponse, LoginRequest, RefreshRequest
from app.services import auth_service

router = APIRouter(prefix="/v1/auth", tags=["Auth"], route_class=AuthRoute)


@router.post("/login", response_model=AuthResponse, status_code=200)
def login(payload: LoginRequest, request: Request):
    ip_address = request.client.host if request.client else "unknown"
    return auth_service.login(payload.studentId, payload.password, ip_address)


@router.post("/refresh", response_model=AuthResponse, status_code=200)
def refresh(payload: RefreshRequest):
    return auth_service.refresh(payload.refreshToken)


def authorized_session(authorization: str | None) -> tuple[str, str]:
    if not authorization or not authorization.startswith("Bearer "):
        raise AuthError(401, "UNAUTHORIZED", "로그인이 필요합니다.")
    student_id, session_id = decode_access_token(authorization.removeprefix("Bearer ").strip())
    if not repository.is_active_session(student_id, session_id):
        raise AuthError(401, "UNAUTHORIZED", "로그인이 필요합니다.")
    return student_id, session_id


@router.post("/logout", status_code=status.HTTP_204_NO_CONTENT)
def logout(authorization: str | None = Header(default=None)):
    student_id, session_id = authorized_session(authorization)
    auth_service.logout(student_id, session_id)
    return None
