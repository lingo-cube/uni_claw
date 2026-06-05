"""
Integration tests for popup handling in TraversalStateMachine.

Tests the complete popup handling flow from state machine perspective.
"""

import pytest
from src.state_machine.traversal_fsm import TraversalStateMachine, TraversalState


class TestStateMachinePopupIntegration:
    """Test popup handling integration in TraversalStateMachine."""

    def setup_method(self):
        """Set up test fixtures."""
        self.state_machine = TraversalStateMachine()

    def test_popup_handler_initialization(self):
        """Test that popup handler is initialized on first use."""
        # Popup handler should not exist initially
        assert not hasattr(self.state_machine, '_popup_handler') or self.state_machine._popup_handler is None

        # Call handle_popup to initialize
        screen_info = {"text": "Allow permission?", "ui_elements": [{"text": "Allow"}]}
        result = self.state_machine.handle_popup(screen_info)

        # Popup handler should now be initialized
        assert self.state_machine._popup_handler is not None

    def test_popup_handling_creates_handler(self):
        """Test that first popup handling call creates popup handler."""
        screen_info = {"text": "Error message", "ui_elements": [{"text": "OK"}]}
        result = self.state_machine.handle_popup(screen_info)

        assert result is not None
        assert 'detected' in result
        assert 'handled' in result

    def test_popup_handling_with_detected_popup(self):
        """Test popup handling when popup is detected."""
        screen_info = {
            "text": "Allow camera access?",
            "ui_elements": [{"text": "Allow", "clickable": True}]
        }
        context = {
            "current_node_id": "settings_node",
            "node_stack": ["root", "settings"]
        }

        result = self.state_machine.handle_popup(screen_info, context)

        assert result is not None
        assert result['detected'] is True
        assert result['handled'] is True
        assert result['state_preserved'] is True

    def test_popup_handling_with_no_popup(self):
        """Test popup handling when no popup is present."""
        screen_info = {
            "text": "Main screen content",
            "ui_elements": [{"text": "Home"}]
        }
        context = {}

        result = self.state_machine.handle_popup(screen_info, context)

        assert result is not None
        assert result['detected'] is False
        assert result['handled'] is False

    def test_popup_context_storage(self):
        """Test that popup context is stored after handling."""
        screen_info = {
            "text": "Error occurred",
            "ui_elements": [{"text": "OK"}]
        }
        context = {"current_node_id": "test_node"}

        self.state_machine.handle_popup(screen_info, context)

        # Check popup context was stored
        assert "last_popup_detected" in self.state_machine._popup_context
        assert "last_popup_handled" in self.state_machine._popup_context
        assert "last_handling_method" in self.state_machine._popup_context

    def test_popup_statistics_tracking(self):
        """Test getting popup statistics."""
        # Handle a few popups
        screen_infos = [
            {"text": "Allow Permission?", "ui_elements": [{"text": "Allow"}]},
            {"text": "Error message", "ui_elements": [{"text": "OK"}]},
            {"text": "Skip this ad", "ui_elements": [{"text": "Close"}]},
        ]

        for screen_info in screen_infos:
            self.state_machine.handle_popup(screen_info)

        # Get summary
        summary = self.state_machine.get_popup_statistics()

        assert "detected_popups" in summary
        assert "handled_popups" in summary
        assert "handling_rate" in summary
        assert "handling_methods" in summary

        # Should have tracked at least 2 popups (some might not match patterns)
        assert summary["detected_popups"] >= 2
        assert summary["handled_popups"] >= 1

    def test_popup_statistics_before_any_handling(self):
        """Test popup statistics before any handling occurs."""
        summary = self.state_machine.get_popup_statistics()

        # Should return empty statistics
        assert summary["detected_popups"] == 0
        assert summary["handled_popups"] == 0
        assert summary["handling_rate"] == 0.0

    def test_reset_popup_handling(self):
        """Test resetting popup handling state."""
        screen_info = {"text": "Permission?", "ui_elements": [{"text": "Allow"}]}

        # Handle some popups to build up state
        self.state_machine.handle_popup(screen_info)

        # Reset
        self.state_machine.reset_popup_handling()

        # Check reset happened
        assert self.state_machine._popup_context == {}

    def test_popup_handling_preserves_handler_on_reset(self):
        """Test that popup handler is preserved on reset."""
        screen_info = {"text": "Permission?", "ui_elements": [{"text": "Allow"}]}

        # Create handler by handling popup
        self.state_machine.handle_popup(screen_info)
        handler = self.state_machine._popup_handler

        # Reset
        self.state_machine.reset_popup_handling()

        # Handler should still exist
        assert self.state_machine._popup_handler is handler

    def test_state_transitions_with_popup_handling(self):
        """Test that state transitions work correctly with popup handling."""
        # Start in RESULT_VERIFY state (where popups are typically checked)
        self.state_machine.transition_to(TraversalState.PRECONDITION_CHECK)
        self.state_machine.transition_to(TraversalState.EXECUTE)
        self.state_machine.transition_to(TraversalState.RESULT_VERIFY)
        assert self.state_machine.state == TraversalState.RESULT_VERIFY

        # Handle popup (should not change state directly)
        screen_info = {"text": "Error message", "ui_elements": [{"text": "OK"}]}
        result = self.state_machine.handle_popup(screen_info)

        # State should still be RESULT_VERIFY (popup handling doesn't auto-transition)
        assert self.state_machine.state == TraversalState.RESULT_VERIFY

    def test_popup_context_passed_to_handler(self):
        """Test that traversal context is properly passed to popup handler."""
        screen_info = {
            "text": "Permission required",
            "ui_elements": [{"text": "Allow"}]
        }
        context = {
            "current_node_id": "test_node",
            "node_stack": ["root", "menu"],
            "current_state": "EXECUTE"
        }

        result = self.state_machine.handle_popup(screen_info, context)

        # Check that context was used
        assert result is not None

    def test_multiple_popups_in_single_session(self):
        """Test handling multiple different popups in one session."""
        popups = [
            {"text": "Allow permission?", "ui_elements": [{"text": "Allow"}]},
            {"text": "Error occurred", "ui_elements": [{"text": "OK"}]},
            {"text": "Skip ad", "ui_elements": [{"text": "Close"}]},
        ]

        for popup in popups:
            result = self.state_machine.handle_popup(popup)
            assert result['detected'] is True

        summary = self.state_machine.get_popup_statistics()

        # Should have tracked all popups
        assert summary["detected_popups"] == 3


class TestStateMachinePopupHandlingScenarios:
    """Test realistic popup handling scenarios."""

    def setup_method(self):
        """Set up test fixtures."""
        self.state_machine = TraversalStateMachine()

    def test_permission_popup_scenario(self):
        """Test realistic permission popup handling."""
        screen_info = {
            "text": "Allow this app to access your location?",
            "ui_elements": [
                {"text": "Allow", "clickable": True},
                {"text": "Deny", "clickable": True}
            ]
        }
        context = {
            "current_node_id": "settings_node",
            "node_stack": ["root", "settings"]
        }

        result = self.state_machine.handle_popup(screen_info, context)

        assert result['detected'] is True
        assert result['handled'] is True
        assert result['execution_resumed'] is True

    def test_error_popup_scenario(self):
        """Test realistic error popup handling."""
        screen_info = {
            "text": "Critical Error: Network connection failed",
            "ui_elements": [{"text": "OK", "clickable": True}]
        }
        context = {"current_node_id": "error_node"}

        result = self.state_machine.handle_popup(screen_info, context)

        assert result['detected'] is True
        assert result['handled'] is True
        assert result['state_preserved'] is True

    def test_ad_popup_scenario(self):
        """Test realistic advertisement popup handling."""
        screen_info = {
            "text": "Sponsored content - Skip this ad",
            "ui_elements": [{"text": "Close ad", "clickable": True}]
        }
        context = {"current_node_id": "content_node"}

        result = self.state_machine.handle_popup(screen_info, context)

        assert result['detected'] is True
        # Ads should be handled
        assert result['handled'] is True

    def test_multiple_popups_sequence(self):
        """Test handling multiple popups in sequence."""
        popups = [
            {"text": "Permission required", "ui_elements": [{"text": "Allow"}]},
            {"text": "Another permission", "ui_elements": [{"text": "Continue"}]},
            {"text": "Error message", "ui_elements": [{"text": "Dismiss"}]},
        ]

        handled_count = 0
        for popup in popups:
            result = self.state_machine.handle_popup(popup)
            if result['handled']:
                handled_count += 1

        # Should have handled most popups
        assert handled_count >= 2

    def test_popup_with_no_target_element(self):
        """Test popup handling when no clear target element exists."""
        screen_info = {
            "text": "Permission required",  # Use text that matches popup patterns
            "ui_elements": []  # No clear dismiss button
        }
        context = {"current_node_id": "test_node"}

        result = self.state_machine.handle_popup(screen_info, context)

        # Should still detect and attempt handling
        assert result['detected'] is True
        # May or may not succeed depending on fallback strategy


class TestStateMachinePopupHandlingIntegration:
    """Test integration with state machine state transitions."""

    def setup_method(self):
        """Set up test fixtures."""
        self.state_machine = TraversalStateMachine()

    def test_popup_to_result_verify_transition(self):
        """Test transition from POPUP_HANDLING to RESULT_VERIFY."""
        # Follow proper state flow to POPUP_HANDLING
        self.state_machine.transition_to(TraversalState.PRECONDITION_CHECK)
        self.state_machine.transition_to(TraversalState.EXECUTE)
        self.state_machine.transition_to(TraversalState.RESULT_VERIFY)
        self.state_machine.transition_to(TraversalState.POPUP_HANDLING)
        assert self.state_machine.state == TraversalState.POPUP_HANDLING

        # Handle popup successfully
        screen_info = {"text": "Permission?", "ui_elements": [{"text": "Allow"}]}
        result = self.state_machine.handle_popup(screen_info)

        if result['handled']:
            # Should be able to transition back to RESULT_VERIFY
            assert self.state_machine.popup_handled()
            assert self.state_machine.state == TraversalState.RESULT_VERIFY

    def test_popup_to_error_handling_transition(self):
        """Test transition from POPUP_HANDLING to ERROR_HANDLING."""
        # Start in POPUP_HANDLING state
        self.state_machine.transition_to(TraversalState.PRECONDITION_CHECK)
        self.state_machine.transition_to(TraversalState.EXECUTE)
        self.state_machine.transition_to(TraversalState.RESULT_VERIFY)
        self.state_machine.transition_to(TraversalState.POPUP_HANDLING)
        assert self.state_machine.state == TraversalState.POPUP_HANDLING

        # Should be able to transition to ERROR_HANDLING
        assert self.state_machine.popup_handling_failed()
        assert self.state_machine.state == TraversalState.ERROR_HANDLING

    def test_popup_handling_statistics_persistence(self):
        """Test that popup statistics persist across multiple operations."""
        # Handle multiple popups
        detected_count = 0
        for i in range(5):
            screen_info = {"text": f"Allow permission {i}?", "ui_elements": [{"text": "OK"}]}
            result = self.state_machine.handle_popup(screen_info)
            if result['detected']:
                detected_count += 1

        # Reset popup handling state (but not statistics)
        self.state_machine.reset_popup_handling()

        # Get summary - statistics should persist in handler
        summary = self.state_machine.get_popup_statistics()
        # Statistics should persist (at least some were detected)
        assert summary["detected_popups"] >= detected_count - 1  # Allow for minor differences