"""
Context recovery from trace spans for breakpoint resume.

Rebuilds a TraversalRuntimeContext by replaying the Span stream.
Supports multiple recovery strategies (FULL, REPLAY, MINIMAL) though
only FULL is implemented in V6.3.
"""

from enum import Enum
from typing import List, Optional

from .context import TraversalRuntimeContext
from .models import SpanNode, StepNode, TraceNode


class RecoveryStrategy(str, Enum):
    """Context recovery strategies."""

    FULL = "full"       # Recover all required + optional fields
    REPLAY = "replay"   # Replay from step boundaries only (future)
    MINIMAL = "minimal" # Recover only current_path and stack (future)


class ContextRebuilder:
    """Rebuilds a TraversalRuntimeContext from a trace span stream.

    Usage::

        nodes = storage.read(trace_id)
        rebuilder = ContextRebuilder()
        ctx = rebuilder.rebuild(nodes, trace_id, RecoveryStrategy.FULL)
    """

    def rebuild(
        self,
        nodes: List[TraceNode],
        trace_id: str,
        strategy: RecoveryStrategy = RecoveryStrategy.FULL,
    ) -> TraversalRuntimeContext:
        """Rebuild a TraversalRuntimeContext by replaying a node stream.

        Args:
            nodes: Trace nodes from storage, in write order.
            trace_id: The trace ID to associate with the context.
            strategy: Recovery strategy to use.

        Returns:
            A reconstructed TraversalRuntimeContext.
        """
        ctx = TraversalRuntimeContext()
        ctx.trace_id = trace_id

        # Sort by timestamp to ensure correct replay order
        sorted_nodes = sorted(nodes, key=lambda n: n.timestamp)

        for node in sorted_nodes:
            self._apply_node(ctx, node, strategy)

        return ctx

    def _apply_node(
        self,
        ctx: TraversalRuntimeContext,
        node: TraceNode,
        strategy: RecoveryStrategy,
    ) -> None:
        """Apply a single trace node's effect to the context."""
        if isinstance(node, StepNode):
            self._apply_step(ctx, node, strategy)
        elif isinstance(node, SpanNode):
            self._apply_span(ctx, node, strategy)

    # -- step replay ---------------------------------------------------------

    def _apply_step(
        self,
        ctx: TraversalRuntimeContext,
        step: StepNode,
        strategy: RecoveryStrategy,
    ) -> None:
        """Replay a StepNode: update current_path and node_stack."""
        if strategy == RecoveryStrategy.FULL:
            # current_path
            if step.page_path:
                ctx.current_path = list(step.page_path)

            # node_stack — push a synthetic StackFrame
            from .context import StackFrame
            ctx.node_stack.append(StackFrame(
                node_id=step.node_id,
                span_id=step.span_id,
                node_type="step",
            ))

    # -- span replay ---------------------------------------------------------

    def _apply_span(
        self,
        ctx: TraversalRuntimeContext,
        span: SpanNode,
        strategy: RecoveryStrategy,
    ) -> None:
        """Replay a SpanNode: update context based on span_type."""
        if strategy == RecoveryStrategy.FULL:
            if span.span_type == "execution":
                self._apply_execution(ctx, span)
            elif span.span_type == "state_transition":
                self._apply_state_transition(ctx, span)
            elif span.span_type == "error":
                self._apply_error(ctx, span)

    # -- individual span handlers --------------------------------------------

    def _apply_execution(
        self, ctx: TraversalRuntimeContext, span: SpanNode
    ) -> None:
        """Update action_history and visited_pages from an execution span."""
        # Record action
        ctx.action_history.append({
            "action": span.action,
            "status": span.status,
            "target": span.target,
            "timestamp": span.timestamp,
        })
        # Keep max 5 recent actions
        if len(ctx.action_history) > 5:
            ctx.action_history = ctx.action_history[-5:]

        # Update visited_pages from page_after
        if span.page_after:
            ctx.visited_pages.add(span.page_after)

        # Update level1/level2 from page path
        if span.page_after:
            parts = span.page_after.split("/")
            if len(parts) >= 1 and parts[0]:
                ctx.visited_level1_menus.add(parts[0])
            if len(parts) >= 2 and parts[1]:
                ctx.visited_level2_menus.add(parts[1])

    def _apply_state_transition(
        self, ctx: TraversalRuntimeContext, span: SpanNode
    ) -> None:
        """Record state transitions (context itself doesn't track current state)."""
        # State transitions are informational — we record them in action_history
        ctx.action_history.append({
            "action": "state_transition",
            "from_state": span.from_state,
            "to_state": span.to_state,
            "timestamp": span.timestamp,
        })
        if len(ctx.action_history) > 5:
            ctx.action_history = ctx.action_history[-5:]

    def _apply_error(
        self, ctx: TraversalRuntimeContext, span: SpanNode
    ) -> None:
        """Update failed_nodes and consecutive_errors from an error span."""
        ctx.consecutive_errors += 1
        if span.parent_span_id:
            ctx.failed_nodes[span.parent_span_id] = {
                "error_type": span.error_type,
                "error_message": span.error_message,
                "severity": span.severity,
                "timestamp": span.timestamp,
            }
