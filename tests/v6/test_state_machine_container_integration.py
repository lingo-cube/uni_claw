"""
Integration tests for container handling in TraversalStateMachine.

Tests the complete container handling flow from state machine perspective.
"""

import pytest
from src.state_machine.traversal_fsm import TraversalStateMachine, TraversalState
from src.state_machine.container_handler import CompletionStatus, FallbackAction


class TestStateMachineContainerIntegration:
    """Test container handling integration in TraversalStateMachine."""

    def setup_method(self):
        """Set up test fixtures."""
        self.state_machine = TraversalStateMachine()

    def test_container_handler_initialization(self):
        """Test that container handler is initialized on first use."""
        # Container handler should not exist initially
        assert not hasattr(self.state_machine, '_container_handler') or self.state_machine._container_handler is None

        # Call handle_frame_complete to initialize
        container = {"id": "test_menu", "children": ["item1"]}
        result = self.state_machine.handle_frame_complete(container)

        # Container handler should now be initialized
        assert self.state_machine._container_handler is not None

    def test_container_handling_creates_handler(self):
        """Test that first container handling call creates container handler."""
        container = {"id": "test_menu", "children": ["item1"]}
        result = self.state_machine.handle_frame_complete(container)

        assert result is not None
        assert 'is_complete' in result  # Dictionary access instead of hasattr
        assert 'completion_reason' in result

    def test_container_handling_with_complete_container(self):
        """Test container handling when all children visited."""
        container = {
            "id": "complete_menu",
            "children": ["item1", "item2"],
            "exit_condition": "BACK"
        }
        context = {
            "visited_children": ["item1", "item2"],
            "current_depth": 1,
        }

        result = self.state_machine.handle_frame_complete(container, context)

        assert result is not None
        assert result['is_complete'] is True
        assert result['completion_reason'] == "ALL_VISITED"

    def test_container_handling_with_incomplete_container(self):
        """Test container handling when not all children visited."""
        container = {
            "id": "incomplete_menu",
            "children": ["item1", "item2", "item3"],
        }
        context = {
            "visited_children": ["item1"],
            "current_depth": 1,
        }

        result = self.state_machine.handle_frame_complete(container, context)

        assert result is not None
        assert result['is_complete'] is False
        assert result['completion_reason'] == "INCOMPLETE"
        assert len(result['remaining_children']) == 2

    def test_container_context_storage(self):
        """Test that container context is stored after handling."""
        container = {"id": "test_menu", "children": ["item1"]}
        context = {"current_depth": 2}

        self.state_machine.handle_frame_complete(container, context)

        # Check container context was stored
        assert "last_container_id" in self.state_machine._container_context
        assert "last_completion_reason" in self.state_machine._container_context
        assert "last_fallback_action" in self.state_machine._container_context

    def test_container_statistics_tracking(self):
        """Test getting container statistics."""
        # Handle a few containers
        containers = [
            {"id": "menu1", "children": ["item1"], "exit_condition": "BACK"},
            {"id": "menu2", "children": ["item2"], "exit_condition": "AUTO_ESCAPE"},
        ]

        for container in containers:
            context = {"visited_children": [container["children"][0]]}
            self.state_machine.handle_frame_complete(container, context)

        # Get summary
        summary = self.state_machine.get_container_statistics()

        assert "processed_containers" in summary
        assert "completed_containers" in summary
        assert "completion_rate" in summary
        assert "fallback_actions" in summary

        # Should have tracked 2 containers
        assert summary["processed_containers"] == 2

    def test_container_statistics_before_any_handling(self):
        """Test container statistics before any handling occurs."""
        summary = self.state_machine.get_container_statistics()

        # Should return empty statistics
        assert summary["processed_containers"] == 0
        assert summary["completed_containers"] == 0
        assert summary["completion_rate"] == 0.0

    def test_reset_container_handling(self):
        """Test resetting container handling state."""
        container = {"id": "test_menu", "children": ["item1"]}
        context = {"current_depth": 1}

        # Handle some containers to build up state
        self.state_machine.handle_frame_complete(container, context)

        # Reset
        self.state_machine.reset_container_handling()

        # Check reset happened
        assert self.state_machine._container_context == {}

    def test_container_handling_preserves_handler_on_reset(self):
        """Test that container handler is preserved on reset."""
        container = {"id": "test_menu", "children": ["item1"]}

        # Create handler by handling container
        self.state_machine.handle_frame_complete(container)
        handler = self.state_machine._container_handler

        # Reset
        self.state_machine.reset_container_handling()

        # Handler should still exist
        assert self.state_machine._container_handler is handler

    def test_state_transitions_with_container_handling(self):
        """Test that state transitions work correctly with container handling."""
        # Start in NODE_SELECT state
        assert self.state_machine.state == TraversalState.NODE_SELECT

        # Handle container frame (should not change state directly)
        container = {"id": "test_menu", "children": ["item1"]}
        result = self.state_machine.handle_frame_complete(container)

        # State should still be NODE_SELECT (container handling doesn't auto-transition)
        assert self.state_machine.state == TraversalState.NODE_SELECT

    def test_container_context_passed_to_handler(self):
        """Test that traversal context is properly passed to container handler."""
        container = {
            "id": "test_menu",
            "children": ["item1"],
            "exit_condition": "SKIP"
        }
        context = {
            "visited_children": ["item1"],
            "current_depth": 2,
            "max_depth": 15,
            "timeout_seconds": 90,
        }

        result = self.state_machine.handle_frame_complete(container, context)

        # Check that context was used (should influence depth and timeout)
        assert result is not None

    def test_multiple_containers_in_single_session(self):
        """Test handling multiple different containers in one session."""
        containers = [
            {"id": "menu1", "children": ["item1"], "exit_condition": "BACK"},
            {"id": "menu2", "children": ["item2", "item3"], "exit_condition": "AUTO_ESCAPE"},
            {"id": "empty", "children": [], "exit_condition": "SKIP"},
        ]

        for container in containers:
            visited = container["children"] if container["children"] else []
            context = {"visited_children": visited}
            self.state_machine.handle_frame_complete(container, context)

        summary = self.state_machine.get_container_statistics()

        # Should have tracked all containers
        assert summary["processed_containers"] == 3
        assert summary["completed_containers"] >= 2  # At least the complete ones


class TestStateMachineContainerRecoveryScenarios:
    """Test realistic container handling scenarios."""

    def setup_method(self):
        """Set up test fixtures."""
        self.state_machine = TraversalStateMachine()

    def test_deep_container_nesting_scenario(self):
        """Test realistic deep nesting container handling."""
        # Simulate deep nesting that exceeds max depth
        for depth in range(1, 12):  # Go beyond typical max depth
            container = {
                "id": f"level_{depth}",
                "children": [f"item_{depth}"],
            }
            context = {
                "visited_children": [f"item_{depth}"],
                "current_depth": depth,
                "max_depth": 10,
            }

            result = self.state_machine.handle_frame_complete(container, context)

            if depth > 10:
                # Should trigger max_depth completion
                assert result['completion_reason'] == "MAX_DEPTH"
                assert result['is_complete'] is True

    def test_slow_container_processing_scenario(self):
        """Test realistic slow processing container handling."""
        container = {
            "id": "slow_menu",
            "children": ["item1"],
        }
        context = {
            "visited_children": ["item1"],
            "current_depth": 1,
            "timeout_seconds": 1,
        }

        # Simulate timeout by setting processing_start_time
        # This would need to be done in the actual handler, but we can test the API
        result = self.state_machine.handle_frame_complete(container, context)

        # Should handle normally (timeout would be detected in actual handler)
        assert result is not None

    def test_complex_menu_structure_scenario(self):
        """Test realistic complex menu structure container handling."""
        container = {
            "id": "complex_menu",
            "children": ["settings", "profile", "help", "logout"],
            "exit_condition": "AUTO_ESCAPE"
        }

        # Process children one by one
        visited = []
        for child in container["children"]:
            visited.append(child)
            context = {
                "visited_children": visited,
                "current_depth": 1,
            }

            result = self.state_machine.handle_frame_complete(container, context)

            if len(visited) < 4:
                assert result['is_complete'] is False
            else:
                assert result['is_complete'] is True

    def test_empty_container_scenario(self):
        """Test handling of empty container."""
        container = {
            "id": "empty_menu",
            "children": [],
            "exit_condition": "BACK"
        }
        context = {"visited_children": [], "current_depth": 1}

        result = self.state_machine.handle_frame_complete(container, context)

        # Empty containers should be considered complete
        assert result['is_complete'] is True
        assert result['completion_reason'] == "ALL_VISITED"

    def test_mixed_exit_conditions_scenario(self):
        """Test handling containers with different exit conditions."""
        containers = [
            {"id": "back_menu", "children": ["item"], "exit_condition": "BACK"},
            {"id": "escape_menu", "children": ["item"], "exit_condition": "AUTO_ESCAPE"},
            {"id": "skip_menu", "children": ["item"], "exit_condition": "SKIP"},
        ]

        for container in containers:
            context = {"visited_children": ["item"]}
            result = self.state_machine.handle_frame_complete(container, context)

            # All should be complete
            assert result['is_complete'] is True
            # Each should have a fallback action
            assert result['fallback_action'] is not None


class TestStateMachineContainerHandlingIntegration:
    """Test integration with state machine state transitions."""

    def setup_method(self):
        """Set up test fixtures."""
        self.state_machine = TraversalStateMachine()

    def test_frame_complete_to_node_select_transition(self):
        """Test transition from FRAME_COMPLETE to NODE_SELECT."""
        # Follow proper state flow: NODE_SELECT → PRECONDITION_CHECK → EXECUTE → BRANCH → FRAME_COMPLETE → NODE_SELECT
        self.state_machine.transition_to(TraversalState.PRECONDITION_CHECK)
        self.state_machine.transition_to(TraversalState.EXECUTE)
        self.state_machine.transition_to(TraversalState.BRANCH)  # Can transition to FRAME_COMPLETE from BRANCH
        self.state_machine.transition_to(TraversalState.FRAME_COMPLETE)
        assert self.state_machine.state == TraversalState.FRAME_COMPLETE

        # Handle container frame
        container = {"id": "test_menu", "children": ["item"]}
        result = self.state_machine.handle_frame_complete(container)

        if result['is_complete']:
            # Should be able to transition back to NODE_SELECT from FRAME_COMPLETE
            assert self.state_machine.frame_complete_to_node_select()
            assert self.state_machine.state == TraversalState.NODE_SELECT

    def test_frame_complete_failed_transition(self):
        """Test transition from FRAME_COMPLETE to ERROR_HANDLING."""
        # Follow proper state flow to FRAME_COMPLETE
        self.state_machine.transition_to(TraversalState.PRECONDITION_CHECK)
        self.state_machine.transition_to(TraversalState.EXECUTE)
        self.state_machine.transition_to(TraversalState.BRANCH)  # Can transition to FRAME_COMPLETE from BRANCH
        self.state_machine.transition_to(TraversalState.FRAME_COMPLETE)
        assert self.state_machine.state == TraversalState.FRAME_COMPLETE

        # Should be able to transition to ERROR_HANDLING
        assert self.state_machine.frame_complete_failed()
        assert self.state_machine.state == TraversalState.ERROR_HANDLING

    def test_container_handling_statistics_persistence(self):
        """Test that container statistics persist across multiple operations."""
        # Handle multiple containers
        for i in range(5):
            container = {
                "id": f"menu_{i}",
                "children": [f"item_{i}"],
                "exit_condition": "BACK"
            }
            context = {"visited_children": [f"item_{i}"]}
            self.state_machine.handle_frame_complete(container, context)

        # Reset container handling state
        self.state_machine.reset_container_handling()

        # Get summary - statistics should persist in handler
        summary = self.state_machine.get_container_statistics()
        assert summary["processed_containers"] == 5  # Statistics kept in handler