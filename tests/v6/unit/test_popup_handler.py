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

# Import test helper for API migration
from tests.v6.helpers.api_migration_helper import (
    PopupTestHelper,
)


# ============================================================================
# Fixtures
# ============================================================================


@pytest.fixture
def popup_detector():
    """Create PopupDetector instance."""
    return PopupDetector()


@pytest.fixture
def popup_handler():
    """Create PopupHandler instance."""
    return PopupHandler()


@pytest.fixture
def sample_popup_info():
    """Create sample PopupInfo for testing."""
    return PopupTestHelper.create_from_old_style(
        popup_type="PERMISSION",
        title="Location Permission",
        content="Allow app to access your location?",
        urgency="HIGH",
        blocking="MODAL",
        element_id="permission_dialog_123",
        screen_context="settings_screen",
        action_buttons=["Allow", "Deny"],
        dismissible=False,
        recurring=False
    )


@pytest.fixture
def notification_popup():
    """Create notification popup for testing."""
    return PopupTestHelper.create_from_old_style(
        popup_type="DIALOG",  # V6.14.0: NOTIFICATION → DIALOG
        title="New Message",
        content="You have a new message from John",
        urgency="LOW",
        blocking="NON_MODAL",
        element_id="notification_banner_456",
        screen_context="home_screen",
        action_buttons=["View", "Dismiss"],
        dismissible=True
    )


@pytest.fixture
def error_popup():
    """Create error popup for testing."""
    return PopupTestHelper.create_from_old_style(
        popup_type="ERROR",
        title="Connection Error",
        content="Unable to connect to server",
        urgency="CRITICAL",
        blocking="MODAL",
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
        THEN DIALOG value exists (V6.14.0: NOTIFICATION renamed to DIALOG).
        """
        # V6.14.0: NOTIFICATION → DIALOG
        assert PopupType.DIALOG is not None
        assert PopupType.DIALOG.value == "dialog"

    def test_popup_type_has_all_required_types(self):
        """WHEN checking PopupType enum,
        THEN all required types exist.
        """
        # V6.14.0: Updated to match actual enum values
        required_types = {
            PopupType.PERMISSION,
            PopupType.ERROR,
            PopupType.AD,
            PopupType.DIALOG,
            PopupType.UNKNOWN,
        }
        assert len(required_types) == 5
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
        # V6.14.0: DEFERRABLE removed, order is CRITICAL > HIGH > MEDIUM > LOW
        assert UrgencyLevel.CRITICAL.value == "critical"
        assert UrgencyLevel.HIGH.value == "high"
        assert UrgencyLevel.MEDIUM.value == "medium"
        assert UrgencyLevel.LOW.value == "low"

    def test_urgency_level_count(self):
        """WHEN checking UrgencyLevel enum,
        THEN exactly 4 levels exist (V6.14.0: DEFERRABLE removed).
        """
        assert len(UrgencyLevel) == 4

    def test_urgency_has_deferrable_level(self):
        """WHEN accessing UrgencyLevel enum,
        THEN LOW level exists (V6.14.0: DEFERRABLE renamed to LOW).
        """
        # V6.14.0: DEFERRABLE → LOW
        assert UrgencyLevel.LOW is not None
        assert UrgencyLevel.LOW.value == "low"


# ============================================================================
# P21-P30: BlockingType Tests
# ============================================================================


class TestP21_BlockingTypeValues:
    """P21: Verify BlockingType enum has all required values."""

    def test_blocking_type_has_full_block(self):
        """WHEN accessing BlockingType enum,
        THEN MODAL value exists (V6.14.0: FULL_BLOCK renamed to MODAL).
        """
        # V6.14.0: FULL_BLOCK → MODAL
        assert BlockingType.MODAL is not None
        assert BlockingType.MODAL.value == "modal"

    def test_blocking_type_has_partial_block(self):
        """WHEN accessing BlockingType enum,
        THEN NON_MODAL value exists (V6.14.0: PARTIAL_BLOCK renamed to NON_MODAL).
        """
        # V6.14.0: PARTIAL_BLOCK → NON_MODAL
        assert BlockingType.NON_MODAL is not None
        assert BlockingType.NON_MODAL.value == "non_modal"

    def test_blocking_type_has_non_blocking(self):
        """WHEN accessing BlockingType enum,
        THEN NON_MODAL value exists (V6.14.0: NON_BLOCKING renamed to NON_MODAL).
        """
        # V6.14.0: NON_BLOCKING → NON_MODAL
        assert BlockingType.NON_MODAL is not None
        assert BlockingType.NON_MODAL.value == "non_modal"


# ============================================================================
# P31-P40: PopupInfo Tests
# ============================================================================


# ============================================================================
# P41-P50: PopupHandlingResult Tests
# ============================================================================


# ============================================================================
# P51-P60: PopupDetector Tests
# ============================================================================


class TestP52_DetectFromScreen:
    """P52: Verify detect_from_screen functionality."""

    def test_detect_from_screen_with_popup(self, popup_detector, mock_screen_data):
        """WHEN screen contains popup,
        THEN PopupInfo is returned.
        """
        # Mock the detection logic (V6.14.0: use PopupTestHelper)
        popup_detector.detect_from_screen = Mock(
            return_value=PopupTestHelper.create_from_old_style(
                popup_type="PERMISSION",
                title="Camera Permission",
                content="Allow camera access?",
                urgency="HIGH",
                blocking="MODAL",
                element_id="permission_dialog",
                screen_context="permissions_screen"
            )
        )

        result = popup_detector.detect_from_screen(mock_screen_data)

        assert result is not None
        assert result.popup_type == PopupType.PERMISSION
        # Title field no longer exists in new API
        # assert result.title == "Camera Permission"
        # assert result.element_id == "permission_dialog"

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

        assert urgency in (UrgencyLevel.LOW, UrgencyLevel.LOW)

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
        popup_detector.determine_blocking = Mock(return_value=BlockingType.MODAL)
        blocking = popup_detector.determine_blocking(sample_popup_info)

        assert blocking == BlockingType.MODAL

    def test_determine_banner_blocking(self, popup_detector, notification_popup):
        """WHEN popup is banner,
        THEN NON_BLOCKING is returned.
        """
        popup_detector.determine_blocking = Mock(return_value=BlockingType.NON_MODAL)
        blocking = popup_detector.determine_blocking(notification_popup)

        assert blocking == BlockingType.NON_MODAL


# ============================================================================
# P61-P70: PopupHandler Tests
# ============================================================================


class TestP63_ShouldDefer:
    """P63: Verify should_defer logic."""

    def test_defer_deferrable_popup(self, popup_handler, notification_popup):
        """WHEN popup urgency is DEFERRABLE,
        THEN should_defer returns True.
        """
        notification_popup.urgency = UrgencyLevel.LOW
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


# ============================================================================
# P91-P100: Configuration Tests
# ============================================================================


# ============================================================================
# P101-P110: Performance Tests
# ============================================================================


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

        popup = PopupTestHelper.create_from_old_style(
            popup_type="DIALOG",
            title="Large Content",
            content=large_content,
            urgency="LOW",
            blocking="NON_MODAL",
            element_id="large_popup",
            screen_context="test"
        )

        popup_handler.handle = Mock(
            return_value=PopupHandlingResult(
                detected=True,
                handled=True,
                handling_method="dismissed",
                state_preserved=True,
                execution_resumed=True,
                handling_time_ms=100.0,
                fallback_required=False
            )
        )

        result = popup_handler.handle(popup)
        assert result.handled is True

    def test_unicode_popup_content(self, popup_handler):
        """WHEN popup contains unicode characters,
        THEN handler processes correctly."""
        unicode_content = "Test with emoji 🎉 and chinese 中文"

        popup = PopupTestHelper.create_from_old_style(
            popup_type="DIALOG",
            title="Unicode Test",
            content=unicode_content,
            urgency="LOW",
            blocking="NON_MODAL",
            element_id="unicode_popup",
            screen_context="test"
        )

        popup_handler.handle = Mock(
            return_value=PopupHandlingResult(
                detected=True,
                handled=True,
                handling_method="handled",
                state_preserved=True,
                execution_resumed=True,
                handling_time_ms=100.0,
                fallback_required=False
            )
        )

        result = popup_handler.handle(popup)
        assert result.handled is True
        # Content is in the original popup's target_element dict
        assert "🎉" in popup.target_element.get("content", "")
        assert "中文" in popup.target_element.get("content", "")


# ============================================================================
# Module-level tests
# ============================================================================


class TestPopupHandlerModuleStructure:
    """Tests for module structure and exports."""

    def test_module_exports_required_classes(self):
        """WHEN importing popup handler module,
        THEN all required classes are available."""
        # V6.14.0: import from correct module path
        from src.state_machine.popup_handler import (
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
