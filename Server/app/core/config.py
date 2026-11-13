"""서버 환경설정 (.env)."""
from pathlib import Path
from pydantic_settings import BaseSettings, SettingsConfigDict

ROOT = Path(__file__).resolve().parents[2]


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=ROOT / ".env", env_file_encoding="utf-8", extra="ignore")

    app_name: str = "Zzapmagochi Server"
    app_version: str = "0.1.0"
    api_prefix: str = "/v1"
    database_url: str = f"sqlite:///{(ROOT / 'data' / 'tamagotchi.db').as_posix()}"


settings = Settings()
