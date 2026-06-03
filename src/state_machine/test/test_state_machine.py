"""
Unit tests for state machine system.

Tests cover:
- GlobalStateMachine state transitions
- TraversalStateMachine state transitions
- NodeStack operations
- StackFrame operations
- Precondition validation
- Automatic navigation
- State machine interaction
"""

import pytest

from src.state_machine.global_fsm import GlobalStateMachine, GlobalState
from src.state_machine.traversal_fsm import TraversalStateMachine, TraversalState
from src.state_machine.node_stack import NodeStack, StackFrame
from src.state_machine.interaction import (
    StateMachineOrchestrator,
    TraversalContext,
    NavigationResult,
)
from src.graph.node import TraversalNode, NodeType, Operation


class TestGlobalStateMachine:
    """Tests for GlobalStateMachine."""

    def test_initial_state(self):
        """Test initial state is IDLE."""
        fsm = GlobalStateMachine()
        assert fsm.state == GlobalState.IDLE

    def test_is_active(self):
        """Test is_active property."""
        fsm = GlobalStateMachine()
        assert fsm.is_active

        fsm.start_initialization()
        assert fsm.is_active

        fsm.complete()
        assert not fsm.is_active

    def test_is_terminal(self):
        """Test is_terminal property."""
        fsm = GlobalStateMachine()
        assert not fsm.is_terminal

        fsm.complete()
        assert fsm.is_terminal

    def test_can_transition_to(self):
        """Test can_transition_to validation."""
        fsm = GlobalStateMachine()
        assert fsm.can_transition_to(GlobalState.INITIALIZING)
        assert not fsm.can_transition_to(GlobalState.COMPLETED)

    def test_start_initialization(self):
        """Test starting initialization."""
        fsm = GlobalStateMachine()
        result = fsm.start_initialization()
        assert result is True
        assert fsm.state == GlobalState.INITIALIZING

    def test_start_traversing(self):
        """Test starting traversing."""
        fsm = GlobalStateMachine()
        fsm.start_initialization()
        result = fsm.start_traversing()
        assert result is True
        assert fsm.state == GlobalState.TRAVERSING

    def test_pause_and_resume(self):
        """Test pause and resume."""
        fsm = GlobalStateMachine()
        fsm.start_initialization()
        fsm.start_traversing()

        assert fsm.pause() is True
        assert fsm.state == GlobalState.PAUSED

        assert fsm.resume() is True
        assert fsm.state == GlobalState.TRAVERSING

    def test_report_error(self):
        """Test error reporting."""
        fsm = GlobalStateMachine()
        fsm.start_initialization()
        fsm.start_traversing()

        error = Exception("Test error")
        result = fsm.report_error(error, context={"test": "data"})
        assert result is True
        assert fsm.state == GlobalState.ERROR
        assert fsm.error_context is not None

    def test_start_recovery(self):
        """Test recovery from error."""
        fsm = GlobalStateMachine()
        fsm.start_initialization()
        fsm.start_traversing()
        fsm.report_error(Exception("Test"))

        result = fsm.start_recovery("restart_app")
        assert result is True
        assert fsm.state == GlobalState.RECOVERING

    def test_complete(self):
        """Test completion."""
        fsm = GlobalStateMachine()
        fsm.start_initialization()
        fsm.start_traversing()

        result = fsm.complete()
        assert result is True
        assert fsm.state == GlobalState.COMPLETED

    def test_terminate(self):
        """Test termination."""
        fsm = GlobalStateMachine()
        fsm.start_initialization()

        result = fsm.terminate("User aborted")
        assert result is True
        assert fsm.state == GlobalState.TERMINATED

    def test_invalid_transition_raises_error(self):
        """Test that invalid transitions raise error."""
        fsm = GlobalStateMachine()
        fsm.start_initialization()

        # Can't go from INITIALIZING to COMPLETED directly
        with pytest.raises(ValueError, match="Invalid transition"):
            fsm.transition_to(GlobalState.COMPLETED)

    def test_transition_history(self):
        """Test transition history recording."""
        fsm = GlobalStateMachine()
        fsm.start_initialization()
        fsm.start_traversing()

        history = fsm.get_transition_history()
        assert len(history) == 2
        assert history[0].to_state == GlobalState.INITIALIZING
        assert history[1].to_state == GlobalState.TRAVERSING

    def test_reset(self):
        """Test resetting state machine."""
        fsm = GlobalStateMachine()
        fsm.start_initialization()
        fsm.start_traversing()
        fsm.report_error(Exception("test"))

        fsm.reset()

        assert fsm.state == GlobalState.IDLE
        assert fsm.error_context is None


class TestTraversalStateMachine:
    """Tests for TraversalStateMachine."""

    def test_initial_state(self):
        """Test initial state is NODE_SELECT."""
        fsm = TraversalStateMachine()
        assert fsm.state == TraversalState.NODE_SELECT

    def test_set_current_node(self):
        """Test setting current node."""
        fsm = TraversalStateMachine()
        fsm.set_current_node("test_node")
        assert fsm.current_node_id == "test_node"

    def test_start_node_select(self):
        """Test starting node selection."""
        fsm = TraversalStateMachine()
        result = fsm.start_node_select("node1")
        assert result is True
        assert fsm.state == TraversalState.NODE_SELECT

    def test_start_precondition_check(self):
        """Test starting precondition check."""
        fsm = TraversalStateMachine()
        fsm.start_precondition_check()
        assert fsm.state == TraversalState.PRECONDITION_CHECK

    def test_precondition_failed(self):
        """Test precondition failed handling."""
        fsm = TraversalStateMachine()
        fsm.start_precondition_check()
        result = fsm.precondition_failed()
        assert result is True
        assert fsm.state == TraversalState.BRANCH

    def test_start_execute(self):
        """Test starting execution."""
        fsm = TraversalStateMachine()
        fsm.start_precondition_check()
        fsm.start_execute()
        assert fsm.state == TraversalState.EXECUTE

    def test_execution_failed(self):
        """Test execution failed handling."""
        fsm = TraversalStateMachine()
        fsm.start_execute()
        error = Exception("Execution failed")
        result = fsm.execution_failed(error)
        assert result is True
        assert fsm.state == TraversalState.BRANCH

    def test_start_result_verify(self):
        """Test starting result verification."""
        fsm = TraversalStateMachine()
        fsm.start_execute()
        fsm.start_result_verify()
        assert fsm.state == TraversalState.RESULT_VERIFY

    def test_branch_to_children(self):
        """Test branching to children processing."""
        fsm = TraversalStateMachine()
        fsm.start_result_verify()
        result = fsm.branch_to_children()
        assert result is True
        assert fsm.state == TraversalState.BRANCH

    def test_branch_to_next_node(self):
        """Test branching to next node."""
        fsm = TraversalStateMachine()
        fsm.branch_to_children()
        result = fsm.branch_to_next_node()
        assert result is True
        assert fsm.state == TraversalState.NODE_SELECT

    def test_execution_result(self):
        """Test setting and getting execution result."""
        fsm = TraversalStateMachine()
        result_data = {"success": True, "screenshot": "test.png"}
        fsm.set_execution_result(result_data)
        assert fsm.execution_result == result_data

    def test_precondition_result(self):
        """Test setting and getting precondition result."""
        fsm = TraversalStateMachine()
        fsm.set_precondition_result(True)
        assert fsm.precondition_result is True

    def test_transition_history(self):
        """Test transition history recording."""
        fsm = TraversalStateMachine()
        fsm.start_node_select("node1")
        fsm.start_precondition_check()
        fsm.start_execute()

        history = fsm.get_transition_history()
        assert len(history) >= 2

    def test_reset(self):
        """Test resetting state machine."""
        fsm = TraversalStateMachine()
        fsm.set_current_node("test")
        fsm.set_execution_result({"success": True})
        fsm.set_precondition_result(True)

        fsm.reset()

        assert fsm.state == TraversalState.NODE_SELECT
        assert fsm.current_node_id is None


class TestStackFrame:
    """Tests for StackFrame."""

    def test_create_frame(self):
        """Test creating a stack frame."""
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        frame = StackFrame(node=node, child_queue=["c1", "c2"])
        assert frame.node_id == "test"
        assert len(frame.child_queue) == 2

    def test_has_children(self):
        """Test has_children property."""
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        frame = StackFrame(node=node, child_queue=["c1"])
        assert frame.has_children

        empty_frame = StackFrame(node=node)
        assert not empty_frame.has_children

    def test_is_complete(self):
        """Test is_complete property."""
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        frame = StackFrame(node=node, child_queue=["c1"])
        assert not frame.is_complete

        frame.current_child_idx = 1
        assert frame.is_complete

    def test_remaining_children(self):
        """Test remaining_children property."""
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        frame = StackFrame(node=node, child_queue=["c1", "c2", "c3"])
        assert frame.remaining_children == 3

        frame.current_child_idx = 1
        assert frame.remaining_children == 2

    def test_get_next_child(self):
        """Test getting next child."""
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        frame = StackFrame(node=node, child_queue=["c1", "c2"])

        child = frame.get_next_child()
        assert child == "c1"
        assert frame.current_child_idx == 1

        child = frame.get_next_child()
        assert child == "c2"

        child = frame.get_next_child()
        assert child is None

    def test_peek_next_child(self):
        """Test peeking at next child."""
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        frame = StackFrame(node=node, child_queue=["c1", "c2"])

        child = frame.peek_next_child()
        assert child == "c1"
        assert frame.current_child_idx == 0  # Not advanced

    def test_reset_child_index(self):
        """Test resetting child index."""
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        frame = StackFrame(node=node, child_queue=["c1", "c2"])
        frame.get_next_child()
        frame.get_next_child()

        frame.reset_child_index()
        assert frame.current_child_idx == 0


class TestNodeStack:
    """Tests for NodeStack."""

    def test_empty_stack(self):
        """Test empty stack."""
        stack = NodeStack()
        assert stack.is_empty
        assert stack.size == 0

    def test_push_and_pop(self):
        """Test push and pop operations."""
        stack = NodeStack()
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        stack.push(node)
        assert stack.size == 1

        frame = stack.pop()
        assert frame is not None
        assert frame.node_id == "test"
        assert stack.is_empty

    def test_push_with_children(self):
        """Test pushing node with children."""
        stack = NodeStack()
        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        stack.push(node, children=["c1", "c2", "c3"])
        frame = stack.top()

        # Children should be reversed for DFS
        assert frame.child_queue == ["c3", "c2", "c1"]

    def test_top_operation(self):
        """Test top operation."""
        stack = NodeStack()
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        stack.push(node)
        frame = stack.top()
        assert frame is not None
        assert frame.node_id == "test"

        # Stack should still have the frame
        assert stack.size == 1

    def test_peek_operation(self):
        """Test peek operation."""
        stack = NodeStack()
        node1 = TraversalNode(
            node_id="n1",
            name="N1",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        node2 = TraversalNode(
            node_id="n2",
            name="N2",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        stack.push(node1)
        stack.push(node2)

        # Peek at top (offset 0)
        assert stack.peek(0).node_id == "n2"
        # Peek at second from top (offset 1)
        assert stack.peek(1).node_id == "n1"
        # Invalid offset
        assert stack.peek(5) is None

    def test_depth_limit(self):
        """Test depth limit enforcement."""
        stack = NodeStack(max_depth=3)
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        stack.push(node)
        stack.push(node)
        stack.push(node)

        # Fourth push should fail
        with pytest.raises(RuntimeError, match="depth limit"):
            stack.push(node)

    def test_get_node_path(self):
        """Test getting node path."""
        stack = NodeStack()
        node1 = TraversalNode(
            node_id="n1",
            name="N1",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        node2 = TraversalNode(
            node_id="n2",
            name="N2",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        stack.push(node1)
        stack.push(node2)

        path = stack.get_node_path()
        assert path == ["n1", "n2"]

    def test_contains_node(self):
        """Test contains_node method."""
        stack = NodeStack()
        node1 = TraversalNode(
            node_id="n1",
            name="N1",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        node2 = TraversalNode(
            node_id="n2",
            name="N2",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        stack.push(node1)
        stack.push(node2)

        assert stack.contains_node("n1")
        assert stack.contains_node("n2")
        assert not stack.contains_node("n3")

    def test_get_depth_of_node(self):
        """Test get_depth_of_node method."""
        stack = NodeStack()
        node1 = TraversalNode(
            node_id="n1",
            name="N1",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        node2 = TraversalNode(
            node_id="n2",
            name="N2",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        stack.push(node1)
        stack.push(node2)

        assert stack.get_depth_of_node("n1") == 0
        assert stack.get_depth_of_node("n2") == 1
        assert stack.get_depth_of_node("n3") == -1

    def test_clear(self):
        """Test clearing stack."""
        stack = NodeStack()
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        stack.push(node)
        stack.push(node)
        assert stack.size == 2

        stack.clear()
        assert stack.is_empty

    def test_get_summary(self):
        """Test getting stack summary."""
        stack = NodeStack()
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        stack.push(node, children=["c1", "c2"])
        summary = stack.get_summary()

        assert summary["size"] == 1
        assert summary["current_node"] == "test"
        assert "path" in summary

    def test_repr(self):
        """Test string representation."""
        stack = NodeStack()
        assert "empty" in repr(stack)

        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        stack.push(node)
        assert "test" in repr(stack)


class TestTraversalContext:
    """Tests for TraversalContext."""

    def test_mark_and_check_page_visited(self):
        """Test marking and checking visited pages."""
        context = TraversalContext()
        context.mark_page_visited("SettingsPage")
        assert context.is_page_visited("SettingsPage")
        assert not context.is_page_visited("OtherPage")

    def test_mark_and_check_node_visited(self):
        """Test marking and checking visited nodes."""
        context = TraversalContext()
        context.mark_node_visited("node_123")
        assert context.is_node_visited("node_123")
        assert not context.is_node_visited("node_456")

    def test_current_path(self):
        """Test current path tracking."""
        context = TraversalContext()
        context.current_path = ["Home", "Settings"]
        assert context.current_path == ["Home", "Settings"]


class TestStateMachineOrchestrator:
    """Tests for StateMachineOrchestrator."""

    def test_initialization(self):
        """Test orchestrator initialization."""
        orchestrator = StateMachineOrchestrator()
        assert orchestrator.global_fsm.state == GlobalState.IDLE
        assert orchestrator.node_stack.is_empty

    def test_initialize_with_root_node(self):
        """Test initialization with root node."""
        orchestrator = StateMachineOrchestrator()
        root = TraversalNode(
            node_id="root",
            name="Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        result = orchestrator.initialize(root)
        assert result is True
        assert orchestrator.global_fsm.state == GlobalState.TRAVERSING
        assert orchestrator.node_stack.size == 1

    def test_register_callbacks(self):
        """Test registering callbacks."""
        orchestrator = StateMachineOrchestrator()

        def nav_callback(page, timeout):
            return NavigationResult(success=True)

        orchestrator.register_navigation_callback(nav_callback)
        # Callback registered (test passes if no exception)
        assert True

    def test_status_summary(self):
        """Test getting status summary."""
        orchestrator = StateMachineOrchestrator()
        summary = orchestrator.get_status_summary()

        assert "global_state" in summary
        assert "traversal_state" in summary
        assert "stack" in summary
        assert "is_complete" in summary

    def test_is_traversal_complete(self):
        """Test checking if traversal is complete."""
        orchestrator = StateMachineOrchestrator()

        # Empty stack means complete
        assert orchestrator.is_traversal_complete()

        root = TraversalNode(
            node_id="root",
            name="Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        orchestrator.initialize(root)

        # With nodes, not complete
        assert not orchestrator.is_traversal_complete()
