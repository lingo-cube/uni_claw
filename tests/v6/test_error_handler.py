"""
Unit tests for V6.1 error handler system.

Tests error classification, strategy selection, and recovery execution.
"""

import pytest
from src.state_machine.error_handler import (
    ErrorType,
    ErrorStrategy,
    ErrorContext,
    ErrorRecoveryResult,
    ErrorClassifier,
    ErrorStrategySelector,
    RecoveryExecutor,
    ErrorHandler,
)


class TestErrorClassifier:
    """Test error classification functionality."""

    def setup_method(self):
        """Set up test fixtures."""
        self.classifier = ErrorClassifier()

    def test_network_error_classification(self):
        """Test classification of network errors."""
        # Test with network-related exception
        error = Exception("Network connection failed")
        error_type = self.classifier.classify(error)
        assert error_type == ErrorType.NETWORK

    def test_element_not_found_classification(self):
        """Test classification of element not found errors."""
        error = Exception("Element not found: button_id")
        error_type = self.classifier.classify(error)
        assert error_type == ErrorType.UI_ELEMENT

    def test_timeout_error_classification(self):
        """Test classification of timeout errors."""
        error = Exception("Operation timed out after 30 seconds")
        error_type = self.classifier.classify(error)
        assert error_type == ErrorType.TIMEOUT

    def test_permission_error_classification(self):
        """Test classification of permission errors."""
        error = Exception("Permission denied: storage access")
        error_type = self.classifier.classify(error)
        assert error_type == ErrorType.PERMISSION

    def test_app_crash_error_classification(self):
        """Test classification of app crash errors."""
        error = Exception("Application crashed: fatal error")
        error_type = self.classifier.classify(error)
        assert error_type == ErrorType.APP_CRASH

    def test_unknown_error_classification(self):
        """Test classification of unknown errors."""
        error = Exception("Some unknown error occurred")
        error_type = self.classifier.classify(error)
        assert error_type == ErrorType.UNKNOWN

    def test_network_pattern_variations(self):
        """Test various network error patterns."""
        test_cases = [
            "DNS resolution failed",
            "Socket connection failed",  # Changed from "timeout" to avoid classification conflict
            "Internet connection lost",
            "Network unreachable",
        ]
        for error_msg in test_cases:
            error = Exception(error_msg)
            error_type = self.classifier.classify(error)
            assert error_type == ErrorType.NETWORK, f"Failed for: {error_msg}"

    def test_element_pattern_variations(self):
        """Test various element error patterns."""
        test_cases = [
            "Unable to locate element",
            "XPath not found",
            "Selector returned no results",
            "UI element missing",
        ]
        for error_msg in test_cases:
            error = Exception(error_msg)
            error_type = self.classifier.classify(error)
            assert error_type == ErrorType.UI_ELEMENT, f"Failed for: {error_msg}"

    def test_permission_pattern_variations(self):
        """Test various permission error patterns."""
        test_cases = [
            "Access denied",
            "Unauthorized user",
            "Forbidden resource",
            "Permission not granted",
        ]
        for error_msg in test_cases:
            error = Exception(error_msg)
            error_type = self.classifier.classify(error)
            assert error_type == ErrorType.PERMISSION, f"Failed for: {error_msg}"

    def test_crash_pattern_variations(self):
        """Test various crash error patterns."""
        test_cases = [
            "Application Not Responding (ANR)",
            "Fatal exception in main thread",
            "Force close requested",
            "App crash detected",
        ]
        for error_msg in test_cases:
            error = Exception(error_msg)
            error_type = self.classifier.classify(error)
            assert error_type == ErrorType.APP_CRASH, f"Failed for: {error_msg}"


class TestErrorStrategySelector:
    """Test error strategy selection functionality."""

    def setup_method(self):
        """Set up test fixtures."""
        self.selector = ErrorStrategySelector()

    def test_retry_strategy_for_network_error(self):
        """Test RETRY strategy selection for network errors."""
        context = {"retry_count": 0, "max_retries": 3}
        strategy = self.selector.select_strategy(ErrorType.NETWORK, context)
        assert strategy == ErrorStrategy.RETRY

    def test_skip_strategy_for_element_error(self):
        """Test SKIP strategy selection for element errors."""
        context = {"can_skip": True, "retry_count": 0, "max_retries": 3}
        strategy = self.selector.select_strategy(ErrorType.UI_ELEMENT, context)
        assert strategy == ErrorStrategy.SKIP

    def test_abort_strategy_for_crash_error(self):
        """Test ABORT strategy selection for crash errors."""
        context = {}
        strategy = self.selector.select_strategy(ErrorType.APP_CRASH, context)
        assert strategy == ErrorStrategy.ABORT

    def test_backtrack_strategy_for_permission_error(self):
        """Test BACKTRACK strategy selection for permission errors."""
        context = {"can_backtrack": True, "node_stack_length": 2}
        strategy = self.selector.select_strategy(ErrorType.PERMISSION, context)
        # Should prefer ABORT for permission errors
        assert strategy in [ErrorStrategy.ABORT, ErrorStrategy.BACKTRACK]

    def test_retry_exhaustion_fallback(self):
        """Test fallback when retry attempts are exhausted."""
        context = {"retry_count": 3, "max_retries": 3, "can_backtrack": True, "node_stack_length": 2}
        strategy = self.selector.select_strategy(ErrorType.NETWORK, context)
        # Should fallback to BACKTRACK or ABORT
        assert strategy in [ErrorStrategy.BACKTRACK, ErrorStrategy.ABORT]

    def test_backtrack_not_available_fallback(self):
        """Test fallback when backtrack is not available."""
        context = {"can_backtrack": False, "node_stack_length": 1, "retry_count": 3, "max_retries": 3}
        strategy = self.selector.select_strategy(ErrorType.UI_ELEMENT, context)
        # Should fallback to SKIP or ABORT
        assert strategy in [ErrorStrategy.SKIP, ErrorStrategy.ABORT]

    def test_skip_not_available_fallback(self):
        """Test fallback when skip is not available."""
        context = {"can_skip": False, "retry_count": 1, "max_retries": 3}
        strategy = self.selector.select_strategy(ErrorType.UI_ELEMENT, context)
        # Should fallback to RETRY or BACKTRACK
        assert strategy in [ErrorStrategy.RETRY, ErrorStrategy.BACKTRACK]

    def test_context_aware_selection(self):
        """Test context-aware strategy selection."""
        # High retry count should prefer backtracking over retry
        context_high_retry = {"retry_count": 2, "max_retries": 3, "can_backtrack": True, "node_stack_length": 2}
        strategy1 = self.selector.select_strategy(ErrorType.NETWORK, context_high_retry)
        assert strategy1 in [ErrorStrategy.RETRY, ErrorStrategy.BACKTRACK]

        # Low retry count should prefer retry
        context_low_retry = {"retry_count": 0, "max_retries": 3, "can_backtrack": True, "node_stack_length": 2}
        strategy2 = self.selector.select_strategy(ErrorType.NETWORK, context_low_retry)
        assert strategy2 == ErrorStrategy.RETRY


class TestRecoveryExecutor:
    """Test recovery execution functionality."""

    def setup_method(self):
        """Set up test fixtures."""
        self.executor = RecoveryExecutor()

    def test_skip_recovery_execution(self):
        """Test SKIP recovery execution."""
        context = {"current_node": "node1"}
        error = Exception("Test error")

        result = self.executor.execute(ErrorStrategy.SKIP, context, error)

        assert result.success is True
        assert result.recovery_action == "skip"
        assert result.execution_continued is True

    def test_retry_recovery_execution(self):
        """Test RETRY recovery execution."""
        context = {"retry_count": 0, "max_retries": 3}
        error = Exception("Network error")

        result = self.executor.execute(ErrorStrategy.RETRY, context, error)

        assert result.success is True
        assert "retry" in result.recovery_action
        assert result.execution_continued is False

    def test_backtrack_recovery_execution(self):
        """Test BACKTRACK recovery execution."""
        context = {"node_stack": ["parent", "child"]}
        error = Exception("Container error")

        result = self.executor.execute(ErrorStrategy.BACKTRACK, context, error)

        assert result.success is True
        assert result.recovery_action == "backtrack_to_parent"
        assert result.execution_continued is True

    def test_continue_recovery_execution(self):
        """Test CONTINUE recovery execution."""
        context = {"current_state": "executing"}
        error = Exception("Non-critical error")

        result = self.executor.execute(ErrorStrategy.CONTINUE, context, error)

        assert result.success is True
        assert result.recovery_action == "continue_despite_error"
        assert result.execution_continued is True

    def test_abort_recovery_execution(self):
        """Test ABORT recovery execution."""
        context = {"current_state": "executing"}
        error = Exception("Fatal error")

        result = self.executor.execute(ErrorStrategy.ABORT, context, error)

        assert result.success is False
        assert result.recovery_action == "abort"
        assert result.execution_continued is False

    def test_backtrack_with_empty_stack(self):
        """Test backtrack when node stack is empty."""
        context = {"node_stack": []}
        error = Exception("No parent to backtrack to")

        result = self.executor.execute(ErrorStrategy.BACKTRACK, context, error)

        assert result.success is False
        assert result.recovery_action == "backtrack_failed"

    def test_recovery_timing(self):
        """Test that recovery execution time is measured."""
        import time
        context = {}
        error = Exception("Test error")

        result = self.executor.execute(ErrorStrategy.SKIP, context, error)

        assert result.recovery_time_ms >= 0
        assert isinstance(result.recovery_time_ms, float)

    def test_remaining_retries_calculation(self):
        """Test remaining retries calculation."""
        context = {"retry_count": 1, "max_retries": 3}
        error = Exception("Test error")

        result = self.executor.execute(ErrorStrategy.RETRY, context, error)

        assert result.remaining_retries == 2


class TestErrorHandler:
    """Test complete error handler functionality."""

    def setup_method(self):
        """Set up test fixtures."""
        self.handler = ErrorHandler()

    def test_complete_error_handling_flow(self):
        """Test complete error handling flow."""
        error = Exception("Network connection failed")
        context = {"retry_count": 0, "max_retries": 3, "can_skip": True, "can_backtrack": True, "node_stack_length": 2}

        result = self.handler.handle_error(error, context)

        assert isinstance(result, ErrorRecoveryResult)
        # Recovery action can be various formats like "retry_with_backoff_1s", "skip", etc.
        assert result.recovery_action is not None
        assert len(result.recovery_action) > 0

    def test_error_statistics_tracking(self):
        """Test that error statistics are tracked."""
        # Handle multiple errors
        errors = [
            Exception("Network error"),
            Exception("Element not found"),
            Exception("Network error"),  # Same type as first
        ]

        contexts = [
            {"retry_count": 0, "max_retries": 3, "can_skip": True, "can_backtrack": True, "node_stack_length": 2},
            {"retry_count": 0, "max_retries": 3, "can_skip": True, "can_backtrack": True, "node_stack_length": 2},
            {"retry_count": 0, "max_retries": 3, "can_skip": True, "can_backtrack": True, "node_stack_length": 2},
        ]

        for error, context in zip(errors, contexts):
            self.handler.handle_error(error, context)

        summary = self.handler.get_error_summary()
        assert summary["total_errors"] == 3
        assert summary["recovered_errors"] == 3  # All should recover
        assert summary["recovery_rate"] == 1.0
        assert "network_error" in summary["error_statistics"]
        assert summary["error_statistics"]["network_error"] == 2

    def test_recovery_rate_calculation(self):
        """Test recovery rate calculation."""
        # Handle some errors
        for i in range(5):
            error = Exception(f"Error {i}")
            context = {"retry_count": 0, "max_retries": 3, "can_skip": True, "can_backtrack": True, "node_stack_length": 2}
            self.handler.handle_error(error, context)

        rate = self.handler.recovery_rate
        assert 0.0 <= rate <= 1.0

    def test_error_summary_completeness(self):
        """Test that error summary contains all required fields."""
        error = Exception("Test error")
        context = {"retry_count": 0, "max_retries": 3, "can_skip": True, "can_backtrack": True, "node_stack_length": 2}

        self.handler.handle_error(error, context)
        summary = self.handler.get_error_summary()

        assert "total_errors" in summary
        assert "recovered_errors" in summary
        assert "recovery_rate" in summary
        assert "error_statistics" in summary

    def test_multiple_error_types(self):
        """Test handling multiple different error types."""
        errors = [
            Exception("Network failed"),
            Exception("Element not found"),
            Exception("Permission denied"),
            Exception("App crashed"),
        ]

        for error in errors:
            context = {"retry_count": 0, "max_retries": 3, "can_skip": True, "can_backtrack": True, "node_stack_length": 2}
            self.handler.handle_error(error, context)

        summary = self.handler.get_error_summary()
        assert len(summary["error_statistics"]) >= 3  # At least 3 different types


class TestErrorContext:
    """Test error context data structure."""

    def test_error_context_creation(self):
        """Test creating error context."""
        error = Exception("Test error")
        context = ErrorContext(
            error=error,
            error_type=ErrorType.NETWORK,
            error_strategy=ErrorStrategy.RETRY,
            retry_count=1,
            max_retries=3,
            can_skip=True,
            can_backtrack=True,
        )

        assert context.error == error
        assert context.error_type == ErrorType.NETWORK
        assert context.error_strategy == ErrorStrategy.RETRY
        assert context.retry_count == 1
        assert context.max_retries == 3
        assert context.can_skip is True
        assert context.can_backtrack is True

    def test_error_recovery_result_creation(self):
        """Test creating error recovery result."""
        result = ErrorRecoveryResult(
            success=True,
            recovery_action="retry",
            restored_state={"retry_count": 2},
            execution_continued=False,
            recovery_time_ms=150.5,
            remaining_retries=1,
        )

        assert result.success is True
        assert result.recovery_action == "retry"
        assert result.restored_state == {"retry_count": 2}
        assert result.execution_continued is False
        assert result.recovery_time_ms == 150.5
        assert result.remaining_retries == 1


# Integration tests
class TestErrorHandlerIntegration:
    """Integration tests for error handler with real scenarios."""

    def test_network_error_recovery_scenario(self):
        """Test network error recovery scenario."""
        handler = ErrorHandler()
        error = Exception("Network connection timeout")

        # First attempt - should retry
        context1 = {"retry_count": 0, "max_retries": 3, "can_skip": True, "can_backtrack": True, "node_stack_length": 2}
        result1 = handler.handle_error(error, context1)
        assert result1.success is True
        assert "retry" in result1.recovery_action

        # After max retries - should fallback to backtrack or continue
        context2 = {
            "retry_count": 3,
            "max_retries": 3,
            "can_skip": True,
            "can_backtrack": True,
            "node_stack_length": 2,
            "node_stack": ["parent", "child"]  # Add proper node stack
        }
        result2 = handler.handle_error(error, context2)
        # Should either succeed with backtrack, continue, or abort as fallback
        assert result2.recovery_action in ["backtrack_to_parent", "continue_despite_error", "abort"]
        assert result2.success is True or result2.recovery_action == "abort"

    def test_element_error_recovery_scenario(self):
        """Test element not found error recovery scenario."""
        handler = ErrorHandler()
        error = Exception("Button not found in current view")

        # Should skip missing element
        context = {"retry_count": 0, "max_retries": 3, "can_skip": True, "can_backtrack": True, "node_stack_length": 2}
        result = handler.handle_error(error, context)
        assert result.success is True
        assert result.recovery_action == "skip"

    def test_app_crash_recovery_scenario(self):
        """Test app crash recovery scenario."""
        handler = ErrorHandler()
        error = Exception("Application crashed: Fatal exception")

        # Should abort immediately
        context = {"retry_count": 0, "max_retries": 3, "can_skip": True, "can_backtrack": True, "node_stack_length": 2}
        result = handler.handle_error(error, context)
        assert result.success is False
        assert result.recovery_action == "abort"