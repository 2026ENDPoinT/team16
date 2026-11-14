"""eClass 할 일 크롤링, 브라우저 쿠키 자동 추출 및 동기화 API (/v1/todos).

- 재화 시스템 제거
- 수동 완료 체크 제거 (크롤링/리프레시 기반)
- 서버에서 Playwright(에이전트 브라우저) 또는 HTTP 세션 로그인을 통해
  사용자의 서울과기대 eClass 쿠키를 자동 추출하여 DB에 저장하고 즉시 할 일을 크롤링한다.
- 리프레시 결과 할 일 개수가 이전보다 줄어들면 decreasedCount > 0 을 반환해
  클라이언트(룸 말풍선 & PiP)가 '와! 해냈어!' 축하 대사를 띄우도록 한다.
"""

from datetime import datetime, timezone
import html
import http.cookiejar
import re
import threading
import time
import urllib.parse
import urllib.request

from fastapi import APIRouter, Header

from app.api.v1.auth import authorized_session
from app.core.auth_errors import AuthRoute
from app.repositories.auth_repository import connection, initialize as init_auth_db
from app.schemas.task import TodoItemData, TodoListResponse, TodoSyncRequest

router = APIRouter(prefix="/todos", tags=["todos"], route_class=AuthRoute)

_lock = threading.Lock()
_initialized = False

ECLASS_BASE_URL = "https://eclass.seoultech.ac.kr"
ECLASS_LOGIN_PAGE_URL = "https://eclass.seoultech.ac.kr/ilos/main/member/login_form.acl"
ECLASS_LOGIN_POST_URL = "https://eclass.seoultech.ac.kr/ilos/lo/login.acl"
ECLASS_HOME_URL = "https://eclass.seoultech.ac.kr/ilos/main/main_form.acl"
ECLASS_TODO_URL = "https://eclass.seoultech.ac.kr/ilos/main/main_todo_list.acl"

DEFAULT_ECLASS_TODOS = [
    {
        "id": "eclass_1",
        "type": "assignment",
        "title": "[과제] 트리 및 그래프 탐색 구현 과제",
        "courseName": "자료구조",
        "dueAt": "11월 18일 오후 11:59",
    },
    {
        "id": "eclass_2",
        "type": "lecture",
        "title": "[영상] 11주차 REST API 설계와 비동기 통신",
        "courseName": "웹프로그래밍",
        "dueAt": "11월 19일 오후 11:59",
    },
    {
        "id": "eclass_3",
        "type": "assignment",
        "title": "[과제] 동적 계획법(DP) 연습문제 풀이 제출",
        "courseName": "알고리즘",
        "dueAt": "11월 20일 오후 11:59",
    },
    {
        "id": "eclass_4",
        "type": "quiz",
        "title": "[퀴즈] 프로세스 동기화 및 데드락 주간 퀴즈",
        "courseName": "운영체제",
        "dueAt": "11월 21일 오후 6:00",
    },
    {
        "id": "eclass_5",
        "type": "assignment",
        "title": "[과제] ENDPoinT 해커톤 최종 발표 자료 제출",
        "courseName": "캡스톤디자인",
        "dueAt": "11월 22일 오전 9:00",
    },
]


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def init_todo_tables() -> None:
    global _initialized
    init_auth_db()
    with _lock:
        if _initialized:
            return
        with connection() as conn:
            conn.executescript("""
                CREATE TABLE IF NOT EXISTS user_todos (
                    student_id  TEXT NOT NULL,
                    todo_id     TEXT NOT NULL,
                    item_type   TEXT NOT NULL DEFAULT 'assignment',
                    title       TEXT NOT NULL,
                    course_name TEXT NOT NULL DEFAULT '',
                    due_at      TEXT NOT NULL DEFAULT '',
                    ord         INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (student_id, todo_id)
                );

                CREATE TABLE IF NOT EXISTS user_todo_meta (
                    student_id     TEXT PRIMARY KEY,
                    last_count     INTEGER NOT NULL DEFAULT 0,
                    synced_at      TEXT NOT NULL DEFAULT '',
                    cookie_header  TEXT NOT NULL DEFAULT ''
                );
            """)
        _initialized = True


def _ensure_initial_todos(conn, student_id: str) -> None:
    meta = conn.execute(
        "SELECT * FROM user_todo_meta WHERE student_id = ?", (student_id,)
    ).fetchone()
    if meta is not None:
        return

    now = _now_iso()
    for idx, item in enumerate(DEFAULT_ECLASS_TODOS):
        conn.execute(
            """INSERT OR IGNORE INTO user_todos
               (student_id, todo_id, item_type, title, course_name, due_at, ord)
               VALUES (?, ?, ?, ?, ?, ?, ?)""",
            (
                student_id,
                item["id"],
                item["type"],
                item["title"],
                item["courseName"],
                item["dueAt"],
                idx,
            ),
        )
    conn.execute(
        """INSERT INTO user_todo_meta (student_id, last_count, synced_at, cookie_header)
           VALUES (?, ?, ?, '')""",
        (student_id, len(DEFAULT_ECLASS_TODOS), now),
    )


def _strip_tags(raw: str) -> str:
    text = re.sub(r"<[^>]+>", " ", raw)
    text = html.unescape(text)
    return re.sub(r"\s+", " ", text).strip()


def _find_class_text(block: str, class_name: str) -> str:
    pattern = rf'<div\b[^>]*class\s*=\s*"[^"]*\b{re.escape(class_name)}\b[^"]*"[^>]*>(.*?)</div>'
    m = re.search(pattern, block, flags=re.IGNORECASE | re.DOTALL)
    return _strip_tags(m.group(1)) if m else ""


def _is_cookie_logged_in(cookie_header: str) -> bool:
    """주어진 쿠키로 eClass 메인 페이지에 접근했을 때 실제 로그인 상태인지 확인한다."""
    cookie = (cookie_header or "").strip()
    if cookie.lower().startswith("cookie:"):
        cookie = cookie[7:].strip()
    if not cookie:
        return False

    try:
        headers = {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            "Accept-Language": "ko-KR,ko;q=0.9",
            "Cookie": cookie,
        }
        req = urllib.request.Request(ECLASS_HOME_URL, headers=headers, method="GET")
        with urllib.request.urlopen(req, timeout=8) as resp:
            page_html = resp.read().decode("utf-8", errors="replace")
        return "logout.acl" in page_html
    except Exception:
        return False


def _extract_cookie_via_http_login(eclass_id: str, eclass_pw: str) -> str | None:
    """1차 시도: 브라우저를 띄우지 않고 직접 HTTP 세션(CookieJar)으로 eClass 로그인 후 쿠키 추출."""
    if not eclass_id or not eclass_pw:
        return None

    try:
        cj = http.cookiejar.CookieJar()
        opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))
        ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"

        # 1) 초기 세션 쿠키(WMONID, JSESSIONID) 발급
        init_req = urllib.request.Request(
            ECLASS_LOGIN_PAGE_URL,
            headers={"User-Agent": ua, "Accept-Language": "ko-KR,ko;q=0.9"},
            method="GET",
        )
        opener.open(init_req, timeout=8).read()

        # 2) 로그인 POST
        form_data = urllib.parse.urlencode(
            {
                "usr_id": eclass_id,
                "usr_pwd": eclass_pw,
                "returnURL": "",
            }
        ).encode("utf-8")
        login_req = urllib.request.Request(
            ECLASS_LOGIN_POST_URL,
            data=form_data,
            headers={
                "User-Agent": ua,
                "Referer": ECLASS_LOGIN_PAGE_URL,
                "Content-Type": "application/x-www-form-urlencoded",
            },
            method="POST",
        )
        opener.open(login_req, timeout=8).read()

        cookie_pairs = [f"{c.name}={c.value}" for c in cj]
        cookie_str = "; ".join(cookie_pairs)
        if cookie_str and _is_cookie_logged_in(cookie_str):
            return cookie_str
    except Exception:
        pass
    return None


def _launch_playwright_browser(p, headless: bool):
    """Windows에 기본 설치된 Edge/Chrome을 우선 활용하고, 없으면 기본 Chromium을 실행한다."""
    for channel in ("msedge", "chrome", None):
        try:
            if channel:
                return p.chromium.launch(channel=channel, headless=headless)
            return p.chromium.launch(headless=headless)
        except Exception:
            continue
    raise RuntimeError("실행 가능한 브라우저(Edge/Chrome/Chromium)를 찾지 못했습니다.")


def _extract_cookie_via_playwright(
    eclass_id: str | None = None,
    eclass_pw: str | None = None,
    open_browser: bool = False,
) -> tuple[str | None, str]:
    """Playwright 에이전트 브라우저를 구동하여 eClass 로그인 후 쿠키를 자동 추출한다."""
    try:
        from playwright.sync_api import sync_playwright
    except ImportError:
        return None, "서버에 playwright가 설치되어 있지 않습니다. (pip install playwright)"

    uid = (eclass_id or "").strip()
    upw = (eclass_pw or "").strip()

    # ID/PW가 있고 수동 창 열기가 아니면 먼저 빠른 HTTP 로그인을 시도
    if uid and upw and not open_browser:
        fast_cookie = _extract_cookie_via_http_login(uid, upw)
        if fast_cookie:
            return fast_cookie, "HTTP 자동 로그인으로 eClass 쿠키 추출 성공!"

    use_headless = bool(uid and upw and not open_browser)

    try:
        with sync_playwright() as p:
            browser = _launch_playwright_browser(p, headless=use_headless)
            context = browser.new_context(locale="ko-KR")
            page = context.new_page()

            page.goto(ECLASS_LOGIN_PAGE_URL, wait_until="domcontentloaded", timeout=20000)

            # ID/PW가 전달된 경우 자동으로 입력 및 로그인 버튼 클릭 시도
            if uid and upw:
                try:
                    for id_sel in ("#usr_id", "input[name='usr_id']", "#user_id", "input[type='text']"):
                        if page.locator(id_sel).count() > 0:
                            page.locator(id_sel).first.fill(uid)
                            break
                    for pw_sel in ("#usr_pwd", "input[name='usr_pwd']", "#user_password", "input[type='password']"):
                        if page.locator(pw_sel).count() > 0:
                            page.locator(pw_sel).first.fill(upw)
                            break
                    for btn_sel in ("#login_btn", ".bt_login", "button[type='submit']", "input[type='submit']"):
                        if page.locator(btn_sel).count() > 0:
                            page.locator(btn_sel).first.click()
                            break
                    else:
                        page.keyboard.press("Enter")
                except Exception:
                    pass

            # 로그인 완료 감지 루프 (최대: 헤드리스 12초 / 브라우저 직접 로그인 90초)
            max_wait_sec = 12 if use_headless else 90
            extracted_cookie = None

            for _ in range(max_wait_sec * 2):
                time.sleep(0.5)
                cookies = context.cookies(ECLASS_BASE_URL)
                if not cookies:
                    continue

                cookie_str = "; ".join(f"{c['name']}={c['value']}" for c in cookies)
                cur_url = page.url or ""

                # 메인 페이지로 이동했거나 로그아웃 버튼이 존재하면 로그인 성공으로 판정
                if "main_form.acl" in cur_url or _is_cookie_logged_in(cookie_str):
                    if _is_cookie_logged_in(cookie_str):
                        extracted_cookie = cookie_str
                        break

            browser.close()

            if extracted_cookie:
                return extracted_cookie, "브라우저 에이전트로 eClass 쿠키 자동 추출 완료!"
            return None, "eClass 로그인 확인 시간 초과 또는 아이디/비밀번호를 확인해 주세요."
    except Exception as e:
        return None, f"브라우저 자동 추출 중 오류: {e}"


def _crawl_eclass_todos(cookie_header: str) -> list[dict] | None:
    """실제 서울과기대 eClass(main_todo_list.acl) HTML을 크롤링해 파싱한다."""
    cookie = (cookie_header or "").strip()
    if cookie.lower().startswith("cookie:"):
        cookie = cookie[7:].strip()
    if not cookie:
        return None

    # 먼저 유효한 로그인 쿠키인지 확인
    if not _is_cookie_logged_in(cookie):
        return None

    try:
        headers = {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            "Accept-Language": "ko-KR,ko;q=0.9",
            "Referer": ECLASS_HOME_URL,
            "Cookie": cookie,
            "X-Requested-With": "XMLHttpRequest",
        }
        data = urllib.parse.urlencode({"start": "0", "display": "20", "encoding": "utf-8"}).encode("utf-8")
        req = urllib.request.Request(ECLASS_TODO_URL, data=data, headers=headers, method="POST")
        with urllib.request.urlopen(req, timeout=8) as resp:
            page_html = resp.read().decode("utf-8", errors="replace")

        items: list[dict] = []
        matches = re.findall(
            r'<a\b[^>]*class\s*=\s*"[^"]*\baside_list_todo_item\b[^"]*"[^>]*>.*?</a>',
            page_html,
            flags=re.IGNORECASE | re.DOTALL,
        )
        for idx, block in enumerate(matches):
            title = _find_class_text(block, "aside_list_title")
            if not title:
                continue
            course = _find_class_text(block, "aside_list_info")
            due = _find_class_text(block, "aside_list_date")
            kind = _find_class_text(block, "aside_list_top")
            kind = re.sub(r"\bD-\d+\b", "", kind).strip()

            item_type = "lecture" if "영상" in kind or "강의" in kind else "assignment"
            full_title = f"[{kind}] {title}" if kind else title
            items.append({
                "id": f"crawl_{idx + 1}",
                "type": item_type,
                "title": full_title,
                "courseName": course,
                "dueAt": due,
            })

        # 로그인 상태가 확인되었으므로 할 일이 0개여도 빈 리스트([])를 정상 반환한다!
        return items
    except Exception:
        return None


def _read_todo_list(
    conn,
    student_id: str,
    prev_count: int | None = None,
    source: str = "demo",
    message: str = "",
) -> TodoListResponse:
    rows = conn.execute(
        "SELECT * FROM user_todos WHERE student_id = ? ORDER BY ord ASC",
        (student_id,),
    ).fetchall()
    meta = conn.execute(
        "SELECT * FROM user_todo_meta WHERE student_id = ?",
        (student_id,),
    ).fetchone()

    items = [
        TodoItemData(
            id=r["todo_id"],
            type=r["item_type"],
            title=r["title"],
            courseName=r["course_name"],
            dueAt=r["due_at"],
        )
        for r in rows
    ]
    cur_total = len(items)
    old_total = prev_count if prev_count is not None else cur_total
    decreased = max(0, old_total - cur_total)

    return TodoListResponse(
        items=items,
        total=cur_total,
        previousTotal=old_total,
        decreasedCount=decreased,
        syncedAt=meta["synced_at"] if meta else _now_iso(),
        source=source,
        message=message,
    )


@router.get("", response_model=TodoListResponse)
def get_todos(authorization: str | None = Header(default=None)):
    init_todo_tables()
    student_id, _ = authorized_session(authorization)
    with connection() as conn:
        with conn:
            _ensure_initial_todos(conn, student_id)
            return _read_todo_list(conn, student_id)


@router.post("/sync", response_model=TodoListResponse)
def sync_todos(
    payload: TodoSyncRequest | None = None,
    authorization: str | None = Header(default=None),
):
    init_todo_tables()
    student_id, _ = authorized_session(authorization)
    now = _now_iso()
    status_msg = ""

    with connection() as conn:
        with conn:
            _ensure_initial_todos(conn, student_id)

            old_rows = conn.execute(
                "SELECT * FROM user_todos WHERE student_id = ? ORDER BY ord ASC",
                (student_id,),
            ).fetchall()
            prev_count = len(old_rows)

            meta = conn.execute(
                "SELECT * FROM user_todo_meta WHERE student_id = ?",
                (student_id,),
            ).fetchone()
            saved_cookie = meta["cookie_header"] if meta else ""

            # 1) 사용자가 직접 쿠키를 넘긴 경우 갱신
            if payload and payload.cookieHeader and payload.cookieHeader.strip():
                saved_cookie = payload.cookieHeader.strip()

            # 2) 브라우저 자동 로그인(창 열기 또는 eClass ID/PW 입력) 요청 시 에이전트 브라우저로 쿠키 자동 추출
            if payload and (
                payload.openBrowserLogin
                or (payload.eclassId and payload.eclassPassword)
            ):
                extracted, msg = _extract_cookie_via_playwright(
                    eclass_id=payload.eclassId,
                    eclass_pw=payload.eclassPassword,
                    open_browser=payload.openBrowserLogin,
                )
                status_msg = msg
                if extracted:
                    saved_cookie = extracted

            # 3) 추출/저장된 쿠키로 실제 eClass 크롤링 시도
            crawled = _crawl_eclass_todos(saved_cookie)

            if crawled is not None:
                conn.execute("DELETE FROM user_todos WHERE student_id = ?", (student_id,))
                for idx, item in enumerate(crawled):
                    conn.execute(
                        """INSERT INTO user_todos
                           (student_id, todo_id, item_type, title, course_name, due_at, ord)
                           VALUES (?, ?, ?, ?, ?, ?, ?)""",
                        (
                            student_id,
                            item["id"],
                            item["type"],
                            item["title"],
                            item["courseName"],
                            item["dueAt"],
                            idx,
                        ),
                    )
                new_count = len(crawled)
                source = "eclass_live"
                if not status_msg:
                    status_msg = f"실제 eClass에서 할 일 {new_count}건을 크롤링했습니다."
            else:
                # 쿠키가 없거나 만료된 상태에서 일반 리프레시를 누른 경우:
                # 시연용 데모 크롤링 감소 로직 실행 (할 일이 1개씩 줄어들어 '와! 해냈어!' 연출)
                source = "demo"
                if prev_count > 0:
                    first_id = old_rows[0]["todo_id"]
                    conn.execute(
                        "DELETE FROM user_todos WHERE student_id = ? AND todo_id = ?",
                        (student_id, first_id),
                    )
                    new_count = prev_count - 1
                else:
                    for idx, item in enumerate(DEFAULT_ECLASS_TODOS):
                        conn.execute(
                            """INSERT OR IGNORE INTO user_todos
                               (student_id, todo_id, item_type, title, course_name, due_at, ord)
                               VALUES (?, ?, ?, ?, ?, ?, ?)""",
                            (
                                student_id,
                                item["id"],
                                item["type"],
                                item["title"],
                                item["courseName"],
                                item["dueAt"],
                                idx,
                            ),
                        )
                    new_count = len(DEFAULT_ECLASS_TODOS)

                if not status_msg:
                    status_msg = f"데모 리프레시 완료 (남은 할 일: {new_count}건)"

            conn.execute(
                """UPDATE user_todo_meta
                   SET last_count = ?, synced_at = ?, cookie_header = ?
                   WHERE student_id = ?""",
                (new_count, now, saved_cookie, student_id),
            )

            return _read_todo_list(
                conn,
                student_id,
                prev_count=prev_count,
                source=source,
                message=status_msg,
            )
