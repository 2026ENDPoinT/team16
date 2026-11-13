"""Application logic: recommend another student's ID for room visiting."""

from app.core.auth_errors import AuthError
from app.repositories import auth_repository, random_visit_repository


def pick_random_student_for_visit(requester_id: str) -> dict[str, str]:
    # authorized_session already confirmed the JWT and active session.
    # Check the account still exists in the same DB used for registration/login.
    if auth_repository.get_user(requester_id) is None:
        raise AuthError(401, "UNAUTHORIZED", "로그인이 필요합니다.")

    chosen_id = random_visit_repository.find_random_other_student_id(requester_id)
    if chosen_id is None:
        raise AuthError(404, "NO_VISITABLE_USER", "방문 후보로 선택할 다른 학생이 없습니다.")
    return {"studentId": chosen_id}
