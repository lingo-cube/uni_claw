"""
Simulation tests for trace system (tasks 12.1-12.5).

Tests:
- 12.1 MemoryStorage integration with simulation
- 12.2 Trace generation during simulation
- 12.3 TraceAnalyzer with simulation traces
- 12.4 Simulation trace-based verification
- 12.5 Visualization report generation from traces
"""

import json

from src.trace.analyzer import TraceAnalyzer
from src.trace.context import TraversalRuntimeContext
from src.trace.models import SessionNode, SpanNode, StepNode
from src.trace.recorder import TraceRecorder
from src.trace.recovery import ContextRebuilder, RecoveryStrategy
from src.trace.storage import MemoryStorage


class TestMemoryStorageSimulationIntegration:
    """12.1: MemoryStorage integration with simulation."""

    def test_memory_storage_single_trace(self):
        ms = MemoryStorage()
        sess = SessionNode(trace_id="sim-1", span_id="sim-1")
        ms.write(sess)
        for i in range(5):
            step = StepNode(trace_id="sim-1", span_id=f"sp{i}")
            ms.write(step)
        nodes = ms.read("sim-1")
        assert len(nodes) == 6  # 1 session + 5 steps

    def test_memory_storage_multi_trace_isolation(self):
        ms = MemoryStorage()
        s1 = SessionNode(trace_id="sim-A", span_id="sim-A")
        s2 = SessionNode(trace_id="sim-B", span_id="sim-B")
        ms.write(s1)
        ms.write(s2)
        ms.write(StepNode(trace_id="sim-A", span_id="spA"))
        ms.write(StepNode(trace_id="sim-B", span_id="spB"))
        assert len(ms.read("sim-A")) == 2
        assert len(ms.read("sim-B")) == 2

    def test_memory_storage_clear_and_reuse(self):
        ms = MemoryStorage()
        s = SessionNode(trace_id="sim-1", span_id="sim-1")
        ms.write(s)
        assert len(ms.read("sim-1")) == 1
        ms.clear("sim-1")
        assert len(ms.read("sim-1")) == 0
        # Reuse same trace_id
        s2 = SessionNode(trace_id="sim-1", span_id="sim-1-new")
        ms.write(s2)
        assert len(ms.read("sim-1")) == 1


class TestTraceGenerationDuringSimulation:
    """12.2: Trace generation during simulation."""

    def _run_simulated_traversal(self, num_steps: int = 5):
        """Helper: run a simulated traversal with TraceRecorder."""
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)

        sess = SessionNode(device_model="SimDevice", os_version="Android 14")
        rec.init(sess)

        pages = ["home", "settings", "display", "sound", "about"]
        for i in range(min(num_steps, len(pages))):
            step = StepNode(
                node_id=f"node_{i}",
                step_type="NODE_SELECT",
                page_path=pages[: i + 1],
            )
            rec.record_step_start(step)

            # AI call
            rec.record_span(SpanNode(
                span_type="ai_call",
                capability="vision",
                provider_id="mock",
                success=True,
                latency_ms=200.0,
                input_tokens=500,
                output_tokens=50,
            ))

            # Execution
            rec.record_span(SpanNode(
                span_type="execution",
                action="click",
                status="success",
                target=f"btn_{pages[i]}" if i < len(pages) - 1 else None,
                duration_ms=80.0,
            ))

            rec.record_step_end(step.span_id, {"ok": True})

        rec.finalize("completed")
        return ms.read(sess.trace_id), sess.trace_id

    def test_simulation_generates_nodes(self):
        nodes, tid = self._run_simulated_traversal(5)
        assert len(nodes) > 10  # session + 5*(step+ai+exec+step_end) + session_end

    def test_simulation_trace_has_session(self):
        nodes, tid = self._run_simulated_traversal(3)
        sessions = [n for n in nodes if n.node_type == "session"]
        assert len(sessions) == 1

    def test_simulation_trace_steps_in_order(self):
        nodes, tid = self._run_simulated_traversal(4)
        steps = [n for n in nodes if n.node_type == "step"]
        assert len(steps) == 4
        # Verify timestamps are monotonically increasing
        for i in range(1, len(steps)):
            assert steps[i].timestamp >= steps[i - 1].timestamp


class TestTraceAnalyzerWithSimulation:
    """12.3: TraceAnalyzer with simulation traces."""

    def _run_and_analyze(self, num_steps: int = 5):
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)

        for i in range(num_steps):
            step = StepNode(node_id=f"n{i}", page_path=[f"page_{j}" for j in range(i + 1)])
            rec.record_step_start(step)
            rec.record_span(SpanNode(span_type="execution", action="click", status="success", duration_ms=50.0))
            rec.record_step_end(step.span_id)

        rec.finalize("completed")
        nodes = ms.read(sess.trace_id)
        return TraceAnalyzer(nodes)

    def test_page_tree_from_simulation(self):
        analyzer = self._run_and_analyze(4)
        pt = analyzer.extract_page_tree()
        assert pt["total_pages"] > 0

    def test_action_sequence_from_simulation(self):
        analyzer = self._run_and_analyze(3)
        actions = analyzer.extract_action_sequence()
        assert len(actions) == 3

    def test_time_analysis_from_simulation(self):
        analyzer = self._run_and_analyze(5)
        ta = analyzer.extract_time_analysis()
        assert ta["execution_count"] == 5

    def test_coverage_from_simulation(self):
        analyzer = self._run_and_analyze(4)
        ca = analyzer.extract_coverage_analysis()
        assert ca["total_nodes"] > 0


class TestSimulationTraceVerification:
    """12.4: Simulation trace-based verification."""

    def test_verify_step_count(self):
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)

        for i in range(7):
            step = StepNode(node_id=f"n{i}")
            rec.record_step_start(step)
            rec.record_step_end(step.span_id)
        rec.finalize("completed")

        nodes = ms.read(sess.trace_id)
        step_nodes = [n for n in nodes if n.node_type == "step"]
        assert len(step_nodes) == 7

    def test_verify_span_types_present(self):
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)

        step = StepNode(node_id="n1")
        rec.record_step_start(step)
        rec.record_span(SpanNode(span_type="ai_call", capability="vision", success=True, latency_ms=100.0))
        rec.record_span(SpanNode(span_type="state_transition", from_state="IDLE", to_state="EXECUTE"))
        rec.record_span(SpanNode(span_type="execution", action="click", status="success"))
        rec.record_span(SpanNode(span_type="error", error_type="TestError", error_message="test"))
        rec.record_step_end(step.span_id)
        rec.finalize("error")

        nodes = ms.read(sess.trace_id)
        span_types = {n.span_type for n in nodes if hasattr(n, 'span_type')}
        assert "ai_call" in span_types
        assert "state_transition" in span_types
        assert "execution" in span_types
        assert "error" in span_types
        assert "step_end" in span_types
        assert "session_end" in span_types

    def test_verify_context_recovery(self):
        """Verify that context can be recovered from simulation traces."""
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)

        pages = ["home", "settings", "display"]
        for i, page in enumerate(pages):
            step = StepNode(node_id=f"n{i}", page_path=pages[: i + 1])
            rec.record_step_start(step)
            rec.record_span(SpanNode(
                span_type="execution", action="click", status="success",
                page_before=pages[i - 1] if i > 0 else None,
                page_after=page,
            ))
            rec.record_step_end(step.span_id)
        rec.finalize("completed")

        nodes = ms.read(sess.trace_id)
        rebuilder = ContextRebuilder()
        ctx = rebuilder.rebuild(nodes, sess.trace_id, RecoveryStrategy.FULL)

        assert ctx.trace_id == sess.trace_id
        assert len(ctx.current_path) >= 1
        assert len(ctx.visited_pages) >= 1


class TestVisualizationReportFromTraces:
    """12.5: Visualization report generation from traces."""

    def test_build_tree_for_visualization(self):
        from src.trace.analyzer import build_tree
        from src.trace.recorder import TraceRecorder
        from src.trace.storage import MemoryStorage
        from src.trace.models import SessionNode, StepNode, SpanNode

        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)

        for i in range(3):
            step = StepNode(node_id=f"n{i}")
            rec.record_step_start(step)
            rec.record_span(SpanNode(span_type="execution", action="click", status="success"))
            rec.record_step_end(step.span_id)
        rec.finalize("completed")

        nodes = ms.read(sess.trace_id)
        root = build_tree(nodes)
        assert root is not None
        assert root.node_type == "session"

    def test_page_visit_heatmap_data(self):
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)

        pages = ["home", "settings", "display", "sound", "settings", "home"]
        for i, page in enumerate(pages):
            step = StepNode(node_id=f"n{i}", page_path=[page])
            rec.record_step_start(step)
            rec.record_step_end(step.span_id)
        rec.finalize("completed")

        nodes = ms.read(sess.trace_id)
        analyzer = TraceAnalyzer(nodes)
        ca = analyzer.extract_coverage_analysis()

        assert "page_visits" in ca
        # home visited at least once
        assert "home" in ca["page_visits"]
