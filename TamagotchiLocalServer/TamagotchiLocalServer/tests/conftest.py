from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from app.core.config import Settings
from app.main import create_app


@pytest.fixture
def test_settings(tmp_path: Path) -> Settings:
    database_file = tmp_path / "isolated_test.db"
    return Settings(_env_file=None, database_url=f"sqlite:///{database_file.as_posix()}")


@pytest.fixture
def client(test_settings: Settings):
    with TestClient(create_app(test_settings)) as test_client:
        yield test_client
