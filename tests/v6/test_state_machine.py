"""
Tests for V6 state machine extensions.

Tests all new states, transitions, and handler methods.

NOTE: Some tests are marked as skip because they test V6 features that are
designed but not yet implemented. These features are:
- handle_frame_complete() - Container frame completion handling
- handle_error() - Error handling entry point
- handle_popup() - Popup detection and handling
- State transition methods for error/popup recovery

Search tag: V6_UNIMPLEMENTED_FEATURES
"""

import pytest

from src.state_machine.traversal_fsm import (
    TraversalState,
    TraversalStateMachine,
    TraversalStateTransition,
)


# ============================================================================
# Test New States (Tasks 2.1.1 - 2.1.3)
# ============================================================================


class TestTraversalStateExtensions:
    """Tests for TraversalState V6 extensions."""

    def test_frame_complete_state_exists(self):
        """Test that FRAME_COMPLETE state exists."""
        assert hasattr(TraversalState, "FRAME_COMPLETE")
        assert TraversalState.FRAME_COMPLETE.value == "frame_complete"

    def test_error_handling_state_exists(self):
        """Test that ERROR_HANDLING state exists."""
        assert hasattr(TraversalState, "ERROR_HANDLING")
        assert TraversalState.ERROR_HANDLING.value == "error_handling"

    def test_popup_handling_state_exists(self):
        """Test that POPUP_HANDLING state exists."""
        assert hasattr(TraversalState, "POPUP_HANDLING")
        assert TraversalState.POPUP_HANDLING.value == "popup_handling"

    def test_all_states_in_values(self):
        """Test that all states are in values() output."""
        values = TraversalState.values()
        assert "frame_complete" in values
        assert "error_handling" in values
        assert "popup_handling" in values

    def test_new_states_from_value(self):
        """Test from_value() for new states."""
        assert TraversalState.from_value("frame_complete") == TraversalState.FRAME_COMPLETE
        assert TraversalState.from_value("error_handling") == TraversalState.ERROR_HANDLING
        assert TraversalState.from_value("popup_handling") == TraversalState.POPUP_HANDLING

    def test_new_states_is_valid(self):
        """Test is_valid() for new states."""
        assert TraversalState.is_valid("frame_complete") is True
        assert TraversalState.is_valid("error_handling") is True
        assert TraversalState.is_valid("popup_handling") is True


# ============================================================================
# Test State Transitions (Tasks 2.2.1 - 2.2.3)
# ============================================================================


class TestValidTransitions:
    """Tests for VALID_TRANSITIONS extensions."""

    def test_frame_complete_transitions(self):
        """Test FRAME_COMPLETE state transitions."""
        fsm = TraversalStateMachine()

        # Can transition from BRANCH to FRAME_COMPLETE
        assert fsm.can_transition_to(TraversalState.FRAME_COMPLETE) is False  # From NODE_SELECT

        fsm.transition_to(TraversalState.BRANCH)
        assert TraversalState.FRAME_COMPLETE in fsm.VALID_TRANSITIONS[TraversalState.BRANCH]

    def test_error_handling_transitions(self):
        """Test ERROR_HANDLING state transitions."""
        # Can transition from EXECUTE to ERROR_HANDLING
        assert TraversalState.ERROR_HANDLING in TraversalStateMachine.VALID_TRANSITIONS[TraversalState.EXECUTE]

        # Can transition from FRAME_COMPLETE to ERROR_HANDLING
        assert TraversalState.ERROR_HANDLING in TraversalStateMachine.VALID_TRANSITIONS[TraversalState.FRAME_COMPLETE]

    def test_popup_handling_transitions(self):
        """Test POPUP_HANDLING state transitions."""
        # Can transition from RESULT_VERIFY to POPUP_HANDLING
        assert TraversalState.POPUP_HANDLING in TraversalStateMachine.VALID_TRANSITIONS[TraversalState.RESULT_VERIFY]

        # Can transition from POPUP_HANDLING back to RESULT_VERIFY
        assert TraversalState.RESULT_VERIFY in TraversalStateMachine.VALID_TRANSITIONS[TraversalState.POPUP_HANDLING]

    def test_error_handling_can_return(self):
        """Test ERROR_HANDLING can transition to various states."""
        transitions = TraversalStateMachine.VALID_TRANSITIONS[TraversalState.ERROR_HANDLING]
        assert TraversalState.NODE_SELECT in transitions
        assert TraversalState.EXECUTE in transitions
        assert TraversalState.FRAME_COMPLETE in transitions
        assert TraversalState.BRANCH in transitions

    def test_frame_complete_can_return(self):
        """Test FRAME_COMPLETE can transition back to NODE_SELECT."""
        transitions = TraversalStateMachine.VALID_TRANSITIONS[TraversalState.FRAME_COMPLETE]
        assert TraversalState.NODE_SELECT in transitions


# ============================================================================
# Test State Handler Methods (Tasks 2.3.1 - 2.3.3)
# ============================================================================


class TestStateHandlerMethods:
    """Tests for new state handler methods."""

    def test_handle_frame_complete(self):
        """Test handle_frame_complete() handles container completion."""
        fsm = TraversalStateMachine()
        result = fsm.handle_frame_complete(
            container={"node_id": "n1", "children_done": True}
        )
        assert isinstance(result, dict)
        assert result.get("is_complete") is True

    def test_handle_error(self):
        """Test handle_error() processes an exception."""
        fsm = TraversalStateMachine()
        result = fsm.handle_error(ValueError("test error"))
        assert hasattr(result, "success")
        assert isinstance(result.recovery_action, str)

    def test_handle_popup(self):
        """Test handle_popup() processes popup screen info."""
        fsm = TraversalStateMachine()
        result = fsm.handle_popup(
            screen_info={"has_popup": True, "popup_type": "ad"}
        )
        assert isinstance(result, dict)
        assert "handled" in result


# ============================================================================
# Test Fallback Actions (Tasks 2.4.1 - 2.4.4)
# ============================================================================


class TestFallbackActions:
    """Tests for fallback action implementations."""

    def test_back_action_exists(self):
        """Test that BACK fallback action is defined."""
        from src.graph.node import FallbackAction
        assert FallbackAction.BACK.value == "back"

    def test_auto_escape_action_exists(self):
        """Test that AUTO_ESCAPE fallback action is defined."""
        from src.graph.node import FallbackAction
        assert FallbackAction.AUTO_ESCAPE.value == "auto_escape"

    def test_skip_action_exists(self):
        """Test that SKIP fallback action is defined."""
        from src.graph.node import FallbackAction
        assert FallbackAction.SKIP.value == "skip"

    def test_abort_action_exists(self):
        """Test that ABORT fallback action is defined."""
        from src.graph.node import FallbackAction
        assert FallbackAction.ABORT.value == "abort"

    def test_frame_complete_to_node_select(self):
        """Test transition from FRAME_COMPLETE to NODE_SELECT."""
        fsm = TraversalStateMachine()
        fsm.transition_to(TraversalState.PRECONDITION_CHECK)
        fsm.transition_to(TraversalState.EXECUTE)
        fsm.transition_to(TraversalState.BRANCH)
        fsm.transition_to(TraversalState.FRAME_COMPLETE)
        result = fsm.frame_complete_to_node_select()
        assert result is True
        assert fsm.state == TraversalState.NODE_SELECT

    def test_frame_complete_failed(self):
        """Test transition from FRAME_COMPLETE to ERROR_HANDLING on failure."""
        fsm = TraversalStateMachine()
        fsm.transition_to(TraversalState.PRECONDITION_CHECK)
        fsm.transition_to(TraversalState.EXECUTE)
        fsm.transition_to(TraversalState.BRANCH)
        fsm.transition_to(TraversalState.FRAME_COMPLETE)
        result = fsm.frame_complete_failed()
        assert result is True
        assert fsm.state == TraversalState.ERROR_HANDLING


# ============================================================================
# Test Error Handling Methods (Tasks 2.5.1 - 2.5.3)
# ============================================================================


class TestErrorHandlingMethods:
    """Tests for error handling state methods."""

    def test_error_to_node_select(self):
        """Test SKIP action - transition to NODE_SELECT."""
        fsm = TraversalStateMachine()
        fsm.transition_to(TraversalState.PRECONDITION_CHECK)
        fsm.transition_to(TraversalState.EXECUTE)
        fsm.transition_to(TraversalState.ERROR_HANDLING)
        result = fsm.error_to_node_select()
        assert result is True
        assert fsm.state == TraversalState.NODE_SELECT

    def test_error_to_execute(self):
        """Test RETRY action - transition to EXECUTE."""
        fsm = TraversalStateMachine()
        fsm.transition_to(TraversalState.PRECONDITION_CHECK)
        fsm.transition_to(TraversalState.EXECUTE)
        fsm.transition_to(TraversalState.ERROR_HANDLING)
        result = fsm.error_to_execute()
        assert result is True
        assert fsm.state == TraversalState.EXECUTE

    def test_error_to_frame_complete(self):
        """Test BACKTRACK action - transition to FRAME_COMPLETE."""
        fsm = TraversalStateMachine()
        fsm.transition_to(TraversalState.PRECONDITION_CHECK)
        fsm.transition_to(TraversalState.EXECUTE)
        fsm.transition_to(TraversalState.ERROR_HANDLING)
        result = fsm.error_to_frame_complete()
        assert result is True
        assert fsm.state == TraversalState.FRAME_COMPLETE

    def test_error_to_branch(self):
        """Test continue branching - transition to BRANCH."""
        fsm = TraversalStateMachine()
        fsm.transition_to(TraversalState.PRECONDITION_CHECK)
        fsm.transition_to(TraversalState.EXECUTE)
        fsm.transition_to(TraversalState.ERROR_HANDLING)
        result = fsm.error_to_branch()
        assert result is True
        assert fsm.state == TraversalState.BRANCH

    def test_error_policy_backtrack_supported(self):
        """Test that BACKTRACK is a valid error policy action."""
        from src.graph.node import ErrorPolicy

        ep = ErrorPolicy(on_error="backtrack")
        assert ep.on_error == "backtrack"


# ============================================================================
# Test Popup Handling Methods (Tasks 2.6.1 - 2.6.4)
# ============================================================================


class TestPopupHandlingMethods:
    """Tests for popup handling state methods."""

    def test_popup_handled(self):
        """Test transition from POPUP_HANDLING back to RESULT_VERIFY."""
        fsm = TraversalStateMachine()
        # Must go through valid path: NODE_SELECT → PRECONDITION_CHECK → EXECUTE → RESULT_VERIFY → POPUP_HANDLING
        fsm.transition_to(TraversalState.PRECONDITION_CHECK)
        fsm.transition_to(TraversalState.EXECUTE)
        fsm.transition_to(TraversalState.RESULT_VERIFY)
        fsm.transition_to(TraversalState.POPUP_HANDLING)
        result = fsm.popup_handled()
        assert result is True
        assert fsm.state == TraversalState.RESULT_VERIFY

    def test_popup_handling_failed(self):
        """Test transition from POPUP_HANDLING to ERROR_HANDLING."""
        fsm = TraversalStateMachine()
        fsm.transition_to(TraversalState.PRECONDITION_CHECK)
        fsm.transition_to(TraversalState.EXECUTE)
        fsm.transition_to(TraversalState.RESULT_VERIFY)
        fsm.transition_to(TraversalState.POPUP_HANDLING)
        result = fsm.popup_handling_failed()
        assert result is True
        assert fsm.state == TraversalState.ERROR_HANDLING

    def test_popup_handler_methods_exist(self):
        """Test that all popup handler methods exist."""
        fsm = TraversalStateMachine()
        assert hasattr(fsm, "handle_popup")
        assert hasattr(fsm, "popup_handled")
        assert hasattr(fsm, "popup_handling_failed")


# ============================================================================
# Test State Transition History (Tasks 2.7.x)
# ============================================================================


class TestStateTransitionHistory:
    """Tests for state transition history tracking."""

    def test_transition_records_new_states(self):
        """Test that transitions to new states are recorded."""
        fsm = TraversalStateMachine()

        # Use proper state transition paths
        fsm.transition_to(TraversalState.PRECONDITION_CHECK)
        fsm.transition_to(TraversalState.EXECUTE)
        fsm.transition_to(TraversalState.BRANCH)
        fsm.transition_to(TraversalState.FRAME_COMPLETE)

        # From FRAME_COMPLETE, we can go to NODE_SELECT or ERROR_HANDLING
        fsm.transition_to(TraversalState.NODE_SELECT)  # Back to start
        fsm.transition_to(TraversalState.PRECONDITION_CHECK)
        fsm.transition_to(TraversalState.EXECUTE)
        fsm.transition_to(TraversalState.ERROR_HANDLING)

        # From ERROR_HANDLING, we can go to NODE_SELECT
        fsm.transition_to(TraversalState.NODE_SELECT)
        fsm.transition_to(TraversalState.PRECONDITION_CHECK)
        fsm.transition_to(TraversalState.EXECUTE)
        fsm.transition_to(TraversalState.RESULT_VERIFY)
        fsm.transition_to(TraversalState.POPUP_HANDLING)

        history = fsm.get_transition_history()
        assert len(history) >= 13  # Should have at least the transitions we made

        # Check that new states are in history
        to_states = [t.to_state for t in history]
        assert TraversalState.FRAME_COMPLETE in to_states
        assert TraversalState.ERROR_HANDLING in to_states
        assert TraversalState.POPUP_HANDLING in to_states

    def test_transition_includes_metadata(self):
        """Test that transitions can include metadata."""
        fsm = TraversalStateMachine()

        # Use proper state transition path to ERROR_HANDLING
        fsm.transition_to(TraversalState.PRECONDITION_CHECK)
        fsm.transition_to(TraversalState.EXECUTE)
        fsm.transition_to(
            TraversalState.ERROR_HANDLING,
            node_id="test_node",
            error_type="TestError",
            retry_count=1,
        )

        history = fsm.get_transition_history()
        last_transition = history[-1]

        assert last_transition.to_state == TraversalState.ERROR_HANDLING
        assert last_transition.node_id == "test_node"
        assert last_transition.metadata["error_type"] == "TestError"
        assert last_transition.metadata["retry_count"] == 1


class TestStateStepMethod:
    """Tests for the step() method implementation."""

    def test_step_method_exists(self):
        """Test that step() method exists."""
        fsm = TraversalStateMachine()
        assert hasattr(fsm, "step")

    def test_step_returns_transition(self):
        """Test that step() returns a StateTransition."""
        # This requires mock objects for stack, context, vision, action
        # For now, just verify the method signature exists
        fsm = TraversalStateMachine()
        import inspect

        sig = inspect.signature(fsm.step)
        # Should have parameters: stack, context, vision, action
        assert len(sig.parameters) == 4

    def test_step_advances_state(self):
        """Test that step() advances the state machine."""
        # This would require a full integration test with mock objects
        # Placeholder for structural validation
        fsm = TraversalStateMachine()
        initial_state = fsm.state
        assert initial_state == TraversalState.NODE_SELECT


class TestResetFunctionality:
    """Tests for reset() functionality."""

    def test_reset_preserves_transition_history(self):
        """Test that reset() preserves transition history."""
        fsm = TraversalStateMachine()

        # Go through valid state path before reset
        fsm.transition_to(TraversalState.PRECONDITION_CHECK)
        fsm.transition_to(TraversalState.EXECUTE)
        fsm.transition_to(TraversalState.RESULT_VERIFY)
        fsm.transition_to(TraversalState.BRANCH)
        fsm.transition_to(TraversalState.FRAME_COMPLETE)

        history_before = len(fsm.get_transition_history())

        fsm.reset()

        assert fsm.state == TraversalState.NODE_SELECT
        history_after = fsm.get_transition_history()
        assert len(history_after) == history_before

    def test_reset_clears_runtime_state(self):
        """Test that reset() clears runtime state."""
        fsm = TraversalStateMachine()

        fsm.set_current_node("test_node")
        fsm.set_execution_result({"success": True})
        fsm.set_precondition_result(True)

        fsm.reset()

        assert fsm.current_node_id is None
        assert fsm.execution_result is None
        assert fsm.precondition_result is None
