"""Public API response: only the selected student ID, as requested."""

from pydantic import BaseModel


class RandomStudentIdResponse(BaseModel):
    studentId: str
