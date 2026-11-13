"""Register random-visit API while preserving existing app/main.py.

Run from Server/: python random_visit_install.py
- Registers just one router, only once.
- Backs up main.py before changing it.
- Never edits the authentication code or database.
"""

import ast
from datetime import datetime
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parent
MAIN = ROOT / "app" / "main.py"
IMPORT = "from app.api.v1.random_visit import router as random_visit_router"
INCLUDE = "app.include_router(random_visit_router)"


def main():
    if not MAIN.is_file():
        raise SystemExit("[오류] Server/app/main.py가 없습니다.")

    source = MAIN.read_text(encoding="utf-8-sig")
    try:
        tree = ast.parse(source)
    except SyntaxError as exc:
        raise SystemExit(f"[오류] main.py 파싱 오류: {exc}") from exc

    if IMPORT in source and INCLUDE in source:
        print("[확인] 랜덤 방문 API가 이미 등록되어 있습니다. 변경하지 않았습니다.")
        return
    if IMPORT in source or INCLUDE in source:
        raise SystemExit("[오류] 라우터 등록이 부분적으로 존재합니다. main.py를 확인하세요.")

    # Do not silently create a second FastAPI app.
    if not any(
        isinstance(node, (ast.Assign, ast.AnnAssign)) and any(
            isinstance(target, ast.Name) and target.id == "app"
            for target in (node.targets if isinstance(node, ast.Assign) else [node.target])
        )
        for node in tree.body
    ):
        raise SystemExit("[오류] 최상위 app 변수가 없습니다. main.py에 라우터를 수동 등록하세요.")

    lines = source.splitlines(keepends=True)
    position = len(lines)
    # Insert before conventional __main__ startup block, rather than after it.
    for node in tree.body:
        if isinstance(node, ast.If) and isinstance(node.test, ast.Compare):
            test = node.test
            if (
                isinstance(test.left, ast.Name) and test.left.id == "__name__"
                and len(test.comparators) == 1
                and isinstance(test.comparators[0], ast.Constant)
                and test.comparators[0].value == "__main__"
            ):
                position = node.lineno - 1
                break

    addition = (
        "\n# Random room-visit candidate API (installed by random_visit_install.py)\n"
        + IMPORT + "\n" + INCLUDE + "\n\n"
    )
    updated = "".join(lines[:position]) + addition + "".join(lines[position:])
    ast.parse(updated)

    backup = MAIN.with_name(
        "main.py.backup-before-random-visit-" + datetime.now().strftime("%Y%m%d%H%M%S%f")
    )
    shutil.copy2(MAIN, backup)
    MAIN.write_text(updated, encoding="utf-8")
    print("[성공] GET /v1/room/random/student-id 등록")
    print("[백업]", backup.name)
    print("[확인] 기존 인증 파일과 회원 DB는 수정하지 않았습니다.")


if __name__ == "__main__":
    main()
