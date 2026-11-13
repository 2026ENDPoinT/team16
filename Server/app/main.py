"""FastAPI 앱 진입점. 현재 구현 범위는 GET /health뿐이다."""
from datetime import datetime, timezone                                                                                                         
from pathlib import Path                                                                                                                        
import sys                                                                                                                                      
                                                                                                                                                    
# 상위 폴더(team16)에서 실행하거나 열었을 때도 'app' 모듈을 찾을 수 있도록 경로 추가                                                            
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))                                                                                    
                                                                                                                                                    
from fastapi import FastAPI                                                                                                                     
from fastapi.middleware.cors import CORSMiddleware                                                                                              

from app.api.v1.router import api_router                                                                                                        
from app.core.config import settings                                                                                                            
                                                                                                                                                    
app = FastAPI(title=settings.app_name, version=settings.app_version)                                                                            
app.add_middleware(                                                                                                                             
    CORSMiddleware,                                                                                                                             
    allow_origins=["*"],                                                                                                                        
    allow_credentials=True,                                                                                                                     
    allow_methods=["*"],                                                                                                                        
    allow_headers=["*"],                                                                                                                        
    )                                                                                                                                               
app.include_router(api_router, prefix=settings.api_prefix)


@app.get("/health", tags=["health"])
def health() -> dict[str, str]:
    """API문서.md 10장: 인증이 필요 없는 서버 상태 조회."""
    server_time = datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")
    return {"status": "ok", "version": settings.app_version, "serverTime": server_time}

# Added by auth_install.py (app-local accounts only)
from app.api.v1.auth import router as auth_router
app.include_router(auth_router)

# Read-only admin user list, added by admin_users_install.py
from app.api.v1.admin_users import router as admin_users_router
app.include_router(admin_users_router)

