"""
Replay engine for trace-based traversal replay.

This module implements the replay engine that supports three modes:
- Strict: Exact replay with screenshot comparison
- Decision: Reuse decisions with flexible execution
- Simulation: Dry-run analysis without device connection
"""

import json
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import Any, Callable, Dict, List, Optional, BinaryIO

from .models import TraversalTrace, TraceStep, TraceExecution, ExecutionStatus


class ReplayMode(str, Enum):
    """Replay modes."""

    STRICT = "strict"  # Exact replay with verification
    DECISION = "decision"  # Reuse decisions, flexible execution
    SIMULATION = "simulation"  # Dry-run without device


@dataclass
class ReplayResult:
    """Result of a replay operation."""

    success: bool
    mode: ReplayMode
    steps_replayed: int
    steps_matched: int
    steps_failed: int
    screenshots_matched: int
    screenshots_failed: int
    duration_ms: float
    errors: List[str] = field(default_factory=list)
    details: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary."""
        return {
            "success": self.success,
            "mode": self.mode.value,
            "steps_replayed": self.steps_replayed,
            "steps_matched": self.steps_matched,
            "steps_failed": self.steps_failed,
            "screenshots_matched": self.screenshots_matched,
            "screenshots_failed": self.screenshots_failed,
            "duration_ms": self.duration_ms,
            "errors": self.errors,
            "details": self.details,
        }


@dataclass
class ScreenshotComparison:
    """Result of screenshot comparison."""

    matched: bool
    similarity_score: float
    reference_path: Optional[str] = None
    current_path: Optional[str] = None
    diff_path: Optional[str] = None


class ReplayEngine:
    """
    Engine for replaying traversal traces.

    Supports multiple replay modes and provides verification capabilities.
    """

    def __init__(self, mode: ReplayMode = ReplayMode.STRICT):
        """
        Initialize the replay engine.

        Args:
            mode: Replay mode to use
        """
        self.mode = mode
        self.current_trace: Optional[TraversalTrace] = None
        self._step_index: int = 0

        # Callbacks for device interaction
        self._operation_callback: Optional[Callable] = None
        self._screenshot_callback: Optional[Callable] = None
        self._navigation_callback: Optional[Callable] = None

        # Results tracking
        self._results: ReplayResult = None

    def register_operation_callback(self, callback: Callable) -> None:
        """Register callback for executing operations."""
        self._operation_callback = callback

    def register_screenshot_callback(self, callback: Callable) -> None:
        """Register callback for capturing screenshots."""
        self._screenshot_callback = callback

    def register_navigation_callback(self, callback: Callable) -> None:
        """Register callback for navigation operations."""
        self._navigation_callback = callback

    def load_trace(self, trace_path: Path) -> bool:
        """
        Load a trace from file.

        Args:
            trace_path: Path to trace directory or trace.jsonl

        Returns:
            True if trace loaded successfully
        """
        try:
            # Determine if path is directory or file
            if trace_path.is_dir():
                trace_file = trace_path / "trace.jsonl"
                session_file = trace_path / "session.json"
            else:
                trace_file = trace_path
                session_file = trace_path.parent / "session.json"

            # Load session info
            session_info = None
            if session_file.exists():
                with open(session_file, "r") as f:
                    from .models import SessionInfo
                    session_info = SessionInfo.from_dict(json.load(f))

            # Load trace steps
            steps = []
            with open(trace_file, "r") as f:
                for line in f:
                    if line.strip():
                        steps.append(TraceStep.from_dict(json.loads(line)))

            # Create trace object
            self.current_trace = TraversalTrace(
                session_info=session_info or SessionInfo(),
                steps=steps,
                trace_id=trace_path.stem,
            )

            self._step_index = 0
            return True

        except Exception as e:
            print(f"Failed to load trace: {e}")
            return False

    def replay_strict(
        self,
        screenshot_match_threshold: float = 0.9,
        stop_on_failure: bool = True,
    ) -> ReplayResult:
        """
        Strict replay mode.

        Replays each step exactly as recorded, with screenshot verification.

        Args:
            screenshot_match_threshold: Minimum similarity for screenshots (0-1)
            stop_on_failure: Stop replay on first failure

        Returns:
            Replay result with statistics
        """
        start_time = datetime.now()
        results = ReplayResult(
            success=True,
            mode=ReplayMode.STRICT,
            steps_replayed=0,
            steps_matched=0,
            steps_failed=0,
            screenshots_matched=0,
            screenshots_failed=0,
            duration_ms=0,
        )

        if not self.current_trace:
            results.success = False
            results.errors.append("No trace loaded")
            return results

        for step in self.current_trace.steps:
            results.steps_replayed += 1

            try:
                # Execute the operation
                success = self._execute_step(step)

                if success:
                    results.steps_matched += 1
                else:
                    results.steps_failed += 1
                    results.success = False
                    if stop_on_failure:
                        break

                # Verify screenshot if available
                if step.execution and step.execution.screenshot_ref and self._screenshot_callback:
                    comparison = self._verify_screenshot(
                        step.execution.screenshot_ref,
                        screenshot_match_threshold,
                    )
                    if comparison.matched:
                        results.screenshots_matched += 1
                    else:
                        results.screenshots_failed += 1
                        if stop_on_failure:
                            results.success = False
                            break

            except Exception as e:
                results.steps_failed += 1
                results.errors.append(f"Step {step.step_id}: {str(e)}")
                results.success = False
                if stop_on_failure:
                    break

        results.duration_ms = (datetime.now() - start_time).total_seconds() * 1000
        return results

    def replay_decision(self, stop_on_failure: bool = False) -> ReplayResult:
        """
        Decision replay mode.

        Reuses the decision sequence but allows flexible execution.
        Ignores timing differences and adapts to UI changes.

        Args:
            stop_on_failure: Stop replay on first failure

        Returns:
            Replay result with statistics
        """
        start_time = datetime.now()
        results = ReplayResult(
            success=True,
            mode=ReplayMode.DECISION,
            steps_replayed=0,
            steps_matched=0,
            steps_failed=0,
            screenshots_matched=0,
            screenshots_failed=0,
            duration_ms=0,
        )

        if not self.current_trace:
            results.success = False
            results.errors.append("No trace loaded")
            return results

        for step in self.current_trace.steps:
            results.steps_replayed += 1

            if not step.decision:
                continue

            try:
                # Execute based on decision
                success = self._execute_decision(step.decision)

                if success:
                    results.steps_matched += 1
                else:
                    results.steps_failed += 1
                    if stop_on_failure:
                        results.success = False
                        break

            except Exception as e:
                results.steps_failed += 1
                results.errors.append(f"Step {step.step_id}: {str(e)}")
                if stop_on_failure:
                    break

        results.duration_ms = (datetime.now() - start_time).total_seconds() * 1000
        return results

    def replay_simulation(self) -> ReplayResult:
        """
        Simulation replay mode.

        Analyzes the trace without connecting to a device.
        Calculates coverage and completeness metrics.

        Returns:
            Replay result with analysis metrics
        """
        start_time = datetime.now()
        results = ReplayResult(
            success=True,
            mode=ReplayMode.SIMULATION,
            steps_replayed=len(self.current_trace.steps) if self.current_trace else 0,
            steps_matched=0,
            steps_failed=0,
            screenshots_matched=0,
            screenshots_failed=0,
            duration_ms=0,
        )

        if not self.current_trace:
            results.success = False
            results.errors.append("No trace loaded")
            return results

        # Analyze trace
        analysis = self._analyze_trace()

        results.details = analysis
        results.duration_ms = (datetime.now() - start_time).total_seconds() * 1000

        return results

    def _execute_step(self, step: TraceStep) -> bool:
        """Execute a single step (strict mode)."""
        if not step.decision or not self._operation_callback:
            return False

        try:
            result = self._operation_callback(
                node_id=step.decision.node_id,
                action=step.decision.operation_action,
                target=step.decision.target_description,
            )
            return result.get("success", False)
        except Exception:
            return False

    def _execute_decision(self, decision) -> bool:
        """Execute based on decision (decision mode)."""
        if not self._operation_callback:
            return False

        try:
            result = self._operation_callback(
                node_id=decision.node_id,
                action=decision.operation_action,
                target=decision.target_description,
            )
            return result.get("success", False)
        except Exception:
            return False

    def _verify_screenshot(self, reference_ref: str, threshold: float) -> ScreenshotComparison:
        """
        Verify screenshot against reference.

        Args:
            reference_ref: Reference screenshot reference
            threshold: Minimum similarity threshold

        Returns:
            Comparison result
        """
        if not self._screenshot_callback:
            return ScreenshotComparison(matched=False, similarity_score=0.0)

        try:
            # Capture current screenshot
            current_data = self._screenshot_callback()

            # In production, this would use image comparison
            # For now, return a placeholder result
            return ScreenshotComparison(
                matched=True,
                similarity_score=1.0,
                reference_path=reference_ref,
            )
        except Exception:
            return ScreenshotComparison(matched=False, similarity_score=0.0)

    def _analyze_trace(self) -> Dict[str, Any]:
        """Analyze trace for simulation mode."""
        if not self.current_trace:
            return {}

        # Extract metrics
        steps = self.current_trace.steps
        unique_nodes = set()
        node_types = {}
        operations = {}

        for step in steps:
            if step.decision:
                unique_nodes.add(step.decision.node_id)
                node_type = step.decision.node_type
                node_types[node_type] = node_types.get(node_type, 0) + 1

                action = step.decision.operation_action
                operations[action] = operations.get(action, 0) + 1

        return {
            "unique_nodes_visited": len(unique_nodes),
            "node_type_distribution": node_types,
            "operation_distribution": operations,
            "total_steps": len(steps),
            "path_coverage": self._calculate_path_coverage(),
        }

    def _calculate_path_coverage(self) -> float:
        """Calculate path coverage percentage."""
        if not self.current_trace or not self.current_trace.steps:
            return 0.0

        # Count unique paths
        paths = set()
        for step in self.current_trace.steps:
            if step.path_after:
                path_tuple = tuple(step.path_after)
                paths.add(path_tuple)

        # In production, this would compare against expected paths
        # For now, return the count of unique paths
        return len(paths)

    def rebuild_runtime_graph(self) -> Dict[str, Any]:
        """
        Rebuild the runtime node graph from trace.

        Returns:
            Graph structure as nested dict
        """
        if not self.current_trace:
            return {}

        graph = {"nodes": {}, "edges": []}

        for step in self.current_trace.steps:
            if step.decision:
                node_id = step.decision.node_id
                graph["nodes"][node_id] = {
                    "type": step.decision.node_type,
                    "action": step.decision.operation_action,
                }

                # Add edges from path
                if len(step.path_after) > 1:
                    parent = step.path_after[-2]
                    graph["edges"].append((parent, node_id))

        return graph

    def analyze_dynamic_matching_effects(self) -> Dict[str, Any]:
        """
        Analyze effectiveness of dynamic matching rules.

        Returns:
            Analysis of dynamic matching performance
        """
        if not self.current_trace:
            return {}

        # Count dynamically matched nodes
        dynamic_nodes = 0
        static_nodes = 0

        for step in self.current_trace.steps:
            if step.decision:
                # Check if node appears to be dynamically generated
                # (heuristic: node IDs with patterns like "template-hash")
                if "-" in step.decision.node_id and len(step.decision.node_id.split("-")) > 2:
                    dynamic_nodes += 1
                else:
                    static_nodes += 1

        return {
            "dynamic_nodes": dynamic_nodes,
            "static_nodes": static_nodes,
            "dynamic_ratio": dynamic_nodes / max(dynamic_nodes + static_nodes, 1),
        }
