"""Integration tests for GET /v1/admin/users, using a fresh temporary SQLite DB.

Run inside Server/: python -m pytest tests/test_admin_users.py -q
Requires the previous login, signup, and admin-account patches already installed.
"""

import sqlite3
import time

from fastapi import FastAPI
from fastapi.testclient import TestClient
import pytest

from app.api.v1.auth import router as auth_router
from app.api.v1.admin_users import router as users_router
from app.core.auth_security import hash_password
from app.repositories import auth_repository


@pytest.fixture
def api(tmp_path, monkeypatch):
    path = tmp_path / "test_admin.sqlite3"
    monkeypatch.setenv("AUTH_DB_PATH", str(path))
    monkeypatch.setenv("AUTH_JWT_SECRET", "test-only-auth-key-01234567890123456789")
    auth_repository.initialize()

    # Same hashed password fields as tools/create_admin.py; no production DB touched.
    salt, digest = hash_password("Admin1234!")
    with auth_repository.connection() as conn:
        with conn:
            conn.execute(
                """INSERT INTO auth_users
                   (student_id, display_name, password_salt, password_hash, role, has_character, created_at)
                   VALUES ('admin', 'admin', ?, ?, 'admin', 0, ?)""",
                (salt, digest, 1731542400),
            )

    app = FastAPI()
    app.include_router(auth_router)
    app.include_router(users_router)
    return TestClient(app), path


def auth_header(token):
    return {"Authorization": f"Bearer {token}"}


def admin_login(client):
    res = client.post("/v1/auth/login", json={"studentId": "admin", "password": "Admin1234!"})
    assert res.status_code == 200, res.text
    assert res.json()["role"] == "admin"
    return res.json()


def test_admin_login_lists_real_accounts_and_omits_secrets(api):
    client, db = api
    signed_up = client.post(
        "/v1/auth/signup", json={"studentId": "20248888", "password": "Test1234!"}
    )
    assert signed_up.status_code == 201, signed_up.text
    admin = admin_login(client)
    response = client.get("/v1/admin/users", headers=auth_header(admin["accessToken"]))
    assert response.status_code == 200, response.text
    body = response.json()
    assert body["total"] == 2
    assert {v["studentId"] for v in body["items"]} == {"admin", "20248888"}
    assert all(set(v) == {"studentId", "role", "createdAt"} for v in body["items"])
    assert all(v["createdAt"].endswith("Z") and "T" in v["createdAt"] for v in body["items"])
    assert not any(secret in response.text.lower() for secret in
                   ("password_hash", "password_salt", "refresh_token", "access_token"))
    with sqlite3.connect(db) as conn:
        assert conn.execute("SELECT count(*) FROM auth_users").fetchone()[0] == 2


def test_no_authorization_returns_401(api):
    client, _ = api
    res = client.get("/v1/admin/users")
    assert res.status_code == 401
    assert res.json()["error"]["code"] == "UNAUTHORIZED"


def test_invalid_token_returns_401(api):
    client, _ = api
    res = client.get("/v1/admin/users", headers=auth_header("bad.token.string"))
    assert res.status_code == 401
    assert res.json()["error"]["code"] == "UNAUTHORIZED"


def test_normal_user_receives_403(api):
    client, _ = api
    registration = client.post("/v1/auth/signup", json={"studentId": "20246666", "password": "Test1234!"})
    assert registration.status_code == 201
    response = client.get("/v1/admin/users", headers=auth_header(registration.json()["accessToken"]))
    assert response.status_code == 403
    assert response.json()["error"]["code"] == "FORBIDDEN"


def test_logout_revokes_access(api):
    client, _ = api
    login = admin_login(client)
    token = login["accessToken"]
    assert client.get("/v1/admin/users", headers=auth_header(token)).status_code == 200
    assert client.post("/v1/auth/logout", headers=auth_header(token)).status_code == 204
    response = client.get("/v1/admin/users", headers=auth_header(token))
    assert response.status_code == 401


def test_current_database_role_matters(api):
    client, _ = api
    login = admin_login(client)
    with auth_repository.connection() as conn:
        with conn:
            conn.execute("UPDATE auth_users SET role='user' WHERE student_id='admin'")
    response = client.get("/v1/admin/users", headers=auth_header(login["accessToken"]))
    assert response.status_code == 403


def test_uses_existing_account_data_without_changes(api):
    client, db = api
    admin = admin_login(client)
    assert client.get("/v1/admin/users", headers=auth_header(admin["accessToken"])).status_code == 200
    with sqlite3.connect(db) as conn:
        assert conn.execute("SELECT count(*) FROM auth_users").fetchone()[0] == 1
