"""
Integration tests for trace system (tasks 11.1-11.9).

Tests:
- 11.1 MockEngine for trace testing
- 11.2 End-to-end trace generation with MockEngine
- 11.3 Trace output format and structure
- 11.4 Trace JSONL parsing and validation
- 11.5 session.json creation and content
- 11.6 Screenshot index mapping
- 11.7 Context recovery from generated traces
- 11.8 Context recovery correctness validation
- 11.9 Trace directory structure and file organization
"""

import json
import tempfile
import time
from pathlib import Path

from src.trace.analyzer import TraceAnalyzer
from src.trace.context import Session, StackFrame, TraversalRuntimeContext
from src.trace.models import SessionNode, SpanNode, StepNode, TraceNode
from src.trace.recorder import TraceRecorder
from src.trace.recovery import ContextRebuilder, RecoveryStrategy
from tests.config.constants import Timeout
from src.trace.storage import FileStorage, MemoryStorage


# ============================================================================
# 11.1: MockEngine for trace testing
# ============================================================================


class MockEngine:
    """Minimal engine simulator that generates a realistic trace.

    Simulates a traversal run with session init, multiple steps,
    state transitions, AI calls, execution spans, errors, and finalize.
    """

    def __init__(self, storage=None):
        self._storage = storage or MemoryStorage()
        self._recorder = TraceRecorder(storage=self._storage)
        self._session_id = ""

    def run(self, step_count: int = 3) -> str:
        """Run a simulated traversal and return the trace_id."""
        session = Session(device_model="Pixel 7", os_version="Android 14")
        self._session_id = session.session_id

        sess_node = SessionNode(
            trace_id=session.session_id,
            span_id=session.session_id,
            device_model=session.device_model,
            os_version=session.os_version,
            status=session.status,
            traversal_mode=session.traversal_mode,
            config=session.config,
        )
        # Write session.json if using FileStorage
        if hasattr(self._storage, "write_session"):
            self._storage.write_session(session.to_dict(), session.session_id)
        self._recorder.init(sess_node)

        page_path = ["home"]
        for i in range(step_count):
            step = StepNode(
                node_id=f"node_{i}",
                step_type="NODE_SELECT",
                page_path=list(page_path),
            )
            self._recorder.record_step_start(step)

            # AI call span
            ai = SpanNode(
                trace_id=session.session_id,
                span_type="ai_call",
                capability="vision",
                provider_id="deepseek",
                success=True,
                latency_ms=300.0 + i * 50,
                input_tokens=1000 + i * 100,
                output_tokens=80,
            )
            self._recorder.record_span(ai)

            # Execution span
            exec_span = SpanNode(
                span_type="execution",
                action="click",
                status="success" if i < step_count - 1 else "failed",
                target=f"btn_page_{i}",
                page_before=page_path[-1],
                page_after=f"page_{i+1}",
                duration_ms=100.0 + i * 20,
            )
            self._recorder.record_span(exec_span)

            if i == step_count - 1:
                # Simulate an error on last step
                err = SpanNode(
                    span_type="error",
                    error_type="TimeoutError",
                    error_message="Operation timed out",
                    severity="error",
                )
                self._recorder.record_span(err)

            self._recorder.record_step_end(
                step.span_id, {"ok": i < step_count - 1}
            )

            page_path.append(f"page_{i+1}")

        self._recorder.finalize("completed" if step_count > 1 else "error")
        return session.session_id


# ============================================================================
# Tests
# ============================================================================


class TestMockEngine:
    """11.1-11.2: MockEngine end-to-end trace generation."""

    def test_mock_engine_generates_trace(self):
        engine = MockEngine()
        tid = engine.run(step_count=3)
        assert len(tid) == 26
        nodes = engine._storage.read(tid)
        assert len(nodes) > 0

    def test_mock_engine_trace_has_all_node_types(self):
        engine = MockEngine()
        tid = engine.run(step_count=3)
        nodes = engine._storage.read(tid)
        types = {n.node_type for n in nodes}
        assert "session" in types
        assert "step" in types
        assert "span" in types

    def test_mock_engine_trace_has_span_types(self):
        engine = MockEngine()
        tid = engine.run(step_count=3)
        nodes = engine._storage.read(tid)
        span_types = {n.span_type for n in nodes if hasattr(n, 'span_type')}
        assert "ai_call" in span_types
        assert "execution" in span_types
        assert "error" in span_types
        assert "step_end" in span_types
        assert "session_end" in span_types


class TestTraceOutputFormat:
    """11.3-11.4: Trace output format and JSONL validation."""

    def test_all_nodes_serializable_to_json(self):
        engine = MockEngine()
        tid = engine.run(step_count=2)
        nodes = engine._storage.read(tid)
        for node in nodes:
            d = node.to_dict()
            json_str = json.dumps(d, default=str)
            assert json_str is not None
            # Round-trip
            parsed = json.loads(json_str)
            restored = TraceNode.from_dict(parsed)
            assert restored.span_id == node.span_id

    def test_trace_ids_consistent(self):
        engine = MockEngine()
        tid = engine.run(step_count=2)
        nodes = engine._storage.read(tid)
        for node in nodes:
            assert node.trace_id == tid, f"Node {node.span_id} has wrong trace_id"

    def test_jsonl_format(self):
        """11.4: Trace nodes can be written and read as JSONL."""
        import os, shutil
        tmpdir = tempfile.mkdtemp()
        try:
            fs = FileStorage(base_dir=tmpdir)
            engine = MockEngine(storage=fs)
            tid = engine.run(step_count=2)
            fs.flush(timeout=Timeout.FLUSH)

            # Read JSONL file directly
            jsonl_path = Path(tmpdir) / tid / "trace.jsonl"
            assert jsonl_path.exists()
            with open(jsonl_path) as f:
                lines = [line.strip() for line in f if line.strip()]
            assert len(lines) > 0

            for line in lines:
                data = json.loads(line)
                assert "trace_id" in data
                assert "span_id" in data
                assert "node_type" in data
                node = TraceNode.from_dict(data)
                assert node is not None
        finally:
            shutil.rmtree(tmpdir)


class TestSessionJson:
    """11.5: session.json creation and content."""

    def test_session_json_created(self):
        engine = MockEngine()
        tid = engine.run(step_count=2)
        nodes = engine._storage.read(tid)
        sess = [n for n in nodes if n.node_type == "session"][0]
        # finalize sets status to "completed"
        assert sess.status in ("running", "completed")
        assert sess.device_model == "Pixel 7"
        assert sess.os_version == "Android 14"

    def test_session_json_with_filestorage(self):
        import shutil
        tmpdir = tempfile.mkdtemp()
        try:
            fs = FileStorage(base_dir=tmpdir)
            sess = SessionNode(device_model="Pixel 7", os_version="Android 14")
            fs.write(sess)
            fs.write_session(sess.to_dict(), sess.trace_id)
            fs.flush(timeout=Timeout.FLUSH)

            sd = fs.read_session(sess.trace_id)
            assert sd is not None
            assert sd.get("device_model") == "Pixel 7"
        finally:
            shutil.rmtree(tmpdir)


class TestScreenshotIndexMapping:
    """11.6: Screenshot index mapping."""

    def test_screenshot_index_write_read(self):
        ms = MemoryStorage()
        sess = SessionNode()
        ms.write(sess)
        # Can't test FileStorage screenshot index in MemoryStorage
        import shutil
        tmpdir = tempfile.mkdtemp()
        try:
            fs = FileStorage(base_dir=tmpdir)
            fs.write(sess)
            fs.write_screenshot_index({"s_1": "home_001.png", "s_2": "settings_001.png"}, sess.trace_id)
            idx = fs.read_screenshot_index(sess.trace_id)
            assert idx == {"s_1": "home_001.png", "s_2": "settings_001.png"}
        finally:
            shutil.rmtree(tmpdir)


class TestContextRecoveryIntegration:
    """11.7-11.8: Context recovery from generated traces."""

    def test_recovery_from_mock_engine_trace(self):
        engine = MockEngine()
        tid = engine.run(step_count=3)
        nodes = engine._storage.read(tid)

        rebuilder = ContextRebuilder()
        ctx = rebuilder.rebuild(nodes, tid, RecoveryStrategy.FULL)

        assert ctx.trace_id == tid
        assert len(ctx.current_path) >= 1
        assert len(ctx.node_stack) >= 1
        assert len(ctx.visited_pages) >= 1
        assert len(ctx.action_history) >= 1

    def test_recovery_correctness(self):
        """11.8: Verify recovery correctness."""
        # Create a trace with known state
        tid = "correctness-trace"
        sess = SessionNode(trace_id=tid, span_id=tid, timestamp=0.0)
        step = StepNode(
            trace_id=tid, span_id="sp1", parent_span_id=tid,
            node_id="settings", page_path=["home", "settings"],
            timestamp=0.1,
        )
        exec_span = SpanNode(
            trace_id=tid, span_id="ex1", parent_span_id="sp1",
            span_type="execution", action="click",
            target="btn_display", status="success",
            page_before="home/settings", page_after="home/settings/display",
            timestamp=0.2,
        )
        nodes = [sess, step, exec_span]

        rebuilder = ContextRebuilder()
        ctx = rebuilder.rebuild(nodes, tid, RecoveryStrategy.FULL)

        # current_path should match last StepNode page_path
        assert ctx.current_path == ["home", "settings"]
        # visited_pages should contain page_after
        assert "home/settings/display" in ctx.visited_pages
        # node_stack should have the step
        assert ctx.node_stack[0].node_id == "settings"


class TestTraceDirectoryStructure:
    """11.9: Trace directory structure and file organization."""

    def test_directory_structure(self):
        import shutil
        tmpdir = tempfile.mkdtemp()
        try:
            fs = FileStorage(base_dir=tmpdir)
            sess = SessionNode()
            fs.write(sess)
            fs.write_screenshot_index({"s_1": "img.png"}, sess.trace_id)
            fs.flush(timeout=Timeout.FLUSH)

            trace_dir = Path(tmpdir) / sess.trace_id
            assert trace_dir.exists()
            assert (trace_dir / "trace.jsonl").exists()
            assert (trace_dir / "screenshots").exists()
            assert (trace_dir / "screenshots" / "index.json").exists()
        finally:
            shutil.rmtree(tmpdir)
