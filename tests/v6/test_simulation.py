"""
Tests for V6 simulation components.

Tests MockVisionService, MockActionExecutor, InMemoryTracer, and SimulationRunner.
"""

import json
import time

from src.graph.plan import TraversalPlan
from src.graph.node import TraversalNode, NodeType, Operation
from src.simulation.mock_vision import MockVisionService, PageAnalysisBuilder
from src.simulation.mock_action import MockActionExecutor
from src.simulation.visualizer import InMemoryTracer, TraceStep, VisitedNode
from src.simulation.runner import SimulationRunner, SimulationResult, PlanDebugger


# ============================================================================
# Test MockVisionService (Tasks 4.2.2 - 4.2.4)
# ============================================================================


class TestMockVisionService:
    """Tests for MockVisionService."""

    def test_create_with_virtual_pages(self):
        """Test creating with virtual pages."""
        virtual_pages = {
            "Main": {"app_name": "TestApp", "page_name": "Main", "items": []},
        }
        vision = MockVisionService(virtual_pages)

        assert vision.virtual_pages == virtual_pages
        assert vision.call_count == 0

    def test_analyze_screenshot_returns_page(self):
        """Test analyze_screenshot returns correct page."""
        virtual_pages = {
            "Main": {"app_name": "TestApp", "page_name": "Main", "items": []},
        }
        vision = MockVisionService(virtual_pages)

        # Inject path
        vision.inject_path("Main")

        result = vision.analyze_screenshot()

        assert result["page_name"] == "Main"
        assert vision.call_count == 1

    def test_analyze_screenshot_empty_for_unknown_path(self):
        """Test analyze_screenshot returns empty for unknown path."""
        vision = MockVisionService({})

        vision.inject_path("Unknown")

        result = vision.analyze_screenshot()

        assert result["app_name"] == ""
        assert result["page_name"] == ""
        assert result["items"] == []

    def test_call_count_increments(self):
        """Test call count increments."""
        vision = MockVisionService({})
        vision.inject_path("Main")

        vision.analyze_screenshot()
        vision.analyze_screenshot()

        assert vision.call_count == 2

    def test_reset_call_count(self):
        """Test reset call count."""
        vision = MockVisionService({})
        vision.inject_path("Main")

        vision.analyze_screenshot()
        vision.reset_call_count()

        assert vision.call_count == 0

    def test_inject_path(self):
        """Test path injection."""
        vision = MockVisionService({})
        vision.inject_path("InjectedPath")

        assert vision._injected_path == "InjectedPath"

    def test_clear_injected_path(self):
        """Test clearing injected path."""
        vision = MockVisionService({})
        vision.inject_path("SomePath")
        vision.clear_injected_path()

        assert vision._injected_path is None


class TestPageAnalysisBuilder:
    """Tests for PageAnalysisBuilder."""

    def test_create_page(self):
        """Test creating a page."""
        page = PageAnalysisBuilder.create(
            app_name="TestApp",
            page_name="Settings",
            items=[],
        )

        assert page["app_name"] == "TestApp"
        assert page["page_name"] == "Settings"

    def test_create_button(self):
        """Test creating a button element."""
        button = PageAnalysisBuilder.create_button(
            text="Click Me",
            x=0.5,
            y=0.5,
        )

        assert button["type"] == "button"
        assert button["text"] == "Click Me"
        assert button["x"] == 0.5

    def test_create_menu_item(self):
        """Test creating a menu item element."""
        item = PageAnalysisBuilder.create_menu_item(
            text="Settings",
        )

        assert item["type"] == "menu_item"
        assert item["text"] == "Settings"


# ============================================================================
# Test MockActionExecutor (Tasks 4.3.3 - 4.3.6)
# ============================================================================


class TestMockActionExecutor:
    """Tests for MockActionExecutor."""

    def test_tap_records_action(self):
        """Test tap records action."""
        executor = MockActionExecutor()
        result = executor.tap(0.5, 0.5)

        assert result is True
        assert len(executor.action_history) == 1
        assert executor.action_history[0]["action"] == "tap"

    def test_swipe_records_action(self):
        """Test swipe records action."""
        executor = MockActionExecutor()
        result = executor.swipe((0.2, 0.5), (0.8, 0.5))

        assert result is True
        assert executor.action_history[0]["action"] == "swipe"
        assert executor.action_history[0]["start"] == [0.2, 0.5]

    def test_press_back_records_action(self):
        """Test press_back records action."""
        executor = MockActionExecutor()
        result = executor.press_back()

        assert result is True
        assert executor.action_history[0]["action"] == "back"

    def test_get_history_returns_copy(self):
        """Test get_history returns a copy."""
        executor = MockActionExecutor()
        executor.tap(0.5, 0.5)

        history = executor.get_history()
        history.append({"action": "fake"})

        # Original should be unchanged
        assert len(executor.action_history) == 1

    def test_get_tap_count(self):
        """Test getting tap count."""
        executor = MockActionExecutor()
        executor.tap(0.5, 0.5)
        executor.tap(0.3, 0.7)

        assert executor.get_tap_count() == 2

    def test_get_back_count(self):
        """Test getting back count."""
        executor = MockActionExecutor()
        executor.press_back()
        executor.press_back()

        assert executor.get_back_count() == 2

    def test_clear_history(self):
        """Test clearing history."""
        executor = MockActionExecutor()
        executor.tap(0.5, 0.5)
        executor.clear_history()

        assert len(executor.action_history) == 0

    def test_simulate_delay(self):
        """Test simulate delay parameter."""
        executor = MockActionExecutor(simulate_delay=0.01)

        start = time.time()
        executor.tap(0.5, 0.5)
        elapsed = time.time() - start

        # Should have delay
        assert elapsed >= 0.01


# ============================================================================
# Test InMemoryTracer (Tasks 4.4.3 - 4.4.7)
# ============================================================================


class TestInMemoryTracer:
    """Tests for InMemoryTracer."""

    def test_start_traversal(self):
        """Test starting a new trace."""
        tracer = InMemoryTracer()
        tracer.start_traversal(None)

        assert len(tracer.steps) == 0
        assert tracer._step_counter == 0
        assert tracer._start_time is not None

    def test_record_transition(self):
        """Test recording a transition."""
        tracer = InMemoryTracer()
        tracer.start_traversal(None)

        # Create a mock transition
        class MockTransition:
            from_state = type('State', (), {'value': 'node_select'})()
            to_state = type('State', (), {'value': 'execute'})()
            node_id = "test_node"
            metadata = {}

        transition = MockTransition()
        tracer.record_transition(transition)

        assert len(tracer.steps) == 1
        assert tracer.steps[0].from_state == "node_select"
        assert tracer.steps[0].to_state == "execute"

    def test_get_trace(self):
        """Test getting trace."""
        tracer = InMemoryTracer()
        tracer.start_traversal(None)

        trace = tracer.get_trace()

        assert isinstance(trace, list)
        # Should return a copy
        trace.append("fake")
        assert len(tracer.steps) == 0

    def test_get_step_count(self):
        """Test getting step count."""
        tracer = InMemoryTracer()
        tracer.start_traversal(None)

        # Record some steps
        class MockState:
            value = "test"

        class MockTransition:
            from_state = MockState()
            to_state = MockState()
            node_id = "node"
            metadata = {}

        for _ in range(5):
            tracer.record_transition(MockTransition())

        assert tracer.get_step_count() == 5

    def test_render_tree(self):
        """Test rendering tree."""
        tracer = InMemoryTracer()
        tracer.start_traversal(None)

        # Add some nodes
        tracer.visited_tree["root"] = VisitedNode(
            node_id="root",
            name="Root",
            node_type="container",
            visited=True,
        )

        tree = tracer.render_tree()

        assert isinstance(tree, str)
        assert "Root" in tree

    def test_render_mermaid(self):
        """Test rendering Mermaid diagram."""
        tracer = InMemoryTracer()
        tracer.start_traversal(None)

        mermaid = tracer.render_mermaid()

        assert "stateDiagram-v2" in mermaid
        assert "[*] --> NODE_SELECT" in mermaid

    def test_export_trace_jsonl(self):
        """Test exporting to JSONL."""
        tracer = InMemoryTracer()
        tracer.start_traversal(None)

        class MockState:
            value = "test"

        class MockTransition:
            from_state = MockState()
            to_state = MockState()
            node_id = "node"
            metadata = {}

        tracer.record_transition(MockTransition())

        jsonl = tracer.export_trace("jsonl")
        lines = jsonl.split("\n")

        assert len(lines) == 1
        # Should be valid JSON
        data = json.loads(lines[0])
        assert data["from_state"] == "test"


# ============================================================================
# Test SimulationRunner (Tasks 4.5.2 - 4.5.4)
# ============================================================================


class TestSimulationRunner:
    """Tests for SimulationRunner."""

    def test_create_runner(self):
        """Test creating simulation runner."""
        plan = TraversalPlan(entry_app="TestApp")
        virtual_pages = {"Main": {"app_name": "TestApp", "page_name": "Main", "items": []}}

        runner = SimulationRunner(virtual_pages, plan)

        assert runner.plan == plan
        assert runner.virtual_pages == virtual_pages
        assert isinstance(runner.vision, MockVisionService)
        assert isinstance(runner.action, MockActionExecutor)

    def test_run_returns_result(self):
        """Test run returns SimulationResult."""
        plan = TraversalPlan(entry_app="TestApp")
        runner = SimulationRunner({}, plan)

        result = runner.run()

        assert isinstance(result, SimulationResult)
        assert isinstance(result.trace, list)
        assert isinstance(result.executed_actions, list)

    def test_get_statistics(self):
        """Test getting statistics."""
        plan = TraversalPlan(entry_app="TestApp")
        runner = SimulationRunner({}, plan)

        stats = runner.get_statistics()

        assert "total_steps" in stats
        assert "visited_nodes" in stats
        assert "action_count" in stats


# ============================================================================
# Test PlanDebugger (Tasks 4.6.2 - 4.6.4)
# ============================================================================


class TestPlanDebugger:
    """Tests for PlanDebugger."""

    def test_create_debugger(self):
        """Test creating plan debugger."""
        plan = TraversalPlan(entry_app="TestApp")
        debugger = PlanDebugger(plan)

        assert debugger.original_plan == plan
        assert debugger.current_plan == plan
        assert debugger.modifications == []

    def test_set_target(self):
        """Test setting target."""
        plan = TraversalPlan(entry_app="TestApp")
        debugger = PlanDebugger(plan)

        modified = debugger.set_target("Version")

        assert modified.completion_policy.target_name == "Version"
        assert len(debugger.modifications) == 1

    def test_undo_last(self):
        """Test undoing last modification."""
        plan = TraversalPlan(entry_app="TestApp")
        debugger = PlanDebugger(plan)

        debugger.set_target("Target1")
        debugger.set_target("Target2")
        debugger.undo_last()

        assert len(debugger.modifications) == 1
        assert debugger.current_plan.completion_policy.target_name == "Target1"

    def test_reset_to_original(self):
        """Test resetting to original plan."""
        plan = TraversalPlan(entry_app="TestApp")
        debugger = PlanDebugger(plan)

        debugger.set_target("SomeTarget")
        debugger.reset_to_original()

        assert debugger.current_plan == plan
        assert len(debugger.modifications) == 0

    def test_get_modification_history(self):
        """Test getting modification history."""
        plan = TraversalPlan(entry_app="TestApp")
        debugger = PlanDebugger(plan)

        debugger.set_target("Target1")
        debugger.set_target("Target2")

        history = debugger.get_modification_history()

        assert len(history) == 2
        assert "Target1" in history[0]["modification"]
        assert "Target2" in history[1]["modification"]
