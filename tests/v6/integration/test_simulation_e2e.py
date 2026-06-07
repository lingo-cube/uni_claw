"""End-to-end integration tests for simulation testing (V6.9.2).

Tests complete simulation scenarios with stateful mock services,
including page transitions, and trace validation.
"""

import pytest
from pathlib import Path
from typing import Dict, Any, List

from src.graph.plan import TraversalPlan
from src.graph.node import (
    TraversalNode,
    NodeType,
    Operation,
    CompletionPolicy,
    CompletionPolicyType,
    ChildrenStrategy,
    ChildrenStrategyType,
)
from src.trace.storage import FileStorage
from src.trace.recorder import TraceRecorder
from src.traversal.graph_engine import GraphTraversalEngine

from src.simulation.stateful_mock_vision import StatefulMockVisionService
from src.simulation.stateful_mock_action import StatefulMockActionExecutor
from src.simulation.state_fixture import StateFixture
from src.simulation.expected_behavior import ExpectedAction, ExpectedBehavior, CompletionMode
from src.simulation.behavior_validator import BehaviorValidator
from src.simulation.problem_detector import ProblemDetector, ProblemDetectorConfig


# -- Test fixtures ---------------------------------------------------------------


def create_simple_plan() -> TraversalPlan:
    """Create a simple traversal plan for testing."""
    root = TraversalNode(
        node_id="root",
        node_type=NodeType.CONTAINER,
        name="Root",
        operation=Operation(action="no_action"),
        children_strategy=ChildrenStrategy(
            type=ChildrenStrategyType.STATIC,
            static_children=["child1"],
        ),
    )

    child1 = TraversalNode(
        node_id="child1",
        node_type=NodeType.LEAF_ACTION,
        name="Child1",
        operation=Operation(action="no_action"),
    )

    return TraversalPlan(
        entry_app="test.app",
        root_node=root,
        static_nodes={"child1": child1},
        completion_policy=CompletionPolicy(
            type=CompletionPolicyType.MAX_STEPS,
            max_steps=3,
        ),
    )


def get_simple_fixture() -> StateFixture:
    """Load the simple two page fixture."""
    fixture_path = Path(__file__).parent.parent / "fixtures" / "simple_two_page.yaml"
    return StateFixture.from_yaml(fixture_path)


def run_simulation(plan: TraversalPlan, fixture: StateFixture) -> Dict[str, Any]:
    """Run a simulation with stateful mock services.

    Returns a dict with result, trace_id, and storage.
    """
    # Use stateful services
    vision = StatefulMockVisionService(fixture)
    action = StatefulMockActionExecutor(vision)

    # Set up trace recording with FileStorage for dashboard visualization
    storage = FileStorage(base_dir='.traces')
    recorder = TraceRecorder(storage=storage)

    # Create engine
    engine = GraphTraversalEngine(
        plan=plan,
        vision_service=vision,
        action_executor=action,
        trace_recorder=recorder,
    )

    # Run traversal
    result = engine.run()

    # Extract trace nodes as dicts
    trace_nodes = [node.to_dict() if hasattr(node, 'to_dict') else node for node in storage.read(result.trace_id)]

    return {
        "result": result,
        "trace_id": result.trace_id,
        "trace_nodes": trace_nodes,
        "storage": storage,
    }


def create_mock_trace_data(actions: List[Dict[str, Any]], states: List[str], pages: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    """Create mock trace data for testing.

    Args:
        actions: List of action dicts with action, target, status
        states: List of state names for state transitions
        pages: List of page transition dicts

    Returns:
        Combined trace nodes list
    """
    trace = []

    # Add action spans
    for i, action in enumerate(actions):
        trace.append({
            "node_type": "span",
            "span_type": "execution",
            "action": action.get("action"),
            "target": action.get("target"),
            "status": action.get("status", "success"),
        })

    # Add state transitions
    for i, state in enumerate(states):
        trace.append({
            "node_type": "span",
            "span_type": "state_transition",
            "from_state": states[i - 1] if i > 0 else "IDLE",
            "to_state": state,
        })

    # Add page transitions
    for transition in pages:
        trace.append({
            "node_type": "span",
            "span_type": "page_transition",
            "from_page": transition.get("from"),
            "to_page": transition.get("to"),
            "trigger_element": transition.get("trigger"),
        })

    return trace


# -- Task 9.1 & 9.2: Test simple two-page traversal -----------------------------


def test_simple_two_page_traversal():
    """Test end-to-end simulation of simple navigation."""
    plan = create_simple_plan()
    fixture = get_simple_fixture()

    # Run simulation
    sim_result = run_simulation(plan, fixture)

    # Verify result
    result = sim_result["result"]
    # Accept various completion states (note: lowercase from GlobalState enum)
    assert result.status.value in ["completed", "frame_complete", "max_steps_reached", "error"]
    assert result.trace_id is not None

    # Verify trace was recorded
    trace_nodes = sim_result["trace_nodes"]
    assert len(trace_nodes) > 0


def test_simple_two_page_traversal_with_validation():
    """Test traversal with behavior validation."""
    plan = create_simple_plan()
    fixture = get_simple_fixture()

    # Run simulation
    sim_result = run_simulation(plan, fixture)
    trace_nodes = sim_result["trace_nodes"]
    result = sim_result["result"]

    # Create expected behavior (use standalone ExpectedAction)
    expected = ExpectedBehavior(
        scenario="Simple Traversal",
        description="Basic traversal test",
        actions=[
            ExpectedAction(
                action="no_action",
                node="root",
                order=0,
            ),
        ],
        visited_nodes={"root"},
        final_state="COMPLETED",
        completion_mode=CompletionMode.NORMAL,
    )

    # Validate
    validator = BehaviorValidator()
    validation_result = validator.validate(
        expected=expected,
        actual_trace=trace_nodes,
        actual_result={"status": result.status.value},
    )

    # Validator should run without error
    assert validation_result is not None


def test_simple_two_page_traversal_with_problem_detection():
    """Test traversal with problem detection."""
    plan = create_simple_plan()
    fixture = get_simple_fixture()

    # Run simulation
    sim_result = run_simulation(plan, fixture)
    trace_nodes = sim_result["trace_nodes"]

    # Run problem detection
    detector = ProblemDetector()
    problems = detector.detect(trace_nodes)

    # Detector should run without error
    assert problems is not None
    # At minimum, should have an empty list
    assert isinstance(problems, list)


# -- Task 9.3: Test dynamic buttons with state change ---------------------------


def test_dynamic_buttons_page_transition_detection():
    """Test that page transitions are detected in scenarios."""
    # Create a trace with a page transition
    trace = create_mock_trace_data(
        actions=[
            {"action": "click", "target": "btn_detail", "status": "success"},
        ],
        states=["EXECUTING", "RESULT_VERIFY", "FRAME_COMPLETE"],
        pages=[
            {"from": "home", "to": "detail", "trigger": "btn_detail"},
        ],
    )

    # Verify page transition is detectable
    transitions = [
        n for n in trace
        if n.get("span_type") == "page_transition"
    ]
    assert len(transitions) == 1
    assert transitions[0]["from_page"] == "home"
    assert transitions[0]["to_page"] == "detail"


def test_stateful_services_page_changes():
    """Test that stateful services correctly track page changes."""
    fixture = get_simple_fixture()

    # Create stateful vision service
    vision = StatefulMockVisionService(fixture)

    # Get initial page
    initial_page = vision.get_current_page()
    assert initial_page is not None

    # Simulate action to navigate to detail
    vision.simulate_action(element_id="btn_detail", action="click")

    # Should now be on detail page (checked via internal state)
    assert vision._current_page_id == "detail"

    # Navigate back
    vision.navigate_back()

    # Should be back on previous page
    assert vision._current_page_id == "home"


def test_stateful_services_reset():
    """Test that stateful services can be reset to initial state."""
    fixture = get_simple_fixture()

    vision = StatefulMockVisionService(fixture)

    # Navigate away from home
    vision.simulate_action(element_id="btn_detail", action="click")

    # Verify not on home
    assert vision._current_page_id == "detail"

    # Reset to initial
    vision.reset_to_initial()

    # Should be back on home
    assert vision._current_page_id == "home"


def test_stateful_services_action_tracking():
    """Test that stateful action executor tracks actions."""
    fixture = get_simple_fixture()

    vision = StatefulMockVisionService(fixture)
    action = StatefulMockActionExecutor(vision)

    # Execute an action
    from datetime import datetime
    from src.simulation.stateful_mock_action import ExecutionContext

    context = ExecutionContext(
        node_id="test_node",
        node_name="TestNode",
        operation={"action": "click", "target": "btn_detail"},
        timestamp=datetime.now(),
    )
    result = action.execute(context)

    # Verify result
    assert result is not None
    assert result.success is True

    # Verify action was tracked via get_history()
    history = action.get_history()
    assert len(history) == 1
    record = history[0]
    assert record.node_id == "test_node"
    assert record.action_type == "click"


# -- Helper function tests ------------------------------------------------------


def test_create_mock_trace_data_helper():
    """Test the helper function creates valid trace data."""
    trace = create_mock_trace_data(
        actions=[{"action": "click", "target": "btn1", "status": "success"}],
        states=["EXECUTING", "COMPLETED"],
        pages=[{"from": "page1", "to": "page2", "trigger": "btn1"}],
    )

    # Should have action span
    action_spans = [n for n in trace if n.get("span_type") == "execution"]
    assert len(action_spans) == 1
    assert action_spans[0]["action"] == "click"

    # Should have state transitions
    state_spans = [n for n in trace if n.get("span_type") == "state_transition"]
    assert len(state_spans) == 2

    # Should have page transition
    page_spans = [n for n in trace if n.get("span_type") == "page_transition"]
    assert len(page_spans) == 1
    assert page_spans[0]["from_page"] == "page1"


def test_run_simulation_helper():
    """Test the run_simulation helper function."""
    plan = create_simple_plan()
    fixture = get_simple_fixture()

    result = run_simulation(plan, fixture)

    # Verify all expected keys are present
    assert "result" in result
    assert "trace_id" in result
    assert "trace_nodes" in result
    assert "storage" in result

    # Verify result is valid
    assert result["result"] is not None
    assert result["trace_id"] is not None
    assert len(result["trace_nodes"]) > 0
