"""
Trace recorder for recording traversal execution.

This module implements the trace recorder that captures traversal steps,
state transitions, and screenshots during execution.
"""

import json
import hashlib
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Any, Callable, Dict, List, Optional, BinaryIO
import shutil

from .models import (
    TraversalTrace,
    TraceStep,
    StateSnapshot,
    SessionInfo,
    TraceDecision,
    TraceExecution,
    ExecutionStatus,
)

from src.state_machine.global_fsm import GlobalState
from src.state_machine.traversal_fsm import TraversalState
from src.state_machine.node_stack import NodeStack


@dataclass
class TraceConfig:
    """Configuration for trace recording."""

    enabled: bool = True
    output_path: Path = field(default_factory=lambda: Path("./traces"))
    keep_count: int = 10  # Number of traces to keep
    snapshot_interval: int = 10  # Steps between snapshots
    save_screenshots: bool = True
    screenshot_format: str = "png"
    compress_old_traces: bool = False


class TraceRecorder:
    """
    Records traversal execution to trace files.

    Captures steps, state transitions, and screenshots during traversal,
    and saves them in JSON Lines format with organized file structure.
    """

    def __init__(self, config: Optional[TraceConfig] = None):
        """
        Initialize the trace recorder.

        Args:
            config: Trace configuration (uses defaults if not provided)
        """
        self.config = config or TraceConfig()
        self.current_trace: Optional[TraversalTrace] = None
        self._step_counter: int = 0
        self._screenshot_counter: int = 0
        self._current_session_dir: Optional[Path] = None

    def start_session(
        self,
        device_id: Optional[str] = None,
        device_name: Optional[str] = None,
        app_version: Optional[str] = None,
        app_package: Optional[str] = None,
        traversal_mode: str = "graph",
        config: Dict[str, Any] = None,
    ) -> None:
        """
        Start a new trace session.

        Args:
            device_id: Device identifier
            device_name: Device name
            app_version: Application version
            app_package: Application package name
            traversal_mode: Traversal mode (graph/linear)
            config: Additional configuration
        """
        if not self.config.enabled:
            return

        self._step_counter = 0
        self._screenshot_counter = 0

        # Create session directory
        self._create_session_directory()

        # Create session info
        session_info = SessionInfo(
            device_id=device_id,
            device_name=device_name,
            app_version=app_version,
            app_package=app_package,
            traversal_mode=traversal_mode,
            config=config or {},
        )

        # Create new trace
        self.current_trace = TraversalTrace(session_info=session_info)

    def _create_session_directory(self) -> None:
        """Create directory for current session."""
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        session_dir = self.config.output_path / f"trace_{timestamp}"
        session_dir.mkdir(parents=True, exist_ok=True)

        # Create screenshots subdirectory
        screenshots_dir = session_dir / "screenshots"
        screenshots_dir.mkdir(exist_ok=True)

        self._current_session_dir = session_dir

    def record_state_transition(
        self,
        global_state: GlobalState,
        traversal_state: TraversalState,
        node_stack: NodeStack,
        current_path: List[str],
        page_analysis: Optional[Dict[str, Any]] = None,
    ) -> TraceStep:
        """
        Record a state transition.

        Args:
            global_state: Current global state
            traversal_state: Current traversal state
            node_stack: Current node stack
            current_path: Current traversal path
            page_analysis: Optional page analysis data

        Returns:
            Created TraceStep
        """
        if not self.config.enabled or not self.current_trace:
            return None

        self._step_counter += 1

        step = TraceStep(
            step_id=self._step_counter,
            timestamp=datetime.now(),
            global_state=global_state.value,
            traversal_state=traversal_state.value,
            page_analysis_summary=self._summarize_page_analysis(page_analysis),
            stack_snapshot=node_stack.get_node_path(),
            path_before=current_path.copy(),
            path_after=current_path.copy(),
        )

        self.current_trace.add_step(step)
        self._write_step_to_file(step)

        return step

    def record_decision(
        self,
        step: TraceStep,
        node_id: str,
        node_type: str,
        operation_action: str,
        target_description: Optional[str] = None,
        reasoning: Optional[str] = None,
        confidence: float = 1.0,
    ) -> None:
        """
        Record a decision made during traversal.

        Args:
            step: Existing trace step to update
            node_id: ID of node being processed
            node_type: Type of node
            operation_action: Action to execute
            target_description: Description of target
            reasoning: Reasoning for decision
            confidence: Confidence in decision
        """
        if not self.config.enabled or not step:
            return

        step.decision = TraceDecision(
            node_id=node_id,
            node_type=node_type,
            operation_action=operation_action,
            target_description=target_description,
            reasoning=reasoning,
            confidence=confidence,
        )

        self._update_step_in_file(step)

    def record_execution_start(self, step: TraceStep) -> None:
        """
        Record start of execution for a step.

        Args:
            step: Trace step to update
        """
        if not self.config.enabled or not step:
            return

        # Mark execution start time in metadata
        step.metadata["execution_start_time"] = datetime.now().isoformat()

    def record_execution_result(
        self,
        step: TraceStep,
        status: ExecutionStatus,
        duration_ms: float,
        screenshot_data: Optional[bytes] = None,
        error_message: Optional[str] = None,
        error_type: Optional[str] = None,
        stack_trace: Optional[str] = None,
    ) -> None:
        """
        Record execution result for a step.

        Args:
            step: Trace step to update
            status: Execution status
            duration_ms: Execution duration in milliseconds
            screenshot_data: Optional screenshot binary data
            error_message: Error message if failed
            error_type: Type of error if failed
            stack_trace: Stack trace if failed
        """
        if not self.config.enabled or not step:
            return

        # Save screenshot if provided
        screenshot_ref = None
        if screenshot_data and self.config.save_screenshots:
            screenshot_ref = self._save_screenshot(screenshot_data)

        step.execution = TraceExecution(
            status=status,
            duration_ms=duration_ms,
            screenshot_ref=screenshot_ref,
            error_message=error_message,
            error_type=error_type,
            stack_trace=stack_trace,
        )

        step.screenshot_ref = screenshot_ref
        self._update_step_in_file(step)

        # Check if we should create a snapshot
        if self._step_counter % self.config.snapshot_interval == 0:
            self._create_snapshot(step)

    def record_error(self, step: TraceStep, error: Exception, context: Dict[str, Any] = None) -> None:
        """
        Record an error that occurred.

        Args:
            step: Trace step to update
            error: Exception that occurred
            context: Optional error context
        """
        if not self.config.enabled or not step:
            return

        step.error = {
            "type": type(error).__name__,
            "message": str(error),
            "timestamp": datetime.now().isoformat(),
            "context": context or {},
        }

        self._update_step_in_file(step)

    def _save_screenshot(self, data: bytes) -> str:
        """
        Save screenshot data to file.

        Args:
            data: Screenshot binary data

        Returns:
            Reference string for the screenshot
        """
        if not self._current_session_dir:
            return None

        self._screenshot_counter += 1
        screenshot_path = (
            self._current_session_dir
            / "screenshots"
            / f"step_{self._step_counter}_screenshot_{self._screenshot_counter}.{self.config.screenshot_format}"
        )

        with open(screenshot_path, "wb") as f:
            f.write(data)

        # Generate hash for reference
        file_hash = hashlib.sha256(data).hexdigest()[:16]
        return f"{screenshot_path.name}:{file_hash}"

    def _create_snapshot(self, step: TraceStep) -> None:
        """
        Create a state snapshot at this point.

        Args:
            step: Current trace step
        """
        if not self.current_trace or not self._current_session_dir:
            return

        snapshot = StateSnapshot(
            snapshot_id=f"snapshot_{self._step_counter}",
            timestamp=datetime.now(),
            step_id=step.step_id,
            full_state={
                "global_state": step.global_state,
                "traversal_state": step.traversal_state,
                "current_path": step.path_after,
            },
            node_stack=[{"node_id": nid} for nid in step.stack_snapshot],
            visited_nodes={},  # Would be populated from context
            current_path=step.path_after,
        )

        self.current_trace.add_snapshot(snapshot)
        self._write_snapshot_to_file(snapshot)

    def _summarize_page_analysis(self, analysis: Optional[Dict[str, Any]]) -> Optional[str]:
        """
        Create a summary of page analysis.

        Args:
            analysis: Page analysis data

        Returns:
            Summary string
        """
        if not analysis:
            return None

        page_name = analysis.get("page_name", "Unknown")
        item_count = len(analysis.get("items", []))
        return f"{page_name} ({item_count} items)"

    def _write_step_to_file(self, step: TraceStep) -> None:
        """Write step to trace.jsonl file."""
        if not self._current_session_dir:
            return

        trace_file = self._current_session_dir / "trace.jsonl"
        with open(trace_file, "a") as f:
            f.write(json.dumps(step.to_dict()) + "\n")

    def _update_step_in_file(self, step: TraceStep) -> None:
        """
        Update a step in the trace file.

        Since we're using JSON Lines (append-only), this rewrites
        the entire file. For production, consider using a database
        for better performance.
        """
        if not self._current_session_dir:
            return

        trace_file = self._current_session_dir / "trace.jsonl"

        # Read all steps
        steps = []
        if trace_file.exists():
            with open(trace_file, "r") as f:
                for line in f:
                    if line.strip():
                        steps.append(TraceStep.from_dict(json.loads(line)))

        # Update the matching step
        for i, s in enumerate(steps):
            if s.step_id == step.step_id:
                steps[i] = step
                break

        # Rewrite file
        with open(trace_file, "w") as f:
            for s in steps:
                f.write(json.dumps(s.to_dict()) + "\n")

    def _write_snapshot_to_file(self, snapshot: StateSnapshot) -> None:
        """Write snapshot to snapshots.jsonl file."""
        if not self._current_session_dir:
            return

        snapshot_file = self._current_session_dir / "snapshots.jsonl"
        with open(snapshot_file, "a") as f:
            f.write(json.dumps(snapshot.to_dict()) + "\n")

    def end_session(self) -> Optional[TraversalTrace]:
        """
        End the current trace session.

        Finalizes the trace and saves summary.

        Returns:
            The completed trace or None if tracing was disabled
        """
        if not self.config.enabled or not self.current_trace:
            return None

        # Finalize trace (generate summary)
        self.current_trace.finalize()

        # Save summary
        self._save_summary()

        trace = self.current_trace
        self.current_trace = None

        # Clean up old traces
        self._cleanup_old_traces()

        return trace

    def _save_summary(self) -> None:
        """Save trace summary to summary.json."""
        if not self._current_session_dir or not self.current_trace:
            return

        summary_file = self._current_session_dir / "summary.json"
        with open(summary_file, "w") as f:
            json.dump(self.current_trace.summary.to_dict(), f, indent=2)

        # Also save session info
        session_file = self._current_session_dir / "session.json"
        with open(session_file, "w") as f:
            json.dump(self.current_trace.session_info.to_dict(), f, indent=2)

    def _cleanup_old_traces(self) -> None:
        """Remove old traces, keeping only the most recent ones."""
        if not self.config.output_path.exists():
            return

        # Get all trace directories
        trace_dirs = sorted(
            [d for d in self.config.output_path.glob("trace_*") if d.is_dir()],
            key=lambda p: p.stat().st_mtime,
            reverse=True,
        )

        # Remove excess traces
        for old_dir in trace_dirs[self.config.keep_count :]:
            try:
                shutil.rmtree(old_dir)
            except Exception as e:
                print(f"Failed to remove old trace directory {old_dir}: {e}")

    def get_current_trace(self) -> Optional[TraversalTrace]:
        """Get the current trace being recorded."""
        return self.current_trace

    def is_recording(self) -> bool:
        """Check if currently recording."""
        return self.current_trace is not None
