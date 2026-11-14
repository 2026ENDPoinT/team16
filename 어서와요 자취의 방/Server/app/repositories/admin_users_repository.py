"""Read-only access to app accounts. Shares the existing auth SQLite database."""

from app.repositories import auth_repository


def list_all_users() -> list[dict]:
    """Return public account metadata only, never password hashes or session tokens."""
    auth_repository.initialize()
    with auth_repository.connection() as conn:
        rows = conn.execute(
            """SELECT student_id, role, created_at
                 FROM auth_users
                 ORDER BY created_at DESC, student_id ASC"""
        ).fetchall()
    return [dict(row) for row in rows]
