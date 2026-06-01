"""
Data models for the trace system.

This module defines the data structures for recording and storing
traversal traces, steps, snapshots, and summaries.
"""

from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from typing import Any, Dict, List, Optional
import hashlib


class ExecutionStatus(str, Enum):
    """Status of node execution."""

    SUCCESS = "success"
    FAILED = "failed"
    SKIPPED = "skipped"
    TIMEOUT = "timeout"

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings.

        Returns:
            List of enum values
        """
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "ExecutionStatus":
        """Create an enum instance from a string value.

        Args:
            value: String value to convert

        Returns:
            ExecutionStatus enum instance

        Raises:
            ValueError: If value is not a valid enum value
        """
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid enum value.

        Args:
            value: String value to validate

        Returns:
            True if value is valid, False otherwise
        """
        return value in cls.values()


@dataclass
class TraceDecision:
    """Decision made during traversal."""

    node_id: str
    node_type: str
    operation_action: str
    target_description: Optional[str] = None
    reasoning: Optional[str] = None
    confidence: float = 1.0


@dataclass
class TraceExecution:
    """Execution result of a step."""

    status: ExecutionStatus
    duration_ms: float
    screenshot_ref: Optional[str] = None
    error_message: Optional[str] = None
    error_type: Optional[str] = None
    stack_trace: Optional[str] = None


@dataclass
class TraceStep:
    """
    Single step in a traversal trace.

    Records the complete context and outcome of one traversal step.
    """

    step_id: int
    timestamp: datetime
    global_state: str
    traversal_state: str
    page_analysis_summary: Optional[str] = None
    decision: Optional[TraceDecision] = None
    execution: Optional[TraceExecution] = None
    stack_snapshot: List[str] = field(default_factory=list)
    path_before: List[str] = field(default_factory=list)
    path_after: List[str] = field(default_factory=list)
    screenshot_ref: Optional[str] = None
    error: Optional[Dict[str, Any]] = None
    metadata: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        return {
            "step_id": self.step_id,
            "timestamp": self.timestamp.isoformat(),
            "global_state": self.global_state,
            "traversal_state": self.traversal_state,
            "page_analysis_summary": self.page_analysis_summary,
            "decision": {
                "node_id": self.decision.node_id,
                "node_type": self.decision.node_type,
                "operation_action": self.decision.operation_action,
                "target_description": self.decision.target_description,
                "reasoning": self.decision.reasoning,
                "confidence": self.decision.confidence,
            }
            if self.decision
            else None,
            "execution": {
                "status": self.execution.status.value,
                "duration_ms": self.execution.duration_ms,
                "screenshot_ref": self.execution.screenshot_ref,
                "error_message": self.execution.error_message,
                "error_type": self.execution.error_type,
                "stack_trace": self.execution.stack_trace,
            }
            if self.execution
            else None,
            "stack_snapshot": self.stack_snapshot,
            "path_before": self.path_before,
            "path_after": self.path_after,
            "screenshot_ref": self.screenshot_ref,
            "error": self.error,
            "metadata": self.metadata,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "TraceStep":
        """Create TraceStep from dictionary."""
        decision = None
        if data.get("decision"):
            decision = TraceDecision(**data["decision"])

        execution = None
        if data.get("execution"):
            exec_data = data["execution"].copy()
            exec_data["status"] = ExecutionStatus(exec_data["status"])
            execution = TraceExecution(**exec_data)

        return cls(
            step_id=data["step_id"],
            timestamp=datetime.fromisoformat(data["timestamp"]),
            global_state=data["global_state"],
            traversal_state=data["traversal_state"],
            page_analysis_summary=data.get("page_analysis_summary"),
            decision=decision,
            execution=execution,
            stack_snapshot=data.get("stack_snapshot", []),
            path_before=data.get("path_before", []),
            path_after=data.get("path_after", []),
            screenshot_ref=data.get("screenshot_ref"),
            error=data.get("error"),
            metadata=data.get("metadata", {}),
        )


@dataclass
class StateSnapshot:
    """
    Complete state snapshot at a point in time.

    Captures the full traversal state for recovery or analysis.
    """

    snapshot_id: str
    timestamp: datetime
    step_id: int
    full_state: Dict[str, Any]
    node_stack: List[Dict[str, Any]]
    visited_nodes: Dict[str, str]
    current_path: List[str]
    metadata: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        return {
            "snapshot_id": self.snapshot_id,
            "timestamp": self.timestamp.isoformat(),
            "step_id": self.step_id,
            "full_state": self.full_state,
            "node_stack": self.node_stack,
            "visited_nodes": self.visited_nodes,
            "current_path": self.current_path,
            "metadata": self.metadata,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "StateSnapshot":
        """Create StateSnapshot from dictionary."""
        return cls(
            snapshot_id=data["snapshot_id"],
            timestamp=datetime.fromisoformat(data["timestamp"]),
            step_id=data["step_id"],
            full_state=data["full_state"],
            node_stack=data["node_stack"],
            visited_nodes=data["visited_nodes"],
            current_path=data["current_path"],
            metadata=data.get("metadata", {}),
        )


@dataclass
class SessionInfo:
    """Information about the trace session."""

    device_id: Optional[str] = None
    device_name: Optional[str] = None
    app_version: Optional[str] = None
    app_package: Optional[str] = None
    start_time: datetime = field(default_factory=datetime.now)
    end_time: Optional[datetime] = None
    traversal_mode: str = "graph"  # "graph" or "linear"
    config: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary."""
        return {
            "device_id": self.device_id,
            "device_name": self.device_name,
            "app_version": self.app_version,
            "app_package": self.app_package,
            "start_time": self.start_time.isoformat(),
            "end_time": self.end_time.isoformat() if self.end_time else None,
            "traversal_mode": self.traversal_mode,
            "config": self.config,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "SessionInfo":
        """Create SessionInfo from dictionary."""
        return cls(
            device_id=data.get("device_id"),
            device_name=data.get("device_name"),
            app_version=data.get("app_version"),
            app_package=data.get("app_package"),
            start_time=datetime.fromisoformat(data["start_time"]),
            end_time=datetime.fromisoformat(data["end_time"]) if data.get("end_time") else None,
            traversal_mode=data.get("traversal_mode", "graph"),
            config=data.get("config", {}),
        )


@dataclass
class TraceSummary:
    """
    Summary statistics for a traversal trace.

    Provides aggregated information about the traversal.
    """

    total_steps: int
    successful_operations: int
    failed_operations: int
    skipped_operations: int
    total_duration_ms: float
    visited_pages_count: int
    visited_nodes_count: int
    screenshots_count: int
    errors_count: int
    errors_by_type: Dict[str, int] = field(default_factory=dict)
    max_stack_depth: int = 0
    unique_nodes_visited: int = 0

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary."""
        return {
            "total_steps": self.total_steps,
            "successful_operations": self.successful_operations,
            "failed_operations": self.failed_operations,
            "skipped_operations": self.skipped_operations,
            "total_duration_ms": self.total_duration_ms,
            "visited_pages_count": self.visited_pages_count,
            "visited_nodes_count": self.visited_nodes_count,
            "screenshots_count": self.screenshots_count,
            "errors_count": self.errors_count,
            "errors_by_type": self.errors_by_type,
            "max_stack_depth": self.max_stack_depth,
            "unique_nodes_visited": self.unique_nodes_visited,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "TraceSummary":
        """Create TraceSummary from dictionary."""
        return cls(
            total_steps=data["total_steps"],
            successful_operations=data["successful_operations"],
            failed_operations=data["failed_operations"],
            skipped_operations=data["skipped_operations"],
            total_duration_ms=data["total_duration_ms"],
            visited_pages_count=data["visited_pages_count"],
            visited_nodes_count=data["visited_nodes_count"],
            screenshots_count=data["screenshots_count"],
            errors_count=data["errors_count"],
            errors_by_type=data.get("errors_by_type", {}),
            max_stack_depth=data.get("max_stack_depth", 0),
            unique_nodes_visited=data.get("unique_nodes_visited", 0),
        )


@dataclass
class TraversalTrace:
    """
    Complete traversal trace.

    Contains all information about a traversal session including
    session info, steps, snapshots, and summary.
    """

    session_info: SessionInfo
    steps: List[TraceStep] = field(default_factory=list)
    state_snapshots: List[StateSnapshot] = field(default_factory=list)
    summary: Optional[TraceSummary] = None
    trace_id: str = field(default_factory=lambda: datetime.now().strftime("%Y%m%d_%H%M%S"))

    def add_step(self, step: TraceStep) -> None:
        """Add a step to the trace."""
        self.steps.append(step)

    def add_snapshot(self, snapshot: StateSnapshot) -> None:
        """Add a state snapshot to the trace."""
        self.state_snapshots.append(snapshot)

    def finalize(self) -> None:
        """
        Finalize the trace by generating summary.

        Should be called when traversal is complete.
        """
        self.session_info.end_time = datetime.now()
        self.summary = self._generate_summary()

    def _generate_summary(self) -> TraceSummary:
        """Generate summary from trace data."""
        successful = 0
        failed = 0
        skipped = 0
        errors_by_type = {}
        screenshots = 0
        max_depth = 0
        visited_nodes = set()

        total_duration = 0.0
        if self.steps:
            first_time = self.steps[0].timestamp
            last_time = self.steps[-1].timestamp
            total_duration = (last_time - first_time).total_seconds() * 1000

        for step in self.steps:
            if step.execution:
                if step.execution.status == ExecutionStatus.SUCCESS:
                    successful += 1
                elif step.execution.status == ExecutionStatus.FAILED:
                    failed += 1
                elif step.execution.status == ExecutionStatus.SKIPPED:
                    skipped += 1

                if step.execution.error_type:
                    errors_by_type[step.execution.error_type] = (
                        errors_by_type.get(step.execution.error_type, 0) + 1
                    )

            if step.screenshot_ref:
                screenshots += 1

            if step.decision:
                visited_nodes.add(step.decision.node_id)

            current_depth = len(step.stack_snapshot)
            if current_depth > max_depth:
                max_depth = current_depth

        return TraceSummary(
            total_steps=len(self.steps),
            successful_operations=successful,
            failed_operations=failed,
            skipped_operations=skipped,
            total_duration_ms=total_duration,
            visited_pages_count=len(set(s.path_after[-1] for s in self.steps if s.path_after)),
            visited_nodes_count=len(visited_nodes),
            screenshots_count=screenshots,
            errors_count=failed,
            errors_by_type=errors_by_type,
            max_stack_depth=max_depth,
            unique_nodes_visited=len(visited_nodes),
        )

    def to_dict(self) -> Dict[str, Any]:
        """Convert trace to dictionary."""
        return {
            "trace_id": self.trace_id,
            "session_info": self.session_info.to_dict(),
            "steps": [step.to_dict() for step in self.steps],
            "state_snapshots": [snap.to_dict() for snap in self.state_snapshots],
            "summary": self.summary.to_dict() if self.summary else None,
        }
