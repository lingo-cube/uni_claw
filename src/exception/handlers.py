"""Exception handler implementations.

This module defines the exception handler interface and all built-in handlers
for the uni-claw exception handling system.
"""

import logging
from abc import ABC, abstractmethod
from typing import Optional

from .context import ExceptionAction, ExceptionContext, ExceptionHandlingResult, RecoveryAction
from .exceptions import (
    ADBDisconnectedException,
    AIAnalysisFailedException,
    AppCrashException,
    DeviceException,
    DeviceOfflineException,
    ElementNotFoundException,
    ExceptionSeverity,
    PathMismatchException,
    PopupDetectedException,
    TraversalException,
    UIException,
    LoadingTimeoutException,
)

logger = logging.getLogger(__name__)


class ExceptionHandler(ABC):
    """Abstract base class for exception handlers.

    Handlers are tried in priority order by ExceptionHandlingChain.
    Each handler determines if it can handle an exception via can_handle(),
    and returns a handling result via handle().
    """

    @abstractmethod
    def can_handle(self, context: ExceptionContext) -> bool:
        """Check if this handler can process the given exception context.

        Args:
            context: Exception context containing exception, severity, state, etc.

        Returns:
            True if this handler should process this exception, False otherwise
        """
        pass

    @abstractmethod
    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """Handle the exception and return the action to take.

        Args:
            context: Exception context containing exception, severity, state, etc.

        Returns:
            ExceptionHandlingResult with action, message, and optional recovery instructions
        """
        pass


class FatalExceptionHandler(ExceptionHandler):
    """Handler for fatal exceptions that should terminate traversal.

    Handles:
        - Exceptions with FATAL severity

    Returns:
        - TERMINATE action (stops traversal)
    """

    def can_handle(self, context: ExceptionContext) -> bool:
        """Check if exception has FATAL severity."""
        # Check if severity is FATAL
        if isinstance(context.severity, ExceptionSeverity):
            return context.severity == ExceptionSeverity.FATAL
        # Handle string comparison for compatibility
        return context.severity == "fatal"

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """Return TERMINATE action for fatal exceptions."""
        return ExceptionHandlingResult.terminate(
            message=f"Fatal exception: {context.exception.message}"
        )


class DeviceExceptionHandler(ExceptionHandler):
    """Handler for device-related exceptions.

    Handles:
        - ADBDisconnectedException → RECOVER with RECONNECT_ADB
        - AppCrashException → RECOVER with RESTART_APP
        - DeviceOfflineException → TERMINATE

    Returns:
        - RECOVER action with recovery_action for recoverable issues
        - TERMINATE action for unrecoverable issues
    """

    def __init__(self, adb_client=None):
        """Initialize device exception handler.

        Args:
            adb_client: Optional ADB client for recovery operations
        """
        self.adb = adb_client

    def can_handle(self, context: ExceptionContext) -> bool:
        """Check if exception is a DeviceException."""
        return isinstance(context.exception, DeviceException)

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """Handle device exceptions with appropriate recovery."""
        exc = context.exception

        if isinstance(exc, ADBDisconnectedException):
            return ExceptionHandlingResult.recover(
                recovery=RecoveryAction.RECONNECT_ADB,
                new_state="recovering",
                message="Reconnecting ADB",
            )
        elif isinstance(exc, AppCrashException):
            return ExceptionHandlingResult.recover(
                recovery=RecoveryAction.RESTART_APP,
                new_state="recovering",
                message="Restarting app",
            )
        elif isinstance(exc, DeviceOfflineException):
            return ExceptionHandlingResult.terminate(
                message="Device offline, cannot continue"
            )

        # Fallback for unknown device exceptions
        return ExceptionHandlingResult.terminate(
            message=f"Unhandled device exception: {exc.message}"
        )


class UIExceptionHandler(ExceptionHandler):
    """Handler for UI-related exceptions.

    Handles:
        - PopupDetectedException → RECOVER with CLOSE_POPUP
        - PageRedirectException → IGNORE_UI_CHANGE
        - LoadingTimeoutException → RETRY

    Returns:
        - RECOVER action for popup handling
        - IGNORE action for harmless redirects
        - RETRY action for timeout issues
    """

    def __init__(self, adb_client=None):
        """Initialize UI exception handler.

        Args:
            adb_client: Optional ADB client for recovery operations
        """
        self.adb = adb_client

    def can_handle(self, context: ExceptionContext) -> bool:
        """Check if exception is a UIException."""
        return isinstance(context.exception, UIException)

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """Handle UI exceptions based on specific type."""
        exc = context.exception

        if isinstance(exc, PopupDetectedException):
            return ExceptionHandlingResult.recover(
                recovery=RecoveryAction.CLOSE_POPUP,
                new_state="handling_popup",
                message="Closing popup",
            )
        elif isinstance(exc, PageRedirectException):
            return ExceptionHandlingResult.ignore(
                message="Ignoring page redirect"
            )
        elif isinstance(exc, LoadingTimeoutException):
            return ExceptionHandlingResult.retry(
                message="Page loading timeout, retrying",
                retry_count=context.retry_count,
                max_retries=3,
            )

        # Fallback for unknown UI exceptions
        return ExceptionHandlingResult.ignore(
            message=f"Unhandled UI exception: {exc.message}"
        )


class RetryHandler(ExceptionHandler):
    """Handler for retryable exceptions.

    Handles:
        - Exceptions with ERROR severity
        - Only when retry_count < max_retries

    Returns:
        - RETRY action to retry the operation
    """

    def __init__(self, max_retries: int = 3):
        """Initialize retry handler.

        Args:
            max_retries: Maximum number of retry attempts (default 3)
        """
        self.max_retries = max_retries

    def can_handle(self, context: ExceptionContext) -> bool:
        """Check if exception should be retried."""
        # Only retry ERROR severity exceptions
        if isinstance(context.severity, ExceptionSeverity):
            is_error = context.severity == ExceptionSeverity.ERROR
        else:
            is_error = context.severity == "error"

        # Check if retry count is below limit
        has_retries = context.retry_count < self.max_retries

        return is_error and has_retries

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """Return RETRY action with retry count in message."""
        return ExceptionHandlingResult.retry(
            message=f"Retrying operation",
            retry_count=context.retry_count,
            max_retries=self.max_retries,
        )


class BacktrackHandler(ExceptionHandler):
    """Handler for critical exceptions that require backtracking.

    Handles:
        - Exceptions with CRITICAL severity
        - When retry_count >= max_retries

    Returns:
        - BACKTRACK action to return to previous node
    """

    def __init__(self, max_retries: int = 3):
        """Initialize backtrack handler.

        Args:
            max_retries: Retry limit used to determine when to backtrack
        """
        self.max_retries = max_retries

    def can_handle(self, context: ExceptionContext) -> bool:
        """Check if exception should trigger backtrack."""
        # Only backtrack for CRITICAL severity
        if isinstance(context.severity, ExceptionSeverity):
            is_critical = context.severity == ExceptionSeverity.CRITICAL
        else:
            is_critical = context.severity == "critical"

        # Only when retries are exhausted
        exhausted = context.retry_count >= self.max_retries

        return is_critical and exhausted

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """Return BACKTRACK action."""
        return ExceptionHandlingResult.backtrack(
            message=f"Backtracking after {context.retry_count} failed attempts"
        )


# Phase 2: AI-driven handler (placeholder for future implementation)
#
# class AIDrivenExceptionHandler(ExceptionHandler):
#     """Handler that uses AI to analyze screenshots and make decisions.
#
#     This is a Phase 2 feature that will analyze the current screen state
#     and make intelligent decisions about how to recover from exceptions.
#
#     Phase 2 Requirements:
#         - Analyze screenshot with AI
#         - Understand context and visual state
#         - Make decision based on analysis
#         - Support learning from feedback
#
#     Integration Hook:
#         - ExceptionContext.screenshot field (currently commented)
#         - ExceptionHandlingResult.ai_result field (currently commented)
#     """
#
#     def __init__(self, ai_service=None):
#         """Initialize AI-driven handler.
#
#         Args:
#             ai_service: AI service for screenshot analysis
#         """
#         self.ai = ai_service
#
#     def can_handle(self, context: ExceptionContext) -> bool:
#         """Check if AI analysis is available and needed."""
#         # Phase 2: Check if screenshot is available
#         # return context.screenshot is not None and self.ai is not None
#         return False  # Disabled in Phase 1
#
#     def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
#         """Analyze screenshot and make AI-driven decision."""
#         # Phase 2: Implement AI analysis
#         # analysis = self.ai.analyze_exception(context.screenshot, context.exception)
#         # return ExceptionHandlingResult.from_ai_decision(analysis)
#         raise NotImplementedError("AI-driven handler not implemented in Phase 1")
