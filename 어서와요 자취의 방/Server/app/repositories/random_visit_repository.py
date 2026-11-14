"""Select a random *account candidate* for visiting. Existing auth DB is read-only.

This repository deliberately does not claim that the account has a public room:
there is no room/visibility table in the currently supplied authentication storage.
Once the room model exists, replace the candidate query with a room-aware query.
"""

from app.repositories import auth_repository


def find_random_other_student_id(requester_id: str) -> str | None:
    auth_repository.initialize()
    with auth_repository.connection() as conn:
        # A requester's ID comes from the verified access token, not a query parameter.
        # Exclude administrators from random visits. Do not select the caller.
        row = conn.execute(
            """SELECT student_id
                 FROM auth_users
                 WHERE student_id != ? AND role = 'user'
                 ORDER BY RANDOM()
                 LIMIT 1""",
            (requester_id,),
        ).fetchone()
    return str(row["student_id"]) if row is not None else None
