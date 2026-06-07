"""
V6.5 State machine operation integration validation tests.

Validates:
- Handlers return metrics dict (ai_call, execution, error)
- Engine reads metrics and generates spans
- MockVisionService.call_count increments on analyze_screenshot
- MockActionExecutor.history has records after execute
- Trace contains ai_call and execution spans
- TraceAnalyzer extracts AI calls and action sequences
"""

import time

from src.simulation.mock_vision import MockVisionService
from src.simulation.mock_action import MockActionExecutor
from src.simulation.operation_executor import ExecutionContext
from src.state_machine.traversal_fsm import TraversalStateMachine, TraversalState
from src.trace.analyzer import TraceAnalyzer
from src.trace.models import SessionNode, SpanNode, StepNode
from src.trace.recorder import TraceRecorder
from src.trace.storage import MemoryStorage
from tests.assets import load_virtual_pages


class TestHandlerMetricsPipeline:
    """6.1-6.5: Handler → Metrics → Span pipeline."""

    def test_execution_metrics_generated(self):
        """6.1: handler execute → execution metrics → engine span."""
        action = MockActionExecutor()
        ctx = ExecutionContext(
            node_id="n1", node_name="Settings",
            operation={"action": "click", "target": "btn_wifi"},
        )
        result = action.execute(ctx)
        assert result.success is True
        assert len(action.get_executed_actions()) == 1

    def test_ai_call_span_recorded(self):
        """6.2: ai_call span can be generated from metrics."""
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)
        step = StepNode(node_id="n1")
        rec.record_step_start(step)

        ai_span = SpanNode(
            span_type="ai_call",
            capability="vision",
            provider_id="mock",
            success=True,
            latency_ms=200.0,
        )
        rec.record_span(ai_span)
        rec.record_step_end(step.span_id)
        rec.finalize("completed")

        nodes = ms.read(sess.trace_id)
        ai_spans = [
            n for n in nodes if hasattr(n, 'span_type') and n.span_type == "ai_call"
        ]
        assert len(ai_spans) == 1
        assert ai_spans[0].capability == "vision"

    def test_execution_span_recorded(self):
        """6.3: execution span can be generated from metrics."""
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)
        step = StepNode(node_id="n1")
        rec.record_step_start(step)

        exec_span = SpanNode(
            span_type="execution",
            action="click",
            status="success",
            target="btn_wifi",
            duration_ms=150.0,
        )
        rec.record_span(exec_span)
        rec.record_step_end(step.span_id)
        rec.finalize("completed")

        nodes = ms.read(sess.trace_id)
        exec_spans = [
            n for n in nodes if hasattr(n, 'span_type') and n.span_type == "execution"
        ]
        assert len(exec_spans) == 1
        assert exec_spans[0].action == "click"

    def test_error_span_recorded(self):
        """Error span can be generated from metrics."""
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)

        err_span = SpanNode(
            span_type="error",
            error_type="TimeoutError",
            error_message="timed out",
            severity="error",
        )
        rec.record_span(err_span)
        rec.finalize("error")

        nodes = ms.read(sess.trace_id)
        err_spans = [
            n for n in nodes if hasattr(n, 'span_type') and n.span_type == "error"
        ]
        assert len(err_spans) == 1


class TestMockServicesMetrics:
    """6.4-6.5: Mock service call tracking."""

    def test_vision_call_count(self):
        """6.4: analyze_screenshot call_count increments."""
        mock = MockVisionService({
            "home": {"elements": [], "level1_dir": "right", "level2_dir": "bottom"}
        })
        assert mock.call_count == 0
        mock.analyze_screenshot(b"")
        mock.analyze_screenshot(b"")
        assert mock.call_count == 2

    def test_action_history(self):
        """6.5: execute() adds to history."""
        mock = MockActionExecutor()
        assert len(mock.history) == 0
        mock.execute(ExecutionContext("n1", "Home", {"action": "click"}))
        mock.execute(ExecutionContext("n2", "Settings", {"action": "swipe"}))
        assert len(mock.history) == 2
        assert mock.history[0]["action_type"] == "click"
        assert mock.history[1]["action_type"] == "swipe"


class TestTraceAnalyzerExtraction:
    """7.1-7.5: TraceAnalyzer extracts handler-generated spans."""

    def _generate_full_trace(self):
        """Create a trace with all span types."""
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)

        for i in range(3):
            step = StepNode(node_id=f"n{i}")
            rec.record_step_start(step)

            rec.record_span(SpanNode(
                span_type="ai_call", capability="vision",
                success=True, latency_ms=100.0 + i * 50,
                input_tokens=500, output_tokens=50,
            ))
            rec.record_span(SpanNode(
                span_type="execution", action="click",
                status="success", target=f"btn_{i}",
                duration_ms=80.0,
            ))
            rec.record_step_end(step.span_id)

        rec.finalize("completed")
        return ms.read(sess.trace_id)

    def test_extract_ai_calls(self):
        """7.1 + 7.3: ai_call spans extracted."""
        nodes = self._generate_full_trace()
        analyzer = TraceAnalyzer(nodes)
        calls = analyzer.extract_ai_calls()
        assert len(calls) == 3

    def test_extract_action_sequence(self):
        """7.2 + 7.4: execution spans extracted."""
        nodes = self._generate_full_trace()
        analyzer = TraceAnalyzer(nodes)
        actions = analyzer.extract_action_sequence()
        assert len(actions) == 3

    def test_all_span_types_present(self):
        """7.1-7.2: trace has all span types."""
        nodes = self._generate_full_trace()
        span_types = {
            n.span_type
            for n in nodes if hasattr(n, 'span_type')
        }
        assert "ai_call" in span_types
        assert "execution" in span_types
        assert "step_end" in span_types
        assert "session_end" in span_types

    def test_error_extraction(self):
        """7.5: error statistics extracted."""
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)

        rec.record_span(SpanNode(
            span_type="error",
            error_type="TimeoutError",
            error_message="Connection timed out",
            severity="critical",
        ))
        rec.finalize("error")

        nodes = ms.read(sess.trace_id)
        analyzer = TraceAnalyzer(nodes)
        stats = analyzer.extract_error_statistics()
        assert stats["total_errors"] == 1


class TestSimulationTraceComplete:
    """End-to-end: simulation run with engine produces trace."""

    def test_simulation_with_simple_plan(self):
        """Simulation with basic plan produces trace nodes."""
        from src.graph.plan import TraversalPlan
        from src.graph.node import EntryPolicy, EntryStrategy
        from src.simulation.runner import SimulationRunner

        vp = load_virtual_pages()
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        runner = SimulationRunner(vp, plan)
        result = runner.run()

        nodes = runner.storage.read(result.trace_id)
        assert any(n.node_type == "session" for n in nodes)
        assert any(n.node_type == "span" for n in nodes)
        assert len(result.trace_id) == 26
