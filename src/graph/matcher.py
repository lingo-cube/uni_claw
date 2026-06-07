"""
Dynamic matcher for matching UI elements to templates.

This module provides the dynamic matching logic that determines which template
to apply to each discovered UI element during traversal.
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Dict, List, Optional

from .node import TraversalNode
from .template import TemplateRegistry


class MatchCondition:
    """
    Condition for matching UI elements.

    Defines criteria to evaluate whether a MenuItem matches a rule.
    """

    def __init__(self, condition: Dict[str, Any]):
        """
        Initialize match condition from dictionary.

        Supported condition fields:
        - type: UI element type (menu_item, switch, slider, button)
        - expected_action: Expected action type (click, swipe, etc.)
        - text_pattern: Regex pattern for text content
        - min_index: Minimum index in list
        - max_index: Maximum index in list
        - custom: Custom condition expression
        """
        self.type = condition.get("type")
        self.expected_action = condition.get("expected_action")
        self.text_pattern = condition.get("text_pattern")
        self.min_index = condition.get("min_index")
        self.max_index = condition.get("max_index")
        self.custom = condition.get("custom")
        self.raw = condition

    def matches(self, menu_item: Dict[str, Any]) -> bool:
        """
        Evaluate if a MenuItem matches this condition.

        Args:
            menu_item: MenuItem data dict with fields like type, text, index, etc.

        Returns:
            True if the condition matches
        """
        # Type check
        if self.type is not None:
            item_type = menu_item.get("type")
            if item_type != self.type:
                return False

        # Expected action check
        if self.expected_action is not None:
            item_action = menu_item.get("expected_action")
            if item_action != self.expected_action:
                return False

        # Text pattern check (if pattern specified)
        if self.text_pattern is not None:
            import re

            item_text = menu_item.get("text", "")
            if not re.search(self.text_pattern, item_text):
                return False

        # Index range check
        item_index = menu_item.get("index")
        if item_index is not None:
            if self.min_index is not None and item_index < self.min_index:
                return False
            if self.max_index is not None and item_index > self.max_index:
                return False

        # If all checks passed or no conditions specified
        return True


class MatchAction(str, Enum):
    """Action to take when a match is found."""

    GENERATE_CHILD = "generate_child"  # Create child node from template
    SKIP = "skip"  # Skip this element
    EXECUTE_INLINE = "execute_inline"  # Execute operation immediately, no child node


class MatchStatus(str, Enum):
    """Status of a match operation."""

    MATCHED = "matched"  # Element matched a rule
    NOT_MATCHED = "not_matched"  # Element did not match any rule
    ERROR = "error"  # Error occurred during matching


@dataclass
class MatchResult:
    """
    Result of matching a UI element.

    Contains information about the match and what action to take.
    """

    matched: bool
    rule_id: Optional[str] = None
    template_id: Optional[str] = None
    action: MatchAction = MatchAction.SKIP
    menu_item: Optional[Dict[str, Any]] = None
    context: Dict[str, Any] = field(default_factory=dict)

    def __bool__(self) -> bool:
        """Truth value is whether matched."""
        return self.matched

    @property
    def status(self) -> MatchStatus:
        """Get match status as MatchStatus enum."""
        return MatchStatus.MATCHED if self.matched else MatchStatus.NOT_MATCHED


class DynamicMatcher:
    """
    Dynamic matcher for UI element to template matching.

    Evaluates UI elements against rules and determines which template
    to instantiate for each element.
    """

    def __init__(self, template_registry: TemplateRegistry):
        """
        Initialize the dynamic matcher.

        Args:
            template_registry: Template registry for instantiating matched nodes
        """
        self.template_registry = template_registry
        self.rules: Dict[str, Dict[str, Any]] = {}
        self.match_history: List[Dict[str, Any]] = []

    def load_rules(self, dynamic_rules: Dict[str, Any]) -> None:
        """
        Load dynamic matching rules.

        Args:
            dynamic_rules: Dictionary mapping rule_id to rule configuration
        """
        self.rules = dynamic_rules.copy()

    def match(self, menu_item: Dict[str, Any], parent_node: TraversalNode) -> MatchResult:
        """
        Match a menu item against loaded rules.

        Args:
            menu_item: MenuItem data dict
            parent_node: Parent node for context

        Returns:
            MatchResult with match information
        """
        for rule_id, rule_config in self.rules.items():
            condition = MatchCondition(rule_config.get("match_condition", {}))

            if condition.matches(menu_item):
                # Found a match
                action_str = rule_config.get("action", "generate_child")
                action = MatchAction(action_str) if isinstance(action_str, str) else action_str

                template_id = rule_config.get("child_template")
                if not template_id and action == MatchAction.GENERATE_CHILD:
                    # Need template for child generation
                    return MatchResult(matched=False, menu_item=menu_item)

                result = MatchResult(
                    matched=True,
                    rule_id=rule_id,
                    template_id=template_id,
                    action=action,
                    menu_item=menu_item,
                    context=self._build_context(menu_item, parent_node),
                )

                # Record match for debugging
                self._record_match(result)

                return result

        # No match found
        return MatchResult(matched=False, menu_item=menu_item)

    def match_all(
        self, menu_items: List[Dict[str, Any]], parent_node: TraversalNode
    ) -> List[MatchResult]:
        """
        Match multiple menu items against rules.

        Args:
            menu_items: List of MenuItem dicts
            parent_node: Parent node for context

        Returns:
            List of MatchResults in same order as input
        """
        results = []
        for item in menu_items:
            result = self.match(item, parent_node)
            results.append(result)
        return results

    def instantiate_match(self, match_result: MatchResult) -> Optional[TraversalNode]:
        """
        Instantiate a node from a match result.

        Args:
            match_result: Match result from match() or match_all()

        Returns:
            TraversalNode instance or None if instantiation fails
        """
        if not match_result.matched:
            return None

        if match_result.action != MatchAction.GENERATE_CHILD:
            return None

        if not match_result.template_id:
            return None

        return self.template_registry.instantiate(
            match_result.template_id, match_result.context
        )

    def _build_context(self, menu_item: Dict[str, Any], parent_node: TraversalNode) -> Dict[str, Any]:
        """Build context dict for template instantiation."""
        return {
            "item_text": menu_item.get("text", ""),
            "item_index": menu_item.get("index", 0),
            "coordinate_x": menu_item.get("coordinate_x", 0.5),
            "coordinate_y": menu_item.get("coordinate_y", 0.5),
            "parent_id": parent_node.node_id,
            "name": menu_item.get("text", f"item_{menu_item.get('index', 0)}"),
        }

    def _record_match(self, result: MatchResult) -> None:
        """Record match result for debugging."""
        self.match_history.append(
            {
                "rule_id": result.rule_id,
                "template_id": result.template_id,
                "action": result.action.value,
                "menu_item": result.menu_item,
            }
        )

    def get_match_history(self) -> List[Dict[str, Any]]:
        """Get history of matches made."""
        return self.match_history.copy()

    def clear_history(self) -> None:
        """Clear match history."""
        self.match_history.clear()

    def get_statistics(self) -> Dict[str, Any]:
        """
        Get statistics about matches made.

        Returns:
            Dict with match statistics
        """
        if not self.match_history:
            return {"total": 0, "by_rule": {}, "by_action": {}}

        by_rule: Dict[str, int] = {}
        by_action: Dict[str, int] = {}

        for record in self.match_history:
            rule_id = record.get("rule_id", "unknown")
            action = record.get("action", "unknown")

            by_rule[rule_id] = by_rule.get(rule_id, 0) + 1
            by_action[action] = by_action.get(action, 0) + 1

        return {
            "total": len(self.match_history),
            "by_rule": by_rule,
            "by_action": by_action,
        }
