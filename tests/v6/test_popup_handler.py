"""
Unit tests for V6.1 popup handler system.

Tests popup detection, classification, and handling functionality.
"""

import pytest
from src.state_machine.popup_handler import (
    PopupType,
    UrgencyLevel,
    BlockingType,
    PopupInfo,
    PopupHandlingResult,
    PopupDetector,
    PopupClassifier,
    PopupActionHandler,
    StateRestorer,
    PopupHandler,
)


class TestPopupDetector:
    """Test popup detection functionality."""

    def setup_method(self):
        """Set up test fixtures."""
        self.detector = PopupDetector()

    def test_permission_popup_detection(self):
        """Test detection of permission popups."""
        screen_info = {
            "text": "Allow this app to access your location?",
            "ui_elements": []
        }

        detected = self.detector.detect_popup(screen_info)
        assert detected is True

    def test_error_popup_detection(self):
        """Test detection of error popups."""
        screen_info = {
            "text": "Error: Network connection failed",
            "ui_elements": []
        }

        detected = self.detector.detect_popup(screen_info)
        assert detected is True

    def test_ad_popup_detection(self):
        """Test detection of advertisement popups."""
        screen_info = {
            "text": "Skip this advertisement to continue",
            "ui_elements": []
        }

        detected = self.detector.detect_popup(screen_info)
        assert detected is True

    def test_dialog_popup_detection(self):
        """Test detection of dialog popups."""
        screen_info = {
            "text": "Confirm your action",
            "ui_elements": []
        }

        detected = self.detector.detect_popup(screen_info)
        assert detected is True

    def test_no_popup_detection(self):
        """Test no false positive popup detection."""
        screen_info = {
            "text": "Welcome to the main screen",
            "ui_elements": [{"text": "Home"}]
        }

        detected = self.detector.detect_popup(screen_info)
        assert detected is False

    def test_popup_detection_in_ui_elements(self):
        """Test popup detection from UI elements."""
        screen_info = {
            "text": "Main screen",
            "ui_elements": [{"text": "Allow permission"}]
        }

        detected = self.detector.detect_popup(screen_info)
        assert detected is True


class TestPopupClassifier:
    """Test popup classification functionality."""

    def setup_method(self):
        """Set up test fixtures."""
        self.classifier = PopupClassifier()

    def test_permission_popup_classification(self):
        """Test classification of permission popups."""
        screen_info = {
            "text": "Allow camera access?",
            "ui_elements": [{"text": "Allow", "clickable": True}, {"text": "Deny", "clickable": True}]
        }

        popup_info = self.classifier.classify_popup(screen_info)

        assert popup_info.popup_type == PopupType.PERMISSION
        assert popup_info.urgency_level == UrgencyLevel.HIGH
        assert popup_info.blocking_type == BlockingType.MODAL
        assert popup_info.target_element is not None
        assert popup_info.dismiss_strategy == "auto_close"

    def test_error_popup_classification(self):
        """Test classification of error popups."""
        screen_info = {
            "text": "Critical Error: Application failed",
            "ui_elements": [{"text": "OK", "clickable": True}]
        }

        popup_info = self.classifier.classify_popup(screen_info)

        assert popup_info.popup_type == PopupType.ERROR
        assert popup_info.urgency_level == UrgencyLevel.CRITICAL
        assert popup_info.target_element is not None

    def test_ad_popup_classification(self):
        """Test classification of advertisement popups."""
        screen_info = {
            "text": "Sponsored content - Skip ad",
            "ui_elements": [{"text": "Close ad", "clickable": True}]
        }

        popup_info = self.classifier.classify_popup(screen_info)

        assert popup_info.popup_type == PopupType.AD
        assert popup_info.urgency_level == UrgencyLevel.LOW
        assert popup_info.blocking_type in [BlockingType.NON_MODAL, BlockingType.TOAST]

    def test_unknown_popup_classification(self):
        """Test classification of unknown popups."""
        screen_info = {
            "text": "Some unknown message",
            "ui_elements": [{"text": "OK"}]
        }

        popup_info = self.classifier.classify_popup(screen_info)

        assert popup_info.popup_type == PopupType.UNKNOWN
        assert popup_info.urgency_level == UrgencyLevel.MEDIUM

    def test_dismiss_target_finding(self):
        """Test finding dismiss target elements."""
        screen_info = {
            "text": "Permission required",
            "ui_elements": [
                {"text": "Allow", "clickable": True},
                {"text": "Close", "clickable": True}
            ]
        }

        popup_info = self.classifier.classify_popup(screen_info)

        assert popup_info.target_element is not None
        assert popup_info.target_element["text"] == "Allow"

    def test_confidence_calculation(self):
        """Test confidence calculation in classification."""
        screen_info = {
            "text": "Allow this permission to continue",
            "ui_elements": [{"text": "Grant permission", "clickable": True}]
        }

        popup_info = self.classifier.classify_popup(screen_info)

        assert 0.0 <= popup_info.confidence <= 1.0
        assert popup_info.confidence > 0.5  # Should have reasonable confidence


class TestStateRestorer:
    """Test state preservation and restoration."""

    def setup_method(self):
        """Set up test fixtures."""
        self.restorer = StateRestorer()

    def test_state_preservation(self):
        """Test preserving execution state."""
        context = {
            "current_node_id": "node123",
            "node_stack": ["root", "menu", "item"],
            "current_state": "EXECUTE",
            "execution_result": {"status": "success"}
        }

        state_id = self.restorer.preserve_state(context)

        assert state_id is not None
        assert state_id.startswith("state_")

    def test_state_restoration(self):
        """Test restoring preserved state."""
        original_context = {
            "current_node_id": "node456",
            "node_stack": ["root", "settings"],
            "current_state": "PRECONDITION_CHECK",
            "execution_result": {"status": "pending"}
        }

        state_id = self.restorer.preserve_state(original_context)

        # Modify context
        modified_context = {
            "current_node_id": "different",
            "node_stack": [],
            "current_state": "ERROR",
        }

        # Restore state
        restored = self.restorer.restore_state(state_id, modified_context)

        assert restored is True
        assert modified_context["current_node_id"] == "node456"
        assert modified_context["node_stack"] == ["root", "settings"]
        assert modified_context["current_state"] == "PRECONDITION_CHECK"

    def test_state_validation(self):
        """Test validation of restored state."""
        valid_context = {
            "current_node_id": "node789",
            "node_stack": ["root"],
            "current_state": "NODE_SELECT"
        }

        is_valid = self.restorer.validate_restored_state(valid_context)
        assert is_valid is True

    def test_invalid_state_validation(self):
        """Test validation fails for invalid state."""
        invalid_context = {
            "current_node_id": "node000",
            # Missing node_stack and current_state
        }

        is_valid = self.restorer.validate_restored_state(invalid_context)
        assert is_valid is False

    def test_multiple_state_preservation(self):
        """Test preserving and restoring multiple states."""
        contexts = [
            {"current_node_id": "node1", "node_stack": ["a"], "current_state": "STATE1"},
            {"current_node_id": "node2", "node_stack": ["b"], "current_state": "STATE2"},
        ]

        state_ids = []
        for context in contexts:
            state_id = self.restorer.preserve_state(context)
            state_ids.append(state_id)

        # Restore each state
        for i, state_id in enumerate(state_ids):
            restore_context = {}
            restored = self.restorer.restore_state(state_id, restore_context)
            assert restored is True
            assert restore_context["current_node_id"] == contexts[i]["current_node_id"]


class TestPopupActionHandler:
    """Test popup action execution."""

    def setup_method(self):
        """Set up test fixtures."""
        self.handler = PopupActionHandler()

    def test_auto_close_handling(self):
        """Test auto-close handling method."""
        popup_info = PopupInfo(
            popup_type=PopupType.PERMISSION,
            confidence=0.9,
            target_element={"text": "Allow", "clickable": True},
            dismiss_strategy="auto_close"
        )
        context = {}

        result = self.handler.handle_popup(popup_info, context)

        assert result['success'] is True
        assert result['method'] == 'click_dismiss_button'

    def test_back_handling(self):
        """Test back button handling method."""
        popup_info = PopupInfo(
            popup_type=PopupType.AD,
            confidence=0.8,
            target_element=None,
            dismiss_strategy="back"
        )
        context = {}

        result = self.handler.handle_popup(popup_info, context)

        assert result['success'] is True
        assert result['method'] == 'press_back'

    def test_wait_timeout_handling(self):
        """Test wait timeout handling method."""
        popup_info = PopupInfo(
            popup_type=PopupType.PERMISSION,
            confidence=0.7,
            dismiss_strategy="wait_timeout"
        )
        context = {}

        result = self.handler.handle_popup(popup_info, context)

        assert result['success'] is True
        assert result['method'] == 'wait_for_timeout'


class TestPopupHandler:
    """Test complete popup handler functionality."""

    def setup_method(self):
        """Set up test fixtures."""
        self.handler = PopupHandler()

    def test_complete_popup_handling_flow(self):
        """Test complete popup handling flow."""
        screen_info = {
            "text": "Allow camera access?",
            "ui_elements": [{"text": "Allow", "clickable": True}]
        }
        context = {
            "current_node_id": "test_node",
            "node_stack": ["root"],
            "current_state": "EXECUTE"
        }

        result = self.handler.handle_popup(screen_info, context)

        assert result.detected is True
        assert result.handled is True
        assert result.handling_method in ["click_dismiss_button", "click_dismiss_button_or_back"]
        assert result.state_preserved is True

    def test_no_popup_scenario(self):
        """Test handling when no popup is present."""
        screen_info = {
            "text": "Main screen content",
            "ui_elements": [{"text": "Home"}]
        }
        context = {}

        result = self.handler.handle_popup(screen_info, context)

        assert result.detected is False
        assert result.handled is False
        assert result.handling_time_ms >= 0

    def test_statistics_tracking(self):
        """Test that popup statistics are tracked."""
        screen_infos = [
            {"text": "Allow permission?", "ui_elements": [{"text": "Allow"}]},
            {"text": "Error occurred", "ui_elements": [{"text": "OK"}]},
        ]

        for screen_info in screen_infos:
            context = {}
            self.handler.handle_popup(screen_info, context)

        stats = self.handler.get_popup_statistics()

        assert stats["detected_popups"] == 2
        assert stats["handled_popups"] >= 1
        assert 0.0 <= stats["handling_rate"] <= 1.0

    def test_handling_rate_calculation(self):
        """Test handling rate calculation."""
        # Handle some popups
        for i in range(5):
            screen_info = {"text": "Allow permission?", "ui_elements": [{"text": "Allow"}]}
            context = {}
            self.handler.handle_popup(screen_info, context)

        rate = self.handler.handling_rate
        assert 0.0 <= rate <= 1.0


class TestPopupHandlingIntegration:
    """Integration tests for popup handling scenarios."""

    def test_permission_popup_scenario(self):
        """Test realistic permission popup handling."""
        handler = PopupHandler()

        screen_info = {
            "text": "Allow this app to access your location?",
            "ui_elements": [
                {"text": "Allow", "clickable": True},
                {"text": "Deny", "clickable": True}
            ]
        }
        context = {
            "current_node_id": "settings_node",
            "node_stack": ["root", "settings"],
            "current_state": "EXECUTE"
        }

        result = handler.handle_popup(screen_info, context)

        assert result.detected is True
        assert result.handled is True
        # State should be preserved and execution resumed
        assert result.execution_resumed is True

    def test_error_popup_scenario(self):
        """Test realistic error popup handling."""
        handler = PopupHandler()

        screen_info = {
            "text": "Critical Error: Network timeout",
            "ui_elements": [{"text": "OK", "clickable": True}]
        }
        context = {"current_node_id": "error_node"}

        result = handler.handle_popup(screen_info, context)

        assert result.detected is True
        assert result.handled is True
        assert result.state_preserved is True

    def test_ad_popup_scenario(self):
        """Test realistic advertisement popup handling."""
        handler = PopupHandler()

        screen_info = {
            "text": "Sponsored - Skip this ad",
            "ui_elements": [{"text": "Close", "clickable": True}]
        }
        context = {"current_node_id": "content_node"}

        result = handler.handle_popup(screen_info, context)

        assert result.detected is True
        # Ads should be handled with lower priority but still handled
        assert result.handled is True

    def test_multiple_popups_in_sequence(self):
        """Test handling multiple popups in sequence."""
        handler = PopupHandler()

        popups = [
            {"text": "Permission required", "ui_elements": [{"text": "Allow"}]},
            {"text": "Error message", "ui_elements": [{"text": "OK"}]},
            {"text": "Skip ad", "ui_elements": [{"text": "Close"}]},
        ]

        for popup in popups:
            context = {}
            result = handler.handle_popup(popup, context)
            assert result.detected is True

        stats = handler.get_popup_statistics()
        assert stats["detected_popups"] == 3
        assert stats["handled_popups"] == 3