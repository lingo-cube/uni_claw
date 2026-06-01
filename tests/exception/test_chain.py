"""Unit tests for ExceptionHandlingChain."""

import pytest
from datetime import datetime
from src.exception.chain import ExceptionHandlingChain
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
)
from src.exception.handlers import (
    BacktrackHandler,
    DeviceExceptionHandler,
    FatalExceptionHandler,
    RetryHandler,
    UIExceptionHandler,
)
from src.state.content_tree import TraversalState


class TestExceptionHandlingChain:
    """Tests for ExceptionHandlingChain."""

    def test_create_default_chain(self):
        """Test creating default chain with all handlers."""
        chain = ExceptionHandlingChain.create_default()

        assert len(chain.handlers) == 5
        assert isinstance(chain.handlers[0], FatalExceptionHandler)
        assert isinstance(chain.handlers[1], DeviceExceptionHandler)
        assert isinstance(chain.handlers[2], UIExceptionHandler)
        assert isinstance(chain.handlers[3], RetryHandler)
        assert isinstance(chain.handlers[4], BacktrackHandler)

    def test_add_handler_without_priority(self):
        """Test adding handler without priority (appends to end)."""
        chain = ExceptionHandlingChain()
        initial_count = len(chain.handlers)

        handler = RetryHandler()
        chain.add_handler(handler)

        assert len(chain.handlers) == initial_count + 1
        assert chain.handlers[-1] is handler

    def test_add_handler_with_priority(self):
        """Test adding handler with specific priority."""
        chain = ExceptionHandlingChain.create_default()
        initial_count = len(chain.handlers)

        handler = RetryHandler()
        chain.add_handler(handler, priority=2)

        assert len(chain.handlers) == initial_count + 1
        assert chain.handlers[2] is handler

    def test_set_handlers(self):
        """Test replacing all handlers."""
        chain = ExceptionHandlingChain()
        new_handlers = [FatalExceptionHandler(), RetryHandler()]
        chain.set_handlers(new_handlers)

        assert chain.handlers == new_handlers
        assert len(chain.handlers) == 2

    def test_handle_fatal_exception(self):
        """Test handling FATAL exception returns TERMINATE."""
        chain = ExceptionHandlingChain.create_default()
        context = _create_context(DeviceOfflineException())

        result = chain.handle(context)

        assert result.action == ExceptionAction.TERMINATE

    def test_handle_device_exception(self):
        """Test handling device exception returns RECOVER."""
        chain = ExceptionHandlingChain.create_default()
        context = _create_context(ADBDisconnectedException())

        result = chain.handle(context)

        assert result.action == ExceptionAction.RECOVER
        assert result.recovery_action == RecoveryAction.RECONNECT_ADB

    def test_handle_ui_exception(self):
        """Test handling UI exception returns appropriate action."""
        chain = ExceptionHandlingChain.create_default()
        context = _create_context(PopupDetectedException("Ad"))

        result = chain.handle(context)

        assert result.action == ExceptionAction.RECOVER
        assert result.recovery_action == RecoveryAction.CLOSE_POPUP

    def test_handle_retryable_exception(self):
        """Test handling retryable exception returns RETRY."""
        chain = ExceptionHandlingChain.create_default()
        context = _create_context(ElementNotFoundException("Button"), retry_count=1)

        result = chain.handle(context)

        assert result.action == ExceptionAction.RETRY

    def test_first_match_wins(self):
        """Test that first matching handler wins (no subsequent handlers called)."""
        chain = ExceptionHandlingChain.create_default()
        # DeviceOfflineException is FATAL, should be handled by FatalExceptionHandler (first)
        context = _create_context(DeviceOfflineException())

        result = chain.handle(context)

        # FatalExceptionHandler should handle it, not DeviceExceptionHandler
        assert result.action == ExceptionAction.TERMINATE
        assert result.recovery_action is None  # No recovery action for TERMINATE

    def test_no_handler_match_returns_ignore(self):
        """Test that unmatched exceptions return IGNORE."""
        chain = ExceptionHandlingChain()
        # Empty chain - no handlers
        context = _create_context(ElementNotFoundException("Button"))

        result = chain.handle(context)

        assert result.action == ExceptionAction.IGNORE

    def test_handler_priority_order(self):
        """Test handlers are tried in priority order."""
        call_order = []

        class TrackingHandler:
            """Handler that tracks when it's called."""

            def __init__(self, name):
                self.name = name
                self.can_handle_called = False
                self.handle_called = False

            def can_handle(self, context):
                self.can_handle_called = True
                call_order.append(self.name)
                return False  # Never match

            def handle(self, context):
                self.handle_called = True
                return ExceptionHandlingResult.ignore()

        chain = ExceptionHandlingChain()
        h1 = TrackingHandler("Handler1")
        h2 = TrackingHandler("Handler2")
        h3 = TrackingHandler("Handler3")

        chain.add_handler(h1, priority=0)
        chain.add_handler(h2, priority=1)
        chain.add_handler(h3, priority=2)

        context = _create_context(ElementNotFoundException("Button"))
        chain.handle(context)

        # All handlers should have been tried in order
        assert call_order == ["Handler1", "Handler2", "Handler3"]
        assert all(h.can_handle_called for h in [h1, h2, h3])
        assert not any(h.handle_called for h in [h1, h2, h3])


# Helper functions


def _create_context(exception, retry_count: int = 0) -> ExceptionContext:
    """Create a test ExceptionContext."""
    return ExceptionContext(
        exception=exception,
        severity=exception.severity,
        state=TraversalState(),
        node=None,
        operation="test_operation",
        timestamp=datetime.now(),
        retry_count=retry_count,
    )
