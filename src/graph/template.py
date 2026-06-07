"""
Template registry system for dynamic node instantiation.

This module provides the template registry that allows loading node templates
from JSON files and instantiating them with runtime data.
"""

import json
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional

from .node import (
    ChildrenStrategy,
    ChildrenStrategyType,
    DynamicRule,
    ErrorPolicy,
    NodeType,
    Operation,
    Precondition,
    RestoreAction,
    Target,
    TraversalNode,
)


class TemplateRegistryError(Exception):
    """Base exception for template registry errors."""

    pass


@dataclass
class Template:
    """
    Template definition for node instantiation.

    A template defines a reusable pattern for creating TraversalNode instances,
    with placeholders that can be filled with runtime values.
    """

    template_id: str
    node_type: NodeType
    operation: Dict[str, Any]
    precondition: Optional[Dict[str, Any]] = None
    children_strategy: Optional[Dict[str, Any]] = None
    error_policy: Optional[Dict[str, Any]] = None
    meta: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        """Convert template to dictionary representation."""
        result = {
            "node_type": self.node_type.value,
            "operation": self.operation,
            "meta": self.meta,
        }
        if self.precondition:
            result["precondition"] = self.precondition
        if self.children_strategy:
            result["children_strategy"] = self.children_strategy
        if self.error_policy:
            result["error_policy"] = self.error_policy
        return result


class PlaceholderResolver:
    """
    Resolves placeholders in template definitions.

    Supported placeholders:
    - {{item_text}} - UI element text content
    - {{item_index}} - UI element index
    - {{coordinate_x}} - X coordinate
    - {{coordinate_y}} - Y coordinate
    - {{parent_id}} - Parent node ID
    """

    PLACEHOLDER_PATTERN = re.compile(r"\{\{(\w+)\}\}")

    SUPPORTED_PLACEHOLDERS = {
        "item_text",
        "item_index",
        "coordinate_x",
        "coordinate_y",
        "parent_id",
    }

    @classmethod
    def resolve(cls, value: Any, context: Dict[str, Any]) -> Any:
        """
        Resolve placeholders in a value.

        Args:
            value: The value containing placeholders (can be dict, list, or str)
            context: Mapping of placeholder names to actual values

        Returns:
            The value with placeholders resolved
        """
        if isinstance(value, str):
            return cls._resolve_string(value, context)
        elif isinstance(value, dict):
            return {k: cls.resolve(v, context) for k, v in value.items()}
        elif isinstance(value, list):
            return [cls.resolve(item, context) for item in value]
        else:
            return value

    @classmethod
    def _resolve_string(cls, text: str, context: Dict[str, Any]) -> str:
        """Resolve placeholders in a string."""
        matches = cls.PLACEHOLDER_PATTERN.findall(text)
        if not matches:
            return text

        result = text
        for placeholder in matches:
            if placeholder not in cls.SUPPORTED_PLACEHOLDERS:
                raise ValueError(f"Unsupported placeholder: {{{{placeholder}}}}")
            if placeholder not in context:
                raise ValueError(f"Missing value for placeholder: {{{{placeholder}}}}")
            result = result.replace(f"{{{{{placeholder}}}}}", str(context[placeholder]))

        return result


class TemplateInstantiator:
    """
    Instantiates TraversalNode from templates.

    Takes a template and context data, resolves placeholders, and creates
    a concrete TraversalNode instance.
    """

    def __init__(self, resolver: PlaceholderResolver = None):
        self.resolver = resolver or PlaceholderResolver()

    def instantiate(
        self,
        template: Template,
        context: Dict[str, Any],
        parent_path: Optional[List[str]] = None,
    ) -> TraversalNode:
        """
        Create a TraversalNode from a template.

        Args:
            template: The template to instantiate
            context: Runtime data for placeholder resolution
            parent_path: Optional parent path for concatenation (V6.9)

        Returns:
            A concrete TraversalNode instance
        """
        # Generate unique node_id
        node_id = self._generate_node_id(template, context)

        # Resolve all placeholders
        resolved = self.resolver.resolve(template.to_dict(), context)

        # Create node components
        operation = self._create_operation(resolved["operation"])
        precondition = (
            self._create_precondition(resolved["precondition"])
            if "precondition" in resolved
            else None
        )
        children_strategy = (
            self._create_children_strategy(resolved.get("children_strategy", {}))
            if "children_strategy" in resolved
            else None
        )
        error_policy = (
            self._create_error_policy(resolved["error_policy"])
            if "error_policy" in resolved
            else None
        )

        # Create the node
        node = TraversalNode(
            node_id=node_id,
            name=context.get("name", resolved.get("name", node_id)),
            node_type=NodeType(resolved["node_type"]),
            operation=operation,
            precondition=precondition,
            children_strategy=children_strategy
            or ChildrenStrategy(type=ChildrenStrategyType.NONE),
            error_policy=error_policy,
            meta=resolved.get("meta", {}),
        )

        # V6.9: Path concatenation
        if node.precondition:
            if parent_path:
                node.precondition.path = parent_path + [node.name]
            else:
                # When no parent_path, use node name as the path
                node.precondition.path = [node.name]

        return node

    def _generate_node_id(self, template: Template, context: Dict[str, Any]) -> str:
        """Generate a unique node ID from template and context."""
        # Use template_id + context identifiers
        parts = [template.template_id]

        # Add identifying context
        for key in ["item_text", "item_index", "parent_id"]:
            if key in context:
                parts.append(str(context[key]))

        return "-".join(parts)

    def _create_operation(self, op_dict: Dict[str, Any]) -> Operation:
        """Create Operation from dictionary."""
        target = None
        if "target" in op_dict:
            target_dict = op_dict["target"]
            target = Target(
                by=target_dict["by"], value=target_dict["value"], meta=target_dict.get("meta", {})
            )

        restore = None
        if "restore" in op_dict:
            restore_dict = op_dict["restore"]
            restore_target = None
            if "target" in restore_dict:
                rt_dict = restore_dict["target"]
                restore_target = Target(
                    by=rt_dict["by"], value=rt_dict["value"], meta=rt_dict.get("meta", {})
                )
            restore = RestoreAction(
                action=restore_dict["action"],
                target=restore_target,
                params=restore_dict.get("params", {}),
            )

        return Operation(
            action=op_dict["action"],
            target=target,
            params=op_dict.get("params", {}),
            restore=restore,
        )

    def _create_precondition(self, pc_dict: Dict[str, Any]) -> Precondition:
        """Create Precondition from dictionary."""
        return Precondition(
            page_name=pc_dict.get("page_name"),
            path=pc_dict.get("path"),
            ui_condition=pc_dict.get("ui_condition"),
            timeout_seconds=pc_dict.get("timeout_seconds", 5.0),
        )

    def _create_children_strategy(self, cs_dict: Dict[str, Any]) -> ChildrenStrategy:
        """Create ChildrenStrategy from dictionary."""
        strategy_type = ChildrenStrategyType(cs_dict.get("type", "none"))

        dynamic_rules = {}
        if "dynamic_rules" in cs_dict:
            for rule_id, rule_dict in cs_dict["dynamic_rules"].items():
                dynamic_rules[rule_id] = DynamicRule(
                    rule_id=rule_id,
                    match_condition=rule_dict["match_condition"],
                    child_template=rule_dict["child_template"],
                    action=rule_dict.get("action", "generate_child"),
                )

        return ChildrenStrategy(
            type=strategy_type,
            static_children=cs_dict.get("static_children", []),
            dynamic_rules=dynamic_rules,
            max_children=cs_dict.get("max_children", 100),
        )

    def _create_error_policy(self, ep_dict: Dict[str, Any]) -> ErrorPolicy:
        """Create ErrorPolicy from dictionary."""
        return ErrorPolicy(
            on_error=ep_dict["on_error"],
            max_retries=ep_dict.get("max_retries", 1),
            fallback_target=ep_dict.get("fallback_target"),
            continue_on_error=ep_dict.get("continue_on_error", False),
        )


class TemplateValidator:
    """
    Validates template definitions.

    Checks template structure, references, and placeholders.
    """

    def validate(self, template: Template) -> List[str]:
        """
        Validate a template.

        Returns:
            List of validation warnings (empty if valid)
        """
        warnings = []

        # Check required fields
        if not template.template_id:
            warnings.append("Template missing template_id")

        # Check operation
        if not template.operation:
            warnings.append("Template missing operation")
        elif "action" not in template.operation:
            warnings.append("Operation missing action field")

        # Check placeholders
        placeholders = self._extract_placeholders(template)
        for ph in placeholders:
            if ph not in PlaceholderResolver.SUPPORTED_PLACEHOLDERS:
                warnings.append(f"Unsupported placeholder: {{{{ph}}}}")

        # Check node type
        if isinstance(template.node_type, str):
            try:
                NodeType(template.node_type)
            except ValueError:
                warnings.append(f"Invalid node_type: {template.node_type}")

        return warnings

    def _extract_placeholders(self, template: Template) -> set:
        """Extract all placeholders used in template."""
        placeholders = set()

        def extract_from_value(value):
            if isinstance(value, str):
                matches = PlaceholderResolver.PLACEHOLDER_PATTERN.findall(value)
                placeholders.update(matches)
            elif isinstance(value, dict):
                for v in value.values():
                    extract_from_value(v)
            elif isinstance(value, list):
                for item in value:
                    extract_from_value(item)

        extract_from_value(template.operation)
        if template.precondition:
            extract_from_value(template.precondition)
        if template.children_strategy:
            extract_from_value(template.children_strategy)
        if template.error_policy:
            extract_from_value(template.error_policy)

        return placeholders


class TemplateRegistry:
    """
    Registry for managing node templates.

    Loads templates from JSON files and provides access for instantiation.
    """

    DEFAULT_TEMPLATES: Dict[str, Dict[str, Any]] = {
        "menu_container": {
            "node_type": "container",  # V6.9.3: Use container to allow child generation
            "operation": {"action": "click", "target": {"by": "text", "value": "{{item_text}}"}},
            "precondition": {
                "page_name": None,  # Will be set dynamically based on current page
                "timeout_seconds": 5.0,
            },
            "children_strategy": {
                "type": "dynamic_match",  # V6.9.3: Enable dynamic child generation for multi-layer traversal
                "dynamic_rules": {
                    # Only generate leaf nodes (switches, sliders) - no more containers
                    "switch_rule": {
                        "match_condition": {"type": "switch"},
                        "child_template": "switch_leaf",
                    },
                    "slider_rule": {
                        "match_condition": {"type": "slider"},
                        "child_template": "slider_leaf",
                    },
                },
            },
            "error_policy": {"on_error": "skip", "continue_on_error": True},
        },
        "switch_leaf": {
            "node_type": "leaf_switch",
            "operation": {
                "action": "click",
                "target": {"by": "text", "value": "{{item_text}}"},
                "restore": {"action": "click", "target": {"by": "text", "value": "{{item_text}}"}},
            },
            "children_strategy": {"type": "none"},
            "error_policy": {"on_error": "skip"},
        },
        "slider_leaf": {
            "node_type": "leaf_slider",
            "operation": {
                "action": "swipe",
                "target": {"by": "coordinate", "value": ["{{coordinate_x}}", "{{coordinate_y}}"]},
                "params": {"direction": "right", "distance": 0.1},
                "restore": {
                    "action": "swipe",
                    "target": {"by": "coordinate", "value": ["{{coordinate_x}}", "{{coordinate_y}}"]},
                    "params": {"direction": "left", "distance": 0.1},
                },
            },
            "children_strategy": {"type": "none"},
            "error_policy": {"on_error": "skip"},
        },
    }

    def __init__(self, custom_path: Optional[Path] = None):
        """
        Initialize the template registry.

        Args:
            custom_path: Optional path to custom template JSON file
        """
        self.templates: Dict[str, Template] = {}
        self.instantiator = TemplateInstantiator()
        self.validator = TemplateValidator()

        # Load built-in defaults
        self._load_builtin_templates()

        # Load custom templates if path provided
        if custom_path:
            self.load_from_file(custom_path)

    def _load_builtin_templates(self):
        """Load built-in default templates."""
        for template_id, template_dict in self.DEFAULT_TEMPLATES.items():
            self.templates[template_id] = Template(
                template_id=template_id,
                node_type=NodeType(template_dict["node_type"]),
                operation=template_dict["operation"],
                precondition=template_dict.get("precondition"),
                children_strategy=template_dict.get("children_strategy"),
                error_policy=template_dict.get("error_policy"),
                meta=template_dict.get("meta", {}),
            )

    def load_from_file(self, path: Path) -> None:
        """
        Load templates from a JSON file.

        Args:
            path: Path to the JSON file

        Raises:
            TemplateRegistryError: If file cannot be loaded or parsed
        """
        try:
            with open(path, "r", encoding="utf-8") as f:
                data = json.load(f)
        except FileNotFoundError:
            raise TemplateRegistryError(f"Template file not found: {path}")
        except json.JSONDecodeError as e:
            raise TemplateRegistryError(f"Invalid JSON in template file: {e}")

        if "templates" not in data:
            raise TemplateRegistryError("Missing 'templates' key in JSON file")

        for template_id, template_dict in data["templates"].items():
            try:
                template = Template(
                    template_id=template_id,
                    node_type=NodeType(template_dict["node_type"]),
                    operation=template_dict["operation"],
                    precondition=template_dict.get("precondition"),
                    children_strategy=template_dict.get("children_strategy"),
                    error_policy=template_dict.get("error_policy"),
                    meta=template_dict.get("meta", {}),
                )

                # Validate
                warnings = self.validator.validate(template)
                for warning in warnings:
                    print(f"Template validation warning for {template_id}: {warning}")

                self.templates[template_id] = template
            except Exception as e:
                raise TemplateRegistryError(f"Error loading template {template_id}: {e}")

    def get_template(self, template_id: str) -> Optional[Template]:
        """Get a template by ID."""
        return self.templates.get(template_id)

    def instantiate(
        self,
        template_id: str,
        context: Dict[str, Any],
        parent_path: Optional[List[str]] = None,
    ) -> Optional[TraversalNode]:
        """
        Instantiate a template with context data.

        Args:
            template_id: ID of the template to instantiate
            context: Runtime data for placeholder resolution
            parent_path: Optional parent path for concatenation (V6.9)

        Returns:
            TraversalNode instance or None if template not found
        """
        template = self.get_template(template_id)
        if not template:
            return None

        return self.instantiator.instantiate(template, context, parent_path)

    def list_templates(self) -> List[str]:
        """List all available template IDs."""
        return list(self.templates.keys())

    def validate_all(self) -> Dict[str, List[str]]:
        """
        Validate all loaded templates.

        Returns:
            Mapping of template_id to list of warnings
        """
        results = {}
        for template_id, template in self.templates.items():
            warnings = self.validator.validate(template)
            if warnings:
                results[template_id] = warnings
        return results
