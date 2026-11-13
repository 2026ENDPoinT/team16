"""SQLite repository for app-local accounts and refresh sessions.

IMPORTANT: This repository NEVER auto-creates a user. Only `create_user_with_session`
performs registration and only when explicitly called by POST /auth/signup.
Existing SQLite user records and sessions are preserved on patch installation.
"""
from contextlib import contextmanager
import os
from pathlib import Path
import sqlite3
import threading
import time

from app.core.auth_security import fingerprint_refresh_token

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
    """Create tables only, not user accounts. Safe for an existing auth.sqlite3."""
    key = str(database_path().resolve())
    with _init_lock:
        if key in _initialized_paths:
            return
        with connection() as conn:
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
        _initialized_paths.add(key)


def get_user(student_id: str):
    initialize()
    with connection() as conn:
        return conn.execute(
            "SELECT * FROM auth_users WHERE student_id = ?", (student_id,)
        ).fetchone()


def student_id_exists(student_id: str) -> bool:
    initialize()
    with connection() as conn:
        return conn.execute(
            "SELECT 1 FROM auth_users WHERE student_id = ?", (student_id,)
        ).fetchone() is not None


def create_user_with_session(
    student_id: str, salt: str, digest: str,
    session_id: str, refresh_token: str, expires_at: int,
) -> bool:
    """Register account and first login session atomically; False if ID already exists."""
    initialize()
    with connection() as conn:
        conn.execute("BEGIN IMMEDIATE")
        try:
            result = conn.execute(
                """INSERT INTO auth_users
                   (student_id, display_name, password_salt, password_hash,
                    role, has_character, created_at)
                   VALUES (?, ?, ?, ?, 'user', 0, ?)
                   ON CONFLICT(student_id) DO NOTHING""",
                (student_id, student_id, salt, digest, int(time.time())),
            )
            if result.rowcount != 1:
                conn.rollback()
                return False
            conn.execute(
                """INSERT INTO auth_refresh_tokens
                   (session_id, student_id, token_hash, expires_at)
                   VALUES (?, ?, ?, ?)""",
                (session_id, student_id, fingerprint_refresh_token(refresh_token), expires_at),
            )
            conn.commit()
            return True
        except Exception:
            conn.rollback()
            raise


def update_last_login(student_id: str):
    initialize()
    with connection() as conn:
        with conn:
            conn.execute(
                "UPDATE auth_users SET last_login_at = ? WHERE student_id = ?",
                (int(time.time()), student_id),
            )


def create_session(student_id: str, session_id: str, refresh_token: str, expires_at: int):
    initialize()
    with connection() as conn:
        with conn:
            conn.execute(
                """INSERT INTO auth_refresh_tokens
                   (session_id, student_id, token_hash, expires_at)
                   VALUES (?, ?, ?, ?)""",
                (session_id, student_id, fingerprint_refresh_token(refresh_token), expires_at),
            )


def rotate_session(old_token: str, new_token: str, new_expires_at: int):
    """Atomic one-time rotation; returns (student_id, session_id) or None."""
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
        return conn.execute(
            """SELECT 1 FROM auth_refresh_tokens
               WHERE session_id = ? AND student_id = ? AND expires_at > ?""",
            (session_id, student_id, int(time.time())),
        ).fetchone() is not None


def delete_session(student_id: str, session_id: str) -> None:
    initialize()
    with connection() as conn:
        with conn:
            conn.execute(
                "DELETE FROM auth_refresh_tokens WHERE student_id = ? AND session_id = ?",
                (student_id, session_id),
            )
