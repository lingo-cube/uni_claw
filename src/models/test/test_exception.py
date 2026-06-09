"""Tests for exception handling models.

This module tests the models from src/exception/exceptions.py and
src/exception/context.py including:
- ExceptionSeverity enum
- ExceptionAction enum
- RecoveryAction enum
- TraversalException (base class and subclasses)
- ExceptionContext
- ExceptionHandlingResult
"""

import pytest
from datetime import datetime
from src.exception.exceptions import (
    ExceptionSeverity,
    TraversalException,
    ElementNotFoundException,
    PathMismatchException,
    ClickFailedException,
    PopupDetectedException,
)
from src.exception.context import (
    ExceptionAction,
    RecoveryAction,
    ExceptionContext,
    ExceptionHandlingResult,
)


class TestExceptionSeverity:
    """Tests for ExceptionSeverity enum."""

    def test_severity_values(self):
        """Test ExceptionSeverity has correct values."""
        assert ExceptionSeverity.INFO.value == "info"
        assert ExceptionSeverity.WARNING.value == "warning"
        assert ExceptionSeverity.ERROR.value == "error"
        assert ExceptionSeverity.CRITICAL.value == "critical"
        assert ExceptionSeverity.FATAL.value == "fatal"

    def test_severity_values_method(self):
        """Test ExceptionSeverity.values() method."""
        values = ExceptionSeverity.values()
        assert len(values) == 5
        assert "error" in values

    def test_severity_from_value(self):
        """Test ExceptionSeverity.from_value() method."""
        severity = ExceptionSeverity.from_value("error")
        assert severity == ExceptionSeverity.ERROR

    def test_severity_from_value_invalid(self):
        """Test ExceptionSeverity.from_value() with invalid value."""
        with pytest.raises(ValueError, match="Invalid ExceptionSeverity value"):
            ExceptionSeverity.from_value("invalid")

    def test_severity_is_valid(self):
        """Test ExceptionSeverity.is_valid() method."""
        assert ExceptionSeverity.is_valid("critical") is True
        assert ExceptionSeverity.is_valid("invalid") is False


class TestExceptionAction:
    """Tests for ExceptionAction enum."""

    def test_action_values(self):
        """Test ExceptionAction has correct values."""
        assert ExceptionAction.RETRY.value == "retry"
        assert ExceptionAction.SKIP.value == "skip"
        assert ExceptionAction.BACKTRACK.value == "backtrack"
        assert ExceptionAction.RECOVER.value == "recover"
        assert ExceptionAction.TERMINATE.value == "terminate"
        assert ExceptionAction.IGNORE.value == "ignore"

    def test_action_values_method(self):
        """Test ExceptionAction.values() method."""
        values = ExceptionAction.values()
        assert len(values) == 6

    def test_action_from_value(self):
        """Test ExceptionAction.from_value() method."""
        action = ExceptionAction.from_value("retry")
        assert action == ExceptionAction.RETRY


class TestRecoveryAction:
    """Tests for RecoveryAction enum."""

    def test_recovery_values(self):
        """Test RecoveryAction has correct values."""
        assert RecoveryAction.RECONNECT_ADB.value == "reconnect_adb"
        assert RecoveryAction.RESTART_APP.value == "restart_app"
        assert RecoveryAction.CLOSE_POPUP.value == "close_popup"
        assert RecoveryAction.NAVIGATE_BACK.value == "navigate_back"
        assert RecoveryAction.WAIT_AND_RETRY.value == "wait_and_retry"
        assert RecoveryAction.IGNORE_UI_CHANGE.value == "ignore_ui_change"

    def test_recovery_values_method(self):
        """Test RecoveryAction.values() method."""
        values = RecoveryAction.values()
        assert len(values) == 6


class TestTraversalException:
    """Tests for TraversalException base class."""

    def test_basic_exception(self):
        """Test creating basic traversal exception."""
        exc = TraversalException("Test error")
        assert str(exc) == "[ERROR] Test error"
        assert exc.message == "Test error"
        assert exc.severity == ExceptionSeverity.ERROR

    def test_exception_with_severity(self):
        """Test exception with custom severity."""
        exc = TraversalException(
            "Warning message",
            severity=ExceptionSeverity.WARNING,
        )
        assert exc.severity == ExceptionSeverity.WARNING
        assert str(exc) == "[WARNING] Warning message"

    def test_exception_with_cause(self):
        """Test exception with cause."""
        cause = ValueError("Original error")
        exc = TraversalException("Wrapper message", cause=cause)
        assert exc.__cause__ is cause


class TestElementNotFoundException:
    """Tests for ElementNotFoundException."""

    def test_creation(self):
        """Test creating element not found exception."""
        exc = ElementNotFoundException("Settings button")
        assert "Element not found" in str(exc)
        assert exc.severity == ExceptionSeverity.ERROR

    def test_with_context(self):
        """Test with page context."""
        exc = ElementNotFoundException(
            "WiFi toggle",
            context="Settings page",
        )
        assert "WiFi toggle" in str(exc)
        assert "Settings page" in str(exc)


class TestPathMismatchException:
    """Tests for PathMismatchException."""

    def test_creation(self):
        """Test creating path mismatch exception."""
        exc = PathMismatchException(
            expected=["Home", "Settings"],
            actual=["Home", "Settings", "Display"],
        )
        assert "Path mismatch" in str(exc)
        assert exc.severity == ExceptionSeverity.WARNING


class TestClickFailedException:
    """Tests for ClickFailedException."""

    def test_creation(self):
        """Test creating click failed exception."""
        exc = ClickFailedException(target="WiFi", attempts=3)
        assert "Click failed after 3 attempts" in str(exc)
        assert exc.severity == ExceptionSeverity.ERROR


class TestPopupDetectedException:
    """Tests for PopupDetectedException."""

    def test_creation(self):
        """Test creating popup detected exception."""
        exc = PopupDetectedException(popup_info="Confirm dialog")
        assert "Popup detected" in str(exc)
        assert exc.severity == ExceptionSeverity.INFO

    def test_default_severity(self):
        """Test popup has INFO severity by default."""
        exc = PopupDetectedException()
        assert exc.severity == ExceptionSeverity.INFO


class TestExceptionContext:
    """Tests for ExceptionContext model."""

    def test_creation(self):
        """Test creating exception context."""
        exc = TraversalException("Test error")
        context = ExceptionContext(
            exception=exc,
            severity=ExceptionSeverity.ERROR,
            state="current_state",
            node=None,
            operation="click",
            timestamp=datetime.now(),
            retry_count=1,
        )
        assert context.exception == exc
        assert context.operation == "click"
        assert context.retry_count == 1

    def test_to_dict(self):
        """Test serialization to dictionary."""
        from src.models import TraversalState  # Backward compatibility alias

        exc = ElementNotFoundException("Settings")
        state = TraversalState(current_path=["Settings", "Display"])
        context = ExceptionContext(
            exception=exc,
            severity=ExceptionSeverity.ERROR,
            state=state,
            node=None,
            operation="navigate",
            timestamp=datetime.now(),
            retry_count=2,
        )
        data = context.to_dict()
        assert data["exception_type"] == "ElementNotFoundException"
        assert data["severity"] == "error"
        assert data["operation"] == "navigate"
        assert data["retry_count"] == 2


class TestExceptionHandlingResult:
    """Tests for ExceptionHandlingResult model."""

    def test_creation(self):
        """Test creating handling result."""
        result = ExceptionHandlingResult(
            action=ExceptionAction.RETRY,
            message="Retrying operation",
        )
        assert result.action == ExceptionAction.RETRY
        assert result.message == "Retrying operation"

    def test_with_recovery_action(self):
        """Test result with recovery action."""
        result = ExceptionHandlingResult(
            action=ExceptionAction.RECOVER,
            message="Executing recovery",
            recovery_action=RecoveryAction.CLOSE_POPUP,
        )
        assert result.recovery_action == RecoveryAction.CLOSE_POPUP

    def test_to_dict(self):
        """Test serialization to dictionary."""
        result = ExceptionHandlingResult(
            action=ExceptionAction.SKIP,
            message="Skipping",
        )
        data = result.to_dict()
        assert data["action"] == "skip"
        assert data["message"] == "Skipping"

    def test_factory_retry(self):
        """Test retry factory method."""
        result = ExceptionHandlingResult.retry("Retrying", retry_count=1, max_retries=3)
        assert result.action == ExceptionAction.RETRY
        assert "retry 2/3" in result.message

    def test_factory_skip(self):
        """Test skip factory method."""
        result = ExceptionHandlingResult.skip("Skipping this item")
        assert result.action == ExceptionAction.SKIP
        assert result.message == "Skipping this item"

    def test_factory_backtrack(self):
        """Test backtrack factory method."""
        result = ExceptionHandlingResult.backtrack("Going back")
        assert result.action == ExceptionAction.BACKTRACK

    def test_factory_recover(self):
        """Test recover factory method."""
        result = ExceptionHandlingResult.recover(
            recovery=RecoveryAction.NAVIGATE_BACK,
            message="Going back to recover",
        )
        assert result.action == ExceptionAction.RECOVER
        assert result.recovery_action == RecoveryAction.NAVIGATE_BACK

    def test_factory_terminate(self):
        """Test terminate factory method."""
        result = ExceptionHandlingResult.terminate("Cannot continue")
        assert result.action == ExceptionAction.TERMINATE

    def test_factory_ignore(self):
        """Test ignore factory method."""
        result = ExceptionHandlingResult.ignore("Ignoring and continuing")
        assert result.action == ExceptionAction.IGNORE
