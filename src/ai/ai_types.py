"""Core types for AI Strategy Advisor and AI capabilities."""

from dataclasses import dataclass, field
from enum import Enum
from typing import Optional, List, Dict, Any


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

    @classmethod
    def from_string(cls, value: str) -> "DecisionResult":
        """Alias for from_value for backward compatibility."""
        return cls.from_value(value)


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


__all__ = ["DecisionResult", "ContainerInference", "MismatchDetails", "Suggestion", "PageTypeVerification",
           "SafetyEvaluation", "PageLevelGuidance", "SafetyScreeningResult", "ContextDecisionResult"]


@dataclass
class MismatchDetails:
    """Details about page type mismatch."""
    missing_items: List[str] = field(default_factory=list)
    unexpected_items: List[str] = field(default_factory=list)
    type_conflict: Optional[str] = None


@dataclass
class Suggestion:
    """Suggestion for handling mismatch."""
    action: str  # "retry", "fallback", "skip"
    target: Optional[str] = None
    reason: str = ""


@dataclass
class PageTypeVerification:
    """Result of page type verification."""
    is_match: bool
    confidence: float
    actual_type: str
    reasoning: str = ""
    mismatch_details: Optional[MismatchDetails] = None
    suggestion: Optional[Suggestion] = None


@dataclass
class SafetyEvaluation:
    """Safety evaluation for a single element."""
    name: str
    safety_tag: str  # "safe", "unsafe", "caution"
    confidence: float
    reason: str
    context_dependency: Optional[str] = None
    task_relevance: Optional[str] = None


@dataclass
class PageLevelGuidance:
    """Page-level safety guidance."""
    overall_safe_to_proceed: bool
    recommended_max_parallel: int = 3
    special_precautions: List[str] = field(default_factory=list)
    task_suitability: Optional[str] = None


@dataclass
class SafetyScreeningResult:
    """Result of safety screening."""
    evaluations: List[SafetyEvaluation]
    page_level_guidance: Optional[PageLevelGuidance] = None


@dataclass
class ContextDecisionResult:
    """Result of context-aware decision making."""
    result: str  # "success", "unsure", "fallback"
    action: str  # "click", "back", "scroll", etc.
    target: Optional[str] = None
    params: Optional[Dict[str, Any]] = None
    reasoning: str = ""
    confidence: float = 0.5
    safety_verified: bool = True
