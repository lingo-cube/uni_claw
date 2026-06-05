"""
Popup handling system for V6.1 traversal state machine.

This module provides comprehensive popup detection, classification,
and handling capabilities for automated UI traversal.
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Dict, List, Optional, Set
import logging
import time
import re

logger = logging.getLogger(__name__)


class PopupType(str, Enum):
    """Categories of popups that can be detected."""

    PERMISSION = "permission"  # Permission request dialogs
    ERROR = "error"  # Error message dialogs
    AD = "ad"  # Advertisement popups
    DIALOG = "dialog"  # General dialog boxes
    UNKNOWN = "unknown"  # Uncategorized popups


class UrgencyLevel(str, Enum):
    """Urgency levels for popup handling."""

    LOW = "low"  # Can be handled later
    MEDIUM = "medium"  # Should be handled soon
    HIGH = "high"  # Requires immediate attention
    CRITICAL = "critical"  # Blocks all operations


class BlockingType(str, Enum):
    """Blocking behavior of popups."""

    MODAL = "modal"  # Blocks UI interaction
    NON_MODAL = "non_modal"  # Allows background interaction
    TOAST = "toast"  # Temporary notification


@dataclass
class PopupInfo:
    """Popup detection and classification information."""

    popup_type: PopupType
    confidence: float
    target_element: Optional[Dict[str, Any]] = None
    dismiss_strategy: str = "auto_close"
    timeout_seconds: int = 10
    urgency_level: UrgencyLevel = UrgencyLevel.MEDIUM
    blocking_type: BlockingType = BlockingType.MODAL
    detected_elements: List[Dict[str, Any]] = field(default_factory=list)


@dataclass
class PopupHandlingResult:
    """Popup handling execution result."""

    detected: bool
    handled: bool
    handling_method: str
    state_preserved: bool
    execution_resumed: bool
    handling_time_ms: float
    fallback_required: bool
    error_message: Optional[str] = None


class PopupDetector:
    """Detect popups from screen information."""

    # Permission popup patterns
    PERMISSION_PATTERNS = [
        r"permission",
        r"allow.*access",
        r"grant.*permission",
        r"request.*permission",
        r"continue.*with.*ads",
        r"accept.*terms",
    ]

    # Error popup patterns
    ERROR_PATTERNS = [
        r"error",
        r"failed",
        r"exception",
        r"warning",
        r"alert",
        r"cannot.*continue",
    ]

    # Ad popup patterns
    AD_PATTERNS = [
        r"advertisement",
        r"sponsored",
        r"skip.*ad",
        r"close.*ad",
        r"remove.*ads",
        r"upgrade.*now",
    ]

    # Dialog popup patterns
    DIALOG_PATTERNS = [
        r"dialog",
        r"confirm",
        r"alert.*dialog",
        r"message.*box",
        r"notification",
    ]

    def __init__(self):
        """Initialize popup detector."""
        self._compile_patterns()

    def _compile_patterns(self):
        """Compile regex patterns for efficiency."""
        self._compiled_patterns = {
            PopupType.PERMISSION: [re.compile(p, re.IGNORECASE) for p in self.PERMISSION_PATTERNS],
            PopupType.ERROR: [re.compile(p, re.IGNORECASE) for p in self.ERROR_PATTERNS],
            PopupType.AD: [re.compile(p, re.IGNORECASE) for p in self.AD_PATTERNS],
            PopupType.DIALOG: [re.compile(p, re.IGNORECASE) for p in self.DIALOG_PATTERNS],
        }

    def detect_popup(self, screen_info: Dict[str, Any]) -> bool:
        """
        Detect if a popup is present on the screen.

        Args:
            screen_info: Current screen information

        Returns:
            True if popup detected, False otherwise
        """
        # Check for popup indicators in screen text
        screen_text = screen_info.get("text", "").lower()

        # Check all popup patterns
        for popup_type, patterns in self._compiled_patterns.items():
            for pattern in patterns:
                if pattern.search(screen_text):
                    return True

        # Check for popup-specific UI elements
        ui_elements = screen_info.get("ui_elements", [])
        for element in ui_elements:
            element_text = element.get("text", "").lower()
            if self._matches_popup_patterns(element_text):
                return True

        return False

    def _matches_popup_patterns(self, text: str) -> bool:
        """Check if text matches any popup pattern."""
        for patterns in self._compiled_patterns.values():
            for pattern in patterns:
                if pattern.search(text):
                    return True
        return False


class PopupClassifier:
    """Classify detected popups into specific types."""

    def __init__(self):
        """Initialize popup classifier."""
        self.detector = PopupDetector()

    def classify_popup(self, screen_info: Dict[str, Any]) -> PopupInfo:
        """
        Classify popup and extract handling information.

        Args:
            screen_info: Current screen information

        Returns:
            PopupInfo with classification details
        """
        # Get screen text for analysis
        screen_text = screen_info.get("text", "")
        ui_elements = screen_info.get("ui_elements", [])

        # Determine popup type
        popup_type = self._determine_popup_type(screen_text, ui_elements)

        # Find target elements for dismissal
        target_element = self._find_dismiss_target(ui_elements, popup_type)

        # Determine dismiss strategy
        dismiss_strategy = self._determine_dismiss_strategy(popup_type, ui_elements)

        # Determine urgency level
        urgency = self._determine_urgency(popup_type, screen_text)

        # Determine blocking type
        blocking = self._determine_blocking_type(popup_type, ui_elements)

        # Calculate confidence
        confidence = self._calculate_confidence(popup_type, screen_text, ui_elements)

        return PopupInfo(
            popup_type=popup_type,
            confidence=confidence,
            target_element=target_element,
            dismiss_strategy=dismiss_strategy,
            urgency_level=urgency,
            blocking_type=blocking,
            detected_elements=ui_elements,
        )

    def _determine_popup_type(self, screen_text: str, ui_elements: List[Dict]) -> PopupType:
        """Determine the type of popup."""
        text_lower = screen_text.lower()

        # Check each popup type's patterns
        for popup_type, patterns in self.detector._compiled_patterns.items():
            for pattern in patterns:
                if pattern.search(text_lower):
                    return popup_type

        # Check UI element texts
        for element in ui_elements:
            element_text = element.get("text", "").lower()
            for popup_type, patterns in self.detector._compiled_patterns.items():
                for pattern in patterns:
                    if pattern.search(element_text):
                        return popup_type

        return PopupType.UNKNOWN

    def _find_dismiss_target(self, ui_elements: List[Dict], popup_type: PopupType) -> Optional[Dict[str, Any]]:
        """Find the target element for dismissing the popup."""
        # Priority dismiss button texts by popup type
        dismiss_texts = {
            PopupType.PERMISSION: ["allow", "accept", "continue", "grant", "ok"],
            PopupType.ERROR: ["ok", "close", "dismiss", "acknowledge"],
            PopupType.AD: ["close", "skip", "x", "dismiss"],
            PopupType.DIALOG: ["ok", "cancel", "close", "yes", "no"],
            PopupType.UNKNOWN: ["close", "ok", "cancel"],
        }

        priority_texts = dismiss_texts.get(popup_type, ["close", "ok"])

        # Search for dismiss buttons
        for element in ui_elements:
            element_text = element.get("text", "").lower()
            element_resource_id = element.get("resource_id", "").lower()

            # Check text matches
            for text in priority_texts:
                if text in element_text or text in element_resource_id:
                    return element

            # Check for common dismiss button characteristics
            if element.get("clickable", False):
                if any(word in element_text for word in ["close", "cancel", "dismiss"]):
                    return element

        return None

    def _determine_dismiss_strategy(self, popup_type: PopupType, ui_elements: List[Dict]) -> str:
        """Determine the best strategy for dismissing the popup."""
        if self._find_dismiss_target(ui_elements, popup_type):
            return "auto_close"

        # Fallback strategies
        if popup_type == PopupType.AD:
            return "back"
        elif popup_type == PopupType.PERMISSION:
            return "wait_timeout"
        elif popup_type == PopupType.ERROR:
            return "auto_close_or_back"
        else:
            return "back"

    def _determine_urgency(self, popup_type: PopupType, screen_text: str) -> UrgencyLevel:
        """Determine the urgency level of popup handling."""
        # Permission popups are high urgency (block functionality)
        if popup_type == PopupType.PERMISSION:
            return UrgencyLevel.HIGH

        # Error popups are medium-high urgency
        if popup_type == PopupType.ERROR:
            if "critical" in screen_text.lower() or "fatal" in screen_text.lower():
                return UrgencyLevel.CRITICAL
            return UrgencyLevel.MEDIUM

        # Ads are low urgency
        if popup_type == PopupType.AD:
            return UrgencyLevel.LOW

        # Default to medium
        return UrgencyLevel.MEDIUM

    def _determine_blocking_type(self, popup_type: PopupType, ui_elements: List[Dict]) -> BlockingType:
        """Determine if the popup is blocking or non-blocking."""
        # Permission popups are typically modal
        if popup_type == PopupType.PERMISSION:
            return BlockingType.MODAL

        # Error popups are modal
        if popup_type == PopupType.ERROR:
            return BlockingType.MODAL

        # Ads can be non-modal or toast
        if popup_type == PopupType.AD:
            # Check if it's a toast-style popup
            for element in ui_elements:
                if "toast" in element.get("resource_id", "").lower():
                    return BlockingType.TOAST
            return BlockingType.NON_MODAL

        return BlockingType.MODAL

    def _calculate_confidence(self, popup_type: PopupType, screen_text: str, ui_elements: List[Dict]) -> float:
        """Calculate confidence in the popup classification."""
        # Start with base confidence
        confidence = 0.5

        # Increase confidence based on pattern matches
        text_lower = screen_text.lower()

        # Count matching patterns
        pattern_matches = 0
        if popup_type in self.detector._compiled_patterns:
            for pattern in self.detector._compiled_patterns[popup_type]:
                if pattern.search(text_lower):
                    pattern_matches += 1
                    confidence += 0.1

        # Check UI element matches
        for element in ui_elements:
            element_text = element.get("text", "").lower()
            if popup_type in self.detector._compiled_patterns:
                for pattern in self.detector._compiled_patterns[popup_type]:
                    if pattern.search(element_text):
                        pattern_matches += 1
                        confidence += 0.05

        # Cap confidence at 1.0
        return min(confidence, 1.0)


class StateRestorer:
    """Preserve and restore execution state during popup handling."""

    def __init__(self):
        """Initialize state restorer."""
        self._saved_states: Dict[str, Any] = {}

    def preserve_state(self, context: Dict[str, Any]) -> str:
        """
        Preserve current execution state.

        Args:
            context: Current traversal context

        Returns:
            State ID for later restoration
        """
        state_id = f"state_{time.time()}"

        # Save important context information
        self._saved_states[state_id] = {
            "current_node_id": context.get("current_node_id"),
            "node_stack": context.get("node_stack", []).copy(),
            "current_state": context.get("current_state"),
            "execution_result": context.get("execution_result"),
            "timestamp": time.time(),
        }

        return state_id

    def restore_state(self, state_id: str, context: Dict[str, Any]) -> bool:
        """
        Restore preserved execution state.

        Args:
            state_id: ID of state to restore
            context: Current traversal context to update

        Returns:
            True if restoration successful, False otherwise
        """
        if state_id not in self._saved_states:
            logger.error(f"State ID {state_id} not found for restoration")
            return False

        saved_state = self._saved_states[state_id]

        # Restore context
        context["current_node_id"] = saved_state["current_node_id"]
        context["node_stack"] = saved_state["node_stack"].copy()
        context["current_state"] = saved_state["current_state"]
        context["execution_result"] = saved_state["execution_result"]

        # Clean up saved state
        del self._saved_states[state_id]

        return True

    def validate_restored_state(self, context: Dict[str, Any]) -> bool:
        """
        Validate that restored state is consistent.

        Args:
            context: Current traversal context

        Returns:
            True if state is valid, False otherwise
        """
        # Check that critical fields are present
        required_fields = ["current_node_id", "node_stack", "current_state"]
        for field in required_fields:
            if field not in context:
                return False

        # Validate node stack integrity
        node_stack = context.get("node_stack", [])
        if not isinstance(node_stack, list):
            return False

        return True


class PopupActionHandler:
    """Execute popup handling actions."""

    def __init__(self):
        """Initialize popup action handler."""
        self._action_handlers = {
            "auto_close": self._handle_auto_close,
            "back": self._handle_back,
            "wait_timeout": self._handle_wait_timeout,
            "auto_close_or_back": self._handle_auto_close_or_back,
        }

    def handle_popup(self, popup_info: PopupInfo, context: Dict[str, Any]) -> Dict[str, Any]:
        """
        Execute popup handling action.

        Args:
            popup_info: Classified popup information
            context: Current traversal context

        Returns:
            Handling result dictionary
        """
        start_time = time.time()
        success = False
        handling_method = popup_info.dismiss_strategy
        state_preserved = True
        execution_resumed = False
        error_message = None

        try:
            action_handler = self._action_handlers.get(popup_info.dismiss_strategy)
            if action_handler:
                result = action_handler(popup_info, context)
                success = result.get('success', False)
                handling_method = result.get('method', popup_info.dismiss_strategy)
                execution_resumed = result.get('resumed', False)
            else:
                error_message = f"No handler for strategy: {popup_info.dismiss_strategy}"
                logger.error(error_message)

        except Exception as e:
            error_message = f"Popup handling failed: {e}"
            logger.error(error_message)
            success = False

        handling_time_ms = (time.time() - start_time) * 1000

        return {
            'success': success,
            'method': handling_method,
            'state_preserved': state_preserved,
            'resumed': execution_resumed,
            'handling_time_ms': handling_time_ms,
            'error_message': error_message,
        }

    def _handle_auto_close(self, popup_info: PopupInfo, context: Dict[str, Any]) -> Dict[str, Any]:
        """Handle popup by clicking dismiss button."""
        # Simulate clicking the target element
        if popup_info.target_element:
            return {
                'success': True,
                'method': 'click_dismiss_button',
                'resumed': True,
            }
        return {
            'success': False,
            'method': 'auto_close_failed',
            'resumed': False,
        }

    def _handle_back(self, popup_info: PopupInfo, context: Dict[str, Any]) -> Dict[str, Any]:
        """Handle popup by pressing back."""
        return {
            'success': True,
            'method': 'press_back',
            'resumed': True,
        }

    def _handle_wait_timeout(self, popup_info: PopupInfo, context: Dict[str, Any]) -> Dict[str, Any]:
        """Handle popup by waiting for it to timeout."""
        # For permission popups that might timeout
        return {
            'success': True,
            'method': 'wait_for_timeout',
            'resumed': True,
        }

    def _handle_auto_close_or_back(self, popup_info: PopupInfo, context: Dict[str, Any]) -> Dict[str, Any]:
        """Handle popup by auto-close or fallback to back."""
        # Try auto-close first
        if popup_info.target_element:
            return {
                'success': True,
                'method': 'click_dismiss_button_or_back',
                'resumed': True,
            }
        # Fallback to back
        return {
            'success': True,
            'method': 'press_back_fallback',
            'resumed': True,
        }


class PopupHandler:
    """Complete popup handling system for V6.1."""

    def __init__(self):
        """Initialize popup handler."""
        self.detector = PopupDetector()
        self.classifier = PopupClassifier()
        self.action_handler = PopupActionHandler()
        self.restorer = StateRestorer()

        # Statistics
        self.detected_count = 0
        self.handled_count = 0
        self.handling_statistics: Dict[str, int] = {}
        self.total_handling_time_ms = 0.0

    def handle_popup(self, screen_info: Dict[str, Any], context: Dict[str, Any]) -> PopupHandlingResult:
        """
        Handle popup detection and processing.

        Args:
            screen_info: Current screen information
            context: Current traversal context

        Returns:
            PopupHandlingResult with handling details
        """
        start_time = time.time()

        # Detect popup
        detected = self.detector.detect_popup(screen_info)
        if not detected:
            return PopupHandlingResult(
                detected=False,
                handled=False,
                handling_method="none",
                state_preserved=True,
                execution_resumed=False,
                handling_time_ms=0.0,
                fallback_required=False,
            )

        self.detected_count += 1

        # Classify popup
        popup_info = self.classifier.classify_popup(screen_info)

        # Preserve state
        state_id = self.restorer.preserve_state(context)

        # Handle popup
        handling_result = self.action_handler.handle_popup(popup_info, context)

        # Restore state
        if handling_result['success']:
            restored = self.restorer.restore_state(state_id, context)
            validated = self.restorer.validate_restored_state(context)

            if not restored or not validated:
                handling_result['success'] = False
                handling_result['error_message'] = "State restoration failed"

        # Update statistics
        handling_method = handling_result['method']
        self.handling_statistics[handling_method] = self.handling_statistics.get(handling_method, 0) + 1

        if handling_result['success']:
            self.handled_count += 1

        handling_time_ms = (time.time() - start_time) * 1000
        self.total_handling_time_ms += handling_time_ms

        return PopupHandlingResult(
            detected=True,
            handled=handling_result['success'],
            handling_method=handling_result['method'],
            state_preserved=True,
            execution_resumed=handling_result['resumed'],
            handling_time_ms=handling_time_ms,
            fallback_required=not handling_result['success'],
            error_message=handling_result.get('error_message'),
        )

    @property
    def handling_rate(self) -> float:
        """Calculate popup handling success rate."""
        if self.detected_count == 0:
            return 0.0
        return self.handled_count / self.detected_count

    def get_popup_statistics(self) -> Dict[str, Any]:
        """Get comprehensive popup handling statistics."""
        return {
            "detected_popups": self.detected_count,
            "handled_popups": self.handled_count,
            "handling_rate": self.handling_rate,
            "handling_methods": self.handling_statistics.copy(),
            "total_handling_time_ms": self.total_handling_time_ms,
        }