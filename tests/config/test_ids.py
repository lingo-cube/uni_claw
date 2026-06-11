"""
Test ID generator.

Provides unified ID creation for test scenarios with consistent formatting.
"""

from typing import Optional


class TestIdGenerator:
    """
    Generates consistent, semantically meaningful test IDs.

    All IDs are lowercase with underscores for readability.
    """

    _counter = 0

    @staticmethod
    def node_id(name: str, index: Optional[int] = None) -> str:
        """
        Generate a node ID from a name and optional index.

        Args:
            name: The semantic name for the node (e.g., "TestNode", "Child")
            index: Optional index to append (e.g., 1, 2)

        Returns:
            Lowercase node ID with optional index suffix.

        Examples:
            TestIdGenerator.node_id("TestNode") -> "testnode"
            TestIdGenerator.node_id("Child", 1) -> "child_1"
        """
        base_id = name.lower()
        if index is not None:
            return f"{base_id}_{index}"
        return base_id

    @staticmethod
    def span_id(prefix: str, sequence: int) -> str:
        """
        Generate a span ID from a prefix and sequence number.

        Args:
            prefix: The operation prefix (e.g., "op", "http")
            sequence: The sequence number

        Returns:
            Span ID with prefix and sequence.

        Examples:
            TestIdGenerator.span_id("op", 1) -> "op_1"
        """
        return f"{prefix.lower()}_{sequence}"

    @staticmethod
    def trace_id() -> str:
        """
        Generate a unique trace ID.

        Returns:
            A unique trace ID starting with "trace_".

        Examples:
            TestIdGenerator.trace_id() -> "trace_0"
            TestIdGenerator.trace_id() -> "trace_1"
        """
        TestIdGenerator._counter += 1
        return f"trace_{TestIdGenerator._counter}"

    @staticmethod
    def element_id(type_name: str, text: str) -> str:
        """
        Generate an element ID from type name and text.

        Args:
            type_name: The element type (e.g., "button", "input")
            text: The element text content

        Returns:
            Element ID combining type and text.

        Examples:
            TestIdGenerator.element_id("button", "Submit") -> "button_submit"
        """
        return f"{type_name.lower()}_{text.lower()}"
