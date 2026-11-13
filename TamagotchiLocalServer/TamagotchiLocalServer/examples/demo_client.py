"""Simulate Unity A and Unity B using only the Python standard library.

Start the server first: python run_server.py
Then execute: python examples/demo_client.py
"""

import json
import urllib.request

BASE = "http://127.0.0.1:8000/api/v1"


def call(method: str, path: str, body: dict | None = None) -> dict:
    data = json.dumps(body).encode("utf-8") if body is not None else None
    request = urllib.request.Request(
        BASE + path,
        data=data,
        method=method,
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(request, timeout=5) as response:
        return json.load(response)


def main() -> None:
    room = call("POST", "/rooms", {"name": "demo", "initial_state": {"hunger": 70}})
    room_id = room["id"]
    print(f"[Unity A] Created room: {room_id}")

    updated = call("PATCH", f"/rooms/{room_id}/state", {"changes": {"hunger": 90}})
    print(f"[Unity A] Changed hunger -> {updated['state']['hunger']}")

    call("POST", f"/rooms/{room_id}/events", {
        "sender_id": "unity_a", "event_type": "fed", "payload": {"food": "apple"}
    })
    other = call("GET", f"/rooms/{room_id}")
    print(f"[Unity B] Read hunger -> {other['state']['hunger']}")
    inbox = call("GET", f"/rooms/{room_id}/events?after_id=0")
    print(f"[Unity B] Received events -> {inbox['items']}")
    print("Demo completed. Room data remains in SQLite after the server restarts.")


if __name__ == "__main__":
    main()
