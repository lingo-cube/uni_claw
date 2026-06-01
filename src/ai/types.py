"""Core types for AI Strategy Advisor."""

from dataclasses import dataclass
from enum import Enum
from typing import Optional


class DecisionResult(str, Enum):
    """Result of an AI decision."""

    SUCCESS = "success"
    UNSURE = "unsure"
    GIVE_UP = "give_up"


@dataclass(frozen=True)
class ContainerInference:
    """Result of container type inference."""

    container_type: str
    confidence: float
    matched_template: Optional[str] = None

    def __post_init__(self):
        """Validate confidence is in [0, 1]."""
        if not 0.0 <= self.confidence <= 1.0:
            raise ValueError(f"Confidence must be between 0 and 1, got {self.confidence}")


__all__ = ["DecisionResult", "ContainerInference"]
