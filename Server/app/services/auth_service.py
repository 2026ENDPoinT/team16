"""App-local authentication; school/eClass credentials are never used."""
import re
import secrets
import time

from app.core.auth_errors import AuthError
from app.core.auth_rate_limit import (
    clear_login_failures, ensure_login_allowed, record_login_failure,
    consume_request_quota,
)
from app.core.auth_security import (
    ACCESS_EXPIRES_SECONDS, REFRESH_EXPIRES_SECONDS, hash_password,
    make_access_token, new_refresh_token, verify_password,
)
from app.repositories import auth_repository as repository

INVALID_MESSAGE = "학번 또는 비밀번호가 올바르지 않습니다."
# An unknown student ID must not have a significantly cheaper password check.
_DUMMY_SALT, _DUMMY_HASH = hash_password("no-such-account-password")
_STUDENT_ID_PATTERN = re.compile(r"[0-9]{8,10}\Z")


def _is_valid_student_id(student_id: str) -> bool:
    # Same ID rule used by the previous login patch.
    return bool(_STUDENT_ID_PATTERN.fullmatch(student_id))


def _response(student_id: str, session_id: str, refresh_token: str, is_new_user: bool = False):
    user = repository.get_user(student_id)
    return {
        "accessToken": make_access_token(student_id, session_id),
        "refreshToken": refresh_token,
        "expiresIn": ACCESS_EXPIRES_SECONDS,
        "isNewUser": is_new_user, 
        "role": user["role"],
    }


def check_id(student_id: str, client_ip: str):
    consume_request_quota("check-id", client_ip)
    if not _is_valid_student_id(student_id):
        raise AuthError(400, "INVALID_REQUEST", "학번은 숫자 8~10자리여야 합니다.")
    return {"studentId": student_id, "isAvailable": not repository.student_id_exists(student_id)}


def _validate_password(password: str):
    # API문서.md specifies WEAK_PASSWORD but does not define detailed requirements.
    # Provisional app-local policy: 8-128 chars, at least 1 letter and 1 digit.
    if not (8 <= len(password) <= 128) or not re.search(r"[A-Za-z]", password) or not re.search(r"[0-9]", password):
        raise AuthError(422, "WEAK_PASSWORD", "비밀번호는 8~128자이며 영문자와 숫자를 각각 1개 이상 포함해야 합니다.")


def signup(student_id: str, password: str, client_ip: str):
    consume_request_quota("signup", client_ip)
    if not _is_valid_student_id(student_id):
        raise AuthError(400, "INVALID_REQUEST", "학번은 숫자 8~10자리여야 합니다.")
    # Check duplicates before hashing (but INSERT enforces unique ID transactionally).
    if repository.student_id_exists(student_id):
        raise AuthError(409, "STUDENT_ID_TAKEN", "이미 가입된 학번입니다.")
    _validate_password(password)
    salt, digest = hash_password(password)
    session_id = secrets.token_hex(16)
    refresh_token = new_refresh_token()
    if not repository.create_user_with_session(
        student_id, salt, digest, session_id, refresh_token,
        int(time.time()) + REFRESH_EXPIRES_SECONDS,
    ):
        raise AuthError(409, "STUDENT_ID_TAKEN", "이미 가입된 학번입니다.")
    repository.update_last_login(student_id)
    return _response(student_id, session_id, refresh_token, is_new_user=True)


def login(student_id: str, password: str, client_ip: str):
    ensure_login_allowed(client_ip, student_id)
    if not _is_valid_student_id(student_id) or not password:
        record_login_failure(client_ip, student_id)
        raise AuthError(401, "INVALID_CREDENTIALS", INVALID_MESSAGE)
    # CRITICAL: Lookup only. Never INSERT a user when login is called.
    user = repository.get_user(student_id)
    if user is None:
        verify_password(password, _DUMMY_SALT, _DUMMY_HASH)
        valid = False
    else:
        valid = verify_password(password, user["password_salt"], user["password_hash"])
    if not valid:
        record_login_failure(client_ip, student_id)
        raise AuthError(401, "INVALID_CREDENTIALS", INVALID_MESSAGE)
    clear_login_failures(client_ip, student_id)
    session_id = secrets.token_hex(16)
    refresh_token = new_refresh_token()
    repository.create_session(student_id, session_id, refresh_token,
                              int(time.time()) + REFRESH_EXPIRES_SECONDS)
    repository.update_last_login(student_id)
    return _response(student_id, session_id, refresh_token)


def refresh(refresh_token: str):
    if not refresh_token:
        raise AuthError(401, "UNAUTHORIZED", "유효하지 않은 리프레시 토큰입니다.")
    replacement = new_refresh_token()
    rotated = repository.rotate_session(
        refresh_token, replacement, int(time.time()) + REFRESH_EXPIRES_SECONDS
    )
    if rotated is None:
        raise AuthError(401, "UNAUTHORIZED", "유효하지 않은 리프레시 토큰입니다.")
    student_id, session_id = rotated
    return _response(student_id, session_id, replacement)


def logout(student_id: str, session_id: str):
    repository.delete_session(student_id, session_id)
