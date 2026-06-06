"""
Unit tests for context recovery (tasks 10.19-10.20).

Tests:
- 10.19 ContextRebuilder FULL recovery strategy
- 10.20 Span field validation (internal vs external)
"""

from src.trace.models import SessionNode, SpanNode, StepNode
from src.trace.recovery import ContextRebuilder, RecoveryStrategy


def _make_recovery_trace():
    """Create a trace suitable for context recovery testing."""
    tid = "recovery-trace-1"
    sess = SessionNode(trace_id=tid, span_id=tid)
    step1 = StepNode(
        trace_id=tid, span_id="sp1", parent_span_id=tid,
        node_id="home", page_path=["home"],
        timestamp=0.1,
    )
    exec1 = SpanNode(
        trace_id=tid, span_id="ex1", parent_span_id="sp1",
        span_type="execution", action="click",
        target="btn_settings", status="success",
        page_before="home", page_after="home/settings",
        timestamp=0.2,
    )
    step2 = StepNode(
        trace_id=tid, span_id="sp2", parent_span_id="sp1",
        node_id="settings", page_path=["home", "settings"],
        timestamp=0.3,
    )
    exec2 = SpanNode(
        trace_id=tid, span_id="ex2", parent_span_id="sp2",
        span_type="execution", action="click",
        target="btn_display", status="success",
        page_before="home/settings", page_after="home/settings/display",
        timestamp=0.4,
    )
    err1 = SpanNode(
        trace_id=tid, span_id="er1", parent_span_id="sp2",
        span_type="error", error_type="TimeoutError",
        error_message="timed out", severity="error",
        timestamp=0.5,
    )
    trans1 = SpanNode(
        trace_id=tid, span_id="st1", parent_span_id=None,
        span_type="state_transition",
        from_state="IDLE", to_state="TRAVERSING",
        timestamp=0.05,
    )
    return [sess, step1, exec1, step2, exec2, err1, trans1]


class TestContextRebuilder:
    """10.19: ContextRebuilder FULL recovery strategy."""

    def test_rebuild_returns_context(self):
        nodes = _make_recovery_trace()
        rebuilder = ContextRebuilder()
        ctx = rebuilder.rebuild(nodes, "recovery-trace-1", RecoveryStrategy.FULL)
        assert ctx.trace_id == "recovery-trace-1"

    def test_rebuild_current_path(self):
        nodes = _make_recovery_trace()
        rebuilder = ContextRebuilder()
        ctx = rebuilder.rebuild(nodes, "recovery-trace-1", RecoveryStrategy.FULL)
        # Last step page_path should be current
        assert len(ctx.current_path) >= 1
        assert "home" in ctx.current_path

    def test_rebuild_node_stack(self):
        nodes = _make_recovery_trace()
        rebuilder = ContextRebuilder()
        ctx = rebuilder.rebuild(nodes, "recovery-trace-1", RecoveryStrategy.FULL)
        # node_stack should contain step nodes
        assert len(ctx.node_stack) >= 1

    def test_rebuild_visited_pages(self):
        """10.19: visited_pages recovered from execution spans."""
        nodes = _make_recovery_trace()
        rebuilder = ContextRebuilder()
        ctx = rebuilder.rebuild(nodes, "recovery-trace-1", RecoveryStrategy.FULL)
        assert len(ctx.visited_pages) >= 1

    def test_rebuild_action_history(self):
        nodes = _make_recovery_trace()
        rebuilder = ContextRebuilder()
        ctx = rebuilder.rebuild(nodes, "recovery-trace-1", RecoveryStrategy.FULL)
        # execution + state_transition spans → action_history
        assert len(ctx.action_history) >= 1

    def test_rebuild_failed_nodes(self):
        nodes = _make_recovery_trace()
        rebuilder = ContextRebuilder()
        ctx = rebuilder.rebuild(nodes, "recovery-trace-1", RecoveryStrategy.FULL)
        # error span should add to failed_nodes
        assert len(ctx.failed_nodes) >= 1

    def test_rebuild_consecutive_errors(self):
        nodes = _make_recovery_trace()
        rebuilder = ContextRebuilder()
        ctx = rebuilder.rebuild(nodes, "recovery-trace-1", RecoveryStrategy.FULL)
        assert ctx.consecutive_errors >= 1

    def test_rebuild_empty_nodes(self):
        rebuilder = ContextRebuilder()
        ctx = rebuilder.rebuild([], "empty-trace", RecoveryStrategy.FULL)
        assert ctx.trace_id == "empty-trace"
        assert ctx.node_stack == []

    def test_recovery_strategy_enum(self):
        assert RecoveryStrategy.FULL == "full"
        assert RecoveryStrategy.REPLAY == "replay"
        assert RecoveryStrategy.MINIMAL == "minimal"


class TestSpanFieldValidation:
    """10.20: Span field validation (internal vs external)."""

    def test_internal_fields_present(self):
        """State transition must have from_state and to_state."""
        s = SpanNode(
            span_type="state_transition",
            from_state="IDLE",
            to_state="TRAVERSING",
        )
        assert s.from_state is not None
        assert s.to_state is not None

    def test_execution_internal_fields(self):
        s = SpanNode(
            span_type="execution",
            action="click",
            status="success",
        )
        assert s.action is not None
        assert s.status is not None

    def test_ai_call_internal_fields(self):
        s = SpanNode(
            span_type="ai_call",
            capability="vision",
            success=True,
            latency_ms=100.0,
        )
        assert s.capability is not None
        assert s.success is not None

    def test_error_internal_fields(self):
        s = SpanNode(
            span_type="error",
            error_type="TimeoutError",
            error_message="timed out",
        )
        assert s.error_type is not None
        assert s.error_message is not None

    def test_external_fields_can_be_none(self):
        """External fields like confidence, output_summary can be absent."""
        s = SpanNode(
            span_type="execution",
            action="click",
            status="success",
            # page_before, page_after are optional / "external"
        )
        assert s.page_before is None  # OK — optional external field
