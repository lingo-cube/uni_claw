"""
Tests for V6.4 simulation components.

Tests MockVisionService (VisionService ABC), MockActionExecutor (OperationExecutor ABC),
InMemoryTracer, SimulationRunner (real GraphTraversalEngine), and PlanDebugger.
"""

import json
import time

from src.graph.plan import TraversalPlan
from src.graph.node import TraversalNode, NodeType, Operation, EntryPolicy, EntryStrategy
from src.simulation.mock_vision import MockVisionService, PageAnalysisBuilder
from src.simulation.mock_action import MockActionExecutor
from src.simulation.operation_executor import ExecutionContext, OperationExecutor
from src.simulation.runner import SimulationRunner, SimulationResult, PlanDebugger
from src.state.content_tree import PageAnalysis
from src.ai.vision_service import VisionService


# ============================================================================
# Test MockVisionService (V6.4: implements VisionService ABC)
# ============================================================================


class TestMockVisionService:
    """Tests for MockVisionService (VisionService ABC)."""

    def test_implements_vision_service(self):
        vision = MockVisionService({})
        assert isinstance(vision, VisionService)

    def test_analyze_screenshot_returns_page_analysis(self):
        vision = MockVisionService({"home": {"elements": [], "level1_dir": "right", "level2_dir": "bottom"}})
        result = vision.analyze_screenshot(b"")
        assert isinstance(result, PageAnalysis)

    def test_analyze_screenshot_empty_for_empty_pages(self):
        vision = MockVisionService({})
        result = vision.analyze_screenshot(b"")
        assert isinstance(result, PageAnalysis)

    def test_call_count_increments(self):
        vision = MockVisionService({"home": {"elements": [], "level1_dir": "right", "level2_dir": "bottom"}})
        assert vision.call_count == 0
        vision.analyze_screenshot(b"")
        assert vision.call_count == 1

    def test_reset_call_count(self):
        vision = MockVisionService({"home": {"elements": [], "level1_dir": "right", "level2_dir": "bottom"}})
        vision.analyze_screenshot(b"")
        vision.reset()
        assert vision.call_count == 0

    def test_find_app_entry(self):
        vision = MockVisionService({})
        result = vision.find_app_entry(b"", "Target")
        assert result is not None
        assert "x" in result and "y" in result

    def test_set_path_context(self):
        vision = MockVisionService({
            "home/settings": {"elements": [], "level1_dir": "right", "level2_dir": "bottom"}
        })
        vision.set_path_context(["home", "settings"])
        result = vision.analyze_screenshot(b"")
        assert result.current_path == ["home", "settings"]


# ============================================================================
# Test MockActionExecutor (V6.4: implements OperationExecutor ABC)
# ============================================================================


class TestMockActionExecutor:
    """Tests for MockActionExecutor (OperationExecutor ABC)."""

    def test_implements_operation_executor(self):
        executor = MockActionExecutor()
        assert isinstance(executor, OperationExecutor)

    def test_execute_records_action(self):
        executor = MockActionExecutor()
        ctx = ExecutionContext("n1", "Settings", {"action": "click", "target": "btn_wifi"})
        executor.execute(ctx)
        assert len(executor.history) == 1
        assert executor.history[0]["action_type"] == "click"

    def test_execute_returns_result(self):
        from src.simulation.operation_executor import ExecutionResult
        executor = MockActionExecutor()
        ctx = ExecutionContext("n1", "Home", {"action": "click"})
        result = executor.execute(ctx)
        assert isinstance(result, ExecutionResult)
        assert result.success is True

    def test_get_executed_actions(self):
        executor = MockActionExecutor()
        executor.execute(ExecutionContext("n1", "Home", {"action": "click"}))
        executor.execute(ExecutionContext("n2", "Settings", {"action": "swipe"}))
        actions = executor.get_executed_actions()
        assert "click" in actions
        assert "swipe" in actions

    def test_get_history_returns_copy(self):
        executor = MockActionExecutor()
        executor.execute(ExecutionContext("n1", "Home", {"action": "click"}))
        history = executor.get_history()
        history.append({"fake": True})
        assert len(executor.history) == 1  # Original unchanged

    def test_clear_history(self):
        executor = MockActionExecutor()
        executor.execute(ExecutionContext("n1", "Home", {"action": "click"}))
        executor.clear_history()
        assert len(executor.history) == 0

    def test_simulate_delay(self):
        executor = MockActionExecutor(simulate_delay=0.02)
        ctx = ExecutionContext("n1", "Home", {"action": "click"})
        t0 = time.time()
        executor.execute(ctx)
        elapsed = time.time() - t0
        assert elapsed >= 0.018


# ============================================================================
# Test SimulationRunner (V6.4: uses real GraphTraversalEngine)
# ============================================================================


class TestSimulationRunner:
    """Tests for SimulationRunner (V6.4: GraphTraversalEngine + TraceRecorder)."""

    def test_create_with_minimal_config(self):
        vp = {"home": {"elements": [], "level1_dir": "right", "level2_dir": "bottom"}}
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        runner = SimulationRunner(vp, plan)
        assert runner.engine is not None
        assert type(runner.engine).__name__ == "GraphTraversalEngine"

    def test_run_produces_trace(self):
        vp = {"home": {"elements": [], "level1_dir": "right", "level2_dir": "bottom"}}
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        runner = SimulationRunner(vp, plan)
        result = runner.run()
        assert len(result.trace_id) == 26

    def test_get_result_after_run(self):
        vp = {"home": {"elements": [], "level1_dir": "right", "level2_dir": "bottom"}}
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        runner = SimulationRunner(vp, plan)
        runner.run()
        result = runner.get_result()
        assert result is not None

    def test_storage_accessible(self):
        vp = {"home": {"elements": [], "level1_dir": "right", "level2_dir": "bottom"}}
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        runner = SimulationRunner(vp, plan)
        assert runner.storage is not None


# ============================================================================
# Test PageAnalysisBuilder (unchanged)
# ============================================================================


class TestPageAnalysisBuilder:
    """Tests for PageAnalysisBuilder."""

    def test_create_page_analysis(self):
        page = PageAnalysisBuilder.create(
            app_name="TestApp",
            page_name="Main",
            items=[{"type": "button", "text": "Click Me"}],
        )
        assert page["app_name"] == "TestApp"
        assert page["page_name"] == "Main"
        assert len(page["items"]) == 1

    def test_create_button(self):
        button = PageAnalysisBuilder.create_button(
            text="OK",
            x=0.5,
            y=0.3,
        )
        assert button["type"] == "button"
        assert button["text"] == "OK"
        assert button["x"] == 0.5
        assert button["y"] == 0.3

    def test_create_menu_item(self):
        item = PageAnalysisBuilder.create_menu_item(
            text="Settings",
            x=0.5,
            y=0.5,
        )
        assert item["type"] == "menu_item"
        assert item["text"] == "Settings"
