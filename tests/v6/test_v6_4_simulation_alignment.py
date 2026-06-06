"""
V6.4 Simulation interface alignment validation tests.

Validates:
- MockVisionService implements VisionService ABC
- MockActionExecutor implements OperationExecutor ABC
- SimulationRunner creates real GraphTraversalEngine (no ImportError)
- Simulation trace contains session/step/span nodes
- TraceAnalyzer extracts data from simulation traces
"""

from src.graph.plan import TraversalPlan
from src.graph.node import EntryPolicy, EntryStrategy
from src.simulation.mock_vision import MockVisionService
from src.simulation.mock_action import MockActionExecutor
from src.simulation.operation_executor import ExecutionContext, OperationExecutor
from src.simulation.runner import SimulationRunner
from src.trace.analyzer import TraceAnalyzer
from src.vision.vision_service import VisionService


class TestMockVisionServiceInterface:
    """7.1: MockVisionService implements VisionService ABC."""

    def test_isinstance_vision_service(self):
        mock = MockVisionService({})
        assert isinstance(mock, VisionService)

    def test_analyze_screenshot_returns_page_analysis(self):
        from src.state.content_tree import PageAnalysis
        mock = MockVisionService({"home": {"items": [], "level1_dir": "right", "level2_dir": "bottom"}})
        result = mock.analyze_screenshot(b"")
        assert isinstance(result, PageAnalysis)
        # Default path resolves to "home" → current_path = ["home"]
        assert "home" in result.current_path

    def test_analyze_screenshot_with_path_context(self):
        mock = MockVisionService({
            "home/settings": {
                "page_name": "settings",
                "items": [{"text": "WiFi", "type": "menu_item", "coordinate": {"x": 0.5, "y": 0.3}}],
                "level1_dir": "right", "level2_dir": "bottom",
            }
        })
        mock.set_path_context(["home", "settings"])
        result = mock.analyze_screenshot(b"")
        assert isinstance(result, type(mock.analyze_screenshot(b"")))
        assert result.current_path == ["home", "settings"]

    def test_find_app_entry(self):
        mock = MockVisionService({})
        result = mock.find_app_entry(b"", "TargetApp")
        assert result is not None
        assert "x" in result and "y" in result

    def test_call_count(self):
        mock = MockVisionService({"home": {"items": [], "level1_dir": "right", "level2_dir": "bottom"}})
        assert mock.call_count == 0
        mock.analyze_screenshot(b"")
        mock.analyze_screenshot(b"")
        assert mock.call_count == 2


class TestMockActionExecutorInterface:
    """7.2: MockActionExecutor implements OperationExecutor ABC."""

    def test_isinstance_operation_executor(self):
        mock = MockActionExecutor()
        assert isinstance(mock, OperationExecutor)

    def test_execute_returns_execution_result(self):
        from src.simulation.operation_executor import ExecutionResult
        mock = MockActionExecutor()
        ctx = ExecutionContext(
            node_id="n1", node_name="Settings",
            operation={"action": "click", "target": "btn_wifi"},
        )
        result = mock.execute(ctx)
        assert isinstance(result, ExecutionResult)
        assert result.success is True

    def test_get_executed_actions(self):
        mock = MockActionExecutor()
        mock.execute(ExecutionContext("n1", "Home", {"action": "click"}))
        mock.execute(ExecutionContext("n2", "Settings", {"action": "swipe"}))
        actions = mock.get_executed_actions()
        assert len(actions) == 2
        assert "click" in actions
        assert "swipe" in actions

    def test_clear_history(self):
        mock = MockActionExecutor()
        mock.execute(ExecutionContext("n1", "Home", {"action": "click"}))
        assert mock.get_action_count() == 1
        mock.clear_history()
        assert mock.get_action_count() == 0

    def test_simulate_delay(self):
        import time
        mock = MockActionExecutor(simulate_delay=0.01)
        ctx = ExecutionContext("n1", "Home", {"action": "click"})
        t0 = time.time()
        mock.execute(ctx)
        elapsed = time.time() - t0
        assert elapsed >= 0.009  # allow small timing variance

    def test_history_property(self):
        mock = MockActionExecutor()
        mock.execute(ExecutionContext("n1", "Home", {"action": "click", "target": "btn"}))
        assert len(mock.history) == 1
        assert mock.history[0]["action_type"] == "click"


class TestSimulationRunnerEngineIntegration:
    """7.3-7.4: SimulationRunner creates real engine, trace has nodes."""

    def test_engine_created_successfully(self):
        vp = {"home": {"items": [], "level1_dir": "right", "level2_dir": "bottom"}}
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        runner = SimulationRunner(vp, plan)
        assert runner.engine is not None
        assert type(runner.engine).__name__ == "GraphTraversalEngine"

    def test_run_produces_trace_with_session_node(self):
        vp = {"home": {"items": [], "level1_dir": "right", "level2_dir": "bottom"}}
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        runner = SimulationRunner(vp, plan)
        result = runner.run()
        nodes = runner.storage.read(result.trace_id)

        assert any(n.node_type == "session" for n in nodes)
        assert any(n.node_type == "span" for n in nodes)

    def test_run_produces_extractable_trace(self):
        vp = {"home": {"items": [], "level1_dir": "right", "level2_dir": "bottom"}}
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        runner = SimulationRunner(vp, plan)
        result = runner.run()

        analyzer = TraceAnalyzer(runner.storage.read(result.trace_id))
        time_analysis = analyzer.extract_time_analysis()
        error_stats = analyzer.extract_error_statistics()
        coverage = analyzer.extract_coverage_analysis()

        assert "total_duration_ms" in time_analysis
        assert "total_errors" in error_stats
        assert "total_pages" in coverage

    def test_simulation_result_has_trace_id(self):
        vp = {"home": {"items": [], "level1_dir": "right", "level2_dir": "bottom"}}
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        runner = SimulationRunner(vp, plan)
        result = runner.run()
        assert len(result.trace_id) == 26

    def test_no_fallback_methods_exist(self):
        """Verify DFS fallback methods are deleted."""
        runner = SimulationRunner(
            {"home": {"items": [], "level1_dir": "right", "level2_dir": "bottom"}},
            TraversalPlan(entry_app="TestApp", entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN)),
        )
        assert not hasattr(runner, "_execute_fallback_simulation")
        assert not hasattr(runner, "_interact_with_next_element")
        assert not hasattr(runner, "_execute_element_action")
        assert not hasattr(runner, "_go_back")

    def test_no_redundant_state_fields(self):
        runner = SimulationRunner(
            {"home": {"items": [], "level1_dir": "right", "level2_dir": "bottom"}},
            TraversalPlan(entry_app="TestApp", entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN)),
        )
        assert not hasattr(runner, "current_path")
        assert not hasattr(runner, "visited_pages")
        assert not hasattr(runner, "tracer")
