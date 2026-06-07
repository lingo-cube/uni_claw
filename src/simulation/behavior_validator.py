"""Behavior validator for simulation testing.

Validates actual simulation execution results against expected behavior
definitions. Supports multi-level node matching with confidence scoring.
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Dict, List, Optional, Set

from .expected_behavior import (
    ExpectedBehavior,
    ExpectedAction,
    ExpectedPageTransition,
    CompletionMode,
)


class ValidationResultStatus(str, Enum):
    """Status of a validation result."""

    OK = "ok"  # All validations passed
    FAIL = "fail"  # Critical validation failures
    WARNING = "warning"  # Non-critical issues detected


class MatchType(str, Enum):
    """Type of node match."""

    EXACT = "exact"  # Exact ID match
    FUZZY_ID = "fuzzy_id"  # ID substring match
    FUZZY_TEXT = "fuzzy_text"  # Target text match
    NONE = "none"  # No match


@dataclass
class MatchResult:
    """Result of a node matching attempt.

    Attributes:
        matched: Whether a match was found
        match_type: Type of match (exact, fuzzy, none)
        confidence: Match confidence (0.0 to 1.0)
        reason: Human-readable explanation
        expected_id: Expected node/element ID
        actual_id: Actual node/element ID that matched
    """

    matched: bool
    match_type: MatchType
    confidence: float
    reason: str
    expected_id: str
    actual_id: Optional[str] = None


@dataclass
class ValidationIssue:
    """A validation issue found during comparison.

    Attributes:
        category: Issue category (action_sequence, page_transition, node_visitation, state)
        severity: Issue severity (error, warning, info)
        message: Human-readable issue description
        expected: Expected value
        actual: Actual value found
    """

    category: str
    severity: str
    message: str
    expected: Any
    actual: Any


@dataclass
class ValidationResult:
    """Result of behavior validation.

    Attributes:
        status: Overall validation status
        issues: List of validation issues found
        exact_match_count: Number of exact matches
        fuzzy_match_count: Number of fuzzy matches
        total_validated: Total number of items validated
    """

    status: ValidationResultStatus = ValidationResultStatus.OK
    issues: List[ValidationIssue] = field(default_factory=list)
    exact_match_count: int = 0
    fuzzy_match_count: int = 0
    total_validated: int = 0

    def add_issue(
        self,
        category: str,
        severity: str,
        message: str,
        expected: Any = None,
        actual: Any = None,
    ) -> None:
        """Add a validation issue.

        Args:
            category: Issue category
            severity: Issue severity (error, warning, info)
            message: Issue description
            expected: Expected value
            actual: Actual value
        """
        self.issues.append(ValidationIssue(
            category=category,
            severity=severity,
            message=message,
            expected=expected,
            actual=actual,
        ))

        # Update status based on severity
        if severity == "error":
            self.status = ValidationResultStatus.FAIL
        elif severity == "warning" and self.status == ValidationResultStatus.OK:
            self.status = ValidationResultStatus.WARNING

    def is_valid(self) -> bool:
        """Check if validation passed without errors."""
        return self.status != ValidationResultStatus.FAIL


class BehaviorValidator:
    """Validates actual simulation behavior against expected behavior.

    Supports multi-level node matching with configurable fuzzy match
    severity and comprehensive validation of action sequences, page
    transitions, node visitation, and final state.
    """

    def __init__(self, strict_fuzzy_match: bool = True):
        """Initialize the behavior validator.

        Args:
            strict_fuzzy_match: If True, fuzzy matches are treated as errors;
                if False, fuzzy matches are warnings
        """
        self.strict_fuzzy_match = strict_fuzzy_match

    def validate(
        self,
        expected: ExpectedBehavior,
        actual_trace: List[Dict[str, Any]],
        actual_result: Optional[Dict[str, Any]] = None,
    ) -> ValidationResult:
        """Validate actual execution against expected behavior.

        Args:
            expected: Expected behavior definition
            actual_trace: Actual trace data from execution
            actual_result: Optional final execution result

        Returns:
            ValidationResult with all issues found
        """
        result = ValidationResult()

        # Validate final state
        self._validate_final_state(expected, actual_result, result)

        # Validate action sequence
        actual_actions = self._extract_actions(actual_trace)
        self._validate_action_sequence(expected.actions, actual_actions, result)

        # Validate page transitions
        actual_transitions = self._extract_page_transitions(actual_trace)
        self._validate_page_transitions(expected.page_transitions, actual_transitions, result)

        # Validate node visitation
        actual_visited = self._extract_visited_nodes(actual_trace)
        self._validate_node_visitation(expected.visited_nodes, actual_visited, result)

        # Validate completion mode
        self._validate_completion_mode(expected, actual_result, result)

        result.total_validated = (
            len(expected.actions)
            + len(expected.page_transitions)
            + len(expected.visited_nodes)
            + 1  # final state
            + 1  # completion mode
        )

        return result

    def _validate_final_state(
        self,
        expected: ExpectedBehavior,
        actual_result: Optional[Dict[str, Any]],
        result: ValidationResult,
    ) -> None:
        """Validate the final execution state.

        Args:
            expected: Expected behavior
            actual_result: Actual execution result
            result: ValidationResult to add issues to
        """
        if not actual_result:
            result.add_issue(
                category="state",
                severity="warning",
                message="No actual result provided for state validation",
                expected=expected.final_state,
                actual=None,
            )
            return

        actual_state = actual_result.get("status", "").upper()
        if actual_state != expected.final_state.upper():
            result.add_issue(
                category="state",
                severity="error",
                message=f"Final state mismatch: expected {expected.final_state}, got {actual_state}",
                expected=expected.final_state,
                actual=actual_state,
            )

    def _validate_action_sequence(
        self,
        expected_actions: List[ExpectedAction],
        actual_actions: List[Dict[str, Any]],
        result: ValidationResult,
    ) -> None:
        """Validate the action sequence.

        Args:
            expected_actions: Expected action sequence
            actual_actions: Actual action sequence from trace
            result: ValidationResult to add issues to
        """
        # Check for missing actions
        for i, expected_action in enumerate(expected_actions):
            if i >= len(actual_actions):
                result.add_issue(
                    category="action_sequence",
                    severity="error",
                    message=f"Missing expected action at index {i}: {expected_action.action} on {expected_action.node}",
                    expected=f"{expected_action.action} on {expected_action.node}",
                    actual="None (action not found)",
                )
                continue

            actual_action = actual_actions[i]
            match_result = self._match_node(expected_action.node, actual_action.get("node_id", ""))

            if not match_result.matched:
                result.add_issue(
                    category="action_sequence",
                    severity="error",
                    message=f"Action mismatch at index {i}: expected node {expected_action.node}, got {actual_action.get('node_id')}",
                    expected=f"{expected_action.action} on {expected_action.node}",
                    actual=f"{actual_action.get('action')} on {actual_action.get('node_id')}",
                )
            elif match_result.match_type != MatchType.EXACT:
                # Track fuzzy matches
                result.fuzzy_match_count += 1
                severity = "error" if self.strict_fuzzy_match else "warning"
                result.add_issue(
                    category="action_sequence",
                    severity=severity,
                    message=f"Fuzzy match at index {i}: {match_result.reason}",
                    expected=expected_action.node,
                    actual=actual_action.get("node_id"),
                )
            else:
                result.exact_match_count += 1

            # Check action type
            if actual_action.get("action") != expected_action.action:
                result.add_issue(
                    category="action_sequence",
                    severity="error",
                    message=f"Action type mismatch at index {i}: expected {expected_action.action}, got {actual_action.get('action')}",
                    expected=expected_action.action,
                    actual=actual_action.get("action"),
                )

        # Check for extra actions
        if len(actual_actions) > len(expected_actions):
            for i in range(len(expected_actions), len(actual_actions)):
                result.add_issue(
                    category="action_sequence",
                    severity="warning",
                    message=f"Unexpected extra action at index {i}: {actual_actions[i].get('action')} on {actual_actions[i].get('node_id')}",
                    expected="None (no action expected)",
                    actual=f"{actual_actions[i].get('action')} on {actual_actions[i].get('node_id')}",
                )

    def _validate_page_transitions(
        self,
        expected_transitions: List[ExpectedPageTransition],
        actual_transitions: List[Dict[str, Any]],
        result: ValidationResult,
    ) -> None:
        """Validate page transitions.

        Args:
            expected_transitions: Expected page transitions
            actual_transitions: Actual page transitions from trace
            result: ValidationResult to add issues to
        """
        for i, expected_transition in enumerate(expected_transitions):
            found = False

            for actual_transition in actual_transitions:
                # Check from_page, to_page, and trigger
                if (actual_transition.get("from_page") == expected_transition.from_page
                    and actual_transition.get("to_page") == expected_transition.to_page
                    and actual_transition.get("trigger_element") == expected_transition.trigger):
                    found = True
                    result.exact_match_count += 1
                    break

            if not found:
                result.add_issue(
                    category="page_transition",
                    severity="error",
                    message=f"Missing expected page transition at index {i}: {expected_transition.from_page} -> {expected_transition.to_page}",
                    expected=f"{expected_transition.from_page} -> {expected_transition.to_page} (trigger: {expected_transition.trigger})",
                    actual="Transition not found",
                )

    def _validate_node_visitation(
        self,
        expected_nodes: Set[str],
        actual_nodes: Set[str],
        result: ValidationResult,
    ) -> None:
        """Validate node visitation.

        Args:
            expected_nodes: Expected visited nodes
            actual_nodes: Actual visited nodes from trace
            result: ValidationResult to add issues to
        """
        # Check for missing expected nodes
        for node in expected_nodes:
            if node not in actual_nodes:
                # Try fuzzy matching
                fuzzy_match = False
                for actual_node in actual_nodes:
                    match_result = self._match_node(node, actual_node)
                    if match_result.matched:
                        fuzzy_match = True
                        result.fuzzy_match_count += 1
                        severity = "error" if self.strict_fuzzy_match else "warning"
                        result.add_issue(
                            category="node_visitation",
                            severity=severity,
                            message=f"Fuzzy match for expected node: {match_result.reason}",
                            expected=node,
                            actual=actual_node,
                        )
                        break

                if not fuzzy_match:
                    result.add_issue(
                        category="node_visitation",
                        severity="error",
                        message=f"Expected node not visited: {node}",
                        expected=node,
                        actual="Node not found in visited nodes",
                    )
            else:
                result.exact_match_count += 1

        # Check for unexpected nodes
        for node in actual_nodes:
            # Try to find in expected nodes
            found = node in expected_nodes
            if not found:
                # Try fuzzy matching
                for expected_node in expected_nodes:
                    match_result = self._match_node(expected_node, node)
                    if match_result.matched:
                        found = True
                        break

            if not found:
                result.add_issue(
                    category="node_visitation",
                    severity="warning",
                    message=f"Unexpected visited node: {node}",
                    expected="None (node not in expected set)",
                    actual=node,
                )

    def _validate_completion_mode(
        self,
        expected: ExpectedBehavior,
        actual_result: Optional[Dict[str, Any]],
        result: ValidationResult,
    ) -> None:
        """Validate completion mode.

        Args:
            expected: Expected behavior
            actual_result: Actual execution result
            result: ValidationResult to add issues to
        """
        if not actual_result:
            return

        # Extract actual completion info
        actual_status = actual_result.get("status", "").lower()
        actual_error = actual_result.get("error_type", "")

        # Check based on expected completion mode
        if expected.completion_mode == CompletionMode.NORMAL:
            if actual_status != "completed":
                result.add_issue(
                    category="completion_mode",
                    severity="error",
                    message=f"Expected normal completion but got: {actual_status}",
                    expected="completed",
                    actual=actual_status,
                )

        elif expected.completion_mode == CompletionMode.EXCEPTION:
            if not actual_error:
                result.add_issue(
                    category="completion_mode",
                    severity="error",
                    message=f"Expected exception '{expected.expected_exception}' but execution completed",
                    expected=expected.expected_exception,
                    actual="No exception raised",
                )
            elif expected.expected_exception and expected.expected_exception not in actual_error:
                result.add_issue(
                    category="completion_mode",
                    severity="error",
                    message=f"Expected exception '{expected.expected_exception}' but got: {actual_error}",
                    expected=expected.expected_exception,
                    actual=actual_error,
                )

    def _match_node(self, expected_id: str, actual_id: str) -> MatchResult:
        """Match an expected node ID with an actual node ID.

        Implements multi-level matching:
        1. Exact match (confidence 1.0)
        2. Fuzzy ID substring match (confidence 0.9)
        3. Fuzzy target text match (confidence 0.7)
        4. No match (confidence 0.0)

        Args:
            expected_id: Expected node/element ID
            actual_id: Actual node/element ID

        Returns:
            MatchResult with match details
        """
        # 1. Exact match
        if expected_id == actual_id:
            return MatchResult(
                matched=True,
                match_type=MatchType.EXACT,
                confidence=1.0,
                reason="Exact ID match",
                expected_id=expected_id,
                actual_id=actual_id,
            )

        # 2. Fuzzy ID substring match
        if expected_id in actual_id or actual_id in expected_id:
            return MatchResult(
                matched=True,
                match_type=MatchType.FUZZY_ID,
                confidence=0.9,
                reason=f"ID substring match: '{expected_id}' in '{actual_id}'",
                expected_id=expected_id,
                actual_id=actual_id,
            )

        # 3. No match
        return MatchResult(
            matched=False,
            match_type=MatchType.NONE,
            confidence=0.0,
            reason=f"No match: '{expected_id}' vs '{actual_id}'",
            expected_id=expected_id,
            actual_id=actual_id,
        )

    def _extract_actions(self, trace: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Extract action sequence from trace.

        Args:
            trace: Trace data

        Returns:
            List of action dictionaries
        """
        actions = []

        for node in trace:
            # Look for execution spans
            if node.get("span_type") == "execution":
                actions.append({
                    "action": node.get("action"),
                    "node_id": node.get("target"),
                    "status": node.get("status"),
                })

        return actions

    def _extract_page_transitions(self, trace: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Extract page transitions from trace.

        Args:
            trace: Trace data

        Returns:
            List of page transition dictionaries
        """
        transitions = []

        for node in trace:
            # Look for page_transition spans
            if node.get("span_type") == "page_transition":
                transitions.append({
                    "from_page": node.get("from_page"),
                    "to_page": node.get("to_page"),
                    "trigger_element": node.get("trigger_element"),
                    "trigger_action": node.get("trigger_action"),
                })

        return transitions

    def _extract_visited_nodes(self, trace: List[Dict[str, Any]]) -> Set[str]:
        """Extract visited node IDs from trace.

        Args:
            trace: Trace data

        Returns:
            Set of visited node IDs
        """
        visited = set()

        for node in trace:
            # Look for step nodes (NODE_SELECT)
            if node.get("node_type") == "step":
                node_id = node.get("node_id")
                if node_id:
                    visited.add(node_id)

        return visited
