"""Unit tests for ExceptionContext and ExceptionHandlingResult."""

import json
import pytest
from datetime import datetime
from src.exception.context import (
    ExceptionAction,
    ExceptionContext,
    ExceptionHandlingResult,
    RecoveryAction,
)
from src.exception.exceptions import (
    ElementNotFoundException,
    ExceptionSeverity,
    TraversalException,
)
from src.state.content_tree import TraversalState


class TestExceptionContext:
    """Tests for ExceptionContext dataclass."""

    def test_context_creation(self):
        """Test creating ExceptionContext with all fields."""
        exc = ElementNotFoundException("Button1")
        state = TraversalState()

        context = ExceptionContext(
            exception=exc,
            severity=ExceptionSeverity.ERROR,
            state=state,
            node=None,
            operation="tap_and_wait",
            timestamp=datetime.now(),
            retry_count=1,
        )

        assert context.exception is exc
        assert context.severity == ExceptionSeverity.ERROR
        assert context.state is state
        assert context.node is None
        assert context.operation == "tap_and_wait"
        assert context.retry_count == 1

    def test_context_to_dict(self):
        """Test converting context to dictionary."""
        exc = ElementNotFoundException("Button1", "LoginPage")
        state = TraversalState(current_path=["Home", "Login"])

        context = ExceptionContext(
            exception=exc,
            severity=ExceptionSeverity.ERROR,
            state=state,
            node=None,
            operation="tap_button",
            timestamp=datetime(2024, 1, 1, 12, 0, 0),
            retry_count=0,
        )

        result = context.to_dict()

        assert result["exception_type"] == "ElementNotFoundException"
        assert result["severity"] == "error"
        assert result["operation"] == "tap_button"
        assert result["retry_count"] == 0
        assert result["current_path"] == ["Home", "Login"]


class TestExceptionHandlingResult:
    """Tests for ExceptionHandlingResult dataclass."""

    def test_result_creation(self):
        """Test creating ExceptionHandlingResult."""
        result = ExceptionHandlingResult(
            action=ExceptionAction.RETRY,
            message="Retrying operation",
            new_state="recovering",
            recovery_action=RecoveryAction.WAIT_AND_RETRY,
        )

        assert result.action == ExceptionAction.RETRY
        assert result.message == "Retrying operation"
        assert result.new_state == "recovering"
        assert result.recovery_action == RecoveryAction.WAIT_AND_RETRY

    def test_result_to_dict(self):
        """Test converting result to dictionary."""
        result = ExceptionHandlingResult(
            action=ExceptionAction.RECOVER,
            message="Recovering",
            new_state="handling_popup",
            recovery_action=RecoveryAction.CLOSE_POPUP,
        )

        data = result.to_dict()

        assert data["action"] == "recover"
        assert data["message"] == "Recovering"
        assert data["new_state"] == "handling_popup"
        assert data["recovery_action"] == "close_popup"

    def test_factory_method_retry(self):
        """Test RETRY factory method."""
        result = ExceptionHandlingResult.retry("Retrying", retry_count=1, max_retries=3)
        assert result.action == ExceptionAction.RETRY
        assert "retry 2/3" in result.message

    def test_factory_method_skip(self):
        """Test SKIP factory method."""
        result = ExceptionHandlingResult.skip("Skipping element")
        assert result.action == ExceptionAction.SKIP
        assert result.message == "Skipping element"

    def test_factory_method_backtrack(self):
        """Test BACKTRACK factory method."""
        result = ExceptionHandlingResult.backtrack("Backtracking")
        assert result.action == ExceptionAction.BACKTRACK

    def test_factory_method_recover(self):
        """Test RECOVER factory method."""
        result = ExceptionHandlingResult.recover(
            recovery=RecoveryAction.RECONNECT_ADB,
            new_state="recovering",
        )
        assert result.action == ExceptionAction.RECOVER
        assert result.recovery_action == RecoveryAction.RECONNECT_ADB
        assert result.new_state == "recovering"

    def test_factory_method_terminate(self):
        """Test TERMINATE factory method."""
        result = ExceptionHandlingResult.terminate("Fatal error")
        assert result.action == ExceptionAction.TERMINATE
        assert result.message == "Fatal error"

    def test_factory_method_ignore(self):
        """Test IGNORE factory method."""
        result = ExceptionHandlingResult.ignore("Ignoring")
        assert result.action == ExceptionAction.IGNORE


class TestExceptionAction:
    """Tests for ExceptionAction enum."""

    def test_action_values(self):
        """Test all action values are present."""
        assert ExceptionAction.RETRY.value == "retry"
        assert ExceptionAction.SKIP.value == "skip"
        assert ExceptionAction.BACKTRACK.value == "backtrack"
        assert ExceptionAction.RECOVER.value == "recover"
        assert ExceptionAction.TERMINATE.value == "terminate"
        assert ExceptionAction.IGNORE.value == "ignore"


class TestRecoveryAction:
    """Tests for RecoveryAction enum."""

    def test_recovery_values(self):
        """Test all recovery action values are present."""
        assert RecoveryAction.RECONNECT_ADB.value == "reconnect_adb"
        assert RecoveryAction.RESTART_APP.value == "restart_app"
        assert RecoveryAction.CLOSE_POPUP.value == "close_popup"
        assert RecoveryAction.NAVIGATE_BACK.value == "navigate_back"
        assert RecoveryAction.WAIT_AND_RETRY.value == "wait_and_retry"
        assert RecoveryAction.IGNORE_UI_CHANGE.value == "ignore_ui_change"


class TestJSONSerialization:
    """Tests for JSON serialization and deserialization (Task 2.6)."""

    def test_context_json_serialization(self):
        """Test ExceptionContext can be serialized to JSON."""
        exc = ElementNotFoundException("Button1", "LoginPage")
        state = TraversalState(current_path=["Home", "Login"])

        context = ExceptionContext(
            exception=exc,
            severity=ExceptionSeverity.ERROR,
            state=state,
            node=None,
            operation="tap_button",
            timestamp=datetime(2024, 1, 1, 12, 0, 0),
            retry_count=0,
        )

        # Convert to dict and then to JSON
        context_dict = context.to_dict()
        json_str = json.dumps(context_dict)

        # Verify JSON string is valid
        assert json_str is not None
        assert "exception_type" in json_str
        assert "ElementNotFoundException" in json_str

    def test_context_json_deserialization(self):
        """Test ExceptionContext can be deserialized from JSON."""
        context_dict = {
            "exception_type": "ElementNotFoundException",
            "severity": "error",
            "operation": "tap_button",
            "retry_count": 1,
            "timestamp": "2024-01-01T12:00:00",
            "current_path": ["Home", "Login"],
        }

        json_str = json.dumps(context_dict)
        parsed = json.loads(json_str)

        assert parsed["exception_type"] == "ElementNotFoundException"
        assert parsed["severity"] == "error"
        assert parsed["operation"] == "tap_button"
        assert parsed["retry_count"] == 1
        assert parsed["current_path"] == ["Home", "Login"]

    def test_result_json_serialization(self):
        """Test ExceptionHandlingResult can be serialized to JSON."""
        result = ExceptionHandlingResult(
            action=ExceptionAction.RECOVER,
            message="Recovering from error",
            new_state="handling_popup",
            recovery_action=RecoveryAction.CLOSE_POPUP,
        )

        # Convert to dict and then to JSON
        result_dict = result.to_dict()
        json_str = json.dumps(result_dict)

        # Verify JSON string is valid
        assert json_str is not None
        assert "recover" in json_str
        assert "close_popup" in json_str

    def test_result_json_deserialization(self):
        """Test ExceptionHandlingResult can be deserialized from JSON."""
        result_dict = {
            "action": "recover",
            "message": "Recovering",
            "new_state": "handling_popup",
            "recovery_action": "close_popup",
        }

        json_str = json.dumps(result_dict)
        parsed = json.loads(json_str)

        assert parsed["action"] == "recover"
        assert parsed["message"] == "Recovering"
        assert parsed["new_state"] == "handling_popup"
        assert parsed["recovery_action"] == "close_popup"

    def test_multiple_contexts_serialization(self):
        """Test serializing multiple exception contexts to JSON."""
        contexts = []
        for i in range(3):
            exc = ElementNotFoundException(f"Button{i}")
            state = TraversalState(current_path=["Home", f"Tab{i}"])

            context = ExceptionContext(
                exception=exc,
                severity=ExceptionSeverity.WARNING,
                state=state,
                node=None,
                operation="tap",
                timestamp=datetime(2024, 1, 1, 12, i, 0),
                retry_count=i,
            )
            contexts.append(context.to_dict())

        # Convert to JSON
        json_str = json.dumps(contexts)

        # Verify and parse back
        parsed = json.loads(json_str)
        assert len(parsed) == 3
        assert parsed[0]["retry_count"] == 0
        assert parsed[2]["retry_count"] == 2
