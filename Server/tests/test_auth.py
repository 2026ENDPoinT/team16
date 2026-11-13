"""Run with: python -m pytest tests/test_auth.py -q (from Server)."""
import os
import uuid

from fastapi import FastAPI
from fastapi.testclient import TestClient

from app.api.v1.auth import router


def new_client(tmp_path, monkeypatch):
    monkeypatch.setenv("AUTH_DB_PATH", str(tmp_path / f"{uuid.uuid4().hex}.sqlite3"))
    monkeypatch.setenv("AUTH_JWT_SECRET", "test-only-secret-012345678901234567890123")
    monkeypatch.setenv("AUTH_ENABLE_TEST_USER", "1")
    app = FastAPI()
    app.include_router(router)
    return TestClient(app)


def login(client, password="Test1234!"):
    return client.post("/v1/auth/login", json={"studentId": "20241234", "password": password})


def test_seeded_test_user_login(tmp_path, monkeypatch):
    client = new_client(tmp_path, monkeypatch)
    r = login(client)
    assert r.status_code == 200, r.text
    assert set(r.json()) == {"accessToken", "refreshToken", "expiresIn", "isNewUser", "role"}
    assert r.json()["expiresIn"] == 3600
    assert r.json()["isNewUser"] is True
    assert r.json()["role"] == "user"


def test_wrong_password_does_not_leak_user_existence(tmp_path, monkeypatch):
    client = new_client(tmp_path, monkeypatch)
    wrong = login(client, "incorrect")
    unknown = client.post("/v1/auth/login", json={"studentId": "99999999", "password": "incorrect"})
    assert wrong.status_code == unknown.status_code == 401
    assert wrong.json() == unknown.json()
    assert wrong.json()["error"]["code"] == "INVALID_CREDENTIALS"


def test_refresh_rotates_and_logout_revokes(tmp_path, monkeypatch):
    client = new_client(tmp_path, monkeypatch)
    first = login(client).json()
    refresh1 = first["refreshToken"]
    refreshed = client.post("/v1/auth/refresh", json={"refreshToken": refresh1})
    assert refreshed.status_code == 200, refreshed.text
    data = refreshed.json()
    assert data["refreshToken"] != refresh1
    old = client.post("/v1/auth/refresh", json={"refreshToken": refresh1})
    assert old.status_code == 401
    logout = client.post("/v1/auth/logout", headers={"Authorization": f"Bearer {data['accessToken']}"})
    assert logout.status_code == 204 and not logout.content
    assert client.post("/v1/auth/refresh", json={"refreshToken": data["refreshToken"]}).status_code == 401
    assert client.post("/v1/auth/logout", headers={"Authorization": f"Bearer {data['accessToken']}"}).status_code == 401


def test_bad_json_shape_and_rate_limit(tmp_path, monkeypatch):
    client = new_client(tmp_path, monkeypatch)
    assert client.post("/v1/auth/login", json={"studentId": 1}).status_code == 400
    for _ in range(5):
        assert login(client, "wrong").status_code == 401
    r = login(client)
    assert r.status_code == 429 and r.json()["error"]["code"] == "RATE_LIMITED"
