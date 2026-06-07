"""
Fault injector for controlled failure scenarios.

Provides methods to inject vision failures, action failures, state corruption,
and page mismatches for testing error handling.
"""

from typing import Any, Dict, List, Optional, Callable
from enum import Enum


class FaultType(str, Enum):
    """Types of faults that can be injected."""

    VISION_TIMEOUT = "vision_timeout"
    VISION_NULL_RESULT = "vision_null_result"
    VISION_EXCEPTION = "vision_exception"
    ACTION_TIMEOUT = "action_timeout"
    ACTION_EXCEPTION = "action_exception"
    STATE_CORRUPTION = "state_corruption"
    PAGE_MISMATCH = "page_mismatch"
    NETWORK_ERROR = "network_error"
    ELEMENT_NOT_FOUND = "element_not_found"


class FaultInjector:
    """Injector for controlled fault scenarios in testing.

    Used to test system resilience and error handling under various
    failure conditions.
    """

    def __init__(self):
        self._active_faults: Dict[str, Any] = {}
        self._fault_counts: Dict[str, int] = {}

    def inject_vision_failure(self, fault_type: str) -> None:
        """Configure a vision service failure.

        Args:
            fault_type: Type of vision failure ('timeout', 'null_result', 'exception')
        """
        self._active_faults["vision"] = fault_type
        self._fault_counts["vision"] = 0

    def inject_action_failure(self, fault_type: str) -> None:
        """Configure an action executor failure.

        Args:
            fault_type: Type of action failure ('timeout', 'exception')
        """
        self._active_faults["action"] = fault_type
        self._fault_counts["action"] = 0

    def inject_state_corruption(
        self,
        corruption_type: str = "stack_path_mismatch",
    ) -> Dict[str, Any]:
        """Inject corrupted state for testing recovery.

        Args:
            corruption_type: Type of corruption ('stack_path_mismatch',
                             'orphaned_spans', 'cache_inconsistency')

        Returns:
            Corrupted state dictionary
        """
        corrupted_state = {
            "stack": ["node1", "node2", "node3"],
            "context_current_path": ["node1", "node4"],  # Mismatch!
            "cache": {"node2": ["child1", "child2"]},
        }

        if corruption_type == "stack_path_mismatch":
            # Already set in default
            pass
        elif corruption_type == "orphaned_spans":
            corrupted_state["spans"] = [
                {"span_id": "span1", "parent_span_id": None},  # OK
                {"span_id": "span2", "parent_span_id": "span999"},  # Orphan
            ]
        elif corruption_type == "cache_inconsistency":
            corrupted_state["cached_children"] = ["child1"]
            corrupted_state["actual_elements"] = ["child2"]  # Different

        self._active_faults["state"] = corruption_type
        return corrupted_state

    def inject_mismatched_page(
        self,
        expected_path: List[str],
        actual_path: List[str],
    ) -> Dict[str, Any]:
        """Inject a page mismatch scenario.

        Args:
            expected_path: Path that precondition expects
            actual_path: Path that vision returns

        Returns:
            Scenario data with mismatch
        """
        scenario = {
            "expected_path": expected_path,
            "actual_path": actual_path,
            "relation": self._classify_path_relation(expected_path, actual_path),
        }

        self._active_faults["page_mismatch"] = scenario
        return scenario

    def _classify_path_relation(
        self,
        expected: List[str],
        actual: List[str],
    ) -> str:
        """Classify the relationship between expected and actual paths."""
        if expected == actual:
            return "SATISFIED"
        elif actual[:-1] == expected[: len(actual) - 1]:
            return "DEEPER"
        elif expected[:-1] == actual[: len(expected) - 1]:
            return "NAVIGABLE"
        else:
            return "UNKNOWN"

    def should_inject_fault(self, component: str) -> bool:
        """Check if a fault should be injected for the given component.

        Args:
            component: Component name ('vision', 'action', etc.)

        Returns:
            True if fault should be injected
        """
        return component in self._active_faults

    def get_fault_type(self, component: str) -> Optional[str]:
        """Get the active fault type for a component.

        Args:
            component: Component name

        Returns:
            Fault type or None if no fault
        """
        return self._active_faults.get(component)

    def increment_fault_count(self, component: str) -> int:
        """Increment the fault count for a component.

        Args:
            component: Component name

        Returns:
            New fault count
        """
        if component not in self._fault_counts:
            self._fault_counts[component] = 0
        self._fault_counts[component] += 1
        return self._fault_counts[component]

    def reset_faults(self) -> None:
        """Reset all active faults and counts."""
        self._active_faults.clear()
        self._fault_counts.clear()

    def create_faulty_vision_response(self, fault_type: str) -> Any:
        """Create a faulty vision response based on fault type.

        Args:
            fault_type: Type of vision fault

        Returns:
            Faulty response (None, exception, or timeout)
        """
        if fault_type == "null_result":
            return None
        elif fault_type == "timeout":
            return TimeoutError("Vision timeout")
        elif fault_type == "exception":
            return Exception("Vision service error")
        else:
            return None

    def create_faulty_action_response(self, fault_type: str) -> Any:
        """Create a faulty action response based on fault type.

        Args:
            fault_type: Type of action fault

        Returns:
            Faulty response (exception or timeout)
        """
        if fault_type == "timeout":
            return TimeoutError("Action timeout")
        elif fault_type == "exception":
            return Exception("Action execution failed")
        else:
            return Exception(f"Unknown action fault: {fault_type}")

    def simulate_network_error(self, error_type: str = "timeout") -> Dict[str, Any]:
        """Simulate a network error scenario.

        Args:
            error_type: Type of network error ('timeout', 'connection_refused',
                         'dns_failure', 'network_unreachable')

        Returns:
            Network error scenario data
        """
        return {
            "error_type": error_type,
            "error_message": f"Network error: {error_type}",
            "retry_able": error_type in ["timeout", "connection_refused"],
            "suggested_action": "retry" if error_type == "timeout" else "abort",
        }

    def get_fault_summary(self) -> Dict[str, Any]:
        """Get summary of all active faults.

        Returns:
            Dictionary with fault summary
        """
        return {
            "active_faults": self._active_faults.copy(),
            "fault_counts": self._fault_counts.copy(),
            "total_faults": sum(self._fault_counts.values()),
        }


# ============================================================================
# Helper functions for common fault scenarios
# ============================================================================


def create_timeout_scenario(timeout_ms: int = 5000) -> Dict[str, Any]:
    """Create a timeout scenario for testing.

    Args:
        timeout_ms: Timeout duration in milliseconds

    Returns:
        Timeout scenario data
    """
    return {
        "fault_type": FaultType.VISION_TIMEOUT,
        "timeout_ms": timeout_ms,
        "expected_behavior": "retry_with_backoff",
    }


def create_element_not_found_scenario(
    element_id: str,
    alternatives: List[str],
) -> Dict[str, Any]:
    """Create an element not found scenario.

    Args:
        element_id: ID of element that wasn't found
        alternatives: List of alternative element IDs

    Returns:
        Element not found scenario data
    """
    return {
        "fault_type": FaultType.ELEMENT_NOT_FOUND,
        "element_id": element_id,
        "alternatives": alternatives,
        "expected_behavior": "retry_or_skip",
    }
