"""
Integration tests for graph-state-trace-model.

Tests cover:
- End-to-end graph mode traversal
- Static and dynamic graph traversal
- Mixed mode traversal
- Configuration switching
- V3.0 compatibility
- Depth-first traversal correctness
- Exception handling
"""

import pytest
from pathlib import Path
from datetime import datetime

from src.graph.node import (
    TraversalNode,
    NodeType,
    Operation,
    Target,
    ChildrenStrategy,
    ChildrenStrategyType,
)
from src.graph.template import TemplateRegistry
from src.graph.matcher import DynamicMatcher
from src.state_machine import StateMachineOrchestrator, NodeStack
from src.state_machine.global_fsm import GlobalState
from src.state_machine.traversal_fsm import TraversalState
from src.trace import TraceRecorder, TraceConfig, TraversalTrace
from src.trace.replay import ReplayEngine, ReplayMode
from src.traversal.traversal_engine import TraversalConfig


class TestGraphModelIntegration:
    """Integration tests for graph model components."""

    def test_template_registry_with_matcher(self):
        """Test template registry integrated with dynamic matcher."""
        registry = TemplateRegistry()
        matcher = DynamicMatcher(registry)

        # Load rules
        matcher.load_rules({
            "menu_rule": {
                "match_condition": {"type": "menu_item"},
                "child_template": "menu_container",
            }
        })

        # Test matching
        menu_item = {"type": "menu_item", "text": "Settings", "index": 0}
        root_node = registry.instantiate("menu_container", {"item_text": "Home"})

        result = matcher.match(menu_item, root_node)
        assert result.matched
        assert result.template_id == "menu_container"

    def test_node_stack_depth_first_order(self):
        """Test node stack maintains depth-first order."""
        stack = NodeStack(max_depth=10)

        # Create a simple tree structure
        root = TraversalNode(
            node_id="root",
            name="Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        child1 = TraversalNode(
            node_id="child1",
            name="Child 1",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        child2 = TraversalNode(
            node_id="child2",
            name="Child 2",
            node_type=NodeType.LEAF_ACTION,
            operation=Operation(action="click"),
        )

        # Push root with children (reversed for DFS)
        stack.push(root, children=["child1", "child2"])

        # Verify children are reversed
        frame = stack.top()
        assert frame.child_queue == ["child2", "child1"]

        # Process children in order
        next_child = frame.get_next_child()
        assert next_child == "child2"  # Last child first (depth-first)

        next_child = frame.get_next_child()
        assert next_child == "child1"


class TestStateMachineIntegration:
    """Integration tests for state machine components."""

    def test_orchestrator_initialization(self):
        """Test state machine orchestrator initialization."""
        orchestrator = StateMachineOrchestrator()

        assert orchestrator.global_fsm.state == GlobalState.IDLE
        assert orchestrator.node_stack.is_empty

    def test_orchestrator_with_root_node(self):
        """Test orchestrator with root node initialization."""
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

    def test_node_stack_limit(self):
        """Test node stack depth limit enforcement."""
        stack = NodeStack(max_depth=3)
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        # Push up to limit
        stack.push(node)
        stack.push(node)
        stack.push(node)
        assert stack.size == 3

        # Fourth push should fail
        with pytest.raises(RuntimeError, match="depth limit"):
            stack.push(node)


class TestTraceIntegration:
    """Integration tests for trace system."""

    def test_trace_recorder_with_orchestrator(self, tmp_path):
        """Test trace recorder with state machine orchestrator."""
        config = TraceConfig(enabled=True, output_path=tmp_path)
        recorder = TraceRecorder(config)

        orchestrator = StateMachineOrchestrator()

        # Start recording
        recorder.start_session(device_id="test_device")

        # Simulate some traversal
        root = TraversalNode(
            node_id="root",
            name="Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )
        orchestrator.initialize(root)

        # Record a state transition
        step = recorder.record_state_transition(
            GlobalState.TRAVERSING,
            TraversalState.EXECUTE,
            orchestrator.node_stack,
            ["Root"],
        )

        assert step is not None
        assert step.global_state == "traversing"

        # End session
        trace = recorder.end_session()
        assert trace is not None
        assert len(trace.steps) == 1

    def test_trace_replay_workflow(self, tmp_path):
        """Test complete trace and replay workflow."""
        # Create and record a trace
        config = TraceConfig(enabled=True, output_path=tmp_path)
        recorder = TraceRecorder(config)

        recorder.start_session()
        recorder.record_state_transition(
            GlobalState.TRAVERSING,
            TraversalState.EXECUTE,
            NodeStack(),
            [],
        )
        trace = recorder.end_session()

        # Load and replay
        engine = ReplayEngine(mode=ReplayMode.SIMULATION)
        session_dir = list(tmp_path.glob("trace_*"))[0]
        engine.load_trace(session_dir)

        result = engine.replay_simulation()
        assert result.mode == ReplayMode.SIMULATION
        assert result.steps_replayed == 1


class TestTraversalConfigIntegration:
    """Integration tests for traversal configuration."""

    def test_graph_mode_config_disabled(self):
        """Test graph mode disabled by default."""
        config = TraversalConfig()
        assert config.use_graph_mode is False
        assert config.template_registry_path is None
        assert config.trace_enabled is False

    def test_graph_mode_config_enabled(self):
        """Test graph mode can be enabled."""
        config = TraversalConfig(
            use_graph_mode=True,
            template_registry_path="/path/to/templates.json",
            trace_enabled=True,
            trace_output_path="/path/to/traces",
        )

        assert config.use_graph_mode is True
        assert config.template_registry_path == "/path/to/templates.json"
        assert config.trace_enabled is True

    def test_trace_config_fields(self):
        """Test trace configuration fields."""
        config = TraversalConfig(
            trace_enabled=True,
            trace_keep_count=20,
            trace_snapshot_interval=15,
        )

        assert config.trace_keep_count == 20
        assert config.trace_snapshot_interval == 15


class TestV3Compatibility:
    """Tests for V3.0 compatibility."""

    def test_v3_mode_default(self):
        """Test V3.0 linear mode is default."""
        config = TraversalConfig()
        assert config.use_graph_mode is False

    def test_traversal_state_backward_compatibility(self):
        """Test TraversalState maintains backward compatibility."""
        from src.state.content_tree import TraversalState

        # Create state with V3.0 fields
        state = TraversalState(
            current_path=["Home", "Settings"],
            visited={"item1", "item2"},
            step_count=10,
        )

        # V3.0 fields should work
        assert state.current_path == ["Home", "Settings"]
        assert len(state.visited) == 2
        assert state.step_count == 10

        # V4.0 fields should be present but empty
        assert len(state.node_stack) == 0
        assert state.current_node_id is None
        assert state.use_graph_mode is False


class TestEndToEndGraphTraversal:
    """End-to-end tests for graph mode traversal."""

    def test_simple_graph_traversal(self):
        """Test simple graph traversal with static nodes."""
        orchestrator = StateMachineOrchestrator()

        # Create a simple graph: root -> child1, child2
        root = TraversalNode(
            node_id="root",
            name="Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=["child1", "child2"],
            ),
        )

        # Initialize
        orchestrator.initialize(root)
        assert orchestrator.node_stack.size == 1

        # Get next node (should be child2 due to reversal)
        top_frame = orchestrator.node_stack.top()
        next_child = top_frame.get_next_child()
        assert next_child == "child2"

    def test_dynamic_graph_traversal(self):
        """Test graph traversal with dynamic matching."""
        registry = TemplateRegistry()
        matcher = DynamicMatcher(registry)

        # Set up matching rules
        matcher.load_rules({
            "menu_rule": {
                "match_condition": {"type": "menu_item"},
                "child_template": "menu_container",
            }
        })

        # Simulate menu items from screen
        menu_items = [
            {"type": "menu_item", "text": "Settings", "index": 0},
            {"type": "menu_item", "text": "Display", "index": 1},
        ]

        root = registry.instantiate("menu_container", {"item_text": "Home"})

        # Match all items
        results = matcher.match_all(menu_items, root)

        assert len(results) == 2
        assert all(r.matched for r in results)

    def test_depth_first_traversal_correctness(self):
        """Test that depth-first traversal visits nodes in correct order."""
        stack = NodeStack()

        # Build tree: root -> [A, B] where A -> [A1, A2]
        # DFS should process: root, B, A, A2, A1

        root = TraversalNode(
            node_id="root",
            name="Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        # Push root with children A and B (reversed to B, A)
        stack.push(root, children=["A", "B"])

        # Top frame should have reversed children
        frame = stack.top()
        assert frame.child_queue == ["B", "A"]

        # Get first child (should be B for DFS)
        first = frame.get_next_child()
        assert first == "B"

        # Get second child (should be A)
        second = frame.get_next_child()
        assert second == "A"


class TestMixedModeTraversal:
    """Tests for mixed static/dynamic traversal."""

    def test_mixed_mode_static_root_dynamic_children(self):
        """Test static root with dynamically discovered children."""
        registry = TemplateRegistry()
        matcher = DynamicMatcher(registry)

        # Static root node
        root = TraversalNode(
            node_id="settings_root",
            name="Settings",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click", target=Target(by="text", value="Settings")),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.DYNAMIC_MATCH,
            ),
        )

        # Load rules for settings children
        matcher.load_rules({
            "settings_rule": {
                "match_condition": {"type": "menu_item"},
                "child_template": "menu_container",
            }
        })

        # Simulate discovering children
        children = [
            {"type": "menu_item", "text": "Display", "index": 0},
            {"type": "menu_item", "text": "Sound", "index": 1},
        ]

        results = matcher.match_all(children, root)
        assert len(results) == 2
        assert all(r.matched for r in results)


class TestConfigurationSwitching:
    """Tests for switching between graph and linear modes."""

    def test_switch_from_linear_to_graph(self):
        """Test switching configuration from linear to graph mode."""
        linear_config = TraversalConfig(use_graph_mode=False)
        assert linear_config.use_graph_mode is False

        graph_config = TraversalConfig(
            use_graph_mode=True,
            template_registry_path="/tmp/templates.json",
        )
        assert graph_config.use_graph_mode is True

    def test_graph_mode_preserves_v3_behavior(self):
        """Test that disabling graph mode preserves V3.0 behavior."""
        config = TraversalConfig(use_graph_mode=False)

        # V3.0 specific fields should be available
        assert config.max_steps == 200  # V3.0 default
        assert config.wait_time == 0.5  # V3.0 default
        assert config.skip_readonly is True  # V3.0 default


class TestExceptionHandlingIntegration:
    """Tests for exception handling with graph mode."""

    def test_orchestrator_error_handling(self):
        """Test error handling in orchestrator."""
        orchestrator = StateMachineOrchestrator()

        root = TraversalNode(
            node_id="root",
            name="Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        orchestrator.initialize(root)

        # Simulate error
        error = Exception("Test error")
        orchestrator.global_fsm.report_error(error)

        assert orchestrator.global_fsm.state == GlobalState.ERROR
        assert orchestrator.global_fsm.error_context is not None

    def test_stack_recovery_after_error(self):
        """Test node stack recovery after error."""
        stack = NodeStack()

        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
        )

        # Push some nodes
        stack.push(node, children=["c1", "c2"])
        stack.push(node, children=["c3"])

        # Simulate error by popping
        stack.pop()

        # Stack should still have first frame
        assert stack.size == 1
        assert stack.top().node_id == "test"


class TestTraceRecorderIntegration:
    """Integration tests for trace recorder."""

    def test_full_recording_workflow(self, tmp_path):
        """Test complete recording workflow."""
        config = TraceConfig(enabled=True, output_path=tmp_path)
        recorder = TraceRecorder(config)

        # Start session
        recorder.start_session(device_id="test_device")
        assert recorder.is_recording()

        # Record multiple steps
        for i in range(5):
            recorder.record_state_transition(
                GlobalState.TRAVERSING,
                TraversalState.EXECUTE,
                NodeStack(),
                [f"Step{i}"],
            )

        # End session
        trace = recorder.end_session()
        assert trace is not None
        assert len(trace.steps) == 5
        assert trace.summary is not None

        # Verify files created
        session_dir = list(tmp_path.glob("trace_*"))[0]
        assert (session_dir / "trace.jsonl").exists()
        assert (session_dir / "summary.json").exists()

    def test_trace_cleanup(self, tmp_path):
        """Test old trace cleanup."""
        config = TraceConfig(enabled=True, output_path=tmp_path, keep_count=2)
        recorder = TraceRecorder(config)

        # Create more traces than keep_count
        for i in range(5):
            recorder.start_session()
            recorder.record_state_transition(
                GlobalState.TRAVERSING,
                TraversalState.EXECUTE,
                NodeStack(),
                [],
            )
            recorder.end_session()

        # Only 2 should remain
        trace_dirs = list(tmp_path.glob("trace_*"))
        assert len(trace_dirs) <= 2
