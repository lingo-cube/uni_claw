"""Core types for AI Strategy Advisor."""

from dataclasses import dataclass
from enum import Enum
from typing import Optional


class DecisionResult(str, Enum):
    """Result of an AI decision."""

    SUCCESS = "success"
    UNSURE = "unsure"
    GIVE_UP = "give_up"

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings.

        Returns:
            List of enum values
        """
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "DecisionResult":
        """Create an enum instance from a string value.

        Args:
            value: String value to convert

        Returns:
            DecisionResult enum instance

        Raises:
            ValueError: If value is not a valid enum value
        """
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid enum value.

        Args:
            value: String value to validate

        Returns:
            True if value is valid, False otherwise
        """
        return value in cls.values()


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
