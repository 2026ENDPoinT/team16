"""Database engine and request-local session lifecycle."""

from collections.abc import Iterator
from pathlib import Path

from fastapi import Request
from sqlalchemy import Engine, create_engine, event
from sqlalchemy.orm import Session

from app.db.base import Base
from app.db import models  # noqa: F401 - register tables with Base.metadata


def make_engine(database_url: str) -> Engine:
    sqlite = database_url.startswith("sqlite")
    engine = create_engine(
        database_url,
        connect_args={"check_same_thread": False, "timeout": 15} if sqlite else {},
    )
    if sqlite:
        @event.listens_for(engine, "connect")
        def configure_sqlite(dbapi_connection, connection_record):
            cursor = dbapi_connection.cursor()
            cursor.execute("PRAGMA foreign_keys=ON")
            cursor.execute("PRAGMA busy_timeout=15000")
            cursor.close()
    return engine


def create_tables(engine: Engine) -> None:
    if engine.url.get_backend_name() == "sqlite":
        database = engine.url.database
        if database and database != ":memory:":
            Path(database).expanduser().parent.mkdir(parents=True, exist_ok=True)
    Base.metadata.create_all(bind=engine)


def get_db(request: Request) -> Iterator[Session]:
    """One DB session per HTTP request, automatically closed afterwards."""
    with request.app.state.session_factory() as session:
        yield session
