"""Authorization and DTO conversion for administrator-only user listings."""

from datetime import datetime, timezone

from app.core.auth_errors import AuthError
from app.repositories import auth_repository, admin_users_repository


def list_users_for_admin(requester_id: str) -> dict:
    # Validate the *current* role in SQLite, not a role asserted by the client.
    requester = auth_repository.get_user(requester_id)
    if requester is None:
        raise AuthError(401, "UNAUTHORIZED", "로그인이 필요합니다.")
    if requester["role"] != "admin":
        raise AuthError(403, "FORBIDDEN", "관리자만 접근할 수 있습니다.")

    rows = admin_users_repository.list_all_users()
    items = [
        {
            "studentId": row["student_id"],
            "role": row["role"],
            "createdAt": datetime.fromtimestamp(
                row["created_at"], timezone.utc
            ).isoformat(timespec="seconds").replace("+00:00", "Z"),
        }
        for row in rows
    ]
    return {"items": items, "total": len(items)}
