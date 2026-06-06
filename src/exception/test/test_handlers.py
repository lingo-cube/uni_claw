"""Unit tests for exception handlers."""

import pytest
from datetime import datetime
from src.exception.context import (
    ExceptionAction,
    ExceptionContext,
    ExceptionHandlingResult,
    RecoveryAction,
)
from src.exception.exceptions import (
    ADBDisconnectedException,
    AppCrashException,
    DeviceOfflineException,
    ElementNotFoundException,
    ExceptionSeverity,
    PopupDetectedException,
    TraversalException,
)
from src.exception.handlers import (
    BacktrackHandler,
    DeviceExceptionHandler,
    FatalExceptionHandler,
    RetryHandler,
    UIExceptionHandler,
)
from src.state.content_tree import TraversalState


class TestFatalExceptionHandler:
    """Tests for FatalExceptionHandler."""

    def test_can_handle_fatal_severity(self):
        """Test handler matches FATAL severity."""
        handler = FatalExceptionHandler()
        context = _create_context_with_severity(ExceptionSeverity.FATAL)
        assert handler.can_handle(context) is True

    def test_cannot_handle_non_fatal_severity(self):
        """Test handler rejects non-FATAL severity."""
        handler = FatalExceptionHandler()
        for severity in [ExceptionSeverity.INFO, ExceptionSeverity.WARNING,
                         ExceptionSeverity.ERROR, ExceptionSeverity.CRITICAL]:
            context = _create_context_with_severity(severity)
            assert handler.can_handle(context) is False

    def test_handle_returns_terminate(self):
        """Test handler returns TERMINATE action."""
        handler = FatalExceptionHandler()
        context = _create_context_with_severity(ExceptionSeverity.FATAL)
        result = handler.handle(context)

        assert result.action == ExceptionAction.TERMINATE
        assert "Fatal" in result.message


class TestDeviceExceptionHandler:
    """Tests for DeviceExceptionHandler."""

    def test_can_handle_device_exceptions(self):
        """Test handler matches all DeviceException subclasses."""
        handler = DeviceExceptionHandler()

        for exc_class, kwargs in [(ADBDisconnectedException, {}), (AppCrashException, {"app": "TestApp"}), (DeviceOfflineException, {})]:
            exc = exc_class(**kwargs)
            context = _create_context_with_exception(exc)
            assert handler.can_handle(context) is True

    def test_cannot_handle_non_device_exceptions(self):
        """Test handler rejects non-DeviceException types."""
        handler = DeviceExceptionHandler()
        exc = ElementNotFoundException("Button1")
        context = _create_context_with_exception(exc)
        assert handler.can_handle(context) is False

    def test_handle_adb_disconnected(self):
        """Test handling ADBDisconnectedException."""
        handler = DeviceExceptionHandler()
        exc = ADBDisconnectedException()
        context = _create_context_with_exception(exc)
        result = handler.handle(context)

        assert result.action == ExceptionAction.RECOVER
        assert result.recovery_action == RecoveryAction.RECONNECT_ADB
        assert result.new_state == "recovering"

    def test_handle_app_crash(self):
        """Test handling AppCrashException."""
        handler = DeviceExceptionHandler()
        exc = AppCrashException("com.example.app")
        context = _create_context_with_exception(exc)
        result = handler.handle(context)

        assert result.action == ExceptionAction.RECOVER
        assert result.recovery_action == RecoveryAction.RESTART_APP

    def test_handle_device_offline(self):
        """Test handling DeviceOfflineException."""
        handler = DeviceExceptionHandler()
        exc = DeviceOfflineException()
        context = _create_context_with_exception(exc)
        result = handler.handle(context)

        assert result.action == ExceptionAction.TERMINATE


class TestUIExceptionHandler:
    """Tests for UIExceptionHandler."""

    def test_can_handle_ui_exceptions(self):
        """Test handler matches UIException subclasses."""
        handler = UIExceptionHandler()
        exc = PopupDetectedException("AdPopup")
        context = _create_context_with_exception(exc)
        assert handler.can_handle(context) is True

    def test_handle_popup_detected(self):
        """Test handling PopupDetectedException."""
        handler = UIExceptionHandler()
        exc = PopupDetectedException("AdPopup")
        context = _create_context_with_exception(exc)
        result = handler.handle(context)

        assert result.action == ExceptionAction.RECOVER
        assert result.recovery_action == RecoveryAction.CLOSE_POPUP
        assert result.new_state == "handling_popup"


class TestRetryHandler:
    """Tests for RetryHandler."""

    def test_can_handle_error_with_retries_remaining(self):
        """Test handler matches ERROR severity with retries available."""
        handler = RetryHandler(max_retries=3)
        context = _create_context_with_severity(ExceptionSeverity.ERROR, retry_count=1)
        assert handler.can_handle(context) is True

    def test_cannot_handle_when_retries_exhausted(self):
        """Test handler rejects when retry_count >= max_retries."""
        handler = RetryHandler(max_retries=3)
        context = _create_context_with_severity(ExceptionSeverity.ERROR, retry_count=3)
        assert handler.can_handle(context) is False

    def test_cannot_handle_non_error_severity(self):
        """Test handler rejects non-ERROR severity."""
        handler = RetryHandler()
        for severity in [ExceptionSeverity.INFO, ExceptionSeverity.WARNING,
                         ExceptionSeverity.CRITICAL, ExceptionSeverity.FATAL]:
            context = _create_context_with_severity(severity, retry_count=0)
            assert handler.can_handle(context) is False

    def test_handle_returns_retry(self):
        """Test handler returns RETRY action."""
        handler = RetryHandler(max_retries=3)
        context = _create_context_with_severity(ExceptionSeverity.ERROR, retry_count=1)
        result = handler.handle(context)

        assert result.action == ExceptionAction.RETRY
        assert "retry" in result.message.lower()


class TestBacktrackHandler:
    """Tests for BacktrackHandler."""

    def test_can_handle_critical_with_retries_exhausted(self):
        """Test handler matches CRITICAL severity when retries exhausted."""
        handler = BacktrackHandler(max_retries=3)
        context = _create_context_with_severity(ExceptionSeverity.CRITICAL, retry_count=3)
        assert handler.can_handle(context) is True

    def test_cannot_handle_when_retries_remaining(self):
        """Test handler rejects when retries still available."""
        handler = BacktrackHandler(max_retries=3)
        context = _create_context_with_severity(ExceptionSeverity.CRITICAL, retry_count=2)
        assert handler.can_handle(context) is False

    def test_cannot_handle_non_critical_severity(self):
        """Test handler rejects non-CRITICAL severity."""
        handler = BacktrackHandler()
        for severity in [ExceptionSeverity.INFO, ExceptionSeverity.WARNING,
                         ExceptionSeverity.ERROR, ExceptionSeverity.FATAL]:
            context = _create_context_with_severity(severity, retry_count=10)
            assert handler.can_handle(context) is False

    def test_handle_returns_backtrack(self):
        """Test handler returns BACKTRACK action."""
        handler = BacktrackHandler()
        context = _create_context_with_severity(ExceptionSeverity.CRITICAL, retry_count=3)
        result = handler.handle(context)

        assert result.action == ExceptionAction.BACKTRACK
        assert "backtrack" in result.message.lower()


# Helper functions


def _create_context_with_severity(severity: ExceptionSeverity, retry_count: int = 0) -> ExceptionContext:
    """Create a test ExceptionContext with given severity."""
    exc = TraversalException("Test error")
    exc._severity = severity

    return ExceptionContext(
        exception=exc,
        severity=severity,
        state=TraversalState(),
        node=None,
        operation="test_operation",
        timestamp=datetime.now(),
        retry_count=retry_count,
    )


def _create_context_with_exception(exception: TraversalException) -> ExceptionContext:
    """Create a test ExceptionContext with given exception."""
    return ExceptionContext(
        exception=exception,
        severity=exception.severity,
        state=TraversalState(),
        node=None,
        operation="test_operation",
        timestamp=datetime.now(),
        retry_count=0,
    )
