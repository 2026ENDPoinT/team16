"""eClass 할 일 크롤링 및 동기화 API (/v1/todos).

- 재화 시스템 제거
- 수동 완료 체크 제거 (크롤링/리프레시 기반)
- 리프레시 결과 할 일 개수가 이전보다 줄어들면 decreasedCount > 0 을 반환해
  클라이언트(룸 말풍선 & PiP)가 '와! 해냈어!' 축하 대사를 띄우도록 한다.
"""

from datetime import datetime, timezone
import html
import re
import threading
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


def _crawl_eclass_todos(cookie_header: str) -> list[dict] | None:
    """실제 서울과기대 eClass(main_todo_list.acl) HTML을 크롤링해 파싱한다."""
    cookie = (cookie_header or "").strip()
    if cookie.lower().startswith("cookie:"):
        cookie = cookie[7:].strip()
    if not cookie:
        return None

    try:
        headers = {
            "User-Agent": "Mozilla/5.0 (compatible; Zzapmagochi/1.0)",
            "Accept-Language": "ko-KR,ko;q=0.9",
            "Referer": ECLASS_HOME_URL,
            "Cookie": cookie,
            "X-Requested-With": "XMLHttpRequest",
        }
        data = urllib.parse.urlencode({"start": "0", "display": "20", "encoding": "utf-8"}).encode("utf-8")
        req = urllib.request.Request(ECLASS_TODO_URL, data=data, headers=headers, method="POST")
        with urllib.request.urlopen(req, timeout=8) as resp:
            page_html = resp.read().decode("utf-8", errors="replace")

        if 'type="password"' in page_html.lower() and "aside_list" not in page_html.lower():
            return None

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

        return items if items else None
    except Exception:
        return None


def _read_todo_list(conn, student_id: str, prev_count: int | None = None) -> TodoListResponse:
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
            if payload and payload.cookieHeader is not None:
                saved_cookie = payload.cookieHeader.strip()

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
            else:
                # 쿠키가 없거나 오프라인 시연 중일 때:
                # 리프레시를 누르면eClass에서 과제 1개를 제출해 할 일이 줄어든 상황을 자연스럽게 재현!
                # (만약 0개 상태에서 다시 리프레시하면 다음 주차 할 일 5개로 리셋)
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

            conn.execute(
                """UPDATE user_todo_meta
                   SET last_count = ?, synced_at = ?, cookie_header = ?
                   WHERE student_id = ?""",
                (new_count, now, saved_cookie, student_id),
            )

            return _read_todo_list(conn, student_id, prev_count=prev_count)
