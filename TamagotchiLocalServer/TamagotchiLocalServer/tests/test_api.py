from fastapi.testclient import TestClient

from app.core.config import Settings
from app.main import create_app


def test_root_and_health(client: TestClient):
    assert client.get("/").status_code == 200
    response = client.get("/api/v1/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_two_clients_can_share_room_and_exchange_events(client: TestClient):
    created = client.post("/api/v1/rooms", json={
        "name": "Unity pair", "initial_state": {"hunger": 70, "mood": "normal"}
    })
    assert created.status_code == 201
    data = created.json()
    room_id = data["id"]
    assert data["state"]["hunger"] == 70
    assert data["revision"] == 0

    # Unity A sends a shallow state patch.
    changed = client.patch(f"/api/v1/rooms/{room_id}/state", json={
        "changes": {"hunger": 90}, "expected_revision": 0
    })
    assert changed.status_code == 200, changed.text
    assert changed.json()["revision"] == 1

    # Unity B can retrieve that state without knowing Unity A's implementation.
    received = client.get(f"/api/v1/rooms/{room_id}")
    assert received.status_code == 200
    assert received.json()["state"] == {"hunger": 90, "mood": "normal"}

    event = client.post(f"/api/v1/rooms/{room_id}/events", json={
        "sender_id": "unity_a", "event_type": "fed", "payload": {"food": "apple"}
    })
    assert event.status_code == 201
    event_id = event.json()["id"]

    # Unity B polls starting after event ID zero.
    events = client.get(f"/api/v1/rooms/{room_id}/events?after_id=0")
    assert events.status_code == 200
    assert events.json()["items"][0]["payload"] == {"food": "apple"}
    assert events.json()["next_after_id"] == event_id

    # No duplicate events if B supplies the last received ID.
    empty = client.get(f"/api/v1/rooms/{room_id}/events?after_id={event_id}")
    assert empty.status_code == 200
    assert empty.json() == {"items": [], "next_after_id": event_id}


def test_revision_conflict_is_409(client: TestClient):
    room_id = client.post("/api/v1/rooms", json={}).json()["id"]
    client.patch(f"/api/v1/rooms/{room_id}/state", json={"changes": {"hp": 1}})
    stale = client.patch(f"/api/v1/rooms/{room_id}/state", json={
        "changes": {"hp": 2}, "expected_revision": 0
    })
    assert stale.status_code == 409
    assert client.get(f"/api/v1/rooms/{room_id}").json()["state"] == {"hp": 1}


def test_error_codes_and_input_validation(client: TestClient):
    missing = "00000000-0000-0000-0000-000000000000"
    assert client.get(f"/api/v1/rooms/{missing}").status_code == 404
    assert client.post(f"/api/v1/rooms/{missing}/events", json={
        "sender_id": "a", "event_type": "foo"
    }).status_code == 404

    room_id = client.post("/api/v1/rooms", json={}).json()["id"]
    assert client.patch(f"/api/v1/rooms/{room_id}/state", json={"changes": {}}).status_code == 422
    assert client.post(f"/api/v1/rooms/{room_id}/events", json={
        "sender_id": "", "event_type": "foo"
    }).status_code == 422
    assert client.get(f"/api/v1/rooms/{room_id}/events?limit=101").status_code == 422


def test_room_deletion_cascades_events(client: TestClient):
    room_id = client.post("/api/v1/rooms", json={}).json()["id"]
    client.post(f"/api/v1/rooms/{room_id}/events", json={"sender_id": "A", "event_type": "hi"})
    removed = client.delete(f"/api/v1/rooms/{room_id}")
    assert removed.status_code == 204
    assert client.get(f"/api/v1/rooms/{room_id}").status_code == 404
    assert client.get(f"/api/v1/rooms/{room_id}/events").status_code == 404


def test_data_survives_restart(test_settings: Settings):
    with TestClient(create_app(test_settings)) as first:
        room_id = first.post("/api/v1/rooms", json={
            "initial_state": {"hp": 72}
        }).json()["id"]
    with TestClient(create_app(test_settings)) as second:
        value = second.get(f"/api/v1/rooms/{room_id}")
        assert value.status_code == 200
        assert value.json()["state"]["hp"] == 72
