from fastapi import APIRouter

router = APIRouter(tags=["system"])


@router.get("/health", summary="Check if the API is running")
def health() -> dict[str, str]:
    return {"status": "ok"}
