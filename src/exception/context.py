"""Exception context and result data structures.

These dataclasses define the context passed to exception handlers
and the results they return.
"""

from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from typing import TYPE_CHECKING, Optional

if TYPE_CHECKING:
    from ..state.content_tree import ContentNode
    from ..state.content_tree import TraversalState
    from .exceptions import TraversalException


class ExceptionAction(str, Enum):
    """Actions that can be taken when handling an exception.

    - RETRY: Retry the operation with incremented retry count
    - SKIP: Skip current operation, continue with next item
    - BACKTRACK: Return to previous node, mark current as failed
    - RECOVER: Execute recovery action, then retry operation
    - TERMINATE: Stop traversal, re-raise exception
    - IGNORE: Log exception but continue normally
    """

    RETRY = "retry"
    SKIP = "skip"
    BACKTRACK = "backtrack"
    RECOVER = "recover"
    TERMINATE = "terminate"
    IGNORE = "ignore"

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings.

        Returns:
            List of enum values
        """
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "ExceptionAction":
        """Create an enum instance from a string value.

        Args:
            value: String value to convert

        Returns:
            ExceptionAction enum instance

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


class RecoveryAction(str, Enum):
    """Specific recovery actions to execute.

    - RECONNECT_ADB: Reconnect ADB connection
    - RESTART_APP: Stop and restart the target app
    - CLOSE_POPUP: Close detected popup
    - NAVIGATE_BACK: Press back button to return
    - WAIT_AND_RETRY: Wait before retrying
    - IGNORE_UI_CHANGE: Log and continue (no action)
    """

    RECONNECT_ADB = "reconnect_adb"
    RESTART_APP = "restart_app"
    CLOSE_POPUP = "close_popup"
    NAVIGATE_BACK = "navigate_back"
    WAIT_AND_RETRY = "wait_and_retry"
    IGNORE_UI_CHANGE = "ignore_ui_change"

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings.

        Returns:
            List of enum values
        """
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "RecoveryAction":
        """Create an enum instance from a string value.

        Args:
            value: String value to convert

        Returns:
            RecoveryAction enum instance

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


@dataclass
class ExceptionContext:
    """Context information passed to exception handlers.

    Contains all relevant information about the exception,
    current state, and operation being performed.
    """

    exception: "TraversalException"  # The exception that occurred
    severity: "ExceptionSeverity"  # Severity level of the exception
    state: "TraversalState"  # Current traversal state
    node: Optional["ContentNode"]  # Current tree node if applicable
    operation: str  # Operation name being performed
    timestamp: datetime  # When the exception occurred
    retry_count: int  # Current retry attempt number

    # Phase 2 placeholders (commented out for future use)
    # screenshot: Optional[bytes] = None  # Screenshot for AI analysis
    # ai_result: Optional[dict] = None  # AI decision result

    def to_dict(self) -> dict:
        """Convert context to dictionary for serialization."""
        from .exceptions import ExceptionSeverity

        return {
            "exception_type": type(self.exception).__name__,
            "exception_message": str(self.exception),
            "severity": self.severity.value if isinstance(self.severity, ExceptionSeverity) else self.severity,
            "operation": self.operation,
            "timestamp": self.timestamp.isoformat(),
            "retry_count": self.retry_count,
            "current_path": self.state.current_path,
        }


@dataclass
class ExceptionHandlingResult:
    """Result returned by exception handlers.

    Determines what action to take and optionally includes
    recovery instructions.
    """

    action: ExceptionAction  # Action to take
    message: str  # Human-readable description
    new_state: Optional[str] = None  # Optional state transition target
    recovery_action: Optional[RecoveryAction] = None  # Recovery action if action is RECOVER

    # Phase 2 placeholder (commented out for future use)
    # ai_result: Optional[dict] = None  # AI decision result

    def to_dict(self) -> dict:
        """Convert result to dictionary for serialization."""
        return {
            "action": self.action.value,
            "message": self.message,
            "new_state": self.new_state,
            "recovery_action": self.recovery_action.value if self.recovery_action else None,
        }

    @classmethod
    def retry(cls, message: str, retry_count: int = 0, max_retries: int = 3) -> "ExceptionHandlingResult":
        """Create a RETRY result."""
        return cls(
            action=ExceptionAction.RETRY,
            message=f"{message} (retry {retry_count + 1}/{max_retries})",
        )

    @classmethod
    def skip(cls, message: str = "Skipping current operation") -> "ExceptionHandlingResult":
        """Create a SKIP result."""
        return cls(action=ExceptionAction.SKIP, message=message)

    @classmethod
    def backtrack(cls, message: str = "Backtracking to previous node") -> "ExceptionHandlingResult":
        """Create a BACKTRACK result."""
        return cls(action=ExceptionAction.BACKTRACK, message=message)

    @classmethod
    def recover(
        cls,
        recovery: RecoveryAction,
        new_state: Optional[str] = None,
        message: Optional[str] = None,
    ) -> "ExceptionHandlingResult":
        """Create a RECOVER result."""
        if message is None:
            message = f"Executing recovery: {recovery.value}"
        return cls(
            action=ExceptionAction.RECOVER,
            message=message,
            new_state=new_state,
            recovery_action=recovery,
        )

    @classmethod
    def terminate(cls, message: str = "Terminating traversal") -> "ExceptionHandlingResult":
        """Create a TERMINATE result."""
        return cls(action=ExceptionAction.TERMINATE, message=message)

    @classmethod
    def ignore(cls, message: str = "Ignoring exception, continuing") -> "ExceptionHandlingResult":
        """Create an IGNORE result."""
        return cls(action=ExceptionAction.IGNORE, message=message)
