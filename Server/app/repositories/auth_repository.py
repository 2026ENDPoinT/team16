"""SQLite persistence: app-local student accounts and rotating refresh tokens."""
from contextlib import contextmanager
import os
from pathlib import Path
import re
import sqlite3
import threading
import time

from app.core.auth_security import hash_password, fingerprint_refresh_token

ROOT = Path(__file__).resolve().parents[2]
_init_lock = threading.Lock()
_initialized_paths: set[str] = set()


def database_path() -> Path:
    configured = os.getenv("AUTH_DB_PATH")
    if configured:
        p = Path(configured)
        return p if p.is_absolute() else ROOT / p
    return ROOT / "data" / "auth.sqlite3"


@contextmanager
def connection():
    path = database_path()
    path.parent.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(str(path), timeout=10)
    conn.row_factory = sqlite3.Row
    try:
        conn.execute("PRAGMA busy_timeout=10000")
        conn.execute("PRAGMA foreign_keys=ON")
        yield conn
    finally:
        conn.close()


def initialize():
    """Idempotent schema creation & one-time dev-only test account seeding."""
    key = str(database_path().resolve())
    with _init_lock:
        if key in _initialized_paths:
            return
        with connection() as conn:
            with conn:
                conn.executescript("""
                CREATE TABLE IF NOT EXISTS auth_users (
                    student_id TEXT PRIMARY KEY,
                    display_name TEXT NOT NULL,
                    password_salt TEXT NOT NULL,
                    password_hash TEXT NOT NULL,
                    role TEXT NOT NULL DEFAULT 'user',
                    has_character INTEGER NOT NULL DEFAULT 0,
                    created_at INTEGER NOT NULL,
                    last_login_at INTEGER
                );
                CREATE TABLE IF NOT EXISTS auth_refresh_tokens (
                    session_id TEXT PRIMARY KEY,
                    student_id TEXT NOT NULL,
                    token_hash TEXT NOT NULL UNIQUE,
                    expires_at INTEGER NOT NULL,
                    FOREIGN KEY(student_id) REFERENCES auth_users(student_id) ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS idx_auth_refresh_student
                  ON auth_refresh_tokens(student_id);
                """)
                if os.getenv("AUTH_ENABLE_TEST_USER", "1") == "1":
                    student_id = os.getenv("AUTH_TEST_STUDENT_ID", "20241234")
                    password = os.getenv("AUTH_TEST_PASSWORD", "Test1234!")
                    if not re.fullmatch(r"[0-9]{8,10}", student_id):
                        raise RuntimeError("테스트 계정 학번은 8~10자리 숫자여야 합니다.")
                    salt, digest = hash_password(password)
                    conn.execute(
                        """INSERT OR IGNORE INTO auth_users
                           (student_id, display_name, password_salt, password_hash, role, created_at)
                           VALUES (?, ?, ?, ?, 'user', ?)""",
                        (student_id, "test", salt, digest, int(time.time())),
                    )
        _initialized_paths.add(key)


def get_user(student_id: str):
    initialize()
    with connection() as conn:
        return conn.execute(
            "SELECT * FROM auth_users WHERE student_id = ?", (student_id,)
        ).fetchone()


def update_last_login(student_id: str):
    with connection() as conn:
        with conn:
            conn.execute(
                "UPDATE auth_users SET last_login_at = ? WHERE student_id = ?",
                (int(time.time()), student_id),
            )


def create_session(student_id: str, session_id: str, refresh_token: str, expires_at: int):
    with connection() as conn:
        with conn:
            conn.execute(
                """INSERT INTO auth_refresh_tokens
                   (session_id, student_id, token_hash, expires_at)
                   VALUES (?, ?, ?, ?)""",
                (session_id, student_id, fingerprint_refresh_token(refresh_token), expires_at),
            )


def rotate_session(old_token: str, new_token: str, new_expires_at: int):
    """Atomic one-time use / rotation; return (student_id, session_id) or None."""
    initialize()
    with connection() as conn:
        conn.execute("BEGIN IMMEDIATE")
        try:
            row = conn.execute(
                """SELECT session_id, student_id, expires_at FROM auth_refresh_tokens
                   WHERE token_hash = ?""", (fingerprint_refresh_token(old_token),)
            ).fetchone()
            if row is None or row["expires_at"] <= int(time.time()):
                conn.rollback()
                return None
            conn.execute(
                """UPDATE auth_refresh_tokens SET token_hash = ?, expires_at = ?
                   WHERE session_id = ?""",
                (fingerprint_refresh_token(new_token), new_expires_at, row["session_id"]),
            )
            conn.commit()
            return row["student_id"], row["session_id"]
        except Exception:
            conn.rollback()
            raise


def is_active_session(student_id: str, session_id: str) -> bool:
    initialize()
    with connection() as conn:
        row = conn.execute(
            """SELECT 1 FROM auth_refresh_tokens
               WHERE session_id = ? AND student_id = ? AND expires_at > ?""",
            (session_id, student_id, int(time.time())),
        ).fetchone()
        return row is not None


def delete_session(student_id: str, session_id: str) -> None:
    with connection() as conn:
        with conn:
            conn.execute(
                "DELETE FROM auth_refresh_tokens WHERE student_id = ? AND session_id = ?",
                (student_id, session_id),
            )
