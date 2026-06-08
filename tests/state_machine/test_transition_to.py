"""
Unit tests for TraversalStateMachine.transition_to() method.

V6.10.2: Tests for enhanced error messages and trace recording.
Covers invalid transitions, valid transitions, and trace recording.
"""

import pytest
from src.state_machine.traversal_fsm import TraversalStateMachine, TraversalState
from src.trace.recorder import TraceRecorder
from src.trace.storage import MemoryStorage
from unittest.mock import Mock, patch


class TestTransitionTo:
    """Test suite for transition_to() method."""

    # ============================================================================
    # Invalid Transition Error Message Tests
    # ============================================================================

    def test_transition_to_invalid_error_message_full(self):
        """
        Given: State machine with transition history
        When: Attempting invalid state transition (EXECUTE → NODE_SELECT)
        Then: Raises ValueError with enhanced error message containing:
              - Current and target states,
              - Current node ID,
              - Target node ID from metadata,
              - Recent 5 transitions,
              - Valid transitions list
        """
        # Setup
        fsm = TraversalStateMachine()
        fsm._state = TraversalState.EXECUTE  # EXECUTE → NODE_SELECT is invalid
        fsm._current_node_id = "node123"

        # Add some transition history
        from src.state_machine.traversal_fsm import TraversalStateTransition
        from datetime import datetime

        for i in range(7):
            transition = TraversalStateTransition(
                from_state=TraversalState.EXECUTE if i > 0 else TraversalState.NODE_SELECT,
                to_state=TraversalState.BRANCH if i < 5 else TraversalState.NODE_SELECT,
                node_id=f"node_{i}",
                timestamp=datetime.now()
            )
            fsm._transition_history.append(transition)

        # Execute & Verify
        with pytest.raises(ValueError) as exc_info:
            fsm.transition_to(
                TraversalState.NODE_SELECT,
                node_id="target_node_456",
                target_node_id="metadata_target_789"
            )

        error_message = str(exc_info.value)

        # Verify error message contains all required fields
        assert "Invalid state transition" in error_message
        assert "execute → node_select" in error_message
        assert "Current node: node123" in error_message
        assert "Target node: metadata_target_789" in error_message
        assert "Recent transitions:" in error_message
        assert "Valid transitions from execute:" in error_message

        # Verify recent transitions format
        assert "→" in error_message  # Transition arrow
        assert "(node:" in error_message  # Node ID format

    def test_transition_to_invalid_error_message_empty_history(self):
        """
        Given: State machine with no transition history
        When: Attempting invalid state transition (RESULT_VERIFY → NODE_SELECT)
        Then: Error message shows 'no recent transitions' instead of empty list
        """
        # Setup
        fsm = TraversalStateMachine()
        fsm._state = TraversalState.RESULT_VERIFY  # RESULT_VERIFY → NODE_SELECT is invalid
        fsm._current_node_id = "node123"

        # Execute & Verify
        with pytest.raises(ValueError) as exc_info:
            fsm.transition_to(TraversalState.NODE_SELECT)

        error_message = str(exc_info.value)

        # Verify empty history is handled gracefully
        assert "no recent transitions" in error_message.lower()
        assert "Current node: node123" in error_message
        assert "Valid transitions from result_verify:" in error_message

    def test_transition_to_invalid_error_message_short_history(self):
        """
        Given: State machine with less than 5 transitions in history
        When: Attempting invalid state transition
        Then: Shows all available transitions without IndexError
        """
        # Setup
        fsm = TraversalStateMachine()
        fsm._state = TraversalState.EXECUTE
        fsm._current_node_id = "node123"

        # Add only 2 transitions
        from src.state_machine.traversal_fsm import TraversalStateTransition
        from datetime import datetime

        for i in range(2):
            transition = TraversalStateTransition(
                from_state=TraversalState.NODE_SELECT if i == 0 else TraversalState.EXECUTE,
                to_state=TraversalState.EXECUTE if i == 0 else TraversalState.RESULT_VERIFY,
                node_id=f"node_{i}",
                timestamp=datetime.now()
            )
            fsm._transition_history.append(transition)

        # Execute & Verify (should not raise IndexError)
        with pytest.raises(ValueError) as exc_info:
            fsm.transition_to(TraversalState.NODE_SELECT)

        error_message = str(exc_info.value)

        # Verify transitions are shown
        assert "Recent transitions:" in error_message
        # Should show 2 transitions
        transition_lines = [line for line in error_message.split('\n') if '→' in line and '(node:' in line]
        assert len(transition_lines) == 2

    # ============================================================================
    # Valid Transition Tests
    # ============================================================================

    def test_transition_to_valid_success(self):
        """
        Given: State machine in NODE_SELECT state
        When: Transitioning to PRECONDITION_CHECK (valid transition)
        Then: Returns True, state updated, transition recorded
        """
        # Setup
        fsm = TraversalStateMachine()
        fsm._state = TraversalState.NODE_SELECT
        fsm._current_node_id = "node123"

        # Execute
        result = fsm.transition_to(
            TraversalState.PRECONDITION_CHECK,
            node_id="node123",
            action="check_precondition"
        )

        # Verify
        assert result is True
        assert fsm._state == TraversalState.PRECONDITION_CHECK
        assert fsm._current_node_id == "node123"
        assert len(fsm._transition_history) == 1

        # Verify transition record
        transition = fsm._transition_history[0]
        assert transition.from_state == TraversalState.NODE_SELECT
        assert transition.to_state == TraversalState.PRECONDITION_CHECK
        assert transition.node_id == "node123"
        assert transition.metadata.get("action") == "check_precondition"

    def test_transition_to_valid_no_node_id(self):
        """
        Given: State machine without current_node_id set
        When: Transitioning with no node_id parameter
        Then: Transition succeeds, node_id remains unchanged
        """
        # Setup
        fsm = TraversalStateMachine()
        fsm._state = TraversalState.EXECUTE
        fsm._current_node_id = None

        # Execute
        result = fsm.transition_to(TraversalState.RESULT_VERIFY)

        # Verify
        assert result is True
        assert fsm._state == TraversalState.RESULT_VERIFY
        assert fsm._current_node_id is None

    # ============================================================================
    # Trace Recording Tests
    # ============================================================================

    def test_transition_to_trace_recording_with_recorder(self):
        """
        Given: State machine with _trace_recorder attribute set
        When: Performing valid state transition
        Then: Records state_transition span to trace
        """
        # Setup
        fsm = TraversalStateMachine()
        fsm._state = TraversalState.NODE_SELECT
        fsm._current_node_id = "node123"

        # Create trace recorder mock
        trace_recorder = Mock()
        fsm._trace_recorder = trace_recorder

        # Execute
        fsm.transition_to(
            TraversalState.PRECONDITION_CHECK,
            node_id="node123",
            action="check_precondition",
            extra_metadata="test_value"
        )

        # Verify trace was recorded
        trace_recorder.record_span.assert_called_once()

        # Get the recorded span
        call_args = trace_recorder.record_span.call_args
        span = call_args[0][0] if call_args[0] else call_args[1].get('span')

        # Verify span properties
        assert span.span_type == "state_transition"
        assert span.action == "state_change"
        assert span.from_state == "node_select"
        assert span.to_state == "precondition_check"
        assert span.state_machine == "traversal"
        assert span.metadata.get("node_id") == "node123"
        assert span.metadata.get("action") == "check_precondition"
        assert span.metadata.get("extra_metadata") == "test_value"

    def test_transition_to_trace_recording_without_recorder(self):
        """
        Given: State machine without _trace_recorder attribute
        When: Performing valid state transition
        Then: Transition succeeds, no crash
        """
        # Setup
        fsm = TraversalStateMachine()
        fsm._state = TraversalState.NODE_SELECT
        fsm._current_node_id = "node123"

        # Ensure _trace_recorder does not exist
        if hasattr(fsm, '_trace_recorder'):
            delattr(fsm, '_trace_recorder')

        # Execute (should not crash)
        result = fsm.transition_to(TraversalState.PRECONDITION_CHECK)

        # Verify transition succeeded
        assert result is True
        assert fsm._state == TraversalState.PRECONDITION_CHECK

    def test_transition_to_trace_recording_none_recorder(self):
        """
        Given: State machine with _trace_recorder = None
        When: Performing valid state transition
        Then: Transition succeeds, no span recorded
        """
        # Setup
        fsm = TraversalStateMachine()
        fsm._state = TraversalState.NODE_SELECT
        fsm._current_node_id = "node123"
        fsm._trace_recorder = None

        # Execute
        result = fsm.transition_to(TraversalState.PRECONDITION_CHECK)

        # Verify transition succeeded
        assert result is True
        assert fsm._state == TraversalState.PRECONDITION_CHECK

    def test_transition_to_invalid_no_trace_recording(self):
        """
        Given: State machine with _trace_recorder attribute set
        When: Attempting invalid state transition (EXECUTE → NODE_SELECT)
        Then: Raises ValueError, no span recorded for failed transition
        """
        # Setup
        fsm = TraversalStateMachine()
        fsm._state = TraversalState.EXECUTE  # EXECUTE → NODE_SELECT is invalid
        fsm._current_node_id = "node123"

        # Create trace recorder mock
        trace_recorder = Mock()
        fsm._trace_recorder = trace_recorder

        # Execute & Verify
        with pytest.raises(ValueError):
            fsm.transition_to(TraversalState.NODE_SELECT)

        # Verify no span was recorded (exception raised before trace recording)
        trace_recorder.record_span.assert_not_called()

    # ============================================================================
    # Multiple Transitions Trace Recording
    # ============================================================================

    def test_transition_to_trace_multiple_transitions(self):
        """
        Given: State machine with _trace_recorder
        When: Performing multiple valid transitions
        Then: Each transition is recorded to trace
        """
        # Setup
        fsm = TraversalStateMachine()
        fsm._current_node_id = "node123"

        # Create trace recorder mock
        trace_recorder = Mock()
        fsm._trace_recorder = trace_recorder

        # Execute multiple transitions
        transitions = [
            (TraversalState.NODE_SELECT, TraversalState.PRECONDITION_CHECK),
            (TraversalState.PRECONDITION_CHECK, TraversalState.EXECUTE),
            (TraversalState.EXECUTE, TraversalState.RESULT_VERIFY),
        ]

        for from_state, to_state in transitions:
            fsm._state = from_state
            fsm.transition_to(to_state, node_id="node123")

        # Verify all transitions were recorded
        assert trace_recorder.record_span.call_count == len(transitions)

        # Verify each span
        call_args_list = trace_recorder.record_span.call_args_list
        expected_transitions = [
            ("node_select", "precondition_check"),
            ("precondition_check", "execute"),
            ("execute", "result_verify"),
        ]

        for i, (from_s, to_s) in enumerate(expected_transitions):
            span = call_args_list[i][0][0]
            assert span.from_state == from_s
            assert span.to_state == to_s
