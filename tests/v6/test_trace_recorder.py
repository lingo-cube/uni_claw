"""
Unit tests for trace recorder (tasks 10.10-10.11).

Tests:
- 10.10 TraceRecorder init, step, span, finalize methods
- 10.11 StepTracker stack operations (enter, exit, parent)
"""

import time

from src.trace.models import SessionNode, SpanNode, StepNode, generate_id
from src.trace.recorder import StepTracker, TraceRecorder
from src.trace.storage import MemoryStorage


class TestStepTracker:
    """10.11: StepTracker stack operations."""

    def test_initial_state(self):
        st = StepTracker()
        assert st.get_parent_span_id() is None
        assert st.depth == 0

    def test_enter(self):
        st = StepTracker()
        st.on_node_enter("span-1")
        assert st.get_parent_span_id() == "span-1"
        assert st.depth == 1

    def test_enter_multiple(self):
        st = StepTracker()
        st.on_node_enter("span-1")
        st.on_node_enter("span-2")
        assert st.get_parent_span_id() == "span-2"
        assert st.depth == 2

    def test_exit(self):
        st = StepTracker()
        st.on_node_enter("span-1")
        st.on_node_enter("span-2")
        exited = st.on_node_exit()
        assert exited == "span-2"
        assert st.get_parent_span_id() == "span-1"
        assert st.depth == 1

    def test_exit_last(self):
        st = StepTracker()
        st.on_node_enter("span-1")
        exited = st.on_node_exit()
        assert exited == "span-1"
        assert st.get_parent_span_id() is None
        assert st.depth == 0

    def test_exit_empty_returns_none(self):
        st = StepTracker()
        assert st.on_node_exit() is None

    def test_clear(self):
        st = StepTracker()
        st.on_node_enter("a")
        st.on_node_enter("b")
        st.clear()
        assert st.depth == 0
        assert st.get_parent_span_id() is None


class TestTraceRecorder:
    """10.10: TraceRecorder init, step, span, finalize."""

    def setup_method(self):
        self._storage = MemoryStorage()
        self._recorder = TraceRecorder(storage=self._storage)

    def test_init(self):
        sess = SessionNode()
        self._recorder.init(sess)
        assert self._recorder.trace_id == sess.trace_id
        nodes = self._storage.read(sess.trace_id)
        assert len(nodes) == 1

    def test_record_step_start(self):
        sess = SessionNode()
        self._recorder.init(sess)
        step = StepNode(node_id="n1", step_type="NODE_SELECT")
        self._recorder.record_step_start(step)
        nodes = self._storage.read(sess.trace_id)
        step_nodes = [n for n in nodes if n.node_type == "step"]
        assert len(step_nodes) >= 1
        assert step_nodes[0].node_id == "n1"

    def test_record_step_start_sets_parent(self):
        sess = SessionNode()
        self._recorder.init(sess)
        step = StepNode(node_id="n1")
        self._recorder.record_step_start(step)
        nodes = self._storage.read(sess.trace_id)
        step_node = [n for n in nodes if n.node_type == "step"][0]
        # parent should be auto-set from StepTracker (session as root)
        assert step_node.parent_span_id is not None

    def test_record_span(self):
        sess = SessionNode()
        self._recorder.init(sess)
        step = StepNode(node_id="n1")
        self._recorder.record_step_start(step)
        span = SpanNode(span_type="execution", action="click")
        self._recorder.record_span(span)
        nodes = self._storage.read(sess.trace_id)
        span_nodes = [n for n in nodes if n.node_type == "span" and n.span_type == "execution"]
        assert len(span_nodes) == 1

    def test_record_step_end(self):
        sess = SessionNode()
        self._recorder.init(sess)
        step = StepNode(node_id="n1")
        self._recorder.record_step_start(step)
        self._recorder.record_step_end(step.span_id, {"ok": True})
        nodes = self._storage.read(sess.trace_id)
        end_spans = [n for n in nodes if hasattr(n, 'span_type') and n.span_type == "step_end"]
        assert len(end_spans) == 1

    def test_finalize(self):
        sess = SessionNode()
        self._recorder.init(sess)
        step = StepNode(node_id="n1")
        self._recorder.record_step_start(step)
        span = SpanNode(span_type="execution", action="click", status="success")
        self._recorder.record_span(span)
        self._recorder.record_step_end(step.span_id, {"ok": True})
        self._recorder.finalize("completed")
        nodes = self._storage.read(sess.trace_id)
        assert len(nodes) >= 4  # session + step + span + step_end + session_end

    def test_full_flow_node_count(self):
        sess = SessionNode()
        self._recorder.init(sess)
        for i in range(3):
            step = StepNode(node_id=f"n{i}")
            self._recorder.record_step_start(step)
            self._recorder.record_span(SpanNode(
                span_type="execution", action="click", status="success"
            ))
            self._recorder.record_step_end(step.span_id)
        self._recorder.finalize("completed")
        nodes = self._storage.read(sess.trace_id)
        # session + 3*(step + span + step_end) + session_end = 11
        step_count = len([n for n in nodes if n.node_type == "step"])
        span_count = len([n for n in nodes if n.node_type == "span"])
        assert step_count >= 3
        assert span_count >= 7  # 3 exec + 3 step_end + 1 session_end

    def test_log_and_continue_on_write_failure(self):
        """10.10: Write failures should not raise exceptions."""
        sess = SessionNode()
        self._recorder.init(sess)
        # Simulate storage failure by replacing with a broken mock
        original = self._recorder._storage

        class BrokenStorage:
            def write(self, node):
                raise OSError("Disk full")

        self._recorder._storage = BrokenStorage()
        try:
            step = StepNode(node_id="n1")
            self._recorder.record_step_start(step)
            span = SpanNode(span_type="execution")
            self._recorder.record_span(span)
            self._recorder.record_step_end(step.span_id)
            self._recorder.finalize("completed")
        except Exception:
            raise AssertionError("log-and-continue failed: exception was raised")
        finally:
            self._recorder._storage = original
