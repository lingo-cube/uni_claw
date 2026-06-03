"""Tests for state machine models.

This module tests the models from src/state_machine/global_fsm.py and
src/state_machine/traversal_fsm.py including:
- GlobalState enum
- GlobalStateTransition
- GlobalStateMachine
- TraversalState enum
- TraversalStateTransition
- TraversalStateMachine
- StackFrame
- NodeStack
"""

import pytest
from datetime import datetime
from src.state_machine.global_fsm import (
    GlobalState,
    GlobalStateTransition,
    GlobalStateMachine,
)
from src.state_machine.traversal_fsm import (
    TraversalState,
    TraversalStateTransition,
    TraversalStateMachine,
)
from src.state_machine.node_stack import StackFrame, NodeStack
from src.graph.node import TraversalNode, Operation, NodeType, ChildrenStrategyType, ChildrenStrategy


class TestGlobalState:
    """Tests for GlobalState enum."""

    def test_global_state_values(self):
        """Test GlobalState has correct values."""
        assert GlobalState.IDLE.value == "idle"
        assert GlobalState.INITIALIZING.value == "initializing"
        assert GlobalState.TRAVERSING.value == "traversing"
        assert GlobalState.PAUSED.value == "paused"
        assert GlobalState.ERROR.value == "error"
        assert GlobalState.RECOVERING.value == "recovering"
        assert GlobalState.COMPLETED.value == "completed"
        assert GlobalState.TERMINATED.value == "terminated"

    def test_global_state_values_method(self):
        """Test GlobalState.values() method."""
        values = GlobalState.values()
        assert len(values) == 8
        assert "idle" in values

    def test_global_state_from_value(self):
        """Test GlobalState.from_value() method."""
        state = GlobalState.from_value("idle")
        assert state == GlobalState.IDLE

    def test_global_state_is_valid(self):
        """Test GlobalState.is_valid() method."""
        assert GlobalState.is_valid("traversing") is True
        assert GlobalState.is_valid("invalid") is False


class TestGlobalStateTransition:
    """Tests for GlobalStateTransition model."""

    def test_transition_creation(self):
        """Test creating state transition."""
        transition = GlobalStateTransition(
            from_state=GlobalState.IDLE,
            to_state=GlobalState.INITIALIZING,
        )
        assert transition.from_state == GlobalState.IDLE
        assert transition.to_state == GlobalState.INITIALIZING

    def test_transition_with_reason(self):
        """Test transition with reason."""
        transition = GlobalStateTransition(
            from_state=GlobalState.TRAVERSING,
            to_state=GlobalState.PAUSED,
            reason="User paused",
        )
        assert transition.reason == "User paused"

    def test_transition_timestamp(self):
        """Test transition has timestamp."""
        before = datetime.now()
        transition = GlobalStateTransition(
            from_state=GlobalState.IDLE,
            to_state=GlobalState.INITIALIZING,
        )
        after = datetime.now()
        assert before <= transition.timestamp <= after


class TestGlobalStateMachine:
    """Tests for GlobalStateMachine model."""

    def test_initial_state(self):
        """Test machine starts in IDLE state."""
        machine = GlobalStateMachine()
        assert machine.state == GlobalState.IDLE

    def test_is_active(self):
        """Test is_active property."""
        machine = GlobalStateMachine()
        assert machine.is_active is True
        assert machine.is_terminal is False

    def test_is_terminal(self):
        """Test terminal states."""
        machine = GlobalStateMachine()

        # Not terminal in IDLE
        assert machine.is_terminal is False

        # Follow proper transitions: IDLE → INITIALIZING → TRAVERSING → COMPLETED
        machine.transition_to(GlobalState.INITIALIZING)
        machine.transition_to(GlobalState.TRAVERSING)
        machine.complete()
        assert machine.is_terminal is True
        assert machine.is_active is False

    def test_can_transition_to(self):
        """Test can_transition_to method."""
        machine = GlobalStateMachine()

        # Valid transition
        assert machine.can_transition_to(GlobalState.INITIALIZING) is True

        # Invalid transition
        assert machine.can_transition_to(GlobalState.COMPLETED) is False

    def test_transition_to(self):
        """Test transition_to method."""
        machine = GlobalStateMachine()

        # Valid transition
        result = machine.transition_to(GlobalState.INITIALIZING, reason="Starting")
        assert result is True
        assert machine.state == GlobalState.INITIALIZING

    def test_invalid_transition_raises(self):
        """Test invalid transition raises ValueError."""
        machine = GlobalStateMachine()

        with pytest.raises(ValueError, match="Invalid transition"):
            machine.transition_to(GlobalState.COMPLETED)

    def test_transition_history(self):
        """Test transition history is tracked."""
        machine = GlobalStateMachine()
        machine.transition_to(GlobalState.INITIALIZING)
        machine.transition_to(GlobalState.TRAVERSING)

        history = machine.get_transition_history()
        assert len(history) == 2

    def test_pause_resume(self):
        """Test pause and resume functionality."""
        machine = GlobalStateMachine()
        # Follow proper transitions: IDLE → INITIALIZING → TRAVERSING
        machine.transition_to(GlobalState.INITIALIZING)
        machine.transition_to(GlobalState.TRAVERSING)

        # Pause
        machine.pause()
        assert machine.is_paused is True

        # Resume
        machine.resume()
        assert machine.state == GlobalState.TRAVERSING

    def test_report_error(self):
        """Test error reporting."""
        machine = GlobalStateMachine()
        # Follow proper transitions: IDLE → INITIALIZING → TRAVERSING
        machine.transition_to(GlobalState.INITIALIZING)
        machine.transition_to(GlobalState.TRAVERSING)

        error = Exception("Test error")
        machine.report_error(error)

        assert machine.state == GlobalState.ERROR
        assert machine.error_context is not None

    def test_reset(self):
        """Test machine reset."""
        machine = GlobalStateMachine()
        # Follow proper transitions
        machine.transition_to(GlobalState.INITIALIZING)
        machine.transition_to(GlobalState.TRAVERSING)
        machine.reset()

        assert machine.state == GlobalState.IDLE
        assert len(machine.get_transition_history()) == 0


class TestTraversalState:
    """Tests for TraversalState enum."""

    def test_traversal_state_values(self):
        """Test TraversalState has correct values."""
        assert TraversalState.NODE_SELECT.value == "node_select"
        assert TraversalState.PRECONDITION_CHECK.value == "precondition_check"
        assert TraversalState.EXECUTE.value == "execute"
        assert TraversalState.RESULT_VERIFY.value == "result_verify"
        assert TraversalState.BRANCH.value == "branch"

    def test_traversal_state_values_method(self):
        """Test TraversalState.values() method."""
        values = TraversalState.values()
        assert len(values) == 5
        assert "node_select" in values

    def test_traversal_state_from_value(self):
        """Test TraversalState.from_value() method."""
        state = TraversalState.from_value("execute")
        assert state == TraversalState.EXECUTE


class TestTraversalStateTransition:
    """Tests for TraversalStateTransition model."""

    def test_transition_creation(self):
        """Test creating traversal state transition."""
        transition = TraversalStateTransition(
            from_state=TraversalState.NODE_SELECT,
            to_state=TraversalState.EXECUTE,
        )
        assert transition.from_state == TraversalState.NODE_SELECT
        assert transition.to_state == TraversalState.EXECUTE

    def test_transition_with_node_id(self):
        """Test transition with node_id."""
        transition = TraversalStateTransition(
            from_state=TraversalState.NODE_SELECT,
            to_state=TraversalState.EXECUTE,
            node_id="node123",
        )
        assert transition.node_id == "node123"


class TestTraversalStateMachine:
    """Tests for TraversalStateMachine model."""

    def test_initial_state(self):
        """Test machine starts in NODE_SELECT."""
        machine = TraversalStateMachine()
        assert machine.state == TraversalState.NODE_SELECT

    def test_can_transition_to(self):
        """Test can_transition_to method."""
        machine = TraversalStateMachine()

        # Valid transition
        assert machine.can_transition_to(TraversalState.PRECONDITION_CHECK) is True

        # Invalid transition
        assert machine.can_transition_to(TraversalState.NODE_SELECT) is False

    def test_transition_to(self):
        """Test transition_to method."""
        machine = TraversalStateMachine()
        result = machine.transition_to(TraversalState.PRECONDITION_CHECK)
        assert result is True
        assert machine.state == TraversalState.PRECONDITION_CHECK

    def test_set_current_node(self):
        """Test set_current_node method."""
        machine = TraversalStateMachine()
        machine.set_current_node("node123")
        assert machine.current_node_id == "node123"

    def test_set_execution_result(self):
        """Test set_execution_result method."""
        machine = TraversalStateMachine()
        result_data = {"success": True}
        machine.set_execution_result(result_data)
        assert machine.execution_result == result_data

    def test_set_precondition_result(self):
        """Test set_precondition_result method."""
        machine = TraversalStateMachine()
        machine.set_precondition_result(True)
        assert machine.precondition_result is True

    def test_transition_history(self):
        """Test transition history is tracked."""
        machine = TraversalStateMachine()
        machine.transition_to(TraversalState.PRECONDITION_CHECK, node_id="node1")
        machine.transition_to(TraversalState.EXECUTE, node_id="node1")

        history = machine.get_transition_history()
        assert len(history) == 2

    def test_reset(self):
        """Test machine reset."""
        machine = TraversalStateMachine()
        # Follow proper transitions: NODE_SELECT → PRECONDITION_CHECK → EXECUTE
        machine.transition_to(TraversalState.PRECONDITION_CHECK)
        machine.transition_to(TraversalState.EXECUTE)
        machine.reset()

        assert machine.state == TraversalState.NODE_SELECT


class TestStackFrame:
    """Tests for StackFrame model."""

    def test_stack_frame_creation(self):
        """Test creating stack frame."""
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )
        frame = StackFrame(node=node)
        assert frame.node_id == "test"
        assert frame.has_children is False

    def test_stack_frame_with_children(self):
        """Test stack frame with child queue."""
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )
        frame = StackFrame(
            node=node,
            child_queue=["child1", "child2", "child3"],
        )
        assert frame.has_children is True
        assert frame.remaining_children == 3

    def test_current_child_idx_validation(self):
        """Test StackFrame validates current_child_idx."""
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )

        # Valid
        StackFrame(node=node, current_child_idx=0)
        StackFrame(node=node, child_queue=["a", "b"], current_child_idx=2)

        # Invalid - negative
        with pytest.raises(ValueError, match="current_child_idx cannot be negative"):
            StackFrame(node=node, current_child_idx=-1)

        # Invalid - exceeds queue length
        with pytest.raises(ValueError, match="cannot exceed"):
            StackFrame(node=node, child_queue=["a"], current_child_idx=5)

    def test_is_complete(self):
        """Test is_complete property."""
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )
        frame = StackFrame(
            node=node,
            child_queue=["child1"],
            current_child_idx=0,
        )
        assert frame.is_complete is False

        frame.current_child_idx = 1
        assert frame.is_complete is True

    def test_get_next_child(self):
        """Test get_next_child method."""
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )
        frame = StackFrame(
            node=node,
            child_queue=["child1", "child2"],
            current_child_idx=0,
        )

        # Get first child
        child = frame.get_next_child()
        assert child == "child1"
        assert frame.current_child_idx == 1

        # Get second child
        child = frame.get_next_child()
        assert child == "child2"

        # No more children
        child = frame.get_next_child()
        assert child is None

    def test_peek_next_child(self):
        """Test peek_next_child method."""
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )
        frame = StackFrame(
            node=node,
            child_queue=["child1", "child2"],
            current_child_idx=0,
        )

        # Peek should not advance
        child = frame.peek_next_child()
        assert child == "child1"
        assert frame.current_child_idx == 0

    def test_duration(self):
        """Test duration property."""
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )
        frame = StackFrame(node=node)
        duration = frame.duration
        assert duration >= 0
        assert duration < 1  # Should be very recent


class TestNodeStack:
    """Tests for NodeStack model."""

    def test_stack_creation(self):
        """Test creating node stack."""
        stack = NodeStack()
        assert stack.is_empty is True
        assert stack.size == 0

    def test_push_pop(self):
        """Test push and pop operations."""
        stack = NodeStack()
        op = Operation(action="no_action")
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.LEAF_ACTION,
            operation=op,
        )

        stack.push(node)
        assert stack.size == 1
        assert not stack.is_empty

        frame = stack.pop()
        assert frame is not None
        assert frame.node_id == "test"
        assert stack.is_empty

    def test_top_peek(self):
        """Test top and peek operations."""
        stack = NodeStack()
        op = Operation(action="no_action")
        node1 = TraversalNode(
            node_id="node1",
            name="Node1",
            node_type=NodeType.LEAF_ACTION,
            operation=op,
        )
        node2 = TraversalNode(
            node_id="node2",
            name="Node2",
            node_type=NodeType.LEAF_ACTION,
            operation=op,
        )

        stack.push(node1)
        stack.push(node2)

        # Top should be node2
        assert stack.top().node_id == "node2"
        assert stack.get_current_node_id() == "node2"

        # Peek at parent (offset 1)
        parent = stack.peek(offset=1)
        assert parent.node_id == "node1"

    def test_get_node_path(self):
        """Test get_node_path method."""
        stack = NodeStack()
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        node1 = TraversalNode(
            node_id="root",
            name="Root",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )
        node2 = TraversalNode(
            node_id="child",
            name="Child",
            node_type=NodeType.LEAF_ACTION,
            operation=op,
        )

        stack.push(node1)
        stack.push(node2)

        path = stack.get_node_path()
        assert path == ["root", "child"]

    def test_depth_limit(self):
        """Test depth limit enforcement."""
        stack = NodeStack(max_depth=2)
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        node1 = TraversalNode(
            node_id="n1",
            name="N1",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )
        node2 = TraversalNode(
            node_id="n2",
            name="N2",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )
        node3 = TraversalNode(
            node_id="n3",
            name="N3",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )

        stack.push(node1)
        stack.push(node2)

        # Third push should exceed limit
        with pytest.raises(RuntimeError, match="depth limit"):
            stack.push(node3)

    def test_clear(self):
        """Test clear operation."""
        stack = NodeStack()
        op = Operation(action="no_action")
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.LEAF_ACTION,
            operation=op,
        )

        stack.push(node)
        assert stack.size == 1

        stack.clear()
        assert stack.is_empty

    def test_get_summary(self):
        """Test get_summary method."""
        stack = NodeStack()
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )

        stack.push(node)
        summary = stack.get_summary()

        assert summary["size"] == 1
        assert summary["current_node"] == "test"
        assert summary["path"] == ["test"]
