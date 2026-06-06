"""
Trace recorder for V6.3 distributed tracing.

Captures trace nodes during traversal and writes them through a pluggable
storage backend. Uses a StepTracker stack to manage parent_span_id context.

All recorder methods follow "log and continue": a write failure logs a
warning but never interrupts the traversal.
"""

import logging
import time
from typing import Any, Dict, List, Optional

from .models import SessionNode, SpanNode, StepNode, TraceNode, generate_id
from .storage import MemoryStorage, TraceStorage

logger = logging.getLogger(__name__)


# ── Step tracker ─────────────────────────────────────────────────────────────


class StepTracker:
    """Manages a stack of step span_ids to track the current parent context.

    Each call to on_node_enter pushes a span_id; on_node_exit pops it.
    The top of the stack is the current parent_span_id for new spans.
    """

    def __init__(self):
        self._stack: List[str] = []

    def on_node_enter(self, span_id: str) -> None:
        """Push a span_id onto the stack — it becomes the current parent."""
        self._stack.append(span_id)

    def on_node_exit(self) -> Optional[str]:
        """Pop the top span_id and return it."""
        if self._stack:
            return self._stack.pop()
        return None

    def get_parent_span_id(self) -> Optional[str]:
        """Return the current parent span_id (top of stack), or None."""
        return self._stack[-1] if self._stack else None

    @property
    def depth(self) -> int:
        return len(self._stack)

    def clear(self) -> None:
        self._stack.clear()


# ── Trace recorder ───────────────────────────────────────────────────────────


class TraceRecorder:
    """Records trace data during a traversal run.

    Writes SessionNode, StepNode, and SpanNode instances to a
    TraceStorage backend. Manages a StepTracker for automatic
    parent_span_id resolution.

    Usage::

        recorder = TraceRecorder(storage)
        recorder.init(session_node)
        recorder.record_step_start(step_node)
        recorder.record_span(span_node)
        recorder.record_step_end(step_span_id, result)
        recorder.finalize("completed")
    """

    def __init__(self, storage: Optional[TraceStorage] = None):
        self._storage = storage or MemoryStorage()
        self._step_tracker = StepTracker()
        self._session_node: Optional[SessionNode] = None
        self._trace_id: Optional[str] = None
        self._initialized = False

    @property
    def storage(self) -> TraceStorage:
        return self._storage

    @property
    def trace_id(self) -> Optional[str]:
        return self._trace_id

    # -- init ----------------------------------------------------------------

    def init(self, session_node: SessionNode) -> None:
        """Initialise a new trace session.

        Writes the SessionNode and configures the recorder for this trace.
        """
        self._session_node = session_node
        self._trace_id = session_node.trace_id
        self._step_tracker.clear()
        self._step_tracker.on_node_enter(session_node.span_id)
        self._initialized = True
        self._safe_write(session_node)

    # -- step lifecycle ------------------------------------------------------

    def record_step_start(
        self, step_node: StepNode, parent_span_id: Optional[str] = None
    ) -> None:
        """Record the start of a traversal step.

        Sets parent_span_id (auto-detected from StepTracker if not given),
        writes the StepNode, and pushes it onto the tracker stack.
        """
        if not self._initialized:
            return

        step_node.trace_id = self._trace_id or ""
        step_node.timestamp = time.time()
        step_node.parent_span_id = parent_span_id or self._step_tracker.get_parent_span_id()

        self._safe_write(step_node)
        self._step_tracker.on_node_enter(step_node.span_id)

    def record_span(
        self, span: SpanNode, parent_span_id: Optional[str] = None
    ) -> None:
        """Record a Span within the current step context."""
        if not self._initialized:
            return

        span.trace_id = self._trace_id or ""
        span.timestamp = time.time()
        span.parent_span_id = parent_span_id or self._step_tracker.get_parent_span_id()

        self._safe_write(span)

    def record_step_end(
        self, step_span_id: str, result: Optional[Dict[str, Any]] = None
    ) -> None:
        """Record the end of a step.

        Creates a step_end Span that backfills the corresponding StepNode.
        Pops the step from the tracker stack.
        """
        if not self._initialized:
            return

        end_span = SpanNode(
            span_type="step_end",
            step_span_id=step_span_id,
            metadata={"result": result} if result else {},
        )
        end_span.trace_id = self._trace_id or ""
        end_span.timestamp = time.time()
        end_span.parent_span_id = self._step_tracker.get_parent_span_id()

        self._safe_write(end_span)
        self._step_tracker.on_node_exit()

    # -- finalize ------------------------------------------------------------

    def finalize(self, status: str = "completed", end_time: Optional[float] = None) -> None:
        """Finalize the trace session.

        Creates a session_end Span and writes it. The session_end span
        backfills the SessionNode's status and end_time when the trace
        is analyzed.
        """
        if not self._initialized:
            return

        end_span = SpanNode(span_type="session_end", status=status)
        end_span.trace_id = self._trace_id or ""
        end_span.timestamp = end_time or time.time()
        end_span.parent_span_id = None

        self._safe_write(end_span)

        # Also update the session node directly
        if self._session_node:
            self._session_node.status = status
            self._session_node.end_time = end_span.timestamp

        # Flush if using FileStorage
        if hasattr(self._storage, "flush"):
            self._storage.flush()

        self._initialized = False

    # -- helpers -------------------------------------------------------------

    def get_parent_span_id(self) -> Optional[str]:
        """Expose the current step context's parent span_id."""
        return self._step_tracker.get_parent_span_id()

    def _safe_write(self, node: TraceNode) -> None:
        """Write a node, logging any failure without raising (log and continue)."""
        try:
            self._storage.write(node)
        except Exception as exc:
            logger.warning(
                "Trace write failed for node %s (span_id=%s): %s",
                node.node_type,
                node.span_id,
                exc,
            )
