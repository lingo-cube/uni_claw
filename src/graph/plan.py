"""
Traversal plan model for V6 graph-based traversal.

This module defines the TraversalPlan class, which serves as the top-level
container for declarative traversal plan definitions.
"""

import json
from dataclasses import asdict, dataclass, field
from typing import TYPE_CHECKING, Any, Dict, List, Optional

if TYPE_CHECKING:
    from .node import Operation, TraversalNode

from .node import (
    CompletionPolicy,
    CompletionPolicyType,
    DynamicRule,
    EntryConfig,
    EntryPolicy,
    EntryStrategy,
    ErrorPolicy,
    ExitCondition,
    ExitConditionType,
    FallbackAction,
    IntentSlots,
    MatchMode,
    NodeType,
    TargetFoundAction,
    TraversalMode,
    TraversalNode,
)


@dataclass
class TraversalPlan:
    """
    Top-level container for declarative traversal plan.

    Defines the complete traversal strategy including entry policy,
    completion policy, and the root node for graph-based traversal.

    Attributes:
        entry_app: Target application name (required)
        entry_policy: How to enter the target application
        entry_config: Entry configuration (wait mode, delays, trace level)
        root_node: Root traversal node
        static_nodes: Static node registry for ID references
        template_registry: Path to template registry JSON
        mode: Traversal execution mode
        completion_policy: Global completion policy
        intent_slots: AI-extracted intent slots
        meta: Additional metadata
    """

    entry_app: str  # Target application name
    plan_name: str = ""  # Human-readable name for dashboard identification
    plan_id: str = ""    # Unique plan identifier for trace grouping
    entry_policy: EntryPolicy = field(default_factory=EntryPolicy)
    entry_config: Optional[EntryConfig] = None  # V6.8: Entry configuration
    root_node: Optional[TraversalNode] = None
    static_nodes: Dict[str, TraversalNode] = field(default_factory=dict)
    template_registry: Optional[str] = None  # Path to template registry JSON
    mode: TraversalMode = TraversalMode.HYBRID
    completion_policy: CompletionPolicy = field(default_factory=CompletionPolicy)
    intent_slots: Optional[IntentSlots] = None
    meta: Dict[str, Any] = field(default_factory=dict)

    def __post_init__(self):
        """Validate traversal plan configuration."""
        if not self.entry_app:
            raise ValueError("entry_app cannot be empty")

    def to_json(self) -> str:
        """
        Serialize the traversal plan to JSON string.

        Returns:
            JSON string representation of the plan

        Raises:
            TypeError: If plan contains non-serializable objects
        """
        # Convert dataclass to dict, handling nested dataclasses
        plan_dict = self._to_dict()

        # Convert to JSON string
        return json.dumps(plan_dict, indent=2, ensure_ascii=False)

    def _to_dict(self) -> Dict[str, Any]:
        """Convert the plan to a dictionary for serialization."""
        result = {
            "entry_app": self.entry_app,
            "mode": self.mode.value if isinstance(self.mode, TraversalMode) else self.mode,
            "meta": self.meta,
        }

        # Add entry_policy
        if self.entry_policy:
            result["entry_policy"] = self._serialize_dataclass(self.entry_policy)

        # Add entry_config (V6.8)
        if self.entry_config:
            result["entry_config"] = self._serialize_dataclass(self.entry_config)

        # Add root_node
        if self.root_node:
            result["root_node"] = self._serialize_node(self.root_node)

        # Add static_nodes
        if self.static_nodes:
            result["static_nodes"] = {
                node_id: self._serialize_node(node)
                for node_id, node in self.static_nodes.items()
            }

        # Add template_registry
        if self.template_registry:
            result["template_registry"] = self.template_registry

        # Add completion_policy
        if self.completion_policy:
            result["completion_policy"] = self._serialize_dataclass(self.completion_policy)

        # Add intent_slots
        if self.intent_slots:
            result["intent_slots"] = self._serialize_dataclass(self.intent_slots)

        return result

    def _serialize_node(self, node: "TraversalNode") -> Dict[str, Any]:
        """Serialize a TraversalNode to dict."""
        from .node import (
            ChildrenStrategyType,
            DynamicRule,
            ErrorPolicy,
            ExitCondition,
            NodeType,
            Operation,
            Precondition,
            RestoreAction,
            Target,
        )

        result = {
            "node_id": node.node_id,
            "name": node.name,
            "node_type": node.node_type.value if isinstance(node.node_type, NodeType) else node.node_type,
            "operation": self._serialize_operation(node.operation),
            "meta": node.meta,
        }

        # Add precondition
        if node.precondition:
            result["precondition"] = self._serialize_dataclass(node.precondition)

        # Add children_strategy
        if node.children_strategy:
            strategy_dict = {
                "type": node.children_strategy.type.value if isinstance(
                    node.children_strategy.type, ChildrenStrategyType
                ) else node.children_strategy.type,
                "static_children": node.children_strategy.static_children,
                "max_children": node.children_strategy.max_children,
            }
            # Serialize dynamic_rules
            if node.children_strategy.dynamic_rules:
                strategy_dict["dynamic_rules"] = {
                    rule_id: self._serialize_dataclass(rule)
                    for rule_id, rule in node.children_strategy.dynamic_rules.items()
                }
            result["children_strategy"] = strategy_dict

        # Add error_policy
        if node.error_policy:
            result["error_policy"] = self._serialize_dataclass(node.error_policy)

        # Add exit_condition (V6)
        if node.exit_condition:
            result["exit_condition"] = self._serialize_dataclass(node.exit_condition)

        return result

    def _serialize_operation(self, op: "Operation") -> Dict[str, Any]:
        """Serialize an Operation to dict."""
        result = {
            "action": op.action,
            "params": op.params,
        }

        # Add target
        if op.target:
            result["target"] = self._serialize_dataclass(op.target)

        # Add restore
        if op.restore:
            result["restore"] = self._serialize_dataclass(op.restore)

        return result

    def _serialize_dataclass(self, obj) -> Dict[str, Any]:
        """Serialize a dataclass instance to dict, handling enum values."""
        if obj is None:
            return {}

        result = asdict(obj)

        # Convert enum values to strings
        def convert_enums(value):
            if hasattr(value, "value"):
                return value.value
            elif isinstance(value, dict):
                return {k: convert_enums(v) for k, v in value.items()}
            elif isinstance(value, (list, tuple)):
                return [convert_enums(item) for item in value]
            return value

        return {k: convert_enums(v) for k, v in result.items()}

    @classmethod
    def from_json(cls, json_str: str) -> "TraversalPlan":
        """
        Deserialize a traversal plan from JSON string.

        Args:
            json_str: JSON string representation of the plan

        Returns:
            TraversalPlan instance

        Raises:
            ValueError: If JSON is invalid or plan is malformed
            KeyError: If required fields are missing
        """
        try:
            data = json.loads(json_str)
        except json.JSONDecodeError as e:
            raise ValueError(f"Invalid JSON: {e}") from e

        return cls._from_dict(data)

    @classmethod
    def _from_dict(cls, data: Dict[str, Any]) -> "TraversalPlan":
        """Create a TraversalPlan from a dictionary."""

        # Extract required field
        entry_app = data.get("entry_app")
        if not entry_app:
            raise ValueError("entry_app is required")

        # Extract mode
        mode_value = data.get("mode", "hybrid")
        mode = TraversalMode.from_value(mode_value) if isinstance(mode_value, str) else mode_value

        # Extract entry_policy
        entry_policy_data = data.get("entry_policy")
        entry_policy = EntryPolicy(**entry_policy_data) if entry_policy_data else EntryPolicy()

        # Extract entry_config (V6.8)
        entry_config_data = data.get("entry_config")
        entry_config = EntryConfig(**entry_config_data) if entry_config_data else None

        # Extract completion_policy
        completion_policy_data = data.get("completion_policy")
        completion_policy = (
            cls._deserialize_completion_policy(completion_policy_data)
            if completion_policy_data
            else CompletionPolicy()
        )

        # Extract intent_slots
        intent_slots_data = data.get("intent_slots")
        intent_slots = IntentSlots(**intent_slots_data) if intent_slots_data else None

        # Extract root_node
        root_node_data = data.get("root_node")
        root_node = cls._deserialize_node(root_node_data) if root_node_data else None

        # Extract static_nodes
        static_nodes_data = data.get("static_nodes", {})
        static_nodes = {
            node_id: cls._deserialize_node(node_data)
            for node_id, node_data in static_nodes_data.items()
        }

        # Extract meta
        meta = data.get("meta", {})
        template_registry = data.get("template_registry")

        return cls(
            entry_app=entry_app,
            entry_policy=entry_policy,
            entry_config=entry_config,
            root_node=root_node,
            static_nodes=static_nodes,
            template_registry=template_registry,
            mode=mode,
            completion_policy=completion_policy,
            intent_slots=intent_slots,
            meta=meta,
        )

    @classmethod
    def _deserialize_completion_policy(cls, data: Dict[str, Any]) -> CompletionPolicy:
        """Deserialize a CompletionPolicy from dict."""

        type_value = data.get("type", "none")
        policy_type = (
            CompletionPolicyType.from_value(type_value) if isinstance(type_value, str) else type_value
        )

        match_mode_value = data.get("match_mode", "contains")
        match_mode = (
            MatchMode.from_value(match_mode_value) if isinstance(match_mode_value, str) else match_mode_value
        )

        action_value = data.get("action_on_found", "mark_and_stop")
        action_on_found = (
            TargetFoundAction.from_value(action_value) if isinstance(action_value, str) else action_value
        )

        return CompletionPolicy(
            type=policy_type,
            target_name=data.get("target_name"),
            match_mode=match_mode,
            action_on_found=action_on_found,
            timeout_seconds=data.get("timeout_seconds"),
            max_steps=data.get("max_steps"),
        )

    @classmethod
    def _deserialize_node(cls, data: Dict[str, Any]) -> "TraversalNode":
        """Deserialize a TraversalNode from dict."""
        from .node import (
            ChildrenStrategy,
            ChildrenStrategyType,
            DynamicRule,
            ErrorPolicy,
            ExitCondition,
            ExitConditionType,
            FallbackAction,
            NodeType,
            Operation,
            Precondition,
            RestoreAction,
            Target,
            TraversalNode,
        )

        node_type_value = data.get("node_type")
        node_type = (
            NodeType.from_value(node_type_value) if isinstance(node_type_value, str) else node_type_value
        )

        # Deserialize operation
        operation_data = data.get("operation", {})
        operation = cls._deserialize_operation(operation_data)

        # Deserialize precondition
        precondition_data = data.get("precondition")
        precondition = Precondition(**precondition_data) if precondition_data else None

        # Deserialize children_strategy
        children_strategy_data = data.get("children_strategy")
        if children_strategy_data:
            type_value = children_strategy_data.get("type", "none")
            strategy_type = (
                ChildrenStrategyType.from_value(type_value) if isinstance(type_value, str) else type_value
            )

            # Deserialize dynamic_rules
            dynamic_rules = {}
            for rule_id, rule_data in children_strategy_data.get("dynamic_rules", {}).items():
                dynamic_rules[rule_id] = DynamicRule(**rule_data)

            children_strategy = ChildrenStrategy(
                type=strategy_type,
                static_children=children_strategy_data.get("static_children", []),
                dynamic_rules=dynamic_rules,
                max_children=children_strategy_data.get("max_children", 100),
            )
        else:
            children_strategy = ChildrenStrategy(type=ChildrenStrategyType.NONE)

        # Deserialize error_policy
        error_policy_data = data.get("error_policy")
        error_policy = ErrorPolicy(**error_policy_data) if error_policy_data else None

        # Deserialize exit_condition (V6)
        exit_condition_data = data.get("exit_condition")
        exit_condition = None
        if exit_condition_data:
            type_value = exit_condition_data.get("type")
            condition_type = (
                ExitConditionType.from_value(type_value) if isinstance(type_value, str) else type_value
            )

            fallback_value = exit_condition_data.get("fallback", "back")
            fallback = (
                FallbackAction.from_value(fallback_value) if isinstance(fallback_value, str) else fallback_value
            )

            exit_condition = ExitCondition(
                type=condition_type,
                fallback=fallback,
                max_depth=exit_condition_data.get("max_depth"),
            )

        meta = data.get("meta", {})

        return TraversalNode(
            node_id=data["node_id"],
            name=data["name"],
            node_type=node_type,
            operation=operation,
            precondition=precondition,
            children_strategy=children_strategy,
            error_policy=error_policy,
            exit_condition=exit_condition,
            meta=meta,
        )

    @classmethod
    def _deserialize_operation(cls, data: Dict[str, Any]):  # Returns Operation
        """Deserialize an Operation from dict."""
        from .node import Operation, RestoreAction, Target

        # Deserialize target
        target_data = data.get("target")
        target = Target(**target_data) if target_data else None

        # Deserialize restore
        restore_data = data.get("restore")
        restore = RestoreAction(**restore_data) if restore_data else None

        return Operation(
            action=data.get("action", "no_action"),
            target=target,
            params=data.get("params", {}),
            restore=restore,
        )

    def get_node_by_id(self, node_id: str) -> Optional[TraversalNode]:
        """
        Get a node by ID from static nodes registry.

        Args:
            node_id: Node identifier to look up

        Returns:
            TraversalNode if found, None otherwise
        """
        return self.static_nodes.get(node_id)

    def add_static_node(self, node: TraversalNode) -> None:
        """
        Add a node to the static nodes registry.

        Args:
            node: TraversalNode to register
        """
        self.static_nodes[node.node_id] = node

    def has_completion_policy(self) -> bool:
        """Check if the plan has a non-default completion policy."""
        return self.completion_policy.type != CompletionPolicyType.NONE
