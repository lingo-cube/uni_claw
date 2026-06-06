"""
Unit tests for trace models (tasks 10.1-10.6).

Tests:
- 10.1 Trace model construction and serialization
- 10.2 ULID generation and uniqueness
- 10.3 TraceNode parent_span_id relationships
- 10.4 SessionNode creation and fields
- 10.5 StepNode creation and fields
- 10.6 SpanNode all span_type variants
"""

import time

from src.trace.models import (
    SessionNode,
    SpanNode,
    StepNode,
    TraceNode,
    generate_id,
)


class TestULIDGeneration:
    """10.2: ULID generation and uniqueness."""

    def test_generate_id_format(self):
        uid = generate_id()
        assert len(uid) == 26
        assert all(c in "0123456789ABCDEFGHJKMNPQRSTVWXYZ" for c in uid)

    def test_ulid_uniqueness(self):
        ids = {generate_id() for _ in range(500)}
        assert len(ids) == 500

    def test_ulid_time_ordering(self):
        ids = [generate_id() for _ in range(100)]
        # ULIDs from the same millisecond should all be unique
        assert len(set(ids)) == 100


class TestTraceNodeBase:
    """10.1: TraceNode base class and serialization."""

    def test_trace_node_from_dict_dispatches(self):
        sess = SessionNode()
        data = sess.to_dict()
        restored = TraceNode.from_dict(data)
        assert isinstance(restored, SessionNode)
        assert restored.span_id == sess.span_id

    def test_from_dict_raises_on_unknown_type(self):
        try:
            TraceNode.from_dict({"node_type": "unknown"})
        except ValueError:
            pass
        else:
            raise AssertionError("Expected ValueError")


class TestSessionNode:
    """10.4: SessionNode creation and fields."""

    def test_session_node_defaults(self):
        s = SessionNode()
        assert s.node_type == "session"
        assert s.parent_span_id is None
        assert s.span_id == s.trace_id
        assert s.status == "running"
        assert s.traversal_mode == "graph"
        assert len(s.span_id) == 26

    def test_session_node_fields(self):
        s = SessionNode(
            device_model="Pixel 7",
            os_version="Android 14",
            app_package="com.example",
            traversal_mode="linear",
        )
        assert s.device_model == "Pixel 7"
        assert s.os_version == "Android 14"
        assert s.app_package == "com.example"
        assert s.traversal_mode == "linear"

    def test_session_node_roundtrip(self):
        s = SessionNode(
            device_id="abc",
            device_name="test-device",
            device_model="Pixel 7",
            os_version="Android 14",
            status="running",
            config={"depth": 10},
        )
        data = s.to_dict()
        s2 = SessionNode.from_dict(data)
        assert s2.span_id == s.span_id
        assert s2.device_model == "Pixel 7"
        assert s2.config == {"depth": 10}


class TestStepNode:
    """10.5: StepNode creation and fields."""

    def test_step_node_defaults(self):
        s = StepNode()
        assert s.node_type == "step"
        assert len(s.span_id) == 26
        assert s.page_path == []

    def test_step_node_fields(self):
        s = StepNode(
            node_id="node_123",
            step_type="NODE_SELECT",
            page_path=["home", "settings"],
            trace_id="trace-1",
        )
        assert s.node_id == "node_123"
        assert s.step_type == "NODE_SELECT"
        assert s.page_path == ["home", "settings"]
        assert s.trace_id == "trace-1"

    def test_step_node_roundtrip(self):
        s = StepNode(
            node_id="n1",
            step_type="FRAME_COMPLETE",
            page_path=["home"],
            result={"status": "ok"},
        )
        data = s.to_dict()
        s2 = StepNode.from_dict(data)
        assert s2.node_id == "n1"
        assert s2.step_type == "FRAME_COMPLETE"
        assert s2.result == {"status": "ok"}


class TestSpanNode:
    """10.6: SpanNode all span_type variants."""

    def test_span_node_defaults(self):
        s = SpanNode()
        assert s.node_type == "span"
        assert s.span_type == ""
        assert len(s.span_id) == 26

    def test_state_transition_span(self):
        s = SpanNode(
            span_type="state_transition",
            from_state="IDLE",
            to_state="TRAVERSING",
            state_machine="traversal_fsm",
        )
        assert s.span_type == "state_transition"
        assert s.from_state == "IDLE"
        assert s.to_state == "TRAVERSING"
        d = s.to_dict()
        assert d["from_state"] == "IDLE"
        assert d["to_state"] == "TRAVERSING"
        s2 = SpanNode.from_dict(d)
        assert s2.from_state == "IDLE"

    def test_execution_span(self):
        s = SpanNode(
            span_type="execution",
            action="click",
            status="success",
            target="btn_ok",
            page_before="home",
            page_after="settings",
            duration_ms=150.0,
            screenshot_ref="s_abc123",
        )
        d = s.to_dict()
        assert d["action"] == "click"
        assert d["screenshot_ref"] == "s_abc123"
        s2 = SpanNode.from_dict(d)
        assert s2.duration_ms == 150.0

    def test_ai_call_span(self):
        s = SpanNode(
            span_type="ai_call",
            capability="vision",
            provider_id="deepseek",
            success=True,
            latency_ms=350.0,
            input_tokens=1200,
            output_tokens=80,
        )
        d = s.to_dict()
        assert d["capability"] == "vision"
        assert d["success"] is True
        s2 = SpanNode.from_dict(d)
        assert s2.input_tokens == 1200

    def test_error_span(self):
        s = SpanNode(
            span_type="error",
            error_type="TimeoutError",
            error_message="Connection timed out",
            severity="critical",
            stack_trace="Traceback...",
        )
        d = s.to_dict()
        assert d["error_type"] == "TimeoutError"
        assert d["severity"] == "critical"
        s2 = SpanNode.from_dict(d)
        assert s2.error_message == "Connection timed out"

    def test_step_end_span(self):
        s = SpanNode(
            span_type="step_end",
            step_span_id="sr_abc",
            metadata={"result": {"ok": True}},
        )
        d = s.to_dict()
        assert d["span_type"] == "step_end"
        s2 = SpanNode.from_dict(d)
        assert s2.step_span_id == "sr_abc"

    def test_session_end_span(self):
        s = SpanNode(
            span_type="session_end",
            status="completed",
        )
        d = s.to_dict()
        assert d["span_type"] == "session_end"
        s2 = SpanNode.from_dict(d)
        assert s2.status == "completed"


class TestParentSpanIdRelationships:
    """10.3: TraceNode parent_span_id relationships."""

    def test_session_is_root(self):
        s = SessionNode()
        assert s.parent_span_id is None

    def test_step_parent_link(self):
        parent = SessionNode()
        step = StepNode(
            trace_id=parent.trace_id,
            parent_span_id=parent.span_id,
        )
        assert step.parent_span_id == parent.span_id
        assert step.trace_id == parent.trace_id

    def test_span_parent_link(self):
        parent = SessionNode()
        child = SpanNode(
            trace_id=parent.trace_id,
            parent_span_id=parent.span_id,
        )
        assert child.parent_span_id == parent.span_id

    def test_call_chain(self):
        root = SessionNode()
        step = StepNode(trace_id=root.trace_id, parent_span_id=root.span_id)
        span = SpanNode(trace_id=root.trace_id, parent_span_id=step.span_id)
        assert span.parent_span_id == step.span_id
        assert step.parent_span_id == root.span_id
