"""Idempotently register the additive auth router in the *existing* Server/app/main.py."""
from pathlib import Path
import ast
import shutil
from datetime import datetime

ROOT = Path(__file__).resolve().parent
MAIN = ROOT / "app" / "main.py"
REQUIREMENTS = ROOT / "requirements.txt"
GITIGNORE = ROOT / ".gitignore"
IMPORT_LINE = "from app.api.v1.auth import router as auth_router"
REGISTER_LINE = "app.include_router(auth_router)"


def append_missing_lines(path: Path, lines: list[str]):
    existing = path.read_text(encoding="utf-8") if path.exists() else ""
    missing = [line for line in lines if line not in existing]
    if missing:
        with path.open("a", encoding="utf-8") as f:
            if existing and not existing.endswith("\n"):
                f.write("\n")
            f.write("\n".join(missing) + "\n")


def run():
    if not MAIN.is_file():
        raise SystemExit("오류: Server/app/main.py가 없습니다. 기존 Server 최상위에서 실행해 주세요.")
    source = MAIN.read_text(encoding="utf-8")
    try:
        ast.parse(source)
    except SyntaxError as e:
        raise SystemExit(f"오류: 기존 main.py 구문 오류가 있습니다: {e}")
    if "app = FastAPI(" not in source and "app=FastAPI(" not in source:
        raise SystemExit("오류: main.py에 'app = FastAPI(...)'가 있는 구조만 자동 등록합니다. 수동 등록해 주세요.")

    if REGISTER_LINE not in source:
        backup = MAIN.with_name("main.py.backup-before-auth-" + datetime.now().strftime("%Y%m%d%H%M%S"))
        shutil.copy2(MAIN, backup)
        # Place new lines before a direct-execution guard when present.
        addition = f"\n\n# Added by auth_install.py (app-local accounts only)\n{IMPORT_LINE}\n{REGISTER_LINE}\n"
        guard = 'if __name__ == "__main__":'
        alt_guard = "if __name__ == '__main__':"
        idx = source.find(guard)
        if idx < 0:
            idx = source.find(alt_guard)
        modified = (source[:idx] + addition + "\n" + source[idx:]) if idx >= 0 else source.rstrip() + addition
        ast.parse(modified)
        MAIN.write_text(modified, encoding="utf-8")
        print(f"인증 라우터 등록 완료. main.py 백업: {backup.name}")
    else:
        print("인증 라우터가 이미 등록되어 있습니다 (중복 등록 생략).")

    req_content = REQUIREMENTS.read_text(encoding="utf-8") if REQUIREMENTS.exists() else ""
    if not any(line.strip().lower().startswith("pyjwt") for line in req_content.splitlines()):
        append_missing_lines(REQUIREMENTS, ["PyJWT>=2.8,<3"])
        print("requirements.txt에 PyJWT 추가 완료")
    append_missing_lines(GITIGNORE, ["data/auth.sqlite3", "data/auth.sqlite3-*", "data/auth_jwt_secret.key"])
    print("설치 준비 완료: python -m pip install -r requirements.txt")
    print("로그인: POST http://127.0.0.1:8000/v1/auth/login")


if __name__ == "__main__":
    run()
