"""Unit tests for interaction.py: NavigationResult, StateMachineOrchestrator (V6.5 API)."""

import pytest

from src.state_machine.interaction import (
    NavigationResult,
    StateMachineOrchestrator,
    TraversalContext,
)
from src.state_machine.global_fsm import GlobalState
from src.state_machine.traversal_fsm import TraversalState
from src.graph.node import TraversalNode, NodeType, Operation


def _make_node(node_id="n1", name="Test", node_type=NodeType.CONTAINER):
    from src.graph.node import ChildrenStrategy, ChildrenStrategyType
    cs = ChildrenStrategy(type=ChildrenStrategyType.STATIC if node_type == NodeType.CONTAINER else ChildrenStrategyType.NONE)
    return TraversalNode(
        node_id=node_id, name=name, node_type=node_type,
        operation=Operation(action="no_action"),
        children_strategy=cs,
    )


class TestNavigationResult:
    def test_create_success(self):
        r = NavigationResult(success=True, actions_taken=["click"], final_path=["home"])
        assert r.success is True
        assert r.actions_taken == ["click"]
        assert r.final_path == ["home"]
        assert r.error_message is None

    def test_create_failure(self):
        r = NavigationResult(success=False, error_message="not found")
        assert r.success is False
        assert r.error_message == "not found"
        assert r.actions_taken == []

    def test_defaults(self):
        r = NavigationResult(success=True)
        assert r.actions_taken == []
        assert r.final_path == []
        assert r.error_message is None


class TestStateMachineOrchestrator:
    def test_initialization(self):
        o = StateMachineOrchestrator()
        assert o.global_fsm.state == GlobalState.IDLE
        assert o.traversal_fsm.state == TraversalState.NODE_SELECT
        assert o.node_stack.is_empty

    def test_initialize_with_root_node(self):
        o = StateMachineOrchestrator()
        root = _make_node("root", "Root")
        result = o.initialize(root)
        assert result is True
        assert o.global_fsm.state == GlobalState.TRAVERSING
        assert not o.node_stack.is_empty

    def test_initialize_failure(self):
        o = StateMachineOrchestrator(max_stack_depth=0)
        root = _make_node("root", "Root")
        result = o.initialize(root)
        assert result is False

    def test_register_navigation_callback(self):
        o = StateMachineOrchestrator()
        called = []

        def cb(target, timeout):
            called.append((target, timeout))
            return NavigationResult(success=True, final_path=[target])

        o.register_navigation_callback(cb)
        assert o._navigation_callback is not None
        # Verify callback works
        result = o._navigation_callback("settings", 5.0)
        assert result.success
        assert called == [("settings", 5.0)]

    def test_register_operation_callback(self):
        o = StateMachineOrchestrator()
        called = []

        def cb(node):
            called.append(node.node_id)
            return {"success": True}

        o.register_operation_callback(cb)
        result = o.execute_node(_make_node("n1"))
        assert result == {"success": True}
        assert called == ["n1"]

    def test_register_children_generator_callback(self):
        o = StateMachineOrchestrator()

        def cb(node, analysis):
            return ["c1", "c2"]

        o.register_children_generator_callback(cb)
        children = o.generate_children(_make_node())
        assert children == ["c1", "c2"]

    def test_execute_node_without_callback(self):
        o = StateMachineOrchestrator()
        result = o.execute_node(_make_node())
        assert result == {"success": False, "error": "No operation callback registered"}

    def test_is_traversal_complete_empty(self):
        o = StateMachineOrchestrator()
        assert o.is_traversal_complete()  # Stack empty

    def test_is_traversal_complete_not_empty(self):
        o = StateMachineOrchestrator()
        o.initialize(_make_node("root"))
        assert not o.is_traversal_complete()

    def test_get_status_summary(self):
        o = StateMachineOrchestrator()
        o.initialize(_make_node("root"))
        summary = o.get_status_summary()
        assert summary["global_state"] == GlobalState.TRAVERSING.value
        assert "stack" in summary
        assert "current_path" in summary


class TestTraversalContext:
    def test_default_context(self):
        ctx = TraversalContext()
        assert ctx.current_path == []
        assert ctx.visited_pages == {}
        assert ctx.visited_nodes == {}

    def test_mark_page_visited(self):
        ctx = TraversalContext()
        ctx.mark_page_visited("settings")
        assert ctx.is_page_visited("settings")
        assert not ctx.is_page_visited("home")

    def test_mark_node_visited(self):
        ctx = TraversalContext()
        ctx.mark_node_visited("n1")
        assert ctx.is_node_visited("n1")
        assert not ctx.is_node_visited("n2")
