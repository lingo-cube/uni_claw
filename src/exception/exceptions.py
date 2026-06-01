"""Exception classes for uni-claw traversal system.

This module defines a hierarchy of exceptions for different error scenarios
during UI traversal, along with severity levels for intelligent handling.
"""

from enum import Enum
from typing import Optional


class ExceptionSeverity(Enum):
    """Severity levels for exception classification.

    Ordered from least to most severe:
    - INFO: Normal variations (popups, redirects) - transparent handling
    - WARNING: Issues needing attention but not blocking - log and continue
    - ERROR: Failures requiring retry - attempt recovery
    - CRITICAL: Serious issues requiring intervention - recover or backtrack
    - FATAL: Unrecoverable failures - terminate traversal
    """

    INFO = "info"
    WARNING = "warning"
    ERROR = "error"
    CRITICAL = "critical"
    FATAL = "fatal"


class TraversalException(Exception):
    """Base exception for all traversal-related exceptions.

    Supports exception chaining and includes default severity levels.
    """

    def __init__(
        self,
        message: str,
        severity: Optional[ExceptionSeverity] = None,
        cause: Optional[Exception] = None,
    ):
        """Initialize traversal exception.

        Args:
            message: Human-readable error description
            severity: Override severity (uses default if not provided)
            cause: Original exception that caused this one
        """
        super().__init__(message)
        self._message = message
        self._severity = severity or self._get_default_severity()
        self.__cause__ = cause

    @property
    def message(self) -> str:
        """Get exception message."""
        return self._message

    @property
    def severity(self) -> ExceptionSeverity:
        """Get exception severity."""
        return self._severity

    def _get_default_severity(self) -> ExceptionSeverity:
        """Get default severity for this exception type."""
        return ExceptionSeverity.ERROR

    def __str__(self) -> str:
        """String representation with severity."""
        return f"[{self._severity.value.upper()}] {self._message}"


# Location-related exceptions (Task 1.3)


class LocationException(TraversalException):
    """Base exception for location/positioning errors."""

    pass


class ElementNotFoundException(LocationException):
    """Raised when AI cannot find expected element in screenshot.

    Context:
        - Element being searched for
        - Current page context
    """

    def __init__(
        self,
        element: str,
        context: Optional[str] = None,
        cause: Optional[Exception] = None,
    ):
        """Initialize element not found exception.

        Args:
            element: Element identifier that wasn't found
            context: Optional page context description
            cause: Original exception
        """
        message = f"Element not found: {element}"
        if context:
            message += f" (context: {context})"
        super().__init__(message, cause=cause)

    def _get_default_severity(self) -> ExceptionSeverity:
        return ExceptionSeverity.ERROR


class PathMismatchException(LocationException):
    """Raised when current path doesn't match expected path after navigation.

    Context:
        - Expected path
        - Actual path
    """

    def __init__(
        self,
        expected: list[str],
        actual: list[str],
        cause: Optional[Exception] = None,
    ):
        """Initialize path mismatch exception.

        Args:
            expected: Expected path after navigation
            actual: Actual path observed
            cause: Original exception
        """
        message = f"Path mismatch: expected {' -> '.join(expected)}, got {' -> '.join(actual)}"
        super().__init__(message, cause=cause)

    def _get_default_severity(self) -> ExceptionSeverity:
        return ExceptionSeverity.WARNING


class CoordinateExpiredException(LocationException):
    """Raised when cached coordinates are no longer valid.

    Context:
        - Cached coordinate
        - Reason for expiration (UI change, element moved, etc.)
    """

    def __init__(
        self,
        coordinate: str,
        reason: Optional[str] = None,
        cause: Optional[Exception] = None,
    ):
        """Initialize coordinate expired exception.

        Args:
            coordinate: The expired coordinate
            reason: Optional reason for expiration
            cause: Original exception
        """
        message = f"Coordinate expired: {coordinate}"
        if reason:
            message += f" (reason: {reason})"
        super().__init__(message, cause=cause)

    def _get_default_severity(self) -> ExceptionSeverity:
        return ExceptionSeverity.ERROR


# Operation-related exceptions (Task 1.4)


class OperationException(TraversalException):
    """Base exception for operation execution errors."""

    pass


class ClickFailedException(OperationException):
    """Raised when tap/click operation fails after retries.

    Context:
        - Target coordinates
        - Attempt count
    """

    def __init__(
        self,
        target: str,
        attempts: int,
        cause: Optional[Exception] = None,
    ):
        """Initialize click failed exception.

        Args:
            target: Target coordinate or element description
            attempts: Number of attempts made
            cause: Original exception
        """
        message = f"Click failed after {attempts} attempts: {target}"
        super().__init__(message, cause=cause)

    def _get_default_severity(self) -> ExceptionSeverity:
        return ExceptionSeverity.ERROR


class InputFailedException(OperationException):
    """Raised when text input operation fails.

    Context:
        - Target element
        - Input text
    """

    def __init__(
        self,
        target: str,
        text: str,
        cause: Optional[Exception] = None,
    ):
        """Initialize input failed exception.

        Args:
            target: Target element description
            text: Input text that failed
            cause: Original exception
        """
        message = f"Input failed for '{target}': {text[:50]}{'...' if len(text) > 50 else ''}"
        super().__init__(message, cause=cause)

    def _get_default_severity(self) -> ExceptionSeverity:
        return ExceptionSeverity.ERROR


# Device-related exceptions (Task 1.5)


class DeviceException(TraversalException):
    """Base exception for device-related errors."""

    pass


class ADBDisconnectedException(DeviceException):
    """Raised when ADB connection is lost.

    Context:
        - Device identifier
        - Connection state
    """

    def __init__(
        self,
        device: Optional[str] = None,
        cause: Optional[Exception] = None,
    ):
        """Initialize ADB disconnected exception.

        Args:
            device: Optional device identifier
            cause: Original exception
        """
        message = "ADB connection lost"
        if device:
            message += f" for device: {device}"
        super().__init__(message, cause=cause)

    def _get_default_severity(self) -> ExceptionSeverity:
        return ExceptionSeverity.CRITICAL


class AppCrashException(DeviceException):
    """Raised when target application crashes.

    Context:
        - App name
        - Crash reason if available
    """

    def __init__(
        self,
        app: str,
        reason: Optional[str] = None,
        cause: Optional[Exception] = None,
    ):
        """Initialize app crash exception.

        Args:
            app: App name that crashed
            reason: Optional crash reason
            cause: Original exception
        """
        message = f"App crashed: {app}"
        if reason:
            message += f" (reason: {reason})"
        super().__init__(message, cause=cause)

    def _get_default_severity(self) -> ExceptionSeverity:
        return ExceptionSeverity.CRITICAL


class DeviceOfflineException(DeviceException):
    """Raised when device goes offline.

    Context:
        - Device identifier
        - Connection state
    """

    def __init__(
        self,
        device: Optional[str] = None,
        cause: Optional[Exception] = None,
    ):
        """Initialize device offline exception.

        Args:
            device: Optional device identifier
            cause: Original exception
        """
        message = "Device went offline"
        if device:
            message += f": {device}"
        super().__init__(message, cause=cause)

    def _get_default_severity(self) -> ExceptionSeverity:
        return ExceptionSeverity.FATAL


# UI-related exceptions (Task 1.6)


class UIException(TraversalException):
    """Base exception for UI state changes."""

    pass


class PopupDetectedException(UIException):
    """Raised when unexpected popup appears.

    Context:
        - Popup description if available
    """

    def __init__(
        self,
        popup_info: Optional[str] = None,
        cause: Optional[Exception] = None,
    ):
        """Initialize popup detected exception.

        Args:
            popup_info: Optional popup description
            cause: Original exception
        """
        message = "Popup detected"
        if popup_info:
            message += f": {popup_info}"
        super().__init__(message, cause=cause)

    def _get_default_severity(self) -> ExceptionSeverity:
        return ExceptionSeverity.INFO


class PageRedirectException(UIException):
    """Raised when unexpected page redirect occurs.

    Context:
        - Redirect destination
    """

    def __init__(
        self,
        destination: str,
        cause: Optional[Exception] = None,
    ):
        """Initialize page redirect exception.

        Args:
            destination: Redirect destination path
            cause: Original exception
        """
        message = f"Page redirect to: {destination}"
        super().__init__(message, cause=cause)

    def _get_default_severity(self) -> ExceptionSeverity:
        return ExceptionSeverity.INFO


class LoadingTimeoutException(UIException):
    """Raised when page loading exceeds timeout.

    Context:
        - Timeout duration
    """

    def __init__(
        self,
        timeout: float,
        cause: Optional[Exception] = None,
    ):
        """Initialize loading timeout exception.

        Args:
            timeout: Timeout duration in seconds
            cause: Original exception
        """
        message = f"Page loading exceeded timeout: {timeout}s"
        super().__init__(message, cause=cause)

    def _get_default_severity(self) -> ExceptionSeverity:
        return ExceptionSeverity.WARNING


# AI-related exceptions (Task 1.7)


class AIException(TraversalException):
    """Base exception for AI service errors."""

    pass


class AIAnalysisFailedException(AIException):
    """Raised when AI service returns error.

    Context:
        - Service name
        - Error details
    """

    def __init__(
        self,
        service: str,
        error: str,
        cause: Optional[Exception] = None,
    ):
        """Initialize AI analysis failed exception.

        Args:
            service: AI service name (e.g., "Claude", "GPT")
            error: Error description
            cause: Original exception
        """
        message = f"AI analysis failed ({service}): {error}"
        super().__init__(message, cause=cause)

    def _get_default_severity(self) -> ExceptionSeverity:
        return ExceptionSeverity.ERROR


class AIResponseInvalidException(AIException):
    """Raised when AI response cannot be parsed.

    Context:
        - Raw response
        - Expected format
    """

    def __init__(
        self,
        response: str,
        expected: str,
        cause: Optional[Exception] = None,
    ):
        """Initialize AI response invalid exception.

        Args:
            response: Raw response (truncated if too long)
            expected: Expected format description
            cause: Original exception
        """
        truncated = response[:100] + "..." if len(response) > 100 else response
        message = f"AI response invalid (expected {expected}): {truncated}"
        super().__init__(message, cause=cause)

    def _get_default_severity(self) -> ExceptionSeverity:
        return ExceptionSeverity.WARNING
