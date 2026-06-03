"""Tests for trace models.

This module tests the models from src/trace/models.py including:
- ExecutionStatus enum
- TraceDecision
- TraceExecution
- TraceStep
- StateSnapshot
- SessionInfo
- TraceSummary
- TraversalTrace
"""

import pytest
from datetime import datetime
from src.trace.models import (
    ExecutionStatus,
    TraceDecision,
    TraceExecution,
    TraceStep,
    StateSnapshot,
    SessionInfo,
    TraceSummary,
    TraversalTrace,
)


class TestExecutionStatus:
    """Tests for ExecutionStatus enum."""

    def test_execution_status_values(self):
        """Test ExecutionStatus has correct values."""
        assert ExecutionStatus.SUCCESS.value == "success"
        assert ExecutionStatus.FAILED.value == "failed"
        assert ExecutionStatus.SKIPPED.value == "skipped"
        assert ExecutionStatus.TIMEOUT.value == "timeout"

    def test_execution_status_values_method(self):
        """Test ExecutionStatus.values() method."""
        values = ExecutionStatus.values()
        assert len(values) == 4
        assert "success" in values

    def test_execution_status_from_value(self):
        """Test ExecutionStatus.from_value() method."""
        status = ExecutionStatus.from_value("success")
        assert status == ExecutionStatus.SUCCESS

    def test_execution_status_is_valid(self):
        """Test ExecutionStatus.is_valid() method."""
        assert ExecutionStatus.is_valid("failed") is True
        assert ExecutionStatus.is_valid("invalid") is False


class TestTraceDecision:
    """Tests for TraceDecision model."""

    def test_creation(self):
        """Test creating TraceDecision."""
        decision = TraceDecision(
            node_id="node123",
            node_type="container",
            operation_action="navigate",
        )
        assert decision.node_id == "node123"
        assert decision.node_type == "container"

    def test_with_optional_fields(self):
        """Test TraceDecision with optional fields."""
        decision = TraceDecision(
            node_id="node456",
            node_type="leaf_switch",
            operation_action="click",
            target_description="Click WiFi toggle",
            reasoning="Toggle WiFi setting",
            confidence=0.95,
        )
        assert decision.target_description == "Click WiFi toggle"
        assert decision.confidence == 0.95


class TestTraceExecution:
    """Tests for TraceExecution model."""

    def test_success_creation(self):
        """Test creating successful execution."""
        execution = TraceExecution(
            status=ExecutionStatus.SUCCESS,
            duration_ms=250.5,
        )
        assert execution.status == ExecutionStatus.SUCCESS
        assert execution.duration_ms == 250.5

    def test_failed_execution(self):
        """Test creating failed execution."""
        execution = TraceExecution(
            status=ExecutionStatus.FAILED,
            duration_ms=5000.0,
            error_message="Element not found",
            error_type="ElementNotFound",
        )
        assert execution.status == ExecutionStatus.FAILED
        assert execution.error_message == "Element not found"
        assert execution.error_type == "ElementNotFound"

    def test_with_screenshot(self):
        """Test execution with screenshot reference."""
        execution = TraceExecution(
            status=ExecutionStatus.SUCCESS,
            duration_ms=150.0,
            screenshot_ref="screenshot_001.png",
        )
        assert execution.screenshot_ref == "screenshot_001.png"


class TestTraceStep:
    """Tests for TraceStep model."""

    def test_creation(self):
        """Test creating TraceStep."""
        step = TraceStep(
            step_id=1,
            timestamp=datetime.now(),
            global_state="ready",
            traversal_state="navigating",
        )
        assert step.step_id == 1
        assert step.global_state == "ready"

    def test_with_decision(self):
        """Test TraceStep with decision."""
        decision = TraceDecision(
            node_id="settings",
            node_type="container",
            operation_action="click",
        )
        step = TraceStep(
            step_id=2,
            timestamp=datetime.now(),
            global_state="traversing",
            traversal_state="executing",
            decision=decision,
        )
        assert step.decision.node_id == "settings"

    def test_with_execution(self):
        """Test TraceStep with execution."""
        execution = TraceExecution(
            status=ExecutionStatus.SUCCESS,
            duration_ms=200.0,
        )
        step = TraceStep(
            step_id=3,
            timestamp=datetime.now(),
            global_state="traversing",
            traversal_state="executing",
            execution=execution,
        )
        assert step.execution.status == ExecutionStatus.SUCCESS

    def test_serialization_roundtrip(self):
        """Test TraceStep serialization and deserialization."""
        decision = TraceDecision(
            node_id="test",
            node_type="leaf_switch",
            operation_action="click",
        )
        execution = TraceExecution(
            status=ExecutionStatus.SUCCESS,
            duration_ms=100.0,
        )

        original_step = TraceStep(
            step_id=1,
            timestamp=datetime.now(),
            global_state="ready",
            traversal_state="navigating",
            decision=decision,
            execution=execution,
            stack_snapshot=["root", "settings"],
            path_before=["Home"],
            path_after=["Home", "Settings"],
        )

        # Serialize
        serialized = original_step.to_dict()

        # Deserialize
        restored_step = TraceStep.from_dict(serialized)

        # Verify
        assert restored_step.step_id == original_step.step_id
        assert restored_step.global_state == original_step.global_state
        assert restored_step.decision.node_id == original_step.decision.node_id
        assert restored_step.execution.status == original_step.execution.status


class TestStateSnapshot:
    """Tests for StateSnapshot model."""

    def test_creation(self):
        """Test creating StateSnapshot."""
        snapshot = StateSnapshot(
            snapshot_id="snap_001",
            timestamp=datetime.now(),
            step_id=1,
            full_state={"global": "ready"},
            node_stack=[],
            visited_nodes={},
            current_path=[],
        )
        assert snapshot.snapshot_id == "snap_001"
        assert snapshot.step_id == 1

    def test_serialization_roundtrip(self):
        """Test StateSnapshot serialization and deserialization."""
        original_snapshot = StateSnapshot(
            snapshot_id="snap_002",
            timestamp=datetime.now(),
            step_id=5,
            full_state={"path": ["Home", "Settings"]},
            node_stack=[
                {"node_id": "root", "name": "Root"},
                {"node_id": "settings", "name": "Settings"},
            ],
            visited_nodes={"root": "visited", "settings": "current"},
            current_path=["Home", "Settings"],
        )

        # Serialize
        serialized = original_snapshot.to_dict()

        # Deserialize
        restored_snapshot = StateSnapshot.from_dict(serialized)

        # Verify
        assert restored_snapshot.snapshot_id == original_snapshot.snapshot_id
        assert restored_snapshot.current_path == original_snapshot.current_path


class TestSessionInfo:
    """Tests for SessionInfo model."""

    def test_creation(self):
        """Test creating SessionInfo."""
        info = SessionInfo(
            device_id="device_123",
            device_name="Tesla Model 3",
            app_version="2024.1.1",
        )
        assert info.device_id == "device_123"
        assert info.device_name == "Tesla Model 3"

    def test_with_defaults(self):
        """Test SessionInfo with default values."""
        info = SessionInfo()
        assert info.start_time is not None
        assert info.traversal_mode == "graph"

    def test_serialization_roundtrip(self):
        """Test SessionInfo serialization and deserialization."""
        original_info = SessionInfo(
            device_id="device_456",
            device_name="Test Device",
            app_version="1.0.0",
            app_package="com.test.app",
            traversal_mode="linear",
        )

        # Serialize
        serialized = original_info.to_dict()

        # Deserialize
        restored_info = SessionInfo.from_dict(serialized)

        # Verify
        assert restored_info.device_id == original_info.device_id
        assert restored_info.device_name == original_info.device_name


class TestTraceSummary:
    """Tests for TraceSummary model."""

    def test_creation(self):
        """Test creating TraceSummary."""
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

    def test_with_errors_by_type(self):
        """Test TraceSummary with error breakdown."""
        summary = TraceSummary(
            total_steps=15,
            successful_operations=12,
            failed_operations=3,
            skipped_operations=0,
            total_duration_ms=10000.0,
            visited_pages_count=5,
            visited_nodes_count=10,
            screenshots_count=15,
            errors_count=3,
            errors_by_type={"ElementNotFound": 2, "Timeout": 1},
        )
        assert summary.errors_by_type["ElementNotFound"] == 2

    def test_serialization_roundtrip(self):
        """Test TraceSummary serialization and deserialization."""
        original_summary = TraceSummary(
            total_steps=20,
            successful_operations=15,
            failed_operations=3,
            skipped_operations=2,
            total_duration_ms=15000.0,
            visited_pages_count=8,
            visited_nodes_count=15,
            screenshots_count=20,
            errors_count=3,
            errors_by_type={"Error1": 2, "Error2": 1},
            max_stack_depth=5,
            unique_nodes_visited=15,
        )

        # Serialize
        serialized = original_summary.to_dict()

        # Deserialize
        restored_summary = TraceSummary.from_dict(serialized)

        # Verify
        assert restored_summary.total_steps == original_summary.total_steps
        assert restored_summary.unique_nodes_visited == original_summary.unique_nodes_visited


class TestTraversalTrace:
    """Tests for TraversalTrace model."""

    def test_creation(self):
        """Test creating TraversalTrace."""
        session = SessionInfo(device_id="device_789")
        trace = TraversalTrace(
            session_info=session,
            trace_id="20240101_120000",
        )
        assert trace.session_info.device_id == "device_789"
        assert trace.trace_id == "20240101_120000"

    def test_add_step(self):
        """Test adding a step to the trace."""
        trace = TraversalTrace(session_info=SessionInfo())

        step = TraceStep(
            step_id=1,
            timestamp=datetime.now(),
            global_state="ready",
            traversal_state="navigating",
        )

        trace.add_step(step)
        assert len(trace.steps) == 1
        assert trace.steps[0].step_id == 1

    def test_add_snapshot(self):
        """Test adding a snapshot to the trace."""
        trace = TraversalTrace(session_info=SessionInfo())

        snapshot = StateSnapshot(
            snapshot_id="snap_001",
            timestamp=datetime.now(),
            step_id=1,
            full_state={},
            node_stack=[],
            visited_nodes={},
            current_path=[],
        )

        trace.add_snapshot(snapshot)
        assert len(trace.state_snapshots) == 1

    def test_finalize(self):
        """Test finalizing the trace."""
        session = SessionInfo()
        trace = TraversalTrace(session_info=session)

        # Add some steps
        for i in range(3):
            trace.add_step(
                TraceStep(
                    step_id=i + 1,
                    timestamp=datetime.now(),
                    global_state="traversing",
                    traversal_state="executing",
                    execution=TraceExecution(
                        status=ExecutionStatus.SUCCESS,
                        duration_ms=100.0,
                    ),
                )
            )

        # Finalize
        trace.finalize()

        # Verify summary is generated
        assert trace.summary is not None
        assert trace.summary.total_steps == 3
        assert trace.summary.successful_operations == 3
        assert session.end_time is not None

    def test_serialization(self):
        """Test TraversalTrace serialization."""
        session = SessionInfo(device_id="test_device")
        trace = TraversalTrace(session_info=session, trace_id="test_trace")

        # Add a step
        step = TraceStep(
            step_id=1,
            timestamp=datetime.now(),
            global_state="ready",
            traversal_state="navigating",
        )
        trace.add_step(step)

        # Finalize to generate summary
        trace.finalize()

        # Serialize
        serialized = trace.to_dict()

        # Verify structure
        assert serialized["trace_id"] == "test_trace"
        assert serialized["session_info"]["device_id"] == "test_device"
        assert len(serialized["steps"]) == 1
        assert serialized["summary"] is not None
