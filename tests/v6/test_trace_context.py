"""
Unit tests for context models (tasks 10.16-10.19).

Tests:
- 10.16 TraversalRuntimeContext creation and mutation
- 10.17 TraversalContext frozen behavior
- 10.18 to_readonly() conversion
"""

from src.trace.context import Session, StackFrame, TraversalRuntimeContext


class TestSession:
    def test_session_creates_trace_id(self):
        s = Session()
        assert len(s.session_id) == 26
        assert s.trace_id == s.session_id

    def test_session_defaults(self):
        s = Session()
        assert s.status == "running"
        assert s.traversal_mode == "graph"

    def test_session_to_dict(self):
        s = Session(device_model="Pixel 7", os_version="Android 14")
        d = s.to_dict()
        assert d["device_model"] == "Pixel 7"
        assert d["status"] == "running"

    def test_session_from_dict(self):
        s = Session.from_dict({
            "session_id": "test-id",
            "device_model": "Pixel 7",
            "os_version": "Android 14",
            "status": "completed",
            "config": {},
        })
        assert s.session_id == "test-id"
        assert s.status == "completed"


class TestStackFrame:
    def test_stack_frame_eq_str(self):
        sf = StackFrame(node_id="n1")
        assert sf == "n1"
        assert "n1" in [sf]

    def test_stack_frame_eq_same(self):
        sf1 = StackFrame(node_id="n1")
        sf2 = StackFrame(node_id="n1")
        assert sf1 == sf2

    def test_stack_frame_neq(self):
        sf1 = StackFrame(node_id="n1")
        sf2 = StackFrame(node_id="n2")
        assert sf1 != sf2


class TestTraversalRuntimeContext:
    """10.16: TraversalRuntimeContext creation and mutation."""

    def test_default_creation(self):
        ctx = TraversalRuntimeContext()
        assert ctx.trace_id == ""
        assert ctx.node_stack == []
        assert ctx.current_path == []
        assert ctx.max_depth == 100

    def test_mutation(self):
        ctx = TraversalRuntimeContext(trace_id="t1")
        ctx.current_path.append("home")
        ctx.visited_pages.add("home")
        ctx.node_stack.append(StackFrame(node_id="n1"))
        ctx.consecutive_errors += 1
        assert ctx.current_path == ["home"]
        assert "home" in ctx.visited_pages
        assert ctx.consecutive_errors == 1

    def test_get_current_depth(self):
        ctx = TraversalRuntimeContext()
        assert ctx.get_current_depth() == 0
        ctx.node_stack.append(StackFrame(node_id="n1"))
        assert ctx.get_current_depth() == 1

    def test_is_at_max_depth(self):
        ctx = TraversalRuntimeContext(max_depth=1)
        assert not ctx.is_at_max_depth()
        ctx.node_stack.append(StackFrame(node_id="n1"))
        assert ctx.is_at_max_depth()

    def test_record_action(self):
        ctx = TraversalRuntimeContext()
        ctx.record_action("click", target="btn")
        assert len(ctx.action_history) == 1
        assert ctx.action_history[0]["action"] == "click"

    def test_record_action_truncates(self):
        ctx = TraversalRuntimeContext()
        for i in range(7):
            ctx.record_action(f"action_{i}")
        assert len(ctx.action_history) == 5
        assert ctx.action_history[-1]["action"] == "action_6"


class TestToReadonly:
    """10.17-10.18: TraversalContext frozen + to_readonly conversion."""

    def test_to_readonly_creates_frozen_context(self):
        ctx = TraversalRuntimeContext(trace_id="t1")
        ctx.current_path = ["home", "settings"]
        ctx.visited_pages.update(["home", "settings"])
        ctx.node_stack.append(StackFrame(node_id="n1", span_id="sp1"))
        ctx.step_count = 5

        ro = ctx.to_readonly()
        # Verify fields mapped correctly
        assert ro.current_path == ["home", "settings"]
        assert "home" in ro.visited_pages
        assert ro.step_count == 5
        assert ro.max_depth == 100
        assert ro.node_stack == ["n1"]

    def test_frozen_context_is_immutable(self):
        """10.17: TraversalContext should be frozen (attribute assignment blocked)."""
        ctx = TraversalRuntimeContext(trace_id="t1")
        ctx.current_path = ["home"]
        ro = ctx.to_readonly()

        # FrozenInstanceError or similar on attribute assignment
        try:
            ro.current_path = ["other"]
        except Exception:
            pass  # Expected — frozen
        else:
            raise AssertionError("Frozen TraversalContext should reject attribute assignment")

    def test_to_readonly_does_not_mutate_original(self):
        ctx = TraversalRuntimeContext(trace_id="t1")
        ctx.current_path = ["home"]
        original_path = ctx.current_path.copy()

        ro = ctx.to_readonly()
        ctx.current_path.append("settings")

        # readonly should be a snapshot from the time of conversion
        assert ro.current_path == ["home"]
        # original should be mutated
        assert ctx.current_path == ["home", "settings"]

    def test_to_readonly_empty_context(self):
        ctx = TraversalRuntimeContext()
        ro = ctx.to_readonly()
        assert ro.current_path == []
        assert ro.node_stack == []
        assert len(ro.visited_pages) == 0
