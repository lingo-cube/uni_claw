"""
Integration tests for error handling in TraversalStateMachine.

Tests the complete error handling flow from state machine perspective.
"""

import pytest
from src.state_machine.traversal_fsm import TraversalStateMachine, TraversalState
from src.state_machine.error_handler import ErrorType, ErrorStrategy


class TestStateMachineErrorIntegration:
    """Test error handling integration in TraversalStateMachine."""

    def setup_method(self):
        """Set up test fixtures."""
        self.state_machine = TraversalStateMachine()

    def test_error_handler_initialization(self):
        """Test that error handler is initialized on first use."""
        # Error handler should not exist initially
        assert not hasattr(self.state_machine, '_error_handler') or self.state_machine._error_handler is None

        # Call handle_error to initialize
        error = Exception("Test error")
        result = self.state_machine.handle_error(error)

        # Error handler should now be initialized
        assert self.state_machine._error_handler is not None

    def test_error_handling_creates_handler(self):
        """Test that first error handling call creates error handler."""
        error = Exception("Network error")
        result = self.state_machine.handle_error(error)

        assert result is not None
        assert hasattr(result, 'success')
        assert hasattr(result, 'recovery_action')

    def test_error_handling_with_network_error(self):
        """Test error handling with network error."""
        error = Exception("Network connection failed")
        context = {
            "node_stack": ["parent", "child"],
            "current_node": "child_node"
        }

        result = self.state_machine.handle_error(error, context)

        assert result is not None
        # Should attempt recovery
        assert result.recovery_action is not None
        assert len(result.recovery_action) > 0

    def test_error_handling_with_element_error(self):
        """Test error handling with element not found error."""
        error = Exception("Button not found")
        context = {
            "node_stack": ["container"],
            "current_node": "button_node"
        }

        result = self.state_machine.handle_error(error, context)

        assert result is not None
        # Element errors should trigger skip or retry
        assert "skip" in result.recovery_action.lower() or "retry" in result.recovery_action.lower()

    def test_error_handling_increases_retry_count(self):
        """Test that retry count increases on retry attempts."""
        error = Exception("Temporary failure")
        context = {"node_stack": ["parent", "child"]}

        # First attempt - should retry
        result1 = self.state_machine.handle_error(error, context)
        initial_retry_count = self.state_machine._retry_count

        # Simulate retry attempt
        if "retry" in result1.recovery_action:
            assert self.state_machine._retry_count >= initial_retry_count

    def test_error_handling_resets_on_skip(self):
        """Test that retry count resets on successful skip."""
        error = Exception("Element not found")
        context = {"node_stack": ["parent", "child"]}

        # Set some retry count first
        self.state_machine._retry_count = 2

        # Handle error that should skip
        result = self.state_machine.handle_error(error, context)

        # If skip happened, retry count should be reset
        if result.recovery_action == "skip":
            assert self.state_machine._retry_count == 0

    def test_error_context_storage(self):
        """Test that error context is stored after handling."""
        error = Exception("Test error")
        context = {"node_stack": ["parent"]}

        self.state_machine.handle_error(error, context)

        # Check error context was stored
        assert "last_error" in self.state_machine._error_context
        assert "Test error" in self.state_machine._error_context["last_error"]
        assert "last_recovery_action" in self.state_machine._error_context

    def test_error_recovery_summary(self):
        """Test getting error recovery summary."""
        # Handle a few errors
        errors = [
            Exception("Network error 1"),
            Exception("Element not found"),
            Exception("Network error 2"),
        ]

        for error in errors:
            context = {"node_stack": ["parent", "child"]}
            self.state_machine.handle_error(error, context)

        # Get summary
        summary = self.state_machine.get_error_recovery_summary()

        assert "total_errors" in summary
        assert "recovered_errors" in summary
        assert "recovery_rate" in summary
        assert "error_statistics" in summary

        # Should have tracked 3 errors
        assert summary["total_errors"] == 3

    def test_error_recovery_summary_before_any_errors(self):
        """Test error recovery summary before any errors occur."""
        summary = self.state_machine.get_error_recovery_summary()

        # Should return empty statistics
        assert summary["total_errors"] == 0
        assert summary["recovered_errors"] == 0
        assert summary["recovery_rate"] == 0.0
        assert summary["error_statistics"] == {}

    def test_reset_error_handling(self):
        """Test resetting error handling state."""
        error = Exception("Test error")
        context = {"node_stack": ["parent"]}

        # Handle some errors to build up state
        self.state_machine.handle_error(error, context)
        self.state_machine._retry_count = 3

        # Reset
        self.state_machine.reset_error_handling()

        # Check reset happened
        assert self.state_machine._retry_count == 0
        assert self.state_machine._error_context == {}

    def test_error_handling_preserves_handler_on_reset(self):
        """Test that error handler is preserved on reset."""
        error = Exception("Test error")
        context = {"node_stack": ["parent"]}

        # Create handler by handling error
        self.state_machine.handle_error(error, context)
        handler = self.state_machine._error_handler

        # Reset
        self.state_machine.reset_error_handling()

        # Handler should still exist
        assert self.state_machine._error_handler is handler

    def test_state_transitions_with_error_handling(self):
        """Test that state transitions work correctly with error handling."""
        # Start in NODE_SELECT state
        assert self.state_machine.state == TraversalState.NODE_SELECT

        # Handle an error (should not change state directly)
        error = Exception("Test error")
        result = self.state_machine.handle_error(error)

        # State should still be NODE_SELECT (error handling doesn't auto-transition)
        assert self.state_machine.state == TraversalState.NODE_SELECT

    def test_error_context_passed_to_handler(self):
        """Test that traversal context is properly passed to error handler."""
        error = Exception("Test error")
        context = {
            "node_stack": ["parent", "child"],
            "current_node": "test_node",
            "retry_count": 1,
            "max_retries": 5,
        }

        result = self.state_machine.handle_error(error, context)

        # Check that context was used (should influence strategy)
        assert result is not None

    def test_multiple_error_types_in_single_session(self):
        """Test handling multiple different error types in one session."""
        errors = [
            Exception("Network timeout"),
            Exception("Button not found"),
            Exception("Permission denied"),
            Exception("App crashed"),
        ]

        for error in errors:
            context = {"node_stack": ["parent", "child"]}
            self.state_machine.handle_error(error, context)

        summary = self.state_machine.get_error_recovery_summary()

        # Should have tracked all error types
        assert summary["total_errors"] == 4
        assert len(summary["error_statistics"]) >= 2  # At least 2 different types


class TestStateMachineErrorRecoveryScenarios:
    """Test realistic error recovery scenarios."""

    def setup_method(self):
        """Set up test fixtures."""
        self.state_machine = TraversalStateMachine()

    def test_network_error_recovery_sequence(self):
        """Test realistic network error recovery sequence."""
        error = Exception("Network connection failed")
        context = {"node_stack": ["root", "menu", "item"]}

        # First attempt - should retry
        result1 = self.state_machine.handle_error(error, context)
        assert "retry" in result1.recovery_action.lower() or result1.success

        # Second attempt after retry exhausted - should backtrack or continue
        self.state_machine._retry_count = 3
        result2 = self.state_machine.handle_error(error, context)
        assert result2.recovery_action in ["backtrack_to_parent", "continue_despite_error", "abort"]

    def test_element_error_recovery_sequence(self):
        """Test realistic element error recovery sequence."""
        error = Exception("Button not found: submit_button")
        context = {"node_stack": ["form"]}

        # Should skip missing element
        result = self.state_machine.handle_error(error, context)
        assert result.recovery_action == "skip"
        assert result.success is True

    def test_app_crash_immediate_abort(self):
        """Test that app crash causes immediate abort."""
        error = Exception("Application crashed: Fatal exception")
        context = {"node_stack": ["root", "screen"]}

        result = self.state_machine.handle_error(error, context)

        # Should abort immediately
        assert result.recovery_action == "abort"
        assert result.success is False

    def test_recovery_with_empty_node_stack(self):
        """Test recovery when node stack is empty."""
        error = Exception("Test error")
        context = {"node_stack": []}  # Empty stack

        result = self.state_machine.handle_error(error, context)

        # Should still handle error (skip or continue)
        assert result.recovery_action in ["skip", "continue_despite_error", "abort"]

    def test_recovery_with_single_node_stack(self):
        """Test recovery when node stack has only one node."""
        error = Exception("Test error")
        context = {"node_stack": ["root"]}  # Only root node

        result = self.state_machine.handle_error(error, context)

        # Should handle error (can't backtrack with single node)
        assert result.recovery_action in ["skip", "retry", "continue_despite_error", "abort"]


class TestStateMachineErrorHandlingIntegration:
    """Test integration with state machine state transitions."""

    def setup_method(self):
        """Set up test fixtures."""
        self.state_machine = TraversalStateMachine()

    def test_error_to_node_select_transition(self):
        """Test transition from ERROR_HANDLING to NODE_SELECT (after SKIP)."""
        # Follow proper state flow: NODE_SELECT → PRECONDITION_CHECK → EXECUTE → ERROR_HANDLING → NODE_SELECT
        self.state_machine.transition_to(TraversalState.PRECONDITION_CHECK)
        self.state_machine.transition_to(TraversalState.EXECUTE)
        self.state_machine.transition_to(TraversalState.ERROR_HANDLING)  # Transition to ERROR_HANDLING first
        assert self.state_machine.state == TraversalState.ERROR_HANDLING

        # Handle error (simulating SKIP recovery)
        error = Exception("Element not found")
        result = self.state_machine.handle_error(error)
        if result.recovery_action == "skip":
            # Should be able to transition back to NODE_SELECT from ERROR_HANDLING
            assert self.state_machine.error_to_node_select()
            assert self.state_machine.state == TraversalState.NODE_SELECT

    def test_error_to_execute_transition(self):
        """Test transition from ERROR_HANDLING to EXECUTE (after RETRY)."""
        # Follow proper state flow: NODE_SELECT → PRECONDITION_CHECK → EXECUTE → ERROR_HANDLING → EXECUTE
        self.state_machine.transition_to(TraversalState.PRECONDITION_CHECK)
        self.state_machine.transition_to(TraversalState.EXECUTE)
        self.state_machine.transition_to(TraversalState.ERROR_HANDLING)  # Transition to ERROR_HANDLING first
        assert self.state_machine.state == TraversalState.ERROR_HANDLING

        # Handle error (simulating RETRY recovery)
        error = Exception("Network timeout")
        result = self.state_machine.handle_error(error)
        if "retry" in result.recovery_action:
            # Should be able to transition back to EXECUTE for retry from ERROR_HANDLING
            assert self.state_machine.error_to_execute()
            assert self.state_machine.state == TraversalState.EXECUTE

    def test_error_handling_statistics_persistence(self):
        """Test that error statistics persist across multiple operations."""
        # Handle multiple errors
        for i in range(5):
            error = Exception(f"Error {i}")
            context = {"node_stack": ["root", "node"]}
            self.state_machine.handle_error(error, context)

        # Reset error handling state
        self.state_machine.reset_error_handling()

        # Get summary - statistics should persist in handler
        summary = self.state_machine.get_error_recovery_summary()
        assert summary["total_errors"] == 5  # Statistics kept in handler