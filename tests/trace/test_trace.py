"""
Unit tests for trace system.

Tests cover:
- Trace data classes
- TraceRecorder
- JSON Lines output
- Replay modes (strict, decision, simulation)
"""

import json
import pytest
from datetime import datetime
from pathlib import Path

from src.trace.models import (
    TraversalTrace,
    TraceStep,
    StateSnapshot,
    TraceSummary,
    SessionInfo,
    TraceDecision,
    TraceExecution,
    ExecutionStatus,
)
from src.trace.recorder import TraceRecorder, TraceConfig
from src.trace.replay import ReplayEngine, ReplayMode, ReplayResult


class TestTraceDataClasses:
    """Tests for trace data classes."""

    def test_session_info(self):
        """Test SessionInfo creation and serialization."""
        info = SessionInfo(
            device_id="test_device",
            app_version="1.0.0",
            traversal_mode="graph",
        )
        assert info.device_id == "test_device"
        assert info.traversal_mode == "graph"

        data = info.to_dict()
        assert data["device_id"] == "test_device"

    def test_trace_decision(self):
        """Test TraceDecision creation."""
        decision = TraceDecision(
            node_id="test_node",
            node_type="container",
            operation_action="click",
            target_description="Settings button",
            confidence=0.95,
        )
        assert decision.node_id == "test_node"
        assert decision.confidence == 0.95

    def test_trace_execution_success(self):
        """Test TraceExecution for successful execution."""
        execution = TraceExecution(
            status=ExecutionStatus.SUCCESS,
            duration_ms=150.5,
            screenshot_ref="screenshot.png:abc123",
        )
        assert execution.status == ExecutionStatus.SUCCESS
        assert execution.duration_ms == 150.5

    def test_trace_execution_failure(self):
        """Test TraceExecution for failed execution."""
        execution = TraceExecution(
            status=ExecutionStatus.FAILED,
            duration_ms=50.0,
            error_message="Element not found",
            error_type="NotFoundError",
        )
        assert execution.status == ExecutionStatus.FAILED
        assert execution.error_message == "Element not found"

    def test_trace_step(self):
        """Test TraceStep creation."""
        step = TraceStep(
            step_id=1,
            timestamp=datetime.now(),
            global_state="traversing",
            traversal_state="execute",
        )
        assert step.step_id == 1
        assert step.global_state == "traversing"

    def test_trace_step_serialization(self):
        """Test TraceStep to_dict and from_dict."""
        step = TraceStep(
            step_id=1,
            timestamp=datetime.now(),
            global_state="traversing",
            traversal_state="execute",
            decision=TraceDecision(
                node_id="test",
                node_type="leaf",
                operation_action="click",
            ),
        )

        data = step.to_dict()
        assert data["step_id"] == 1

        restored = TraceStep.from_dict(data)
        assert restored.step_id == 1
        assert restored.decision.node_id == "test"

    def test_state_snapshot(self):
        """Test StateSnapshot creation."""
        snapshot = StateSnapshot(
            snapshot_id="snap_1",
            timestamp=datetime.now(),
            step_id=5,
            full_state={"test": "data"},
            node_stack=[{"node_id": "n1"}],
            visited_nodes={},
            current_path=["Home"],
        )
        assert snapshot.snapshot_id == "snap_1"
        assert snapshot.step_id == 5

    def test_trace_summary(self):
        """Test TraceSummary creation."""
        summary = TraceSummary(
            total_steps=10,
            successful_operations=8,
            failed_operations=1,
            skipped_operations=1,
            total_duration_ms=5000.0,
            visited_pages_count=3,
            visited_nodes_count=5,
            screenshots_count=8,
            errors_count=1,
        )
        assert summary.total_steps == 10
        assert summary.successful_operations == 8

    def test_traversal_trace(self):
        """Test TraversalTrace creation."""
        trace = TraversalTrace(
            session_info=SessionInfo(device_id="test"),
            trace_id="test_trace",
        )
        assert trace.session_info.device_id == "test"
        assert len(trace.steps) == 0

        step = TraceStep(
            step_id=1,
            timestamp=datetime.now(),
            global_state="traversing",
            traversal_state="execute",
        )
        trace.add_step(step)
        assert len(trace.steps) == 1

    def test_traversal_trace_finalize(self):
        """Test TraversalTrace finalization."""
        trace = TraversalTrace(session_info=SessionInfo())

        # Add some steps
        for i in range(3):
            trace.add_step(
                TraceStep(
                    step_id=i + 1,
                    timestamp=datetime.now(),
                    global_state="traversing",
                    traversal_state="execute",
                )
            )

        trace.finalize()

        assert trace.summary is not None
        assert trace.summary.total_steps == 3
        assert trace.session_info.end_time is not None


class TestTraceRecorder:
    """Tests for TraceRecorder."""

    def test_recorder_disabled(self):
        """Test recorder when disabled."""
        config = TraceConfig(enabled=False)
        recorder = TraceRecorder(config)

        recorder.start_session(device_id="test")
        assert recorder.current_trace is None
        assert not recorder.is_recording()

    def test_recorder_enabled(self, tmp_path):
        """Test recorder when enabled."""
        config = TraceConfig(enabled=True, output_path=tmp_path)
        recorder = TraceRecorder(config)

        recorder.start_session(device_id="test_device")
        assert recorder.is_recording()
        assert recorder.current_trace is not None

    def test_record_state_transition(self, tmp_path):
        """Test recording state transitions."""
        from src.state_machine.global_fsm import GlobalState
        from src.state_machine.traversal_fsm import TraversalState
        from src.state_machine.node_stack import NodeStack

        config = TraceConfig(enabled=True, output_path=tmp_path)
        recorder = TraceRecorder(config)
        recorder.start_session()

        stack = NodeStack()
        step = recorder.record_state_transition(
            GlobalState.TRAVERSING,
            TraversalState.EXECUTE,
            stack,
            ["Home", "Settings"],
        )

        assert step is not None
        assert step.step_id == 1
        assert step.global_state == "traversing"

    def test_record_decision(self, tmp_path):
        """Test recording decisions."""
        config = TraceConfig(enabled=True, output_path=tmp_path)
        recorder = TraceRecorder(config)
        recorder.start_session()

        step = recorder.record_state_transition(
            GlobalState.TRAVERSING,
            TraversalState.EXECUTE,
            NodeStack(),
            [],
        )

        recorder.record_decision(
            step,
            node_id="test_node",
            node_type="leaf",
            operation_action="click",
        )

        assert step.decision is not None
        assert step.decision.node_id == "test_node"

    def test_record_execution_result(self, tmp_path):
        """Test recording execution results."""
        config = TraceConfig(enabled=True, output_path=tmp_path)
        recorder = TraceRecorder(config)
        recorder.start_session()

        step = recorder.record_state_transition(
            GlobalState.TRAVERSING,
            TraversalState.EXECUTE,
            NodeStack(),
            [],
        )

        recorder.record_execution_result(
            step,
            status=ExecutionStatus.SUCCESS,
            duration_ms=100.0,
        )

        assert step.execution is not None
        assert step.execution.status == ExecutionStatus.SUCCESS

    def test_record_screenshot(self, tmp_path):
        """Test screenshot recording."""
        config = TraceConfig(enabled=True, output_path=tmp_path, save_screenshots=True)
        recorder = TraceRecorder(config)
        recorder.start_session()

        step = recorder.record_state_transition(
            GlobalState.TRAVERSING,
            TraversalState.EXECUTE,
            NodeStack(),
            [],
        )

        fake_screenshot = b"fake_image_data"
        recorder.record_execution_result(
            step,
            status=ExecutionStatus.SUCCESS,
            duration_ms=100.0,
            screenshot_data=fake_screenshot,
        )

        assert step.execution.screenshot_ref is not None

    def test_end_session(self, tmp_path):
        """Test ending session and saving files."""
        config = TraceConfig(enabled=True, output_path=tmp_path)
        recorder = TraceRecorder(config)
        recorder.start_session()

        # Record a step
        recorder.record_state_transition(
            GlobalState.TRAVERSING,
            TraversalState.EXECUTE,
            NodeStack(),
            [],
        )

        trace = recorder.end_session()
        assert trace is not None
        assert trace.summary is not None

        # Check files were created
        session_dir = list(tmp_path.glob("trace_*"))[0]
        assert (session_dir / "trace.jsonl").exists()
        assert (session_dir / "summary.json").exists()

    def test_trace_file_format(self, tmp_path):
        """Test JSON Lines output format."""
        config = TraceConfig(enabled=True, output_path=tmp_path)
        recorder = TraceRecorder(config)
        recorder.start_session()

        recorder.record_state_transition(
            GlobalState.TRAVERSING,
            TraversalState.EXECUTE,
            NodeStack(),
            [],
        )

        recorder.end_session()

        # Read and verify format
        session_dir = list(tmp_path.glob("trace_*"))[0]
        with open(session_dir / "trace.jsonl", "r") as f:
            lines = f.readlines()

        assert len(lines) == 1
        data = json.loads(lines[0])
        assert "step_id" in data
        assert "timestamp" in data

    def test_keep_count_limit(self, tmp_path):
        """Test old trace cleanup."""
        config = TraceConfig(enabled=True, output_path=tmp_path, keep_count=2)
        recorder = TraceRecorder(config)

        # Create 3 sessions
        for i in range(3):
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
        assert len(trace_dirs) == 2


class TestReplayEngine:
    """Tests for ReplayEngine."""

    def test_replay_engine_init(self):
        """Test ReplayEngine initialization."""
        engine = ReplayEngine(mode=ReplayMode.STRICT)
        assert engine.mode == ReplayMode.STRICT

    def test_load_trace(self, tmp_path):
        """Test loading a trace."""
        # Create a test trace
        trace_dir = tmp_path / "test_trace"
        trace_dir.mkdir()

        # Create trace.jsonl
        with open(trace_dir / "trace.jsonl", "w") as f:
            step = TraceStep(
                step_id=1,
                timestamp=datetime.now(),
                global_state="traversing",
                traversal_state="execute",
            )
            f.write(json.dumps(step.to_dict()) + "\n")

        # Create session.json
        with open(trace_dir / "session.json", "w") as f:
            session = SessionInfo(device_id="test")
            f.write(json.dumps(session.to_dict()))

        # Load trace
        engine = ReplayEngine()
        result = engine.load_trace(trace_dir)

        assert result is True
        assert engine.current_trace is not None

    def test_strict_replay(self, tmp_path):
        """Test strict replay mode."""
        # Create and save a trace
        trace_dir = tmp_path / "test_trace"
        trace_dir.mkdir()

        trace = TraversalTrace(session_info=SessionInfo())
        trace.add_step(
            TraceStep(
                step_id=1,
                timestamp=datetime.now(),
                global_state="traversing",
                traversal_state="execute",
                decision=TraceDecision(
                    node_id="test",
                    node_type="leaf",
                    operation_action="click",
                ),
                execution=TraceExecution(
                    status=ExecutionStatus.SUCCESS,
                    duration_ms=100,
                ),
            )
        )

        with open(trace_dir / "trace.jsonl", "w") as f:
            for step in trace.steps:
                f.write(json.dumps(step.to_dict()) + "\n")

        with open(trace_dir / "session.json", "w") as f:
            f.write(json.dumps(trace.session_info.to_dict()))

        # Load and replay
        engine = ReplayEngine(mode=ReplayMode.STRICT)
        engine.load_trace(trace_dir)

        result = engine.replay_strict(stop_on_failure=False)

        assert result.mode == ReplayMode.STRICT
        assert result.steps_replayed == 1

    def test_decision_replay(self, tmp_path):
        """Test decision replay mode."""
        # Create trace
        trace_dir = tmp_path / "test_trace"
        trace_dir.mkdir()

        trace = TraversalTrace(session_info=SessionInfo())
        trace.add_step(
            TraceStep(
                step_id=1,
                timestamp=datetime.now(),
                global_state="traversing",
                traversal_state="execute",
                decision=TraceDecision(
                    node_id="test",
                    node_type="leaf",
                    operation_action="click",
                ),
            )
        )

        with open(trace_dir / "trace.jsonl", "w") as f:
            for step in trace.steps:
                f.write(json.dumps(step.to_dict()) + "\n")

        with open(trace_dir / "session.json", "w") as f:
            f.write(json.dumps(trace.session_info.to_dict()))

        # Load and replay
        engine = ReplayEngine(mode=ReplayMode.DECISION)
        engine.load_trace(trace_dir)

        result = engine.replay_decision()

        assert result.mode == ReplayMode.DECISION

    def test_simulation_replay(self, tmp_path):
        """Test simulation replay mode."""
        # Create trace
        trace_dir = tmp_path / "test_trace"
        trace_dir.mkdir()

        trace = TraversalTrace(session_info=SessionInfo())
        for i in range(3):
            trace.add_step(
                TraceStep(
                    step_id=i + 1,
                    timestamp=datetime.now(),
                    global_state="traversing",
                    traversal_state="execute",
                    decision=TraceDecision(
                        node_id=f"node_{i}",
                        node_type="leaf",
                        operation_action="click",
                    ),
                )
            )

        with open(trace_dir / "trace.jsonl", "w") as f:
            for step in trace.steps:
                f.write(json.dumps(step.to_dict()) + "\n")

        with open(trace_dir / "session.json", "w") as f:
            f.write(json.dumps(trace.session_info.to_dict()))

        # Load and analyze
        engine = ReplayEngine(mode=ReplayMode.SIMULATION)
        engine.load_trace(trace_dir)

        result = engine.replay_simulation()

        assert result.mode == ReplayMode.SIMULATION
        assert result.steps_replayed == 3
        assert "unique_nodes_visited" in result.details

    def test_rebuild_runtime_graph(self, tmp_path):
        """Test rebuilding runtime graph."""
        # Create trace
        trace_dir = tmp_path / "test_trace"
        trace_dir.mkdir()

        trace = TraversalTrace(session_info=SessionInfo())
        trace.add_step(
            TraceStep(
                step_id=1,
                timestamp=datetime.now(),
                global_state="traversing",
                traversal_state="execute",
                path_after=["Home", "Settings"],
                decision=TraceDecision(
                    node_id="settings",
                    node_type="container",
                    operation_action="click",
                ),
            )
        )

        with open(trace_dir / "trace.jsonl", "w") as f:
            for step in trace.steps:
                f.write(json.dumps(step.to_dict()) + "\n")

        with open(trace_dir / "session.json", "w") as f:
            f.write(json.dumps(trace.session_info.to_dict()))

        # Load and rebuild
        engine = ReplayEngine()
        engine.load_trace(trace_dir)

        graph = engine.rebuild_runtime_graph()

        assert "nodes" in graph
        assert "edges" in graph
        assert "settings" in graph["nodes"]

    def test_analyze_dynamic_matching(self, tmp_path):
        """Test dynamic matching analysis."""
        # Create trace with dynamic nodes
        trace_dir = tmp_path / "test_trace"
        trace_dir.mkdir()

        trace = TraversalTrace(session_info=SessionInfo())
        trace.add_step(
            TraceStep(
                step_id=1,
                timestamp=datetime.now(),
                global_state="traversing",
                traversal_state="execute",
                decision=TraceDecision(
                    node_id="menu-container-settings-0",  # Dynamic-looking ID
                    node_type="container",
                    operation_action="click",
                ),
            )
        )

        with open(trace_dir / "trace.jsonl", "w") as f:
            for step in trace.steps:
                f.write(json.dumps(step.to_dict()) + "\n")

        with open(trace_dir / "session.json", "w") as f:
            f.write(json.dumps(trace.session_info.to_dict()))

        # Load and analyze
        engine = ReplayEngine()
        engine.load_trace(trace_dir)

        analysis = engine.analyze_dynamic_matching_effects()

        assert "dynamic_nodes" in analysis
        assert "static_nodes" in analysis
