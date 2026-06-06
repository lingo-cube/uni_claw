"""Unit tests for NodeStack and StackFrame (V6.5 API)."""

import pytest
from datetime import datetime

from src.state_machine.node_stack import NodeStack, StackFrame
from src.graph.node import TraversalNode, NodeType, Operation, ChildrenStrategy, ChildrenStrategyType


def _make_node(node_id="n1", name="Test", node_type=None):
    from src.graph.node import ChildrenStrategy, ChildrenStrategyType
    nt = node_type or NodeType.CONTAINER
    cs = ChildrenStrategy(type=ChildrenStrategyType.NONE)
    if nt == NodeType.CONTAINER:
        cs = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
    return TraversalNode(
        node_id=node_id, name=name, node_type=nt,
        operation=Operation(action="no_action"),
        children_strategy=cs,
    )


class TestStackFrame:
    def test_create_frame(self):
        node = _make_node()
        frame = StackFrame(node=node)
        assert frame.node_id == "n1"
        assert not frame.has_children
        assert frame.is_complete
        assert frame.remaining_children == 0

    def test_has_children(self):
        node = _make_node()
        frame = StackFrame(node=node, child_queue=["c1", "c2"])
        assert frame.has_children
        assert not frame.is_complete
        assert frame.remaining_children == 2

    def test_get_next_child(self):
        node = _make_node()
        frame = StackFrame(node=node, child_queue=["c1", "c2"])
        assert frame.get_next_child() == "c1"
        assert frame.current_child_idx == 1
        assert frame.get_next_child() == "c2"
        assert frame.is_complete
        assert frame.get_next_child() is None

    def test_peek_next_child(self):
        node = _make_node()
        frame = StackFrame(node=node, child_queue=["c1", "c2"])
        assert frame.peek_next_child() == "c1"
        assert frame.current_child_idx == 0  # Unchanged

    def test_reset_child_index(self):
        node = _make_node()
        frame = StackFrame(node=node, child_queue=["c1", "c2"])
        frame.get_next_child()  # idx → 1
        frame.reset_child_index()  # idx → 0
        assert frame.get_next_child() == "c1"

    def test_duration(self):
        node = _make_node()
        frame = StackFrame(node=node)
        assert frame.duration >= 0

    def test_invalid_child_idx_raises(self):
        node = _make_node()
        with pytest.raises(ValueError):
            StackFrame(node=node, current_child_idx=-1)

    def test_metadata_default(self):
        node = _make_node()
        frame = StackFrame(node=node)
        assert frame.metadata == {}


class TestNodeStack:
    def test_empty_stack(self):
        s = NodeStack()
        assert s.is_empty
        assert s.size == 0
        assert s.depth == 0

    def test_push_and_size(self):
        s = NodeStack()
        s.push(_make_node("n1"))
        assert s.size == 1
        assert not s.is_empty

    def test_push_with_children(self):
        s = NodeStack()
        s.push(_make_node("root"), children=["c1", "c2", "c3"])
        frame = s.top()
        # Children reversed for DFS
        assert frame.get_next_child() == "c3"
        assert frame.get_next_child() == "c2"
        assert frame.get_next_child() == "c1"

    def test_pop(self):
        s = NodeStack()
        s.push(_make_node("n1"))
        frame = s.pop()
        assert frame.node_id == "n1"
        assert s.is_empty

    def test_pop_empty_returns_none(self):
        s = NodeStack()
        assert s.pop() is None

    def test_top(self):
        s = NodeStack()
        s.push(_make_node("bottom"))
        s.push(_make_node("top"))
        assert s.top().node_id == "top"

    def test_top_empty_returns_none(self):
        s = NodeStack()
        assert s.top() is None

    def test_peek_offset(self):
        s = NodeStack()
        s.push(_make_node("a"))
        s.push(_make_node("b"))
        s.push(_make_node("c"))
        assert s.peek(0).node_id == "c"
        assert s.peek(1).node_id == "b"
        assert s.peek(2).node_id == "a"
        assert s.peek(3) is None
        assert s.peek(-1) is None

    def test_depth_limit(self):
        s = NodeStack(max_depth=2)
        s.push(_make_node("a"))
        s.push(_make_node("b"))
        assert not s.depth_limit_reached
        with pytest.raises(RuntimeError, match="depth limit"):
            s.push(_make_node("c"))
        assert s.depth_limit_reached

    def test_get_node_path(self):
        s = NodeStack()
        s.push(_make_node("root"))
        s.push(_make_node("child"))
        assert s.get_node_path() == ["root", "child"]

    def test_get_current_node_id(self):
        s = NodeStack()
        assert s.get_current_node_id() is None
        s.push(_make_node("n1"))
        assert s.get_current_node_id() == "n1"

    def test_get_parent_node_id(self):
        s = NodeStack()
        s.push(_make_node("root"))
        s.push(_make_node("child"))
        assert s.get_parent_node_id() == "root"

    def test_get_parent_node_id_none(self):
        s = NodeStack()
        s.push(_make_node("root"))
        assert s.get_parent_node_id() is None

    def test_contains_node(self):
        s = NodeStack()
        s.push(_make_node("root"))
        s.push(_make_node("settings"))
        assert s.contains_node("root")
        assert s.contains_node("settings")
        assert not s.contains_node("nonexistent")

    def test_get_depth_of_node(self):
        s = NodeStack()
        s.push(_make_node("root"))
        s.push(_make_node("child"))
        assert s.get_depth_of_node("root") == 0
        assert s.get_depth_of_node("child") == 1
        assert s.get_depth_of_node("nonexistent") == -1

    def test_clear(self):
        s = NodeStack()
        s.push(_make_node("a"))
        s.push(_make_node("b"))
        s.clear()
        assert s.is_empty
        assert not s.depth_limit_reached

    def test_get_summary(self):
        s = NodeStack()
        s.push(_make_node("root"), children=["c1"])
        summary = s.get_summary()
        assert summary["size"] == 1
        assert summary["current_node"] == "root"
        assert summary["path"] == ["root"]
        assert not summary["depth_limit_reached"]

    def test_len(self):
        s = NodeStack()
        assert len(s) == 0
        s.push(_make_node("a"))
        assert len(s) == 1

    def test_repr(self):
        s = NodeStack()
        assert "empty" in repr(s)
        s.push(_make_node("root"))
        assert "root" in repr(s)

    def test_to_list(self):
        s = NodeStack()
        s.push(_make_node("a"))
        s.push(_make_node("b"))
        frames = s.to_list()
        assert len(frames) == 2
        assert frames[0].node_id == "a"
        assert frames[1].node_id == "b"

    def test_max_depth_default(self):
        s = NodeStack()
        assert s.max_depth == 10
