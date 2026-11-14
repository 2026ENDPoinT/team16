"""Only register the new admin user-list router in existing Server/app/main.py.

Run once from Server/: python admin_users_install.py
Safe to re-run. Does not create/modify database records or any authentication code.
"""

import ast
from datetime import datetime
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parent
MAIN = ROOT / "app" / "main.py"
IMPORT_LINE = "from app.api.v1.admin_users import router as admin_users_router"
REGISTER_LINE = "app.include_router(admin_users_router)"


def main():
    if not MAIN.is_file():
        raise SystemExit("[오류] app/main.py를 찾을 수 없습니다. 기존 Server 폴더에서 실행하세요.")

    source = MAIN.read_text(encoding="utf-8-sig")
    try:
        module = ast.parse(source)
    except SyntaxError as exc:
        raise SystemExit(f"[오류] 기존 main.py 구문 오류: {exc}")

    if IMPORT_LINE in source and REGISTER_LINE in source:
        print("[확인] 관리자 사용자 목록 API가 이미 등록되어 있습니다. 변경하지 않았습니다.")
        return
    if IMPORT_LINE in source or REGISTER_LINE in source:
        raise SystemExit("[오류] 불완전한 등록 상태입니다. main.py의 관리자 라우터 등록 부분을 직접 확인하세요.")

    # Safety check: previous authentication patch uses `app = FastAPI(...)`.
    if not any(
        isinstance(node, (ast.Assign, ast.AnnAssign)) and
        any(isinstance(t, ast.Name) and t.id == "app" for t in
            (node.targets if isinstance(node, ast.Assign) else [node.target]))
        for node in module.body
    ):
        raise SystemExit("[오류] main.py에 최상위 app 객체가 없습니다. 수동 등록이 필요합니다.")

    # Insert before `if __name__ == '__main__'` so direct execution also works.
    lines = source.splitlines(keepends=True)
    position = len(lines)
    for node in module.body:
        if isinstance(node, ast.If) and isinstance(node.test, ast.Compare):
            test = node.test
            if (isinstance(test.left, ast.Name) and test.left.id == "__name__" and
                    len(test.comparators) == 1 and isinstance(test.comparators[0], ast.Constant) and
                    test.comparators[0].value == "__main__"):
                position = node.lineno - 1
                break

    addition = "\n# Read-only admin user list, added by admin_users_install.py\n" + IMPORT_LINE + "\n" + REGISTER_LINE + "\n\n"
    updated = "".join(lines[:position]) + addition + "".join(lines[position:])
    ast.parse(updated)

    backup = MAIN.with_name("main.py.backup-before-admin-users-" + datetime.now().strftime("%Y%m%d%H%M%S%f"))
    shutil.copy2(MAIN, backup)
    MAIN.write_text(updated, encoding="utf-8")
    print("[성공] GET /v1/admin/users 등록 완료")
    print("[백업]", backup.name)
    print("[확인] 기존 회원·인증 파일·DB는 변경하지 않았습니다.")


if __name__ == "__main__":
    main()
