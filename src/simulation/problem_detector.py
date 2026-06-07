"""Problem detector for simulation testing.

Automatically detects abnormal execution patterns in simulation traces,
including infinite loops, repeated actions, unvisited nodes, state machine
errors, page mismatches, and orphaned dynamic nodes.
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Dict, List, Optional, Set

from pydantic import BaseModel, Field


class ProblemType(str, Enum):
    """Types of problems that can be detected."""

    INFINITE_LOOP = "infinite_loop"
    REPEATED_ACTION = "repeated_action"
    UNVISITED_NODE = "unvisited_node"
    STATE_MACHINE_ERROR = "state_machine_error"
    PAGE_MISMATCH = "page_mismatch"
    ORPHAN_NODE = "orphan_node"


class ProblemSeverity(str, Enum):
    """Severity levels for detected problems."""

    CRITICAL = "critical"
    ERROR = "error"
    WARNING = "warning"
    INFO = "info"


class SensitivityLevel(str, Enum):
    """Sensitivity levels for detection thresholds."""

    LOW = "low"
    MEDIUM = "medium"
    HIGH = "high"


@dataclass
class Problem:
    """A detected problem in simulation execution.

    Attributes:
        type: Type of problem
        description: Human-readable description
        severity: Problem severity level
        location: Where the problem occurred (node ID, page, etc.)
        evidence: Supporting evidence data
        hint: Optional hint for resolution
    """

    type: ProblemType
    description: str
    severity: ProblemSeverity
    location: str
    evidence: Dict[str, Any] = field(default_factory=dict)
    hint: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary representation."""
        return {
            "type": self.type.value,
            "description": self.description,
            "severity": self.severity.value,
            "location": self.location,
            "evidence": self.evidence,
            "hint": self.hint,
        }


class ProblemDetectorConfig(BaseModel):
    """Configuration for problem detector.

    Attributes:
        max_action_repeats: Maximum allowed consecutive repeats of same action
        max_loop_depth: Maximum depth for state sequence loop detection
        loop_detection_sensitivity: Sensitivity level for loop detection
        enable_infinite_loop_detection: Enable infinite loop detection
        enable_repeated_action_detection: Enable repeated action detection
        enable_unvisited_node_detection: Enable unvisited node detection
        enable_state_machine_error_detection: Enable state machine error detection
        enable_page_mismatch_detection: Enable page mismatch detection
        enable_orphan_node_detection: Enable orphan node detection
    """

    max_action_repeats: int = Field(default=3, ge=1, le=10)
    max_loop_depth: int = Field(default=5, ge=2, le=20)
    loop_detection_sensitivity: SensitivityLevel = Field(default=SensitivityLevel.MEDIUM)
    enable_infinite_loop_detection: bool = Field(default=True)
    enable_repeated_action_detection: bool = Field(default=True)
    enable_unvisited_node_detection: bool = Field(default=True)
    enable_state_machine_error_detection: bool = Field(default=True)
    enable_page_mismatch_detection: bool = Field(default=True)
    enable_orphan_node_detection: bool = Field(default=True)


class ProblemDetector:
    """Detects abnormal execution patterns in simulation traces.

    Supports configurable detection thresholds, sensitivity levels,
    and feature toggles for flexible problem detection.
    """

    # Valid state machine transitions
    VALID_STATE_TRANSITIONS: Dict[str, Set[str]] = {
        "IDLE": {"BINDING", "EXECUTING"},
        "BINDING": {"IDLE", "EXECUTING"},
        "EXECUTING": {"RESULT_VERIFY", "AUTO_ESCAPE", "BRANCH", "FRAME_COMPLETE"},
        "RESULT_VERIFY": {"EXECUTING", "BRANCH", "FRAME_COMPLETE"},
        "AUTO_ESCAPE": {"EXECUTING"},
        "BRANCH": {"NODE_SELECT", "FRAME_COMPLETE"},
        "NODE_SELECT": {"EXECUTING"},
        "FRAME_COMPLETE": {"BRANCH", "NODE_SELECT", "COMPLETED", "ERROR"},
        "COMPLETED": set(),  # Terminal state
        "ERROR": set(),  # Terminal state
    }

    def __init__(self, config: Optional[ProblemDetectorConfig] = None):
        """Initialize the problem detector.

        Args:
            config: Optional detector configuration
        """
        self.config = config or ProblemDetectorConfig()
        self._effective_max_repeats = self.config.max_action_repeats
        self._effective_max_loop_depth = self.config.max_loop_depth
        self._adjust_thresholds()

    def detect(
        self,
        trace: List[Dict[str, Any]],
        expected_nodes: Optional[Set[str]] = None,
        actual_result: Optional[Dict[str, Any]] = None,
    ) -> List[Problem]:
        """Detect problems in execution trace.

        Args:
            trace: Execution trace data
            expected_nodes: Optional set of expected visited nodes
            actual_result: Optional final execution result

        Returns:
            List of detected problems
        """
        problems: List[Problem] = []

        # Extract data from trace
        actions = self._extract_actions(trace)
        state_sequence = self._extract_state_sequence(trace)
        page_transitions = self._extract_page_transitions(trace)
        visited_nodes = self._extract_visited_nodes(trace)
        dynamic_lifecycle = self._extract_dynamic_lifecycle(trace)

        # Run detection methods
        if self.config.enable_infinite_loop_detection:
            problems.extend(self._detect_infinite_loop(actions, state_sequence))

        if self.config.enable_repeated_action_detection:
            problems.extend(self._detect_repeated_actions(actions))

        if self.config.enable_unvisited_node_detection and expected_nodes:
            problems.extend(self._detect_unvisited_nodes(expected_nodes, visited_nodes))

        if self.config.enable_state_machine_error_detection:
            problems.extend(self._detect_state_machine_error(state_sequence, actual_result))

        if self.config.enable_page_mismatch_detection:
            problems.extend(self._detect_page_mismatch(page_transitions))

        if self.config.enable_orphan_node_detection:
            problems.extend(self._detect_orphan_nodes(dynamic_lifecycle))

        return problems

    def _adjust_thresholds(self) -> None:
        """Adjust thresholds based on sensitivity level."""
        sensitivity = self.config.loop_detection_sensitivity

        if sensitivity == SensitivityLevel.LOW:
            # Double the thresholds (less sensitive)
            self._effective_max_repeats = self.config.max_action_repeats * 2
            self._effective_max_loop_depth = self.config.max_loop_depth * 2
        elif sensitivity == SensitivityLevel.HIGH:
            # Halve the thresholds (more sensitive), with minimums
            self._effective_max_repeats = max(1, self.config.max_action_repeats // 2)
            self._effective_max_loop_depth = max(2, self.config.max_loop_depth // 2)

    def _detect_infinite_loop(
        self,
        actions: List[Dict[str, Any]],
        state_sequence: List[str],
    ) -> List[Problem]:
        """Detect infinite loop patterns.

        Args:
            actions: Extracted action sequence
            state_sequence: Extracted state sequence

        Returns:
            List of detected infinite loop problems
        """
        problems: List[Problem] = []

        # Detect repeated action pattern
        if len(actions) >= self._effective_max_repeats:
            last_action = actions[-1]
            repeat_count = 0

            for action in reversed(actions):
                if action.get("action") == last_action.get("action") and action.get("node_id") == last_action.get("node_id"):
                    repeat_count += 1
                else:
                    break

            if repeat_count >= self._effective_max_repeats:
                problems.append(Problem(
                    type=ProblemType.INFINITE_LOOP,
                    description=f"Action repeated {repeat_count} times: {last_action.get('action')} on {last_action.get('node_id')}",
                    severity=ProblemSeverity.CRITICAL,
                    location=last_action.get("node_id", "unknown"),
                    evidence={
                        "action": last_action.get("action"),
                        "node_id": last_action.get("node_id"),
                        "repeat_count": repeat_count,
                    },
                    hint="Check if the target element is accessible and interactive",
                ))

        # Detect state sequence loop
        if len(state_sequence) >= 4:
            loop_pattern = self._find_repeating_patterns(state_sequence)
            if loop_pattern and len(loop_pattern) <= self._effective_max_loop_depth:
                problems.append(Problem(
                    type=ProblemType.INFINITE_LOOP,
                    description=f"State sequence loop detected: {' -> '.join(loop_pattern)}",
                    severity=ProblemSeverity.WARNING,
                    location="state_machine",
                    evidence={"pattern": loop_pattern},
                    hint="Review state machine logic for unintended transitions",
                ))

        return problems

    def _detect_repeated_actions(self, actions: List[Dict[str, Any]]) -> List[Problem]:
        """Detect abnormal repeated actions on the same node.

        Args:
            actions: Extracted action sequence

        Returns:
            List of detected repeated action problems
        """
        problems: List[Problem] = []

        # Count consecutive repeats of same action on same node
        for i in range(len(actions)):
            action = actions[i]
            repeat_count = 1

            for j in range(i + 1, len(actions)):
                next_action = actions[j]
                if (action.get("action") == next_action.get("action")
                    and action.get("node_id") == next_action.get("node_id")):
                    repeat_count += 1
                else:
                    break

            if repeat_count >= self._effective_max_repeats:
                problems.append(Problem(
                    type=ProblemType.REPEATED_ACTION,
                    description=f"Action '{action.get('action')}' repeated {repeat_count} times on {action.get('node_id')}",
                    severity=ProblemSeverity.WARNING,
                    location=action.get("node_id", "unknown"),
                    evidence={
                        "action": action.get("action"),
                        "node_id": action.get("node_id"),
                        "repeat_count": repeat_count,
                    },
                ))

        return problems

    def _detect_unvisited_nodes(
        self,
        expected_nodes: Set[str],
        visited_nodes: Set[str],
    ) -> List[Problem]:
        """Detect nodes that were expected but not visited.

        Args:
            expected_nodes: Set of expected node IDs
            visited_nodes: Set of actually visited node IDs

        Returns:
            List of detected unvisited node problems
        """
        problems: List[Problem] = []

        for node in expected_nodes:
            if node not in visited_nodes:
                problems.append(Problem(
                    type=ProblemType.UNVISITED_NODE,
                    description=f"Expected node not visited: {node}",
                    severity=ProblemSeverity.WARNING,
                    location=node,
                    evidence={"expected_node": node},
                    hint=f"Check if {node} is reachable and has valid traversal path",
                ))

        return problems

    def _detect_state_machine_error(
        self,
        state_sequence: List[str],
        actual_result: Optional[Dict[str, Any]],
    ) -> List[Problem]:
        """Detect state machine errors and invalid transitions.

        Args:
            state_sequence: Extracted state sequence
            actual_result: Optional final execution result

        Returns:
            List of detected state machine error problems
        """
        problems: List[Problem] = []

        # Check for final ERROR state
        if actual_result:
            final_state = actual_result.get("status", "").upper()
            if final_state == "ERROR":
                problems.append(Problem(
                    type=ProblemType.STATE_MACHINE_ERROR,
                    description=f"Simulation ended in ERROR state: {actual_result.get('error_type', 'Unknown')}",
                    severity=ProblemSeverity.CRITICAL,
                    location="final_state",
                    evidence={"error_type": actual_result.get("error_type"), "error_message": actual_result.get("error")},
                ))

        # Check for invalid state transitions
        for i in range(len(state_sequence) - 1):
            from_state = state_sequence[i]
            to_state = state_sequence[i + 1]

            valid_targets = self.VALID_STATE_TRANSITIONS.get(from_state, set())
            if to_state not in valid_targets:
                problems.append(Problem(
                    type=ProblemType.STATE_MACHINE_ERROR,
                    description=f"Invalid state transition: {from_state} -> {to_state}",
                    severity=ProblemSeverity.ERROR,
                    location="state_machine",
                    evidence={"from_state": from_state, "to_state": to_state},
                    hint=f"Valid targets from {from_state} are: {list(valid_targets)}",
                ))

        return problems

    def _detect_page_mismatch(self, page_transitions: List[Dict[str, Any]]) -> List[Problem]:
        """Detect potential page transition failures.

        Args:
            page_transitions: Extracted page transitions

        Returns:
            List of detected page mismatch problems
        """
        problems: List[Problem] = []

        for transition in page_transitions:
            from_page = transition.get("from_page")
            to_page = transition.get("to_page")

            if from_page == to_page:
                problems.append(Problem(
                    type=ProblemType.PAGE_MISMATCH,
                    description=f"Page transition stayed on same page: {from_page}",
                    severity=ProblemSeverity.WARNING,
                    location=from_page or "unknown",
                    evidence={"transition": transition},
                    hint="Check if the transition trigger element is interactive",
                ))

        return problems

    def _detect_orphan_nodes(self, lifecycle_events: List[Dict[str, Any]]) -> List[Problem]:
        """Detect dynamic nodes created but never executed.

        Args:
            lifecycle_events: Extracted dynamic node lifecycle events

        Returns:
            List of detected orphan node problems
        """
        problems: List[Problem] = []

        # Group events by node_id
        node_events: Dict[str, List[str]] = {}
        for event in lifecycle_events:
            node_id = event.get("node_id")
            event_type = event.get("event")

            if node_id not in node_events:
                node_events[node_id] = []
            node_events[node_id].append(event_type)

        # Check for nodes with "created" but no "executed"
        for node_id, events in node_events.items():
            if "created" in events and "executed" not in events:
                problems.append(Problem(
                    type=ProblemType.ORPHAN_NODE,
                    description=f"Dynamic node created but never executed: {node_id}",
                    severity=ProblemSeverity.WARNING,
                    location=node_id,
                    evidence={"lifecycle_events": events},
                    hint="Check if dynamic node matching conditions are correct",
                ))

        return problems

    def _extract_actions(self, trace: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Extract action sequence from trace.

        Args:
            trace: Execution trace

        Returns:
            List of action dictionaries
        """
        actions = []
        for node in trace:
            if node.get("span_type") == "execution":
                actions.append({
                    "action": node.get("action"),
                    "node_id": node.get("target"),
                    "status": node.get("status"),
                })
        return actions

    def _extract_state_sequence(self, trace: List[Dict[str, Any]]) -> List[str]:
        """Extract state sequence from trace.

        Args:
            trace: Execution trace

        Returns:
            List of state names in order
        """
        states = []
        for node in trace:
            if node.get("span_type") == "state_transition":
                to_state = node.get("to_state")
                if to_state:
                    states.append(to_state)
        return states

    def _extract_page_transitions(self, trace: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Extract page transitions from trace.

        Args:
            trace: Execution trace

        Returns:
            List of page transition dictionaries
        """
        transitions = []
        for node in trace:
            if node.get("span_type") == "page_transition":
                transitions.append({
                    "from_page": node.get("from_page"),
                    "to_page": node.get("to_page"),
                    "trigger_element": node.get("trigger_element"),
                })
        return transitions

    def _extract_visited_nodes(self, trace: List[Dict[str, Any]]) -> Set[str]:
        """Extract visited node IDs from trace.

        Args:
            trace: Execution trace

        Returns:
            Set of visited node IDs
        """
        visited = set()
        for node in trace:
            if node.get("node_type") == "step":
                node_id = node.get("node_id")
                if node_id:
                    visited.add(node_id)
        return visited

    def _extract_dynamic_lifecycle(self, trace: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Extract dynamic node lifecycle events.

        Args:
            trace: Execution trace

        Returns:
            List of lifecycle event dictionaries
        """
        events = []
        for node in trace:
            if node.get("span_type") == "dynamic_lifecycle":
                events.append({
                    "event": node.get("event"),
                    "node_id": node.get("node_id"),
                    "parent_id": node.get("parent_id"),
                })
        return events

    def _is_valid_transition(self, from_state: str, to_state: str) -> bool:
        """Check if a state transition is valid.

        Args:
            from_state: Source state
            to_state: Target state

        Returns:
            True if transition is valid
        """
        valid_targets = self.VALID_STATE_TRANSITIONS.get(from_state, set())
        return to_state in valid_targets

    def _find_repeating_patterns(self, sequence: List[str]) -> Optional[List[str]]:
        """Find repeating patterns in a sequence.

        Args:
            sequence: Sequence to analyze

        Returns:
            Repeating pattern if found, None otherwise
        """
        if len(sequence) < 4:
            return None

        # Try different pattern lengths
        for pattern_len in range(2, len(sequence) // 2 + 1):
            pattern = sequence[:pattern_len]

            # Check if pattern repeats
            is_repeating = True
            for i in range(pattern_len, len(sequence), pattern_len):
                for j in range(pattern_len):
                    if i + j >= len(sequence):
                        is_repeating = False
                        break
                    if sequence[i + j] != pattern[j]:
                        is_repeating = False
                        break
                if not is_repeating:
                    break

            if is_repeating:
                return pattern

        return None
