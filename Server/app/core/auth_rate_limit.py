"""Process-local login failure limiter; limits a specific account and a client IP."""
from collections import defaultdict, deque
import threading
import time

from app.core.auth_errors import AuthError

_lock = threading.Lock()
_failures = defaultdict(deque)
LIMIT_PER_ACCOUNT_IP = 5
LIMIT_PER_IP = 20
WINDOW = 60


def _prune(queue, now):
    while queue and queue[0] <= now - WINDOW:
        queue.popleft()


def _keys(client_ip: str, student_id: str):
    return (("account", client_ip, student_id), ("ip", client_ip))


def ensure_login_allowed(client_ip: str, student_id: str):
    now = time.monotonic()
    with _lock:
        for key, limit in zip(_keys(client_ip, student_id), (LIMIT_PER_ACCOUNT_IP, LIMIT_PER_IP)):
            queue = _failures[key]
            _prune(queue, now)
            if len(queue) >= limit:
                raise AuthError(429, "RATE_LIMITED", "로그인 시도가 너무 많습니다. 잠시 후 다시 시도해 주세요.")


def record_login_failure(client_ip: str, student_id: str):
    now = time.monotonic()
    with _lock:
        for key in _keys(client_ip, student_id):
            queue = _failures[key]
            _prune(queue, now)
            queue.append(now)


def clear_login_failures(client_ip: str, student_id: str):
    with _lock:
        _failures.pop(("account", client_ip, student_id), None)
