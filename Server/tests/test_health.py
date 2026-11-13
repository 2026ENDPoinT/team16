"""현재 기본 서버만 검사한다. pytest -q"""
from datetime import datetime
from fastapi.testclient import TestClient
from app.main import app

client = TestClient(app)


def test_health_returns_document_contract():
    response = client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "ok"
    assert data["version"] == "0.1.0"
    assert data["serverTime"].endswith("Z")
    datetime.fromisoformat(data["serverTime"].replace("Z", "+00:00"))


def test_swagger_openapi_available():
    assert client.get("/docs").status_code == 200
    assert client.get("/openapi.json").status_code == 200


def test_business_apis_are_not_fake_implemented():
    paths = client.get("/openapi.json").json()["paths"]
    assert set(paths) == {"/health"}
