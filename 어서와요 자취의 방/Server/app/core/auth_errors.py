"""Authentication-specific errors and response validation (without changing other APIs)."""
from fastapi import APIRouter
from fastapi.exceptions import RequestValidationError
from fastapi.routing import APIRoute
from fastapi.responses import JSONResponse


class AuthError(Exception):
    def __init__(self, status_code: int, code: str, message: str):
        self.status_code = status_code
        self.code = code
        self.message = message
        super().__init__(message)


def error_response(status: int, code: str, message: str):
    return JSONResponse(
        status_code=status,
        content={"error": {"code": code, "message": message, "detail": {}}},
    )


class AuthRoute(APIRoute):
    """Handle authentication errors per API문서.md without affecting unrelated endpoints."""

    def get_route_handler(self):
        original_handler = super().get_route_handler()

        async def handler(request):
            try:
                return await original_handler(request)
            except AuthError as exc:
                return error_response(exc.status_code, exc.code, exc.message)
            except RequestValidationError:
                return error_response(400, "INVALID_REQUEST", "요청 형식이 올바르지 않습니다.")

        return handler
