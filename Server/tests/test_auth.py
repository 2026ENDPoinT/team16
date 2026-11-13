"""Run from Server/: python -m pytest tests/test_auth.py -q"""
import sqlite3
import uuid

from fastapi import FastAPI
from fastapi.testclient import TestClient

from app.api.v1.auth import router
from app.core import auth_rate_limit


PASSWORD = "Test1234!"


def new_client(tmp_path, monkeypatch):
    # Each test simulates a fresh rate-limit cache / worker.
    with auth_rate_limit._lock:
        auth_rate_limit._failures.clear()
        auth_rate_limit._requests.clear()
    path = tmp_path / f"{uuid.uuid4().hex}.sqlite3"
    monkeypatch.setenv("AUTH_DB_PATH", str(path))
    monkeypatch.setenv("AUTH_JWT_SECRET", "test-only-secret-012345678901234567890123")
    # Previously turned ON for test user seeding; it must have NO EFFECT now.
    monkeypatch.setenv("AUTH_ENABLE_TEST_USER", "1")
    app = FastAPI()
    app.include_router(router)
    return TestClient(app), path


def signup(client, student_id="20241234", password=PASSWORD):
    return client.post("/v1/auth/signup", json={"studentId": student_id, "password": password})


def login(client, student_id="20241234", password=PASSWORD):
    return client.post("/v1/auth/login", json={"studentId": student_id, "password": password})


def test_login_unknown_does_not_create_user(tmp_path, monkeypatch):
    client, db = new_client(tmp_path, monkeypatch)
    for _ in range(2):
        response = login(client)
        assert response.status_code == 401
        assert response.json()["error"]["code"] == "INVALID_CREDENTIALS"
    with sqlite3.connect(db) as conn:
        assert conn.execute("SELECT count(*) FROM auth_users").fetchone()[0] == 0


def test_check_id_signup_login_persists_hash(tmp_path, monkeypatch):
    client, db = new_client(tmp_path, monkeypatch)
    res = client.get("/v1/auth/check-id", params={"studentId": "20241234"})
    assert res.status_code == 200 and res.json() == {"studentId": "20241234", "isAvailable": True}

    registered = signup(client)
    assert registered.status_code == 201, registered.text
    assert set(registered.json()) == {"accessToken", "refreshToken", "expiresIn", "isNewUser", "role"}
    assert registered.json()["expiresIn"] == 3600
    assert registered.json()["isNewUser"] is True
    assert registered.json()["role"] == "user"

    res = client.get("/v1/auth/check-id", params={"studentId": "20241234"})
    assert res.status_code == 200 and res.json()["isAvailable"] is False
    successful_login = login(client)
    assert successful_login.status_code == 200
    assert successful_login.json()["isNewUser"] is True
    with sqlite3.connect(db) as conn:
        row = conn.execute("SELECT * FROM auth_users WHERE student_id='20241234'").fetchone()
        assert row is not None
        digest = conn.execute("SELECT password_hash FROM auth_users WHERE student_id='20241234'").fetchone()[0]
        assert digest != PASSWORD and len(digest) == 64


def test_duplicate_student_id_does_not_overwrite_password(tmp_path, monkeypatch):
    client, _ = new_client(tmp_path, monkeypatch)
    assert signup(client).status_code == 201
    duplicate = signup(client, password="Change9999!")
    assert duplicate.status_code == 409
    assert duplicate.json()["error"]["code"] == "STUDENT_ID_TAKEN"
    assert login(client).status_code == 200
    assert login(client, password="Change9999!").status_code == 401


def test_wrong_password_and_unknown_have_same_error(tmp_path, monkeypatch):
    client, _ = new_client(tmp_path, monkeypatch)
    assert signup(client).status_code == 201
    wrong = login(client, password="BadPassword999")
    unknown = login(client, student_id="99999999", password="BadPassword999")
    assert wrong.status_code == unknown.status_code == 401
    assert wrong.json() == unknown.json()


def test_validation_rules(tmp_path, monkeypatch):
    client, _ = new_client(tmp_path, monkeypatch)
    bad_id = signup(client, student_id="abc")
    assert bad_id.status_code == 400 and bad_id.json()["error"]["code"] == "INVALID_REQUEST"
    for weak in ("123", "12345678", "abcdefgh"):
        res = signup(client, password=weak)
        assert res.status_code == 422 and res.json()["error"]["code"] == "WEAK_PASSWORD"
    assert client.get("/v1/auth/check-id", params={"studentId": "abcd"}).status_code == 400
    assert signup(client, password=PASSWORD).status_code == 201
    assert client.post("/v1/auth/signup", json={"studentId": "20249999"}).status_code == 400


def test_refresh_rotate_logout(tmp_path, monkeypatch):
    client, _ = new_client(tmp_path, monkeypatch)
    first = signup(client).json()
    refreshed = client.post("/v1/auth/refresh", json={"refreshToken": first["refreshToken"]})
    assert refreshed.status_code == 200, refreshed.text
    second = refreshed.json()
    assert second["refreshToken"] != first["refreshToken"]
    assert client.post("/v1/auth/refresh", json={"refreshToken": first["refreshToken"]}).status_code == 401
    response = client.post("/v1/auth/logout", headers={"Authorization": f"Bearer {second['accessToken']}"})
    assert response.status_code == 204 and response.content == b""
    assert client.post("/v1/auth/refresh", json={"refreshToken": second["refreshToken"]}).status_code == 401


def test_check_id_rate_limit(tmp_path, monkeypatch):
    client, _ = new_client(tmp_path, monkeypatch)
    # The limiter counts all requests to this endpoint (not just failures).
    for _ in range(10):
        assert client.get("/v1/auth/check-id", params={"studentId": "20245678"}).status_code == 200
    res = client.get("/v1/auth/check-id", params={"studentId": "20245678"})
    assert res.status_code == 429 and res.json()["error"]["code"] == "RATE_LIMITED"
