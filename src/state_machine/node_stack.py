"""
Node stack for depth-first traversal.

This module implements the node stack that maintains the traversal context
for depth-first exploration of the graph.
"""

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional
from datetime import datetime

from src.graph.node import TraversalNode


@dataclass
class StackFrame:
    """
    A frame in the node stack representing a node being processed.

    Each frame contains:
    - The node being processed
    - Queue of child nodes to process
    - Current position in child queue
    - Whether restore operation is pending
    """

    node: TraversalNode
    child_queue: List[str] = field(default_factory=list)
    current_child_idx: int = 0
    pending_restore: bool = False
    entered_at: datetime = field(default_factory=datetime.now)
    metadata: Dict[str, Any] = field(default_factory=dict)

    def __post_init__(self):
        """Validate stack frame configuration."""
        if self.current_child_idx < 0:
            raise ValueError(f"current_child_idx cannot be negative, got {self.current_child_idx}")
        # If child_queue is provided, current_child_idx should not exceed length
        if self.child_queue and self.current_child_idx > len(self.child_queue):
            raise ValueError(
                f"current_child_idx ({self.current_child_idx}) cannot exceed "
                f"child_queue length ({len(self.child_queue)})"
            )

    @property
    def node_id(self) -> str:
        """Get the node ID."""
        return self.node.node_id

    @property
    def has_children(self) -> bool:
        """Check if frame has children to process."""
        return len(self.child_queue) > 0

    @property
    def remaining_children(self) -> int:
        """Get count of remaining children."""
        return max(0, len(self.child_queue) - self.current_child_idx)

    @property
    def is_complete(self) -> bool:
        """Check if all children have been processed."""
        return self.current_child_idx >= len(self.child_queue)

    @property
    def duration(self) -> float:
        """Get time since entering this frame."""
        return (datetime.now() - self.entered_at).total_seconds()

    def get_next_child(self) -> Optional[str]:
        """
        Get the next child ID from the queue.

        Returns:
            Next child ID or None if no more children
        """
        if self.is_complete:
            return None
        child_id = self.child_queue[self.current_child_idx]
        self.current_child_idx += 1
        return child_id

    def peek_next_child(self) -> Optional[str]:
        """
        Peek at the next child ID without advancing.

        Returns:
            Next child ID or None if no more children
        """
        if self.is_complete:
            return None
        return self.child_queue[self.current_child_idx]

    def reset_child_index(self) -> None:
        """Reset child index to 0 (for retries)."""
        self.current_child_idx = 0


class NodeStack:
    """
    Stack for maintaining depth-first traversal context.

    Manages stack frames and provides push/pop/top operations
    with depth limiting for safety.
    """

    DEFAULT_MAX_DEPTH = 10

    def __init__(self, max_depth: int = DEFAULT_MAX_DEPTH):
        """
        Initialize the node stack.

        Args:
            max_depth: Maximum stack depth to prevent infinite recursion
        """
        self._frames: List[StackFrame] = []
        self._max_depth = max_depth
        self._depth_limit_reached: bool = False

    @property
    def is_empty(self) -> bool:
        """Check if stack is empty."""
        return len(self._frames) == 0

    @property
    def size(self) -> int:
        """Get current stack size."""
        return len(self._frames)

    @property
    def depth(self) -> int:
        """Get current depth (alias for size)."""
        return self.size

    @property
    def max_depth(self) -> int:
        """Get maximum allowed depth."""
        return self._max_depth

    @property
    def depth_limit_reached(self) -> bool:
        """Check if depth limit was reached."""
        return self._depth_limit_reached

    def push(self, node: TraversalNode, children: Optional[List[str]] = None) -> bool:
        """
        Push a new frame onto the stack.

        Args:
            node: Node to push
            children: Optional list of child node IDs (will be reversed for DFS)

        Returns:
            True if push succeeded

        Raises:
            RuntimeError: If max depth exceeded
        """
        if len(self._frames) >= self._max_depth:
            self._depth_limit_reached = True
            raise RuntimeError(
                f"Node stack depth limit ({self._max_depth}) exceeded. "
                f"Potential infinite recursion detected."
            )

        # Reverse children for depth-first traversal (last child first)
        child_queue = list(reversed(children or []))

        frame = StackFrame(node=node, child_queue=child_queue)
        self._frames.append(frame)
        return True

    def pop(self) -> Optional[StackFrame]:
        """
        Pop the top frame from the stack.

        Returns:
            Popped frame or None if stack is empty
        """
        if self.is_empty:
            return None
        return self._frames.pop()

    def top(self) -> Optional[StackFrame]:
        """
        Get the top frame without popping.

        Returns:
            Top frame or None if stack is empty
        """
        if self.is_empty:
            return None
        return self._frames[-1]

    def peek(self, offset: int = 0) -> Optional[StackFrame]:
        """
        Peek at a frame by offset from top.

        Args:
            offset: Offset from top (0 = top, 1 = second from top, etc.)

        Returns:
            Frame at offset or None if offset out of range
        """
        if offset < 0 or offset >= len(self._frames):
            return None
        return self._frames[-(offset + 1)]

    def get_node_path(self) -> List[str]:
        """
        Get the path from root to current node.

        Returns:
            List of node IDs from bottom to top
        """
        return [frame.node_id for frame in self._frames]

    def get_current_node_id(self) -> Optional[str]:
        """
        Get the current (top) node ID.

        Returns:
            Current node ID or None if stack is empty
        """
        frame = self.top()
        return frame.node_id if frame else None

    def get_parent_node_id(self) -> Optional[str]:
        """
        Get the parent (second from top) node ID.

        Returns:
            Parent node ID or None if no parent
        """
        frame = self.peek(offset=1)
        return frame.node_id if frame else None

    def contains_node(self, node_id: str) -> bool:
        """
        Check if a node ID exists in the stack.

        Args:
            node_id: Node ID to search for

        Returns:
            True if node ID found in stack
        """
        return any(frame.node_id == node_id for frame in self._frames)

    def get_depth_of_node(self, node_id: str) -> int:
        """
        Get the depth (stack position) of a node.

        Args:
            node_id: Node ID to find

        Returns:
            Depth from bottom (0-based), or -1 if not found
        """
        for i, frame in enumerate(self._frames):
            if frame.node_id == node_id:
                return i
        return -1

    def clear(self) -> None:
        """Clear all frames from the stack."""
        self._frames.clear()
        self._depth_limit_reached = False

    def to_list(self) -> List[StackFrame]:
        """
        Get list of all frames (bottom to top).

        Returns:
            Copy of frames list
        """
        return self._frames.copy()

    def get_summary(self) -> Dict[str, Any]:
        """
        Get a summary of the stack state.

        Returns:
            Dictionary with stack summary
        """
        top_frame = self.top()
        return {
            "size": self.size,
            "max_depth": self._max_depth,
            "depth_limit_reached": self._depth_limit_reached,
            "current_node": self.get_current_node_id(),
            "path": self.get_node_path(),
            "top_frame_complete": top_frame.is_complete if top_frame else None,
            "top_frame_remaining": top_frame.remaining_children if top_frame else 0,
        }

    def __len__(self) -> int:
        """Get stack size."""
        return len(self._frames)

    def __repr__(self) -> str:
        """String representation."""
        path = " -> ".join(self.get_node_path())
        return f"NodeStack(size={self.size}, path={path if path else 'empty'})"
