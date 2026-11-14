"""자취방·캐릭터·인벤토리·지갑 SQLite DB 접근 모듈."""
from datetime import datetime, timezone
import threading

from app.repositories.auth_repository import connection, get_user, initialize as init_auth_db
from app.schemas.character import (
    CharacterData,
    CharacterPartsData,
    CharacterUpdateRequest,
    SurveySubmitRequest,
)
from app.schemas.me import MeResponse, OnboardingData
from app.schemas.room import (
    RoomGridSize,
    RoomLayout,
    RoomPlacementData,
    RoomUpdateRequest,
)

_lock = threading.Lock()
_initialized = False

DEFAULT_INVENTORY = [
    "wall_0", "wall_1", "wall_2", "wall_3",
    "floor_0", "floor_1", "floor_2", "floor_3",
    "furn_bed", "furn_box", "furn_frame", "furn_fridge", "furn_todo", "furn_streak",
    "cloth_outer_0", "cloth_outer_1", "cloth_acc_0",
    "cloth_top_0", "cloth_bottom_0", "cloth_shoe_0",
]

DEFAULT_PLACEMENTS = [
    {"furnitureId": "furn_bed", "gridX": 0, "gridY": 0, "gridZ": 0, "rotation": 0},
    {"furnitureId": "furn_fridge", "gridX": 4, "gridY": 0, "gridZ": 0, "rotation": 0},
    {"furnitureId": "furn_box", "gridX": 2, "gridY": 0, "gridZ": 0, "rotation": 0},
]


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def init_tables() -> None:
    global _initialized
    init_auth_db()
    with _lock:
        if _initialized:
            return
        with connection() as conn:
            conn.executescript("""
                CREATE TABLE IF NOT EXISTS characters (
                    student_id  TEXT PRIMARY KEY,
                    nickname    TEXT NOT NULL,
                    persona     TEXT NOT NULL DEFAULT 'backend',
                    catchphrase TEXT NOT NULL DEFAULT '',
                    preset_id   TEXT NOT NULL DEFAULT 'chara_0',
                    outer_id    TEXT DEFAULT 'cloth_outer_0',
                    acc_id      TEXT DEFAULT 'cloth_acc_0',
                    top_id      TEXT DEFAULT 'cloth_top_0',
                    bottom_id   TEXT DEFAULT 'cloth_bottom_0',
                    shoe_id     TEXT DEFAULT 'cloth_shoe_0'
                );

                CREATE TABLE IF NOT EXISTS wallets (
                    student_id   TEXT PRIMARY KEY,
                    total_earned INTEGER NOT NULL DEFAULT 0,
                    balance      INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS inventory (
                    student_id TEXT NOT NULL,
                    item_id    TEXT NOT NULL,
                    PRIMARY KEY (student_id, item_id)
                );

                CREATE TABLE IF NOT EXISTS rooms (
                    student_id   TEXT PRIMARY KEY,
                    visibility   TEXT NOT NULL DEFAULT 'friends',
                    wallpaper_id TEXT NOT NULL DEFAULT 'wall_0',
                    floor_id     TEXT NOT NULL DEFAULT 'floor_0',
                    updated_at   TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS room_placements (
                    student_id   TEXT NOT NULL,
                    furniture_id TEXT NOT NULL,
                    grid_x       INTEGER NOT NULL,
                    grid_y       INTEGER NOT NULL,
                    grid_z       INTEGER NOT NULL,
                    rotation     INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (student_id, grid_x, grid_y, grid_z)
                );
            """)
        _initialized = True


def _ensure_user_defaults(conn, student_id: str) -> None:
    """유저의 지갑·인벤토리·방 레코드가 아직 없으면 기본값으로 생성한다."""
    conn.execute(
        "INSERT OR IGNORE INTO wallets (student_id, total_earned, balance) VALUES (?, 100, 100)",
        (student_id,),
    )
    for item_id in DEFAULT_INVENTORY:
        conn.execute(
            "INSERT OR IGNORE INTO inventory (student_id, item_id) VALUES (?, ?)",
            (student_id, item_id),
        )
    room_row = conn.execute(
        "SELECT 1 FROM rooms WHERE student_id = ?", (student_id,)
    ).fetchone()
    if room_row is None:
        conn.execute(
            """INSERT INTO rooms (student_id, visibility, wallpaper_id, floor_id, updated_at)
               VALUES (?, 'friends', 'wall_0', 'floor_0', ?)""",
            (student_id, _now_iso()),
        )
        for p in DEFAULT_PLACEMENTS:
            conn.execute(
                """INSERT OR IGNORE INTO room_placements
                   (student_id, furniture_id, grid_x, grid_y, grid_z, rotation)
                   VALUES (?, ?, ?, ?, ?, ?)""",
                (student_id, p["furnitureId"], p["gridX"], p["gridY"], p["gridZ"],
                p["rotation"]),
            )


def get_character(student_id: str) -> CharacterData:
    init_tables()
    with connection() as conn:
        with conn:
            _ensure_user_defaults(conn, student_id)
            row = conn.execute(
                "SELECT * FROM characters WHERE student_id = ?", (student_id,)
            ).fetchone()
            if row is None:
                conn.execute(
                    """INSERT INTO characters (student_id, nickname, persona, catchphrase,
                    preset_id)
                       VALUES (?, ?, 'backend', '열심히 코딩 중!', 'chara_0')""",
                    (student_id, student_id),
                )
                row = conn.execute(
                    "SELECT * FROM characters WHERE student_id = ?", (student_id,)
                ).fetchone()

    return CharacterData(
        nickname=row["nickname"],
        persona=row["persona"],
        catchphrase=row["catchphrase"],
        presetId=row["preset_id"],
        parts=CharacterPartsData(
            outer=row["outer_id"],
            accessories=row["acc_id"],
            clothTop=row["top_id"],
            clothBottom=row["bottom_id"],
            shoe=row["shoe_id"],
        ),
    )


def save_survey(student_id: str, payload: SurveySubmitRequest) -> CharacterData:
    init_tables()
    with connection() as conn:
        with conn:
            _ensure_user_defaults(conn, student_id)
            conn.execute(
                """INSERT INTO characters (student_id, nickname, persona, catchphrase, preset_id)
                   VALUES (?, ?, ?, ?, 'chara_0')
                   ON CONFLICT(student_id) DO UPDATE SET
                       nickname = excluded.nickname,
                       persona = excluded.persona,
                       catchphrase = excluded.catchphrase""",
                (student_id, payload.nickname, payload.persona, payload.catchphrase),
            )
            conn.execute(
                "UPDATE auth_users SET has_character = 1 WHERE student_id = ?",
                (student_id,),
            )
    return get_character(student_id)


def update_character(student_id: str, payload: CharacterUpdateRequest) -> CharacterData:
    init_tables()
    get_character(student_id)  # 없으면 기본 생성 보장
    with connection() as conn:
        with conn:
            conn.execute(
                """UPDATE characters
                   SET preset_id = ?,
                       outer_id = ?,
                       acc_id = ?,
                       top_id = ?,
                       bottom_id = ?,
                       shoe_id = ?
                   WHERE student_id = ?""",
                (
                    payload.presetId,
                    payload.parts.outer,
                    payload.parts.accessories,
                    payload.parts.clothTop,
                    payload.parts.clothBottom,
                    payload.parts.shoe,
                    student_id,
                ),
            )
    return get_character(student_id)


def get_room(student_id: str) -> RoomLayout:
    init_tables()
    with connection() as conn:
        with conn:
            _ensure_user_defaults(conn, student_id)
            room_row = conn.execute(
                "SELECT * FROM rooms WHERE student_id = ?", (student_id,)
            ).fetchone()
            placement_rows = conn.execute(
                "SELECT * FROM room_placements WHERE student_id = ?", (student_id,)
            ).fetchall()

    placements = [
        RoomPlacementData(
            furnitureId=r["furniture_id"],
            gridX=r["grid_x"],
            gridY=r["grid_y"],
            gridZ=r["grid_z"],
            rotation=r["rotation"],
        )
        for r in placement_rows
    ]
    return RoomLayout(
        ownerStudentId=student_id,
        visibility=room_row["visibility"],
        wallpaperId=room_row["wallpaper_id"],
        floorId=room_row["floor_id"],
        gridSize=RoomGridSize(),
        placements=placements,
        updatedAt=room_row["updated_at"],
    )


def update_room(student_id: str, payload: RoomUpdateRequest) -> RoomLayout:
    init_tables()
    now = _now_iso()
    with connection() as conn:
        with conn:
            _ensure_user_defaults(conn, student_id)
            conn.execute(
                """UPDATE rooms
                   SET visibility = ?, wallpaper_id = ?, floor_id = ?, updated_at = ?
                   WHERE student_id = ?""",
                (payload.visibility, payload.wallpaperId, payload.floorId, now, student_id),
            )
            conn.execute(
                "DELETE FROM room_placements WHERE student_id = ?", (student_id,)
            )
            for p in payload.placements:
                conn.execute(
                    """INSERT OR REPLACE INTO room_placements
                       (student_id, furniture_id, grid_x, grid_y, grid_z, rotation)
                       VALUES (?, ?, ?, ?, ?, ?)""",
                    (student_id, p.furnitureId, p.gridX, p.gridY, p.gridZ, p.rotation),
                )
    return get_room(student_id)


def get_me(student_id: str) -> MeResponse:
    init_tables()
    user = get_user(student_id)
    role = user["role"] if user else "user"
    character = get_character(student_id)
    room = get_room(student_id)

    with connection() as conn:
        inv_rows = conn.execute(
            "SELECT item_id FROM inventory WHERE student_id = ?",
            (student_id,),
        ).fetchall()

    return MeResponse(
        studentId=student_id,
        role=role,
        onboarding=OnboardingData(
            hasCharacter=True,
            hasPersona=True,
            nextScene="scene_room",
        ),
        character=character,
        inventory=[r["item_id"] for r in inv_rows],
        room=room,
    )
