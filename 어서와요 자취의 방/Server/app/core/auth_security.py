"""Password hashing and signed access token helpers. No eClass credentials involved."""
import base64
import hashlib
import hmac
import os
from pathlib import Path
import secrets
import time

import jwt
from jwt import InvalidTokenError

from app.core.auth_errors import AuthError

ROOT = Path(__file__).resolve().parents[2]
ACCESS_EXPIRES_SECONDS = 3600
REFRESH_EXPIRES_SECONDS = 30 * 24 * 3600
PBKDF2_ITERATIONS = 310_000


def hash_password(password: str, salt: str | None = None) -> tuple[str, str]:
    if salt is None:
        salt = secrets.token_hex(16)
    hashed = hashlib.pbkdf2_hmac(
        "sha256", password.encode("utf-8"), bytes.fromhex(salt), PBKDF2_ITERATIONS
    )
    return salt, hashed.hex()


def verify_password(password: str, salt: str, expected_hash: str) -> bool:
    _, actual_hash = hash_password(password, salt)
    return hmac.compare_digest(actual_hash, expected_hash)


def secret_key() -> str:
    env_secret = os.getenv("AUTH_JWT_SECRET")
    if env_secret:
        if len(env_secret) < 32:
            raise RuntimeError("AUTH_JWT_SECRET는 최소 32자 이상이어야 합니다.")
        return env_secret
    path = ROOT / "data" / "auth_jwt_secret.key"
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        fd = os.open(str(path), os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            f.write(secrets.token_urlsafe(48))
    except FileExistsError:
        pass
    return path.read_text(encoding="utf-8").strip()


def make_access_token(student_id: str, session_id: str) -> str:
    now = int(time.time())
    payload = {
        "sub": student_id,
        "sid": session_id,
        "type": "access",
        "iat": now,
        "exp": now + ACCESS_EXPIRES_SECONDS,
        "jti": secrets.token_hex(16),
    }
    return jwt.encode(payload, secret_key(), algorithm="HS256")


def decode_access_token(token: str) -> tuple[str, str]:
    try:
        payload = jwt.decode(
            token,
            secret_key(),
            algorithms=["HS256"],
            options={"require": ["sub", "sid", "type", "iat", "exp"]},
        )
    except jwt.ExpiredSignatureError as exc:
        raise AuthError(401, "TOKEN_EXPIRED", "로그인 시간이 만료되었습니다.") from exc
    except InvalidTokenError as exc:
        raise AuthError(401, "UNAUTHORIZED", "유효하지 않은 인증 토큰입니다.") from exc
    if payload.get("type") != "access" or not isinstance(payload.get("sid"), str):
        raise AuthError(401, "UNAUTHORIZED", "유효하지 않은 인증 토큰입니다.")
    return str(payload["sub"]), payload["sid"]


def new_refresh_token() -> str:
    return secrets.token_urlsafe(48)


def fingerprint_refresh_token(token: str) -> str:
    return hashlib.sha256(token.encode("utf-8")).hexdigest()
