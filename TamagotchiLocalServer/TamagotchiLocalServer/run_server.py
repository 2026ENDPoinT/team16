"""Windows-friendly entry point: python run_server.py"""

import uvicorn
from app.core.config import Settings


if __name__ == "__main__":
    settings = Settings()
    uvicorn.run(
        "app.main:app",
        host=settings.app_host,
        port=settings.app_port,
        reload=False,
    )
