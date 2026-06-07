"""
Trace analyzer for trace analysis capabilities.

Provides methods to build trace trees, extract operations, and count span types.
Used in tests to analyze and assert on trace data.
"""

from typing import Any, Dict, List, Optional

from src.trace.models import SessionNode, SpanNode, StepNode


class TraceAnalyzer:
    """Analyzer for extracting insights from trace data.

    Provides utilities for:
    - Building hierarchical tree structures from flat span lists
    - Extracting ordered operations from traces
    - Counting span types for coverage analysis
    - Finding specific operations by criteria
    """

    @staticmethod
    def build_tree(spans: List[SpanNode]) -> Dict[str, Any]:
        """Build hierarchical tree structure from flat span list.

        Creates a nested dictionary representing parent-child relationships.
        Root spans (parent_span_id=None) become top-level entries.

        Args:
            spans: List of SpanNode instances

        Returns:
            Hierarchical tree dict with span_id -> {data, children: []}
        """
        # Build span lookup
        span_map = {span.span_id: span for span in spans}

        # Build tree structure
        tree: Dict[str, Any] = {}

        for span in spans:
            node = {
                "span_id": span.span_id,
                "span_type": span.span_type,
                "timestamp": span.timestamp,
                "data": span.to_dict(),
                "children": [],
            }

            if span.parent_span_id is None:
                # Root span
                tree[span.span_id] = node
            elif span.parent_span_id in span_map:
                # Child span - add to parent's children
                parent_id = span.parent_span_id
                if parent_id in tree:
                    tree[parent_id]["children"].append(node)
                else:
                    # Parent not yet in tree, create placeholder
                    tree[parent_id] = {"children": [node]}
            else:
                # Orphan span - add as root
                tree[span.span_id] = node

        return tree

    @staticmethod
    def extract_operations(trace: SessionNode) -> List[Dict[str, Any]]:
        """Extract ordered list of operations (click/back/swipe) from trace.

        Filters for action-type spans and returns them in timestamp order.

        Args:
            trace: SessionNode containing operation spans

        Returns:
            Ordered list of operation dicts with action_type, timestamp, etc.
        """
        operations = []

        for span in trace.spans:
            if span.span_type in ["action", "operation"]:
                op_data = {
                    "action_type": span.operation_type if hasattr(span, "operation_type") else span.span_type,
                    "timestamp": span.timestamp,
                    "span_id": span.span_id,
                    "target": span.target_info if hasattr(span, "target_info") else {},
                }
                operations.append(op_data)

        # Sort by timestamp
        operations.sort(key=lambda x: x["timestamp"])

        return operations

    @staticmethod
    def count_span_types(trace: SessionNode) -> Dict[str, int]:
        """Count span types in trace for coverage analysis.

        Args:
            trace: SessionNode containing spans

        Returns:
            Dictionary mapping span_type to count
        """
        span_type_counts: Dict[str, int] = {}

        for span in trace.spans:
            span_type = span.span_type
            span_type_counts[span_type] = span_type_counts.get(span_type, 0) + 1

        return span_type_counts

    @staticmethod
    def find_errors(trace: SessionNode) -> List[SpanNode]:
        """Find all error-type spans in trace.

        Args:
            trace: SessionNode containing spans

        Returns:
            List of SpanNodes with span_type containing 'error'
        """
        return [span for span in trace.spans if "error" in span.span_type.lower()]

    @staticmethod
    def get_execution_path(trace: SessionNode) -> List[str]:
        """Get the execution path as a list of node_ids.

        Args:
            trace: SessionNode containing step spans

        Returns:
            Ordered list of node_ids representing execution path
        """
        path = []

        for span in trace.spans:
            if span.node_type == "step" and hasattr(span, "node_id"):
                path.append(span.node_id)

        return path

    @staticmethod
    def calculate_depth(tree: Dict[str, Any]) -> int:
        """Calculate maximum depth of trace tree.

        Args:
            tree: Tree dict from build_tree()

        Returns:
            Maximum depth (0 for empty tree, 1 for single level, etc.)
        """
        if not tree:
            return 0

        max_depth = 1

        def _depth(node: Dict[str, Any]) -> int:
            children = node.get("children", [])
            if not children:
                return 1
            return 1 + max(_depth(child) for child in children)

        return max(_depth(node) for node in tree.values())

    @staticmethod
    def get_span_duration(span: SpanNode, end_time: Optional[float] = None) -> float:
        """Calculate duration of a span.

        Args:
            span: SpanNode with start timestamp
            end_time: Optional end time (uses current time if None)

        Returns:
            Duration in seconds
        """
        import time

        end = end_time if end_time is not None else time.time()
        return end - span.timestamp

    @staticmethod
    def find_longest_operation(trace: SessionNode, min_duration_ms: float = 0) -> Optional[SpanNode]:
        """Find the longest-running operation in trace.

        Args:
            trace: SessionNode with spans
            min_duration_ms: Minimum duration threshold in milliseconds

        Returns:
            SpanNode of longest operation, or None if none above threshold
        """
        longest = None
        max_duration = 0

        for span in trace.spans:
            duration = TraceAnalyzer.get_span_duration(span)
            duration_ms = duration * 1000

            if duration_ms > min_duration_ms and duration_ms > max_duration:
                max_duration = duration_ms
                longest = span

        return longest
