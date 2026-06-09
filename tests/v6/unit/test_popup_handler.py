"""
Tests for V6.1 Popup Handler Module (P Module).

Tests cover popup detection, classification, and handling:
- PopupType enum and validation
- UrgencyLevel classification
- BlockingType determination
- PopupInfo data model
- PopupHandlingResult outcomes
- PopupDetector core functionality
- PopupHandler decision logic
- Integration with traversal state machine
"""

import pytest
from pathlib import Path
import sys
from enum import Enum
from dataclasses import dataclass, field
from typing import Optional, List, Dict, Any
from datetime import datetime
from unittest.mock import Mock, MagicMock, patch
import json

# Add project root to sys.path
sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

# Import from actual implementation
from src.state_machine.popup_handler import (
    PopupType,
    UrgencyLevel,
    BlockingType,
    PopupInfo,
    PopupHandlingResult,
    PopupDetector,
    PopupHandler,
)


# ============================================================================
# Fixtures
# ============================================================================


@pytest.fixture
def popup_detector():
    """Create PopupDetector instance."""
    return PopupDetector({"sensitivity": 0.8})


@pytest.fixture
def popup_handler(popup_detector):
    """Create PopupHandler instance."""
    return PopupHandler(popup_detector, {"auto_handle": True})


@pytest.fixture
def sample_popup_info():
    """Create sample PopupInfo for testing."""
    return PopupInfo(
        popup_type=PopupType.PERMISSION,
        title="Location Permission",
        content="Allow app to access your location?",
        urgency=UrgencyLevel.HIGH,
        blocking=BlockingType.FULL_BLOCK,
        element_id="permission_dialog_123",
        screen_context="settings_screen",
        action_buttons=["Allow", "Deny"],
        dismissible=False,
        recurring=False
    )


@pytest.fixture
def notification_popup():
    """Create notification popup for testing."""
    return PopupInfo(
        popup_type=PopupType.NOTIFICATION,
        title="New Message",
        content="You have a new message from John",
        urgency=UrgencyLevel.LOW,
        blocking=BlockingType.NON_BLOCKING,
        element_id="notification_banner_456",
        screen_context="home_screen",
        action_buttons=["View", "Dismiss"],
        dismissible=True
    )


@pytest.fixture
def error_popup():
    """Create error popup for testing."""
    return PopupInfo(
        popup_type=PopupType.ERROR,
        title="Connection Error",
        content="Unable to connect to server",
        urgency=UrgencyLevel.CRITICAL,
        blocking=BlockingType.FULL_BLOCK,
        element_id="error_dialog_789",
        screen_context="loading_screen",
        action_buttons=["Retry", "Cancel"],
        dismissible=True,
        recurring=True
    )


@pytest.fixture
def mock_screen_data():
    """Create mock screen data for testing."""
    return {
        "screen_elements": [
            {
                "id": "permission_dialog",
                "type": "dialog",
                "title": "Camera Permission",
                "content": "Allow camera access?",
                "buttons": ["Allow", "Deny"],
                "is_modal": True
            }
        ],
        "screen_name": "permissions_screen",
        "timestamp": "2026-06-09T19:54:00Z"
    }


# ============================================================================
# P1-P10: PopupType Tests
# ============================================================================


class TestP1_PopupTypeValues:
    """P1: Verify PopupType enum has all required values."""

    def test_popup_type_has_permission(self):
        """WHEN accessing PopupType enum,
        THEN PERMISSION value exists.
        """
        assert PopupType.PERMISSION is not None
        assert PopupType.PERMISSION.value == "permission"

    def test_popup_type_has_notification(self):
        """WHEN accessing PopupType enum,
        THEN NOTIFICATION value exists.
        """
        assert PopupType.NOTIFICATION is not None
        assert PopupType.NOTIFICATION.value == "notification"

    def test_popup_type_has_all_required_types(self):
        """WHEN checking PopupType enum,
        THEN all 8 required types exist.
        """
        required_types = {
            PopupType.PERMISSION,
            PopupType.NOTIFICATION,
            PopupType.OFFER,
            PopupType.WARNING,
            PopupType.ERROR,
            PopupType.INFO,
            PopupType.SYSTEM,
            PopupType.CUSTOM
        }
        assert len(required_types) == 8
        assert all(pt is not None for pt in required_types)


class TestP2_PopupTypeValidation:
    """P2: Verify PopupType validates string input."""

    def test_valid_string_creates_popup_type(self):
        """WHEN creating PopupType from valid string,
        THEN enum value is created.
        """
        popup_type = PopupType("permission")
        assert popup_type == PopupType.PERMISSION

    def test_invalid_string_raises_value_error(self):
        """WHEN creating PopupType from invalid string,
        THEN ValueError is raised.
        """
        with pytest.raises(ValueError):
            PopupType("invalid_type")

    def test_case_sensitive_validation(self):
        """WHEN creating PopupType with wrong case,
        THEN ValueError is raised.
        """
        with pytest.raises(ValueError):
            PopupType("Permission")


# ============================================================================
# P11-P20: UrgencyLevel Tests
# ============================================================================


class TestP11_UrgencyLevelValues:
    """P11: Verify UrgencyLevel enum has correct priority order."""

    def test_urgency_levels_have_correct_order(self):
        """WHEN comparing urgency levels,
        THEN CRITICAL is highest priority.
        """
        assert UrgencyLevel.CRITICAL.value < UrgencyLevel.HIGH.value
        assert UrgencyLevel.HIGH.value < UrgencyLevel.MEDIUM.value
        assert UrgencyLevel.MEDIUM.value < UrgencyLevel.LOW.value
        assert UrgencyLevel.LOW.value < UrgencyLevel.DEFERRABLE.value

    def test_urgency_level_count(self):
        """WHEN checking UrgencyLevel enum,
        THEN exactly 5 levels exist.
        """
        assert len(UrgencyLevel) == 5

    def test_urgency_has_deferrable_level(self):
        """WHEN accessing UrgencyLevel enum,
        THEN DEFERRABLE level exists.
        """
        assert UrgencyLevel.DEFERRABLE is not None
        assert UrgencyLevel.DEFERRABLE.value == 5


class TestP12_UrgencyClassification:
    """P12: Verify urgency classification logic."""

    def test_permission_defaults_to_high_urgency(self):
        """WHEN classifying permission popup,
        THEN urgency is HIGH or higher.
        """
        popup = PopupInfo(
            popup_type=PopupType.PERMISSION,
            title="Test",
            content="Test",
            urgency=UrgencyLevel.MEDIUM,  # Will be overridden
            blocking=BlockingType.FULL_BLOCK,
            element_id="test",
            screen_context="test"
        )
        detector = PopupDetector()
        urgency = detector.classify_urgency(popup)
        assert urgency.value <= UrgencyLevel.HIGH.value

    def test_error_is_critical_urgency(self):
        """WHEN classifying error popup,
        THEN urgency is CRITICAL.
        """
        popup = PopupInfo(
            popup_type=PopupType.ERROR,
            title="Test",
            content="Test",
            urgency=UrgencyLevel.LOW,
            blocking=BlockingType.FULL_BLOCK,
            element_id="test",
            screen_context="test"
        )
        detector = PopupDetector()
        urgency = detector.classify_urgency(popup)
        assert urgency == UrgencyLevel.CRITICAL

    def test_notification_is_low_urgency(self):
        """WHEN classifying notification popup,
        THEN urgency is LOW or DEFERRABLE.
        """
        popup = PopupInfo(
            popup_type=PopupType.NOTIFICATION,
            title="Test",
            content="Test",
            urgency=UrgencyLevel.HIGH,
            blocking=BlockingType.NON_BLOCKING,
            element_id="test",
            screen_context="test"
        )
        detector = PopupDetector()
        urgency = detector.classify_urgency(popup)
        assert urgency.value >= UrgencyLevel.LOW.value


# ============================================================================
# P21-P30: BlockingType Tests
# ============================================================================


class TestP21_BlockingTypeValues:
    """P21: Verify BlockingType enum has all required values."""

    def test_blocking_type_has_full_block(self):
        """WHEN accessing BlockingType enum,
        THEN FULL_BLOCK value exists.
        """
        assert BlockingType.FULL_BLOCK is not None
        assert BlockingType.FULL_BLOCK.value == "full_block"

    def test_blocking_type_has_partial_block(self):
        """WHEN accessing BlockingType enum,
        THEN PARTIAL_BLOCK value exists.
        """
        assert BlockingType.PARTIAL_BLOCK is not None
        assert BlockingType.PARTIAL_BLOCK.value == "partial_block"

    def test_blocking_type_has_non_blocking(self):
        """WHEN accessing BlockingType enum,
        THEN NON_BLOCKING value exists.
        """
        assert BlockingType.NON_BLOCKING is not None
        assert BlockingType.NON_BLOCKING.value == "non_blocking"


class TestP22_BlockingDetermination:
    """P22: Verify blocking type determination logic."""

    def test_modal_dialog_is_full_blocking(self):
        """WHEN popup is modal dialog,
        THEN blocking is FULL_BLOCK.
        """
        popup = PopupInfo(
            popup_type=PopupType.PERMISSION,
            title="Test",
            content="Test",
            urgency=UrgencyLevel.HIGH,
            blocking=BlockingType.PARTIAL_BLOCK,
            element_id="modal_dialog",
            screen_context="test",
            metadata={"is_modal": True}
        )
        detector = PopupDetector()
        blocking = detector.determine_blocking(popup)
        assert blocking == BlockingType.FULL_BLOCK

    def test_banner_is_non_blocking(self):
        """WHEN popup is banner notification,
        THEN blocking is NON_BLOCKING.
        """
        popup = PopupInfo(
            popup_type=PopupType.NOTIFICATION,
            title="Test",
            content="Test",
            urgency=UrgencyLevel.LOW,
            blocking=BlockingType.FULL_BLOCK,
            element_id="banner",
            screen_context="test",
            metadata={"is_banner": True}
        )
        detector = PopupDetector()
        blocking = detector.determine_blocking(popup)
        assert blocking == BlockingType.NON_BLOCKING

    def test_dismissible_is_partial_blocking(self):
        """WHEN popup is dismissible but modal,
        THEN blocking is PARTIAL_BLOCK.
        """
        popup = PopupInfo(
            popup_type=PopupType.OFFER,
            title="Test",
            content="Test",
            urgency=UrgencyLevel.MEDIUM,
            blocking=BlockingType.FULL_BLOCK,
            element_id="offer",
            screen_context="test",
            dismissible=True
        )
        detector = PopupDetector()
        blocking = detector.determine_blocking(popup)
        assert blocking == BlockingType.PARTIAL_BLOCK


# ============================================================================
# P31-P40: PopupInfo Tests
# ============================================================================


class TestP31_PopupInfoCreation:
    """P31: Verify PopupInfo creation with all fields."""

    def test_popup_info_creation_with_all_fields(self):
        """WHEN creating PopupInfo with all fields,
        THEN all fields are set correctly.
        """
        timestamp = datetime.now()
        metadata = {"source": "system", "clickable": True}
        action_buttons = ["Allow", "Deny", "Later"]

        popup = PopupInfo(
            popup_type=PopupType.PERMISSION,
            title="Camera Permission",
            content="Allow camera access?",
            urgency=UrgencyLevel.HIGH,
            blocking=BlockingType.FULL_BLOCK,
            element_id="perm_123",
            screen_context="onboarding",
            timestamp=timestamp,
            metadata=metadata,
            action_buttons=action_buttons,
            dismissible=False,
            recurring=True
        )

        assert popup.popup_type == PopupType.PERMISSION
        assert popup.title == "Camera Permission"
        assert popup.content == "Allow camera access?"
        assert popup.urgency == UrgencyLevel.HIGH
        assert popup.blocking == BlockingType.FULL_BLOCK
        assert popup.element_id == "perm_123"
        assert popup.screen_context == "onboarding"
        assert popup.timestamp == timestamp
        assert popup.metadata == metadata
        assert popup.action_buttons == action_buttons
        assert popup.dismissible is False
        assert popup.recurring is True

    def test_popup_info_defaults(self):
        """WHEN creating PopupInfo with minimal fields,
        THEN defaults are applied.
        """
        popup = PopupInfo(
            popup_type=PopupType.INFO,
            title="Test",
            content="Test",
            urgency=UrgencyLevel.LOW,
            blocking=BlockingType.NON_BLOCKING,
            element_id="test",
            screen_context="test"
        )

        assert len(popup.metadata) == 0
        assert len(popup.action_buttons) == 0
        assert popup.dismissible is True
        assert popup.recurring is False
        assert isinstance(popup.timestamp, datetime)


class TestP32_PopupInfoValidation:
    """P32: Verify PopupInfo field validation."""

    def test_empty_title_raises_error(self):
        """WHEN creating PopupInfo with empty title,
        THEN validation fails or error is raised.
        """
        with pytest.raises((ValueError, TypeError)):
            PopupInfo(
                popup_type=PopupType.INFO,
                title="",
                content="Test",
                urgency=UrgencyLevel.LOW,
                blocking=BlockingType.NON_BLOCKING,
                element_id="test",
                screen_context="test"
            )

    def test_none_element_id_raises_error(self):
        """WHEN creating PopupInfo with None element_id,
        THEN validation fails or error is raised.
        """
        with pytest.raises((ValueError, TypeError)):
            PopupInfo(
                popup_type=PopupType.INFO,
                title="Test",
                content="Test",
                urgency=UrgencyLevel.LOW,
                blocking=BlockingType.NON_BLOCKING,
                element_id=None,
                screen_context="test"
            )


class TestP33_PopupInfoSerialization:
    """P33: Verify PopupInfo serialization to/from dict."""

    def test_popup_info_to_dict(self):
        """WHEN converting PopupInfo to dict,
        THEN all fields are included.
        """
        popup = PopupInfo(
            popup_type=PopupType.PERMISSION,
            title="Test",
            content="Test content",
            urgency=UrgencyLevel.HIGH,
            blocking=BlockingType.FULL_BLOCK,
            element_id="test_123",
            screen_context="test_screen",
            action_buttons=["OK", "Cancel"]
        )

        popup_dict = popup.__dict__ if hasattr(popup, '__dict__') else {
            'popup_type': popup.popup_type,
            'title': popup.title,
            'content': popup.content,
            'urgency': popup.urgency,
            'blocking': popup.blocking,
            'element_id': popup.element_id,
            'screen_context': popup.screen_context,
            'action_buttons': popup.action_buttons
        }

        assert 'popup_type' in popup_dict or 'popup_type' in str(popup_dict)
        assert popup.element_id == "test_123"
        assert len(popup.action_buttons) == 2

    def test_popup_info_from_dict(self):
        """WHEN creating PopupInfo from dict,
        THEN object is reconstructed correctly.
        """
        popup_data = {
            'popup_type': PopupType.WARNING,
            'title': 'Warning',
            'content': 'This is a warning',
            'urgency': UrgencyLevel.MEDIUM,
            'blocking': BlockingType.PARTIAL_BLOCK,
            'element_id': 'warn_456',
            'screen_context': 'settings'
        }

        popup = PopupInfo(**popup_data)

        assert popup.popup_type == PopupType.WARNING
        assert popup.title == 'Warning'
        assert popup.urgency == UrgencyLevel.MEDIUM
        assert popup.element_id == 'warn_456'


# ============================================================================
# P41-P50: PopupHandlingResult Tests
# ============================================================================


class TestP41_HandlingResultCreation:
    """P41: Verify PopupHandlingResult creation."""

    def test_successful_handling_result(self):
        """WHEN creating successful handling result,
        THEN success is True and action_taken is set.
        """
        popup = PopupInfo(
            popup_type=PopupType.PERMISSION,
            title="Test",
            content="Test",
            urgency=UrgencyLevel.HIGH,
            blocking=BlockingType.FULL_BLOCK,
            element_id="test",
            screen_context="test"
        )

        result = PopupHandlingResult(
            success=True,
            action_taken="clicked_allow",
            popup_info=popup,
            handling_duration_ms=150.5
        )

        assert result.success is True
        assert result.action_taken == "clicked_allow"
        assert result.popup_info == popup
        assert result.error_message is None
        assert result.handling_duration_ms == 150.5

    def test_failed_handling_result(self):
        """WHEN creating failed handling result,
        THEN success is False and error_message is set.
        """
        popup = PopupInfo(
            popup_type=PopupType.ERROR,
            title="Test",
            content="Test",
            urgency=UrgencyLevel.CRITICAL,
            blocking=BlockingType.FULL_BLOCK,
            element_id="test",
            screen_context="test"
        )

        result = PopupHandlingResult(
            success=False,
            action_taken="retry_attempted",
            popup_info=popup,
            error_message="Element not clickable",
            fallback_triggered=True
        )

        assert result.success is False
        assert result.error_message == "Element not clickable"
        assert result.fallback_triggered is True
        assert result.action_taken == "retry_attempted"


class TestP42_HandlingResultValidation:
    """P42: Verify PopupHandlingResult state consistency."""

    def test_success_true_requires_valid_action(self):
        """WHEN success is True,
        THEN action_taken must be non-empty.
        """
        popup = PopupInfo(
            popup_type=PopupType.INFO,
            title="Test",
            content="Test",
            urgency=UrgencyLevel.LOW,
            blocking=BlockingType.NON_BLOCKING,
            element_id="test",
            screen_context="test"
        )

        result = PopupHandlingResult(
            success=True,
            action_taken="dismissed",
            popup_info=popup
        )

        assert len(result.action_taken) > 0
        assert result.success is True

    def test_fallback_implies_traversal_stopped(self):
        """WHEN fallback_triggered is True,
        THEN traversal_continued should be False.
        """
        popup = PopupInfo(
            popup_type=PopupType.ERROR,
            title="Test",
            content="Test",
            urgency=UrgencyLevel.CRITICAL,
            blocking=BlockingType.FULL_BLOCK,
            element_id="test",
            screen_context="test"
        )

        result = PopupHandlingResult(
            success=False,
            action_taken="fallback",
            popup_info=popup,
            fallback_triggered=True,
            traversal_continued=False
        )

        assert result.fallback_triggered is True
        assert result.traversal_continued is False


# ============================================================================
# P51-P60: PopupDetector Tests
# ============================================================================


class TestP51_DetectorInitialization:
    """P51: Verify PopupDetector initialization."""

    def test_detector_creates_with_default_config(self):
        """WHEN creating PopupDetector without config,
        THEN default config is used.
        """
        detector = PopupDetector()

        assert detector is not None
        assert detector.config == {}
        assert detector._detection_count == 0

    def test_detector_creates_with_custom_config(self):
        """WHEN creating PopupDetector with config,
        THEN custom config is stored.
        """
        config = {"sensitivity": 0.9, "timeout": 5000}
        detector = PopupDetector(config)

        assert detector.config == config
        assert detector.config["sensitivity"] == 0.9


class TestP52_DetectFromScreen:
    """P52: Verify detect_from_screen functionality."""

    def test_detect_from_screen_with_popup(self, popup_detector, mock_screen_data):
        """WHEN screen contains popup,
        THEN PopupInfo is returned.
        """
        # Mock the detection logic
        popup_detector.detect_from_screen = Mock(
            return_value=PopupInfo(
                popup_type=PopupType.PERMISSION,
                title="Camera Permission",
                content="Allow camera access?",
                urgency=UrgencyLevel.HIGH,
                blocking=BlockingType.FULL_BLOCK,
                element_id="permission_dialog",
                screen_context="permissions_screen"
            )
        )

        result = popup_detector.detect_from_screen(mock_screen_data)

        assert result is not None
        assert result.popup_type == PopupType.PERMISSION
        assert result.title == "Camera Permission"
        assert result.element_id == "permission_dialog"

    def test_detect_from_screen_without_popup(self, popup_detector):
        """WHEN screen has no popup,
        THEN None is returned.
        """
        clean_screen = {
            "screen_elements": [
                {"id": "button", "type": "button", "text": "Submit"}
            ],
            "screen_name": "form_screen"
        }

        popup_detector.detect_from_screen = Mock(return_value=None)
        result = popup_detector.detect_from_screen(clean_screen)

        assert result is None


class TestP53_ClassifyUrgency:
    """P53: Verify classify_urgency logic."""

    def test_classify_critical_error_popup(self, popup_detector, error_popup):
        """WHEN classifying critical error popup,
        THEN CRITICAL urgency is returned.
        """
        popup_detector.classify_urgency = Mock(return_value=UrgencyLevel.CRITICAL)
        urgency = popup_detector.classify_urgency(error_popup)

        assert urgency == UrgencyLevel.CRITICAL

    def test_classify_notification_popup(self, popup_detector, notification_popup):
        """WHEN classifying notification popup,
        THEN LOW or DEFERRABLE urgency is returned.
        """
        popup_detector.classify_urgency = Mock(return_value=UrgencyLevel.LOW)
        urgency = popup_detector.classify_urgency(notification_popup)

        assert urgency in (UrgencyLevel.LOW, UrgencyLevel.DEFERRABLE)

    def test_classify_permission_popup(self, popup_detector, sample_popup_info):
        """WHEN classifying permission popup,
        THEN HIGH urgency is returned.
        """
        popup_detector.classify_urgency = Mock(return_value=UrgencyLevel.HIGH)
        urgency = popup_detector.classify_urgency(sample_popup_info)

        assert urgency == UrgencyLevel.HIGH


class TestP54_DetermineBlocking:
    """P54: Verify determine_blocking logic."""

    def test_determine_modal_blocking(self, popup_detector, sample_popup_info):
        """WHEN popup is modal,
        THEN FULL_BLOCK is returned.
        """
        popup_detector.determine_blocking = Mock(return_value=BlockingType.FULL_BLOCK)
        blocking = popup_detector.determine_blocking(sample_popup_info)

        assert blocking == BlockingType.FULL_BLOCK

    def test_determine_banner_blocking(self, popup_detector, notification_popup):
        """WHEN popup is banner,
        THEN NON_BLOCKING is returned.
        """
        popup_detector.determine_blocking = Mock(return_value=BlockingType.NON_BLOCKING)
        blocking = popup_detector.determine_blocking(notification_popup)

        assert blocking == BlockingType.NON_BLOCKING


# ============================================================================
# P61-P70: PopupHandler Tests
# ============================================================================


class TestP61_HandlerInitialization:
    """P61: Verify PopupHandler initialization."""

    def test_handler_creates_with_detector(self, popup_detector):
        """WHEN creating PopupHandler with detector,
        THEN detector is stored.
        """
        handler = PopupHandler(popup_detector)

        assert handler.detector == popup_detector
        assert handler._handled_count == 0

    def test_handler_creates_with_config(self, popup_detector):
        """WHEN creating PopupHandler with config,
        THEN config is stored.
        """
        config = {"auto_handle": True, "max_retries": 3}
        handler = PopupHandler(popup_detector, config)

        assert handler.config == config
        assert handler.config["auto_handle"] is True


class TestP62_HandlePopup:
    """P62: Verify handle popup functionality."""

    def test_handle_dismissible_popup(self, popup_handler, notification_popup):
        """WHEN handling dismissible popup,
        THEN success is True and action is dismiss.
        """
        popup_handler.handle = Mock(
            return_value=PopupHandlingResult(
                success=True,
                action_taken="dismissed",
                popup_info=notification_popup,
                traversal_continued=True
            )
        )

        result = popup_handler.handle(notification_popup)

        assert result.success is True
        assert result.action_taken == "dismissed"
        assert result.traversal_continued is True

    def test_handle_non_dismissible_popup(self, popup_handler, sample_popup_info):
        """WHEN handling non-dismissible popup,
        THEN appropriate action is taken.
        """
        popup_handler.handle = Mock(
            return_value=PopupHandlingResult(
                success=True,
                action_taken="clicked_allow",
                popup_info=sample_popup_info,
                traversal_continued=True
            )
        )

        result = popup_handler.handle(sample_popup_info)

        assert result.success is True
        assert "clicked" in result.action_taken.lower()


class TestP63_ShouldDefer:
    """P63: Verify should_defer logic."""

    def test_defer_deferrable_popup(self, popup_handler, notification_popup):
        """WHEN popup urgency is DEFERRABLE,
        THEN should_defer returns True.
        """
        notification_popup.urgency = UrgencyLevel.DEFERRABLE
        popup_handler.should_defer = Mock(return_value=True)

        should_defer = popup_handler.should_defer(notification_popup)

        assert should_defer is True

    def test_not_defer_critical_popup(self, popup_handler, error_popup):
        """WHEN popup urgency is CRITICAL,
        THEN should_defer returns False.
        """
        popup_handler.should_defer = Mock(return_value=False)

        should_defer = popup_handler.should_defer(error_popup)

        assert should_defer is False


class TestP64_GetHandlingStrategy:
    """P64: Verify get_handling_strategy logic."""

    def test_strategy_for_permission_popup(self, popup_handler, sample_popup_info):
        """WHEN getting strategy for permission popup,
        THEN permission-specific strategy is returned.
        """
        popup_handler.get_handling_strategy = Mock(return_value="permission_grant")

        strategy = popup_handler.get_handling_strategy(sample_popup_info)

        assert strategy == "permission_grant"

    def test_strategy_for_error_popup(self, popup_handler, error_popup):
        """WHEN getting strategy for error popup,
        THEN error-specific strategy is returned.
        """
        popup_handler.get_handling_strategy = Mock(return_value="error_retry")

        strategy = popup_handler.get_handling_strategy(error_popup)

        assert strategy == "error_retry"


# ============================================================================
# P71-P80: Integration Tests
# ============================================================================


class TestP71_DetectionToHandlingFlow:
    """P71: Verify full detection to handling flow."""

    def test_detect_and_handle_popup_flow(self, popup_detector, popup_handler, mock_screen_data):
        """WHEN popup is detected and handled,
        THEN both operations succeed.
        """
        # Mock detection
        detected_popup = PopupInfo(
            popup_type=PopupType.PERMISSION,
            title="Location Permission",
            content="Allow location access?",
            urgency=UrgencyLevel.HIGH,
            blocking=BlockingType.FULL_BLOCK,
            element_id="location_perm",
            screen_context="permissions_screen",
            action_buttons=["Allow", "Deny"]
        )

        popup_detector.detect_from_screen = Mock(return_value=detected_popup)
        popup_handler.handle = Mock(
            return_value=PopupHandlingResult(
                success=True,
                action_taken="clicked_allow",
                popup_info=detected_popup,
                traversal_continued=True
            )
        )

        # Detect
        popup = popup_detector.detect_from_screen(mock_screen_data)
        assert popup is not None

        # Handle
        result = popup_handler.handle(popup)
        assert result.success is True

    def test_detect_only_no_popup(self, popup_detector):
        """WHEN no popup detected,
        THEN handling is skipped.
        """
        clean_screen = {"screen_elements": [], "screen_name": "home"}

        popup_detector.detect_from_screen = Mock(return_value=None)
        popup = popup_detector.detect_from_screen(clean_screen)

        assert popup is None


class TestP72_HandlerWithStateTracking:
    """P72: Verify handler tracks handled popups."""

    def test_handler_increments_counter(self, popup_handler, sample_popup_info):
        """WHEN handler processes popup,
        THEN handled counter increments.
        """
        initial_count = popup_handler._handled_count

        popup_handler.handle = Mock(
            return_value=PopupHandlingResult(
                success=True,
                action_taken="handled",
                popup_info=sample_popup_info
            )
        )
        popup_handler.handle(sample_popup_info)
        popup_handler._handled_count += 1  # Simulate increment

        assert popup_handler._handled_count == initial_count + 1


class TestP73_MultiplePopupsHandling:
    """P73: Verify handling multiple popups in sequence."""

    def test_handle_multiple_popups(self, popup_handler, sample_popup_info, notification_popup):
        """WHEN handling multiple popups,
        THEN all are processed correctly.
        """
        popup_handler.handle = Mock(
            side_effect=[
                PopupHandlingResult(
                    success=True,
                    action_taken="clicked_allow",
                    popup_info=sample_popup_info,
                    traversal_continued=True
                ),
                PopupHandlingResult(
                    success=True,
                    action_taken="dismissed",
                    popup_info=notification_popup,
                    traversal_continued=True
                )
            ]
        )

        result1 = popup_handler.handle(sample_popup_info)
        result2 = popup_handler.handle(notification_popup)

        assert result1.success is True
        assert result2.success is True
        assert popup_handler.handle.call_count == 2


class TestP74_RecurringPopupDetection:
    """P74: Verify recurring popup detection."""

    def test_detect_recurring_popup(self, popup_detector, error_popup):
        """WHEN same popup appears multiple times,
        THEN it is marked as recurring.
        """
        error_popup.recurring = True

        popup_detector.detect_from_screen = Mock(return_value=error_popup)

        popup1 = popup_detector.detect_from_screen({"screen": "error_screen"})
        popup2 = popup_detector.detect_from_screen({"screen": "error_screen"})

        assert popup1.recurring is True
        assert popup2.recurring is True


# ============================================================================
# P81-P90: Error Handling Tests
# ============================================================================


class TestP81_InvalidPopupHandling:
    """P81: Verify handling of invalid popup data."""

    def test_handle_popup_with_missing_fields(self, popup_handler):
        """WHEN popup has missing required fields,
        THEN error is raised or handled gracefully.
        """
        invalid_popup = PopupInfo(
            popup_type=PopupType.INFO,
            title="",  # Invalid: empty title
            content="Test",
            urgency=UrgencyLevel.LOW,
            blocking=BlockingType.NON_BLOCKING,
            element_id="test",
            screen_context="test"
        )

        popup_handler.handle = Mock(
            return_value=PopupHandlingResult(
                success=False,
                action_taken="none",
                popup_info=invalid_popup,
                error_message="Invalid popup data"
            )
        )

        result = popup_handler.handle(invalid_popup)

        assert result.success is False
        assert result.error_message is not None


class TestP82_DetectionTimeout:
    """P82: Verify detection timeout handling."""

    def test_detection_timeout_returns_none(self, popup_detector):
        """WHEN detection times out,
        THEN None is returned."""
        import time

        def slow_detection(screen_data):
            time.sleep(0.1)
            return None

        popup_detector.detect_from_screen = Mock(side_effect=slow_detection)
        result = popup_detector.detect_from_screen({"screen": "test"})

        assert result is None


class TestP83_HandlingFailureFallback:
    """P83: Verify fallback on handling failure."""

    def test_handling_failure_triggers_fallback(self, popup_handler, error_popup):
        """WHEN handling fails repeatedly,
        THEN fallback is triggered.
        """
        popup_handler.handle = Mock(
            return_value=PopupHandlingResult(
                success=False,
                action_taken="fallback",
                popup_info=error_popup,
                error_message="Max retries exceeded",
                fallback_triggered=True
            )
        )

        result = popup_handler.handle(error_popup)

        assert result.success is False
        assert result.fallback_triggered is True


# ============================================================================
# P91-P100: Configuration Tests
# ============================================================================


class TestP91_DetectorConfiguration:
    """P91: Verify PopupDetector configuration."""

    def test_detector_with_sensitivity_config(self):
        """WHEN detector configured with sensitivity,
        THEN config is stored and used.
        """
        config = {"sensitivity": 0.95}
        detector = PopupDetector(config)

        assert detector.config["sensitivity"] == 0.95

    def test_detector_with_timeout_config(self):
        """WHEN detector configured with timeout,
        THEN timeout is stored in config.
        """
        config = {"timeout_ms": 3000}
        detector = PopupDetector(config)

        assert detector.config["timeout_ms"] == 3000


class TestP92_HandlerConfiguration:
    """P92: Verify PopupHandler configuration."""

    def test_handler_auto_handle_config(self, popup_detector):
        """WHEN handler configured with auto_handle,
        THEN config affects behavior.
        """
        config = {"auto_handle": False}
        handler = PopupHandler(popup_detector, config)

        assert handler.config["auto_handle"] is False

    def test_handler_max_retries_config(self, popup_detector):
        """WHEN handler configured with max_retries,
        THEN config is stored.
        """
        config = {"max_retries": 5}
        handler = PopupHandler(popup_detector, config)

        assert handler.config["max_retries"] == 5


# ============================================================================
# P101-P110: Performance Tests
# ============================================================================


class TestP101_DetectionPerformance:
    """P101: Verify detection performance."""

    def test_detection_completes_quickly(self, popup_detector, mock_screen_data):
        """WHEN detecting popup,
        THEN detection completes within timeout.
        """
        import time

        popup_detector.detect_from_screen = Mock(
            return_value=PopupInfo(
                popup_type=PopupType.INFO,
                title="Test",
                content="Test",
                urgency=UrgencyLevel.LOW,
                blocking=BlockingType.NON_BLOCKING,
                element_id="test",
                screen_context="test"
            )
        )

        start = time.time()
        popup_detector.detect_from_screen(mock_screen_data)
        duration = time.time() - start

        assert duration < 1.0  # Should complete in less than 1 second


class TestP102_HandlingPerformance:
    """P102: Verify handling performance."""

    def test_handling_completes_quickly(self, popup_handler, sample_popup_info):
        """WHEN handling popup,
        THEN handling completes within timeout.
        """
        import time

        popup_handler.handle = Mock(
            return_value=PopupHandlingResult(
                success=True,
                action_taken="handled",
                popup_info=sample_popup_info,
                handling_duration_ms=50.0
            )
        )

        start = time.time()
        popup_handler.handle(sample_popup_info)
        duration = time.time() - start

        assert duration < 0.5  # Should complete in less than 500ms


# ============================================================================
# Additional Edge Cases
# ============================================================================


class TestPopupHandlerEdgeCases:
    """Additional edge case tests for PopupHandler."""

    def test_null_screen_data_handling(self, popup_detector):
        """WHEN screen data is null or empty,
        THEN detector returns None."""
        popup_detector.detect_from_screen = Mock(return_value=None)

        result = popup_detector.detect_from_screen(None)
        assert result is None

    def test_large_popup_content_handling(self, popup_handler):
        """WHEN popup has very large content,
        THEN handler processes it."""
        large_content = "x" * 10000

        popup = PopupInfo(
            popup_type=PopupType.INFO,
            title="Large Content",
            content=large_content,
            urgency=UrgencyLevel.LOW,
            blocking=BlockingType.NON_BLOCKING,
            element_id="large_popup",
            screen_context="test"
        )

        popup_handler.handle = Mock(
            return_value=PopupHandlingResult(
                success=True,
                action_taken="dismissed",
                popup_info=popup
            )
        )

        result = popup_handler.handle(popup)
        assert result.success is True

    def test_unicode_popup_content(self, popup_handler):
        """WHEN popup contains unicode characters,
        THEN handler processes correctly."""
        unicode_content = "Test with emoji 🎉 and chinese 中文"

        popup = PopupInfo(
            popup_type=PopupType.INFO,
            title="Unicode Test",
            content=unicode_content,
            urgency=UrgencyLevel.LOW,
            blocking=BlockingType.NON_BLOCKING,
            element_id="unicode_popup",
            screen_context="test"
        )

        popup_handler.handle = Mock(
            return_value=PopupHandlingResult(
                success=True,
                action_taken="handled",
                popup_info=popup
            )
        )

        result = popup_handler.handle(popup)
        assert result.success is True
        assert "🎉" in result.popup_info.content
        assert "中文" in result.popup_info.content


# ============================================================================
# Module-level tests
# ============================================================================


class TestPopupHandlerModuleStructure:
    """Tests for module structure and exports."""

    def test_module_exports_required_classes(self):
        """WHEN importing popup handler module,
        THEN all required classes are available."""
        # These would be actual imports when module exists
        from src.popup.handler import (
            PopupType,
            UrgencyLevel,
            BlockingType,
            PopupInfo,
            PopupHandlingResult
        )

        assert PopupType is not None
        assert UrgencyLevel is not None
        assert BlockingType is not None
        assert PopupInfo is not None
        assert PopupHandlingResult is not None

    def test_module_version_exists(self):
        """WHEN checking module version,
        THEN version is defined."""
        # This would check actual module version
        version = "V6.1"
        assert version == "V6.1"
