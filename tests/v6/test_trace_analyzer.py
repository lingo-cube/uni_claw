"""
Unit tests for trace analyzer (tasks 10.12-10.15).

Tests:
- 10.12 build_tree with parent_span_id resolution
- 10.13 step_end backfill logic
- 10.14 session_end backfill logic
- 10.15 All TraceAnalyzer extraction methods
"""

from src.trace.analyzer import TraceAnalyzer, build_tree
from src.trace.models import SessionNode, SpanNode, StepNode


def _make_trace():
    """Create a minimal trace for testing."""
    sess = SessionNode(trace_id="t1", span_id="t1", status="running")
    step1 = StepNode(
        trace_id="t1", span_id="sp1", parent_span_id="t1",
        node_id="n1", page_path=["home", "settings"],
    )
    exec1 = SpanNode(
        trace_id="t1", span_id="ex1", parent_span_id="sp1",
        span_type="execution", action="click", status="success",
        target="btn_settings", page_before="home", page_after="settings",
        duration_ms=150.0,
    )
    ai1 = SpanNode(
        trace_id="t1", span_id="ai1", parent_span_id="sp1",
        span_type="ai_call", capability="vision", provider_id="deepseek",
        success=True, latency_ms=350.0, input_tokens=1200, output_tokens=80,
    )
    err1 = SpanNode(
        trace_id="t1", span_id="er1", parent_span_id="sp1",
        span_type="error", error_type="TimeoutError",
        error_message="timed out", severity="error",
    )
    step_end1 = SpanNode(
        trace_id="t1", span_id="se1", parent_span_id="sp1",
        span_type="step_end", step_span_id="sp1",
        metadata={"result": {"ok": True}},
    )
    sess_end = SpanNode(
        trace_id="t1", span_id="sse1", parent_span_id=None,
        span_type="session_end", status="completed",
    )
    return [sess, step1, exec1, ai1, err1, step_end1, sess_end]


class TestBuildTree:
    """10.12: build_tree with parent_span_id resolution."""

    def test_build_tree_returns_root(self):
        nodes = _make_trace()
        root = build_tree(nodes)
        assert root is not None
        assert isinstance(root, SessionNode)
        assert root.span_id == "t1"

    def test_children_attached(self):
        nodes = _make_trace()
        root = build_tree(nodes)
        # Session should have children (step1)
        assert len(root.children) >= 1

    def test_step_end_backfill(self):
        """10.13: step_end backfills StepNode.result."""
        nodes = _make_trace()
        root = build_tree(nodes)
        step = [n for n in nodes if n.span_id == "sp1"][0]
        assert step.result == {"ok": True}

    def test_session_end_backfill(self):
        """10.14: session_end backfills SessionNode."""
        nodes = _make_trace()
        root = build_tree(nodes)
        assert root.status == "completed"

    def test_returns_none_for_empty(self):
        assert build_tree([]) is None


class TestTraceAnalyzerExtractors:
    """10.15: All extraction methods."""

    def setup_method(self):
        nodes = _make_trace()
        self._analyzer = TraceAnalyzer(nodes)

    def test_extract_page_tree(self):
        pt = self._analyzer.extract_page_tree()
        assert pt["total_pages"] > 0
        assert "visit_counts" in pt

    def test_extract_state_sequence(self):
        # Add a state_transition span
        trans = SpanNode(
            trace_id="t1", span_id="st1",
            span_type="state_transition",
            from_state="IDLE", to_state="TRAVERSING",
            timestamp=1.0,
        )
        analyzer = TraceAnalyzer(_make_trace() + [trans])
        seq = analyzer.extract_state_sequence()
        assert len(seq) == 1
        assert seq[0]["from_state"] == "IDLE"
        assert seq[0]["to_state"] == "TRAVERSING"

    def test_extract_span_chain(self):
        chain = self._analyzer.extract_span_chain("ex1")
        # Should include ex1 → sp1 → t1
        assert len(chain) >= 2
        span_ids = [c["span_id"] for c in chain]
        assert "ex1" in span_ids
        assert "t1" in span_ids  # root

    def test_extract_span_chain_unknown(self):
        chain = self._analyzer.extract_span_chain("nonexistent")
        assert len(chain) == 0

    def test_extract_ai_calls(self):
        calls = self._analyzer.extract_ai_calls()
        assert len(calls) == 1
        assert calls[0]["capability"] == "vision"
        assert calls[0]["input_tokens"] == 1200

    def test_extract_action_sequence(self):
        actions = self._analyzer.extract_action_sequence()
        assert len(actions) == 1
        assert actions[0]["action"] == "click"
        assert actions[0]["status"] == "success"

    def test_extract_error_statistics(self):
        stats = self._analyzer.extract_error_statistics()
        assert stats["total_errors"] == 1
        assert stats["by_type"].get("TimeoutError") == 1

    def test_extract_time_analysis(self):
        ta = self._analyzer.extract_time_analysis()
        assert "total_duration_ms" in ta
        assert "step_count" in ta

    def test_extract_coverage_analysis(self):
        ca = self._analyzer.extract_coverage_analysis()
        assert "total_pages" in ca
        assert "total_nodes" in ca
        assert "page_visits" in ca
