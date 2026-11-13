"""App factory: router wiring + startup/shutdown, no Unity-specific logic."""

from contextlib import asynccontextmanager

from fastapi import FastAPI
from sqlalchemy.orm import sessionmaker

from app.api.v1.router import api_router
from app.core.config import Settings
from app.db.session import create_tables, make_engine


def create_app(settings: Settings | None = None) -> FastAPI:
    settings = settings or Settings()

    @asynccontextmanager
    async def lifespan(api: FastAPI):
        engine = make_engine(settings.database_url)
        try:
            create_tables(engine)
            api.state.session_factory = sessionmaker(
                bind=engine, autoflush=False, expire_on_commit=False
            )
            yield
        finally:
            engine.dispose()

    api = FastAPI(
        title=settings.app_name,
        description="Local-only, versioned REST API for exchanging Unity app state and events.",
        version="0.1.0",
        lifespan=lifespan,
    )
    api.include_router(api_router)

    @api.get("/", tags=["system"])
    def root() -> dict[str, str]:
        return {"name": settings.app_name, "docs": "/docs", "health": "/api/v1/health"}

    return api


app = create_app()
