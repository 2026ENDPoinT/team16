"""Create exactly one local development admin user.
Run inside Server/: python tools/create_admin.py
Password is entered interactively and PBKDF2-hashed using the app's existing function.
Does not update or delete existing accounts; no login sessions are auto-created.
"""
from getpass import getpass
from pathlib import Path
import sqlite3
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from app.core.auth_security import hash_password  # noqa: E402
from app.repositories import auth_repository as repo  # noqa: E402

ADMIN_ID = "admin"


def main():
    repo.initialize()
    if repo.get_user(ADMIN_ID) is not None:
        print("[중단] 'admin' 계정이 이미 존재합니다. 기존 계정은 변경하지 않았습니다.")
        raise SystemExit(1)

    print("서버 앱 전용 관리자 계정을 생성합니다. (eClass와 무관)")
    password = getpass("관리자 비밀번호 입력: ")
    confirm = getpass("관리자 비밀번호 확인: ")
    if not password or password != confirm:
        raise SystemExit("[오류] 비밀번호가 비어 있거나 재입력값이 다릅니다.")
    if len(password) < 8 or len(password) > 128 or not any(c.isalpha() and c.isascii() for c in password) or not any(c.isdigit() for c in password):
        raise SystemExit("[오류] 비밀번호는 8~128자, 영문자·숫자 각 1개 이상이어야 합니다.")
    salt, digest = hash_password(password)
    del password, confirm

    try:
        with repo.connection() as conn:
            with conn:
                conn.execute(
                    """INSERT INTO auth_users
                       (student_id, display_name, password_salt, password_hash, role,
                        has_character, created_at)
                       VALUES (?, ?, ?, ?, 'admin', 0, ?)""",
                    (ADMIN_ID, ADMIN_ID, salt, digest, int(time.time())),
                )
    except sqlite3.IntegrityError:
        raise SystemExit("[중단] 같은 ID가 이미 존재합니다. 기존 계정은 변경하지 않았습니다.")

    print("[성공] 관리자 계정이 생성되었습니다.")
    print("  studentId: admin")
    print("  role: admin")
    print(f"  DB 경로: {repo.database_path()}")
    print("  비밀번호는 저장하지 않고 해시값만 DB에 저장했습니다.")
    print("  최초 캐릭터 미생성 상태이므로 isNewUser=true로 로그인 응답됩니다.")


if __name__ == "__main__":
    main()
