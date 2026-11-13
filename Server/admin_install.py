"""Patch the prior signup/login implementation to allow the reserved `admin` ID at LOGIN only.
Run inside Server/: python admin_install.py
"""
from datetime import datetime
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parent
TARGET = ROOT / "app" / "services" / "auth_service.py"
OLD = 'if not _is_valid_student_id(student_id) or not password:'
NEW = 'if not (_is_valid_student_id(student_id) or student_id == "admin") or not password:'


def main():
    if not TARGET.is_file():
        raise SystemExit(f"[오류] 기존 로그인 서비스 파일이 없습니다: {TARGET}")
    content = TARGET.read_text(encoding="utf-8")
    if NEW in content:
        print("[확인] admin 로그인 예외가 이미 설치되어 있습니다. 추가 수정하지 않았습니다.")
        return
    if content.count(OLD) != 1:
        raise SystemExit(
            "[오류] 이전 회원가입 패치와 로그인 코드가 다릅니다. 변경을 중단했습니다. "
            "app/services/auth_service.py의 login()에서 학번 형식 검사 코드를 확인하세요."
        )
    backup = TARGET.with_suffix(TARGET.suffix + ".backup-admin-" + datetime.now().strftime("%Y%m%d%H%M%S"))
    shutil.copy2(TARGET, backup)
    updated = content.replace(OLD, NEW, 1)
    TARGET.write_text(updated, encoding="utf-8")
    print("[성공] 로그인에서 예약된 ID 'admin'을 허용하도록 수정했습니다.")
    print(f"[백업] {backup}")
    print("[참고] 회원가입·중복확인 API의 숫자 학번 제한은 그대로 유지됩니다.")


if __name__ == "__main__":
    main()
