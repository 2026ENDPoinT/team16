"""Run from Server/: python -m pytest tests/test_random_visit.py -q"""

import sqlite3
import time

from fastapi import FastAPI
from fastapi.testclient import TestClient
import pytest

from app.api.v1.auth import router as auth_router
from app.api.v1.random_visit import router as random_visit_router
from app.core.auth_security import hash_password
from app.repositories import auth_repository


@pytest.fixture
def api(tmp_path, monkeypatch):
    db_path = tmp_path / "test_random_visit.sqlite3"
    monkeypatch.setenv("AUTH_DB_PATH", str(db_path))
    monkeypatch.setenv("AUTH_JWT_SECRET", "random-visit-test-jwt-key-012345678901")
    auth_repository.initialize()

    salt, digest = hash_password("Admin1234!")
    with auth_repository.connection() as conn:
        with conn:
            conn.execute(
                """INSERT INTO auth_users
                   (student_id, display_name, password_salt, password_hash, role, has_character, created_at)
                   VALUES ('admin', 'admin', ?, ?, 'admin', 0, ?)""",
                (salt, digest, int(time.time())),
            )

    app = FastAPI()
    app.include_router(auth_router)
    app.include_router(random_visit_router)
    return TestClient(app), db_path


def signup(client, student_id):
    res = client.post("/v1/auth/signup", json={"studentId": student_id, "password": "Test1234!"})
    assert res.status_code == 201, res.text
    return res.json()["accessToken"]


def get_target(client, token):
    return client.get("/v1/room/random/student-id", headers={"Authorization": f"Bearer {token}"})


def test_single_other_student_is_always_picked_not_self_or_admin(api):
    client, _ = api
    me_token = signup(client, "20240001")
    signup(client, "20240002")
    for _ in range(10):
        res = get_target(client, me_token)
        assert res.status_code == 200, res.text
        assert res.json() == {"studentId": "20240002"}


def test_no_other_students_returns_404(api):
    client, _ = api
    token = signup(client, "20240001")
    res = get_target(client, token)
    assert res.status_code == 404
    assert res.json()["error"]["code"] == "NO_VISITABLE_USER"


def test_multiple_students_never_include_self_or_admin(api):
    client, _ = api
    token = signup(client, "20240001")
    signup(client, "20240002")
    signup(client, "20240003")
    picked = set()
    for _ in range(50):
        res = get_target(client, token)
        assert res.status_code == 200
        value = res.json()["studentId"]
        assert value in {"20240002", "20240003"}
        picked.add(value)
    assert picked == {"20240002", "20240003"}


def test_admin_can_select_regular_users_but_is_never_selected(api):
    client, _ = api
    signup(client, "20240001")
    login = client.post("/v1/auth/login", json={"studentId": "admin", "password": "Admin1234!"})
    assert login.status_code == 200, login.text
    res = get_target(client, login.json()["accessToken"])
    assert res.status_code == 200
    assert res.json() == {"studentId": "20240001"}


def test_missing_or_malformed_auth_is_401(api):
    client, _ = api
    for headers in ({}, {"Authorization": "Bearer bad.token"}, {"Authorization": "invalid"}):
        res = client.get("/v1/room/random/student-id", headers=headers)
        assert res.status_code == 401
        assert res.json()["error"]["code"] == "UNAUTHORIZED"


def test_logout_revokes_access(api):
    client, _ = api
    token = signup(client, "20240001")
    signup(client, "20240002")
    assert get_target(client, token).status_code == 200
    assert client.post("/v1/auth/logout", headers={"Authorization": f"Bearer {token}"}).status_code == 204
    assert get_target(client, token).status_code == 401


def test_query_reads_existing_members_without_creating_accounts(api):
    client, db_path = api
    token = signup(client, "20240001")
    signup(client, "20240002")
    with sqlite3.connect(db_path) as conn:
        before = conn.execute("SELECT count(*) FROM auth_users").fetchone()[0]
    assert get_target(client, token).status_code == 200
    with sqlite3.connect(db_path) as conn:
        after = conn.execute("SELECT count(*) FROM auth_users").fetchone()[0]
    assert before == after == 3


def test_member_can_pick_user_regardless_of_current_character_flag(api):
    # The existing auth DB contains no room records, so character/visibility
    # eligibility cannot yet be validated by this ID-only candidate endpoint.
    client, _ = api
    token = signup(client, "20240001")
    signup(client, "20240002")
    assert get_target(client, token).json() == {"studentId": "20240002"}


def test_query_parameter_cannot_impersonate_another_student(api):
    client, _ = api
    mine = signup(client, "20240001")
    signup(client, "20240002")
    res = client.get(
        "/v1/room/random/student-id?studentId=20240002",
        headers={"Authorization": f"Bearer {mine}"},
    )
    assert res.status_code == 200
    assert res.json()["studentId"] == "20240002"


def test_route_remains_distinct_from_room_detail_route(api):
    client, _ = api
    token = signup(client, "20240001")
    signup(client, "20240002")
    # Simulate the future detailed room handler being registered earlier.
    app = FastAPI()
    from fastapi import APIRouter
    room_router = APIRouter(prefix="/v1/room")
    @room_router.get("/{studentId}")
    def get_room(studentId: str):
        return {"roomId": studentId}
    app.include_router(room_router)
    app.include_router(random_visit_router)
    separate_client = TestClient(app)
    resp = get_target(separate_client, token)
    assert resp.status_code == 200
    assert resp.json() == {"studentId": "20240002"}
    assert separate_client.get("/v1/room/20240002").json() == {"roomId": "20240002"}
