"""Read-only account audit. From Server/: python tools/list_auth_users.py

Prints student ID, role, and character state only. Never prints password hashes or tokens.
"""
from pathlib import Path
import os
import sqlite3
import sys

ROOT = Path(__file__).resolve().parents[1]
configured = os.getenv("AUTH_DB_PATH")
db_path = (Path(configured) if Path(configured).is_absolute() else ROOT / configured) if configured else ROOT / "data" / "auth.sqlite3"
if not db_path.is_file():
    print(f"사용자 DB가 아직 없습니다: {db_path}")
    sys.exit(0)

# SQLite URI read-only mode: the audit tool cannot accidentally mutate user data.
with sqlite3.connect(db_path.resolve().as_uri() + "?mode=ro", uri=True) as conn:
    try:
        rows = conn.execute("SELECT student_id, role, has_character FROM auth_users ORDER BY created_at").fetchall()
    except sqlite3.OperationalError as exc:
        raise SystemExit(f"계정 테이블 조회 실패: {exc}") from exc

print(f"DB: {db_path}\n가입된 사용자: {len(rows)}명")
for sid, role, has_character in rows:
    print(f"  studentId={sid} | role={role} | hasCharacter={bool(has_character)}")
