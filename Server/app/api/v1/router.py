"""v1 API 라우터 통합. 현재 기능 엔드포인트는 모두 미구현."""
from fastapi import APIRouter
from app.api.v1 import auth, me, tasks, room, shop, external, friends, awards, admin

api_router = APIRouter()
api_router.include_router(auth.router)
api_router.include_router(me.router)
api_router.include_router(tasks.router)
api_router.include_router(room.router)
api_router.include_router(shop.router)
api_router.include_router(external.router)
api_router.include_router(friends.router)
api_router.include_router(awards.router)
api_router.include_router(admin.router)
