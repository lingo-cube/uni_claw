"""
Plan compiler for V6.9 declarative traversal.

This module provides the PlanCompiler that deterministically maps
IntentSlots to TraversalPlan without AI dependency.
"""

import logging
import uuid
from typing import Dict, List, Optional, Any

from src.graph.node import (
    CompletionPolicy,
    CompletionPolicyType,
    ChildrenStrategy,
    ChildrenStrategyType,
    DynamicRule,
    EntryPolicy,
    EntryStrategy,
    ExitCondition,
    ExitConditionType,
    FallbackAction,
    IntentSlots,
    MatchMode,
    NodeType,
    Operation,
    Precondition,
    Target,
    TargetFoundAction,
    TraversalMode,
    TraversalNode,
)
from src.graph.plan import TraversalPlan


logger = logging.getLogger(__name__)


class CompilerError(Exception):
    """Exception raised during plan compilation."""

    pass


class PlanCompiler:
    """
    Plan compiler for V6.9.

    Maps IntentSlots to TraversalPlan using deterministic rules.
    No AI dependency - all mappings are rule-based.
    """

    # Template sets for element_handling values
    TEMPLATE_SETS = {
        "full_interaction": [
            "menu_container",
            "switch_leaf",
            "slider_leaf",
            "leaf_action",
        ],
        "menu_only": [
            "menu_container",
        ],
        "safe_mode": [
            "menu_container",
            "switch_leaf",
            "slider_leaf",
            "leaf_action",
        ],
        "read_only": [
            "leaf_info",
        ],
    }

    def compile(self, slots: IntentSlots) -> TraversalPlan:
        """
        Compile IntentSlots into TraversalPlan.

        Args:
            slots: IntentSlots extracted from natural language

        Returns:
            TraversalPlan ready for execution

        Raises:
            CompilerError: If slots validation fails
        """
        # Validate slots
        self._validate_slots(slots)

        # Build plan components
        entry_policy = self._build_entry_policy(slots)
        root_node = self._build_root_node(slots)
        completion_policy = self._build_completion_policy(slots)

        # Create plan
        plan = TraversalPlan(
            entry_app=slots.target_app or "unknown",
            entry_policy=entry_policy,
            root_node=root_node,
            completion_policy=completion_policy,
            intent_slots=slots,
            mode=TraversalMode.HYBRID,
            static_nodes={},  # Will be populated if needed
        )

        # Add static nodes for target_path scope
        if slots.scope == "target_path" and slots.target:
            plan.static_nodes = self._build_static_nodes(slots)

        return plan

    def _validate_slots(self, slots: IntentSlots) -> None:
        """
        Validate IntentSlots before compilation.

        Raises:
            CompilerError: If validation fails
        """
        errors = []

        # Check required fields
        if not slots.target_app:
            errors.append("target_app is required")

        # Check scope/target combinations
        if slots.scope in ("target_only", "target_path") and not slots.target:
            errors.append(f"target is required when scope is {slots.scope}")

        # Check depth validity
        if slots.depth is not None:
            if slots.depth <= 0:
                errors.append(f"Invalid depth: {slots.depth} (must be positive)")
            elif slots.depth > 1000:
                errors.append(f"Invalid depth: {slots.depth} (must be <= 1000)")

        # Warn about completion override
        if slots.completion and slots.scope:
            logger.warning(
                f"completion='{slots.completion}' overrides scope='{slots.scope}' "
                f"derived completion_policy. Final type will be derived from completion."
            )

        if errors:
            raise CompilerError(f"Slots validation failed: {'; '.join(errors)}")

    def _build_entry_policy(self, slots: IntentSlots) -> EntryPolicy:
        """Build EntryPolicy from slots."""
        return EntryPolicy()

    def _build_root_node(self, slots: IntentSlots) -> TraversalNode:
        """Build root node from slots."""
        # Determine children strategy
        if slots.scope == "target_path":
            # Use STATIC strategy for predefined path
            children_strategy = ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=[f"static_child_0"],  # First segment
                max_children=100,
            )
        else:
            # Use DYNAMIC_MATCH with templates from element_handling
            children_strategy = self._build_dynamic_strategy(slots)

        # Create root node
        # Build meta dict
        meta = {}
        if slots.restore:
            meta["restore"] = slots.restore
        if slots.element_handling == "safe_mode":
            meta["safe_mode"] = True

        return TraversalNode(
            node_id="root",
            name=f"Root_{slots.target_app or 'App'}",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            precondition=Precondition(page_name=slots.target_app),
            children_strategy=children_strategy,
            exit_condition=self._build_exit_condition(slots),
            meta=meta,
        )

    def _build_dynamic_strategy(self, slots: IntentSlots) -> ChildrenStrategy:
        """Build DYNAMIC_MATCH children strategy from element_handling."""
        # Get template set for element_handling
        element_handling = slots.element_handling or "full_interaction"
        template_ids = self.TEMPLATE_SETS.get(
            element_handling,
            self.TEMPLATE_SETS["full_interaction"],  # Default
        )

        # Build dynamic rules
        dynamic_rules = {}
        for idx, template_id in enumerate(template_ids):
            rule_id = f"rule_{idx}"
            # Determine match condition based on template type
            match_condition = self._get_match_condition(template_id)
            dynamic_rules[rule_id] = DynamicRule(
                rule_id=rule_id,
                match_condition=match_condition,
                child_template=template_id,
                action="generate_child",
            )

        return ChildrenStrategy(
            type=ChildrenStrategyType.DYNAMIC_MATCH,
            dynamic_rules=dynamic_rules,
            max_children=100,
        )

    def _get_match_condition(self, template_id: str) -> Dict[str, Any]:
        """Get match condition for a template ID."""
        # Map templates to their match conditions
        if template_id == "menu_container":
            return {"type": "menu_item"}
        elif template_id == "switch_leaf":
            return {"type": "switch"}
        elif template_id == "slider_leaf":
            return {"type": "slider"}
        elif template_id == "leaf_action":
            return {"type": "button"}
        elif template_id == "leaf_info":
            return {}  # Match anything for read-only
        else:
            return {}

    def _build_completion_policy(self, slots: IntentSlots) -> CompletionPolicy:
        """Build CompletionPolicy from slots."""
        # Check for completion override
        if slots.completion == "timeout":
            return CompletionPolicy(
                type=CompletionPolicyType.TIMEOUT,
                timeout_seconds=300,  # Default 5 minutes
            )
        elif slots.completion == "steps":
            return CompletionPolicy(
                type=CompletionPolicyType.MAX_STEPS,
                max_steps=100,  # Default 100 steps
            )

        # No override, use scope
        scope = slots.scope or "full"

        if scope == "full":
            return CompletionPolicy(type=CompletionPolicyType.NONE)
        elif scope == "partial":
            return CompletionPolicy(
                type=CompletionPolicyType.MAX_STEPS,
                max_steps=50,  # Default for partial
            )
        elif scope == "target_only":
            return CompletionPolicy(
                type=CompletionPolicyType.TARGET_FOUND,
                target_name=slots.target or "",
                match_mode=MatchMode.CONTAINS,
                action_on_found=TargetFoundAction.MARK_AND_STOP,
            )
        elif scope == "target_path":
            return CompletionPolicy(type=CompletionPolicyType.NONE)

        # Default
        return CompletionPolicy(type=CompletionPolicyType.NONE)

    def _build_exit_condition(self, slots: IntentSlots) -> ExitCondition:
        """Build ExitCondition from navigation slot."""
        # Map navigation to fallback action
        navigation = slots.navigation or "auto_escape"

        if navigation == "back":
            fallback = FallbackAction.BACK
        else:
            fallback = FallbackAction.AUTO_ESCAPE

        return ExitCondition(
            type=ExitConditionType.ALL_CHILDREN_VISITED,
            fallback=fallback,
            max_depth=slots.depth,
        )

    def _build_static_nodes(self, slots: IntentSlots) -> Dict[str, TraversalNode]:
        """Build static nodes for target_path scope."""
        nodes = {}
        segments = self._parse_target_path(slots.target or "")

        # Build path incrementally
        current_path = []
        for idx, segment in enumerate(segments):
            current_path.append(segment)
            node_id = f"static_child_{idx}"

            # Determine if this is the last segment (leaf)
            is_last = idx == len(segments) - 1

            if is_last:
                # Last segment is a leaf action
                node = TraversalNode(
                    node_id=node_id,
                    name=segment,
                    node_type=NodeType.LEAF_ACTION,
                    operation=Operation(
                        action="click",
                        target=Target(by="text", value=segment),
                    ),
                    precondition=Precondition(path=list(current_path)),
                    children_strategy=ChildrenStrategy(type=ChildrenStrategyType.NONE),
                )
            else:
                # Middle segments are containers
                node = TraversalNode(
                    node_id=node_id,
                    name=segment,
                    node_type=NodeType.CONTAINER,
                    operation=Operation(action="no_action"),
                    precondition=Precondition(path=list(current_path)),
                    children_strategy=ChildrenStrategy(
                        type=ChildrenStrategyType.STATIC,
                        static_children=[f"static_child_{idx + 1}"],
                    ),
                )

            nodes[node_id] = node

        return nodes

    def _parse_target_path(self, target: str) -> List[str]:
        """Parse target path into segments."""
        if not target:
            return []

        # Split by "/" and trim whitespace
        segments = [s.strip() for s in target.split("/")]
        return [s for s in segments if s]
