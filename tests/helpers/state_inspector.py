"""
State inspector for internal state verification.

Provides methods to verify engine state invariants during testing:
- Stack consistency: stack.path matches context.current_path
- Cache coherency: cached children match current page elements
- Span relationships: all Spans have valid parent references
- Metrics completeness: all operations have corresponding metrics
- State machine invariants: all transitions are valid per VALID_TRANSITIONS
"""

from typing import Any, Dict, List, Optional

from src.trace.models import SpanNode, SessionNode


class StateInspector:
    """Inspector for verifying internal engine state invariants.

    Used in tests to assert that the engine maintains consistent state
    across traversal operations.
    """

    # Valid state transitions for state machine verification
    VALID_TRANSITIONS = {
        "IDLE": ["SELECT", "ERROR_HANDLING"],
        "SELECT": ["EXECUTE", "BRANCH", "COMPLETE"],
        "EXECUTE": ["SELECT", "FRAME_COMPLETE", "ERROR_HANDLING"],
        "BRANCH": ["SELECT"],
        "FRAME_COMPLETE": ["SELECT", "COMPLETE", "BACKTRACK"],
        "BACKTRACK": ["SELECT", "COMPLETE"],
        "COMPLETE": ["IDLE"],
        "ERROR_HANDLING": ["SELECT", "COMPLETE", "BACKTRACK"],
    }

    @staticmethod
    def verify_stack_consistency(stack: List[str], context_path: List[str]) -> bool:
        """Verify that stack path matches context current_path.

        Args:
            stack: Current stack path as list of node_ids
            context_path: Current context.current_path as list of node_ids

        Returns:
            True if paths match, False otherwise
        """
        return stack == context_path

    @staticmethod
    def verify_cache_coherency(
        cached_children: List[Any],
        current_elements: List[Any],
        element_id_key: str = "element_id",
    ) -> bool:
        """Verify that cached children match current page elements.

        Args:
            cached_children: List of cached child nodes from engine cache
            current_elements: List of current page elements from vision
            element_id_key: Key name for element ID (default: "element_id")

        Returns:
            True if cached and current elements have matching IDs, False otherwise
        """
        cached_ids = {getattr(child, element_id_key, None) for child in cached_children}
        current_ids = {elem.get(element_id_key) for elem in current_elements if isinstance(elem, dict)}

        return cached_ids == current_ids

    @staticmethod
    def verify_no_orphan_spans(trace: SessionNode) -> bool:
        """Verify that all Spans have valid parent_span_id or are root.

        Args:
            trace: SessionNode containing all spans

        Returns:
            True if all spans have valid parent references, False otherwise
        """
        span_id_set = {span.span_id for span in trace.spans}

        for span in trace.spans:
            # Root spans (with None parent) are valid
            if span.parent_span_id is None:
                continue

            # Non-root spans must have parent in trace
            if span.parent_span_id not in span_id_set:
                return False

        return True

    @staticmethod
    def verify_metrics_completeness(trace: SessionNode) -> bool:
        """Verify that all operations have corresponding metrics.

        Args:
            trace: SessionNode with operations and metrics

        Returns:
            True if all operations have metrics, False otherwise
        """
        # Get all operation IDs from spans
        operation_ids = set()
        for span in trace.spans:
            if hasattr(span, "operation_id") and span.operation_id:
                operation_ids.add(span.operation_id)

        # Get all metric operation IDs
        metric_ids = {metric.operation_id for metric in trace.metrics}

        return operation_ids == metric_ids

    @staticmethod
    def verify_state_machine_invariants(
        fsm: Any,
        VALID_TRANSITIONS: Optional[Dict[str, List[str]]] = None,
    ) -> bool:
        """Verify that all state transitions are valid per VALID_TRANSITIONS.

        Args:
            fsm: State machine instance with transition history
            VALID_TRANSITIONS: Optional dict of valid transitions (uses default if None)

        Returns:
            True if all transitions are valid, False otherwise
        """
        if VALID_TRANSITIONS is None:
            VALID_TRANSITIONS = StateInspector.VALID_TRANSITIONS

        # Get transition history from FSM
        transitions = getattr(fsm, "transition_history", [])
        if not transitions:
            return True  # No transitions to validate

        for from_state, to_state in transitions:
            if from_state not in VALID_TRANSITIONS:
                return False  # Invalid source state

            if to_state not in VALID_TRANSITIONS[from_state]:
                return False  # Invalid transition

        return True

    @staticmethod
    def verify_no_memory_leaks(
        engine: Any,
        max_stack_depth: int = 100,
    ) -> bool:
        """Verify that stack depth doesn't indicate memory leaks.

        Args:
            engine: GraphTraversalEngine instance
            max_stack_depth: Maximum expected stack depth

        Returns:
            True if stack depth is reasonable, False if leak suspected
        """
        stack = getattr(engine, "_stack", None)
        if stack is None:
            return True

        stack_depth = len(stack.path)
        return stack_depth <= max_stack_depth

    @staticmethod
    def verify_cache_size(
        engine: Any,
        max_cache_size: int = 1000,
    ) -> bool:
        """Verify that cache size doesn't indicate unbounded growth.

        Args:
            engine: GraphTraversalEngine instance
            max_cache_size: Maximum expected cache entries

        Returns:
            True if cache size is reasonable, False if growth suspected
        """
        cache = getattr(engine, "_children_cache", None)
        if cache is None:
            return True

        return len(cache) <= max_cache_size
