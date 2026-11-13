"""GET /v1/admin/users: the authenticated app user list (admins only)."""

from fastapi import APIRouter, Header

from app.api.v1.auth import authorized_session
from app.core.auth_errors import AuthRoute
from app.schemas.admin_users import AdminUsersResponse
from app.services import admin_users_service

router = APIRouter(prefix="/v1/admin", tags=["Admin"], route_class=AuthRoute)


@router.get("/users", response_model=AdminUsersResponse, status_code=200)
def get_admin_users(authorization: str | None = Header(default=None)):
    student_id, _session_id = authorized_session(authorization)
    return admin_users_service.list_users_for_admin(student_id)
