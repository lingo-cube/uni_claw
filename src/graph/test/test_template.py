"""
Unit tests for template registry system.

Tests cover:
- Template loading
- Placeholder replacement
- Template instantiation
- Dynamic matcher
- Built-in default templates
"""

import json
import sys
import pytest
from pathlib import Path

# 添加项目根目录到 sys.path
sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from src.graph.node import NodeType, ChildrenStrategyType, TraversalNode
from src.graph.template import (
    Template,
    TemplateRegistry,
    TemplateRegistryError,
    PlaceholderResolver,
    TemplateInstantiator,
    TemplateValidator,
)
from src.graph.matcher import DynamicMatcher, MatchAction


class TestPlaceholderResolver:
    """Tests for PlaceholderResolver."""

    def test_resolve_item_text(self):
        """Test resolving {{item_text}} placeholder."""
        result = PlaceholderResolver.resolve("Click {{item_text}}", {"item_text": "Settings"})
        assert result == "Click Settings"

    def test_resolve_item_index(self):
        """Test resolving {{item_index}} placeholder."""
        result = PlaceholderResolver.resolve("Item {{item_index}}", {"item_index": 5})
        assert result == "Item 5"

    def test_resolve_coordinates(self):
        """Test resolving coordinate placeholders."""
        result = PlaceholderResolver.resolve(
            "Position: {{coordinate_x}}, {{coordinate_y}}",
            {"coordinate_x": 100, "coordinate_y": 200},
        )
        assert result == "Position: 100, 200"

    def test_resolve_parent_id(self):
        """Test resolving {{parent_id}} placeholder."""
        result = PlaceholderResolver.resolve("Child of {{parent_id}}", {"parent_id": "root"})
        assert result == "Child of root"

    def test_resolve_dict(self):
        """Test resolving placeholders in dictionary."""
        input_dict = {
            "target": {"by": "text", "value": "{{item_text}}"},
            "action": "click",
        }
        result = PlaceholderResolver.resolve(input_dict, {"item_text": "Submit"})
        assert result["target"]["value"] == "Submit"

    def test_resolve_list(self):
        """Test resolving placeholders in list."""
        input_list = ["{{item_text}}", "{{item_index}}"]
        result = PlaceholderResolver.resolve(input_list, {"item_text": "Test", "item_index": 1})
        assert result == ["Test", "1"]

    def test_no_placeholders(self):
        """Test string without placeholders."""
        result = PlaceholderResolver.resolve("No placeholders here", {})
        assert result == "No placeholders here"

    def test_unsupported_placeholder_raises_error(self):
        """Test that unsupported placeholder raises error."""
        with pytest.raises(ValueError, match="Unsupported placeholder"):
            PlaceholderResolver.resolve("{{unsupported}}", {})

    def test_missing_value_raises_error(self):
        """Test that missing placeholder value raises error."""
        with pytest.raises(ValueError, match="Missing value"):
            PlaceholderResolver.resolve("{{item_text}}", {})


class TestTemplate:
    """Tests for Template class."""

    def test_create_template(self):
        """Test creating a template."""
        template = Template(
            template_id="test_template",
            node_type=NodeType.CONTAINER,
            operation={"action": "click", "target": {"by": "text", "value": "{{item_text}}"}},
        )
        assert template.template_id == "test_template"
        assert template.node_type == NodeType.CONTAINER

    def test_template_to_dict(self):
        """Test converting template to dictionary."""
        template = Template(
            template_id="test",
            node_type=NodeType.LEAF_SWITCH,
            operation={"action": "click"},
            meta={"description": "Test template"},
        )
        result = template.to_dict()
        assert result["node_type"] == "leaf_switch"
        assert result["operation"]["action"] == "click"
        assert result["meta"]["description"] == "Test template"


class TestTemplateValidator:
    """Tests for TemplateValidator."""

    def test_valid_template_no_warnings(self):
        """Test validator with valid template."""
        template = Template(
            template_id="valid",
            node_type=NodeType.CONTAINER,
            operation={"action": "click"},
        )
        validator = TemplateValidator()
        warnings = validator.validate(template)
        assert len(warnings) == 0

    def test_missing_template_id(self):
        """Test validator catches missing template_id."""
        template = Template(
            template_id="",
            node_type=NodeType.LEAF_ACTION,
            operation={"action": "click"},
        )
        validator = TemplateValidator()
        warnings = validator.validate(template)
        assert any("template_id" in w for w in warnings)

    def test_missing_operation_action(self):
        """Test validator catches missing action in operation."""
        template = Template(
            template_id="test",
            node_type=NodeType.LEAF_ACTION,
            operation={},
        )
        validator = TemplateValidator()
        warnings = validator.validate(template)
        # Empty dict is treated as missing operation
        assert any("operation" in w for w in warnings)

    def test_unsupported_placeholder_warning(self):
        """Test validator warns about unsupported placeholders."""
        template = Template(
            template_id="test",
            node_type=NodeType.LEAF_ACTION,
            operation={"action": "{{unsupported}}"},
        )
        validator = TemplateValidator()
        warnings = validator.validate(template)
        assert any("Unsupported placeholder" in w for w in warnings)

    def test_extract_placeholders(self):
        """Test placeholder extraction from template."""
        template = Template(
            template_id="test",
            node_type=NodeType.LEAF_ACTION,
            operation={
                "action": "click",
                "target": {"by": "text", "value": "{{item_text}}"},
            },
        )
        validator = TemplateValidator()
        placeholders = validator._extract_placeholders(template)
        assert "item_text" in placeholders


class TestTemplateInstantiator:
    """Tests for TemplateInstantiator."""

    def test_instantiate_simple_template(self):
        """Test instantiating a simple template."""
        template = Template(
            template_id="simple",
            node_type=NodeType.LEAF_ACTION,
            operation={"action": "click", "target": {"by": "text", "value": "{{item_text}}"}},
        )
        instantiator = TemplateInstantiator()
        node = instantiator.instantiate(template, {"item_text": "Submit", "name": "Submit Button"})

        assert isinstance(node, TraversalNode)
        assert node.name == "Submit Button"
        assert node.operation.action == "click"
        assert node.operation.target.value == "Submit"

    def test_generate_node_id(self):
        """Test node ID generation."""
        template = Template(
            template_id="menu_template",
            node_type=NodeType.CONTAINER,
            operation={"action": "click"},
            children_strategy={"type": "dynamic_match"},
        )
        instantiator = TemplateInstantiator()
        node = instantiator.instantiate(
            template,
            {"item_text": "Settings", "item_index": 2},
        )
        assert "menu_template" in node.node_id
        assert "Settings" in node.node_id

    def test_instantiate_with_precondition(self):
        """Test instantiating template with precondition."""
        template = Template(
            template_id="test",
            node_type=NodeType.LEAF_ACTION,
            operation={"action": "click"},
            precondition={"page_name": "{{item_text}}"},
        )
        instantiator = TemplateInstantiator()
        node = instantiator.instantiate(template, {"item_text": "SettingsPage"})

        assert node.precondition is not None
        assert node.precondition.page_name == "SettingsPage"

    def test_instantiate_with_children_strategy(self):
        """Test instantiating template with children strategy."""
        template = Template(
            template_id="container",
            node_type=NodeType.CONTAINER,
            operation={"action": "click"},
            children_strategy={"type": "dynamic_match"},
        )
        instantiator = TemplateInstantiator()
        node = instantiator.instantiate(template, {})

        assert node.children_strategy.type == ChildrenStrategyType.DYNAMIC_MATCH

    def test_instantiate_with_restore_action(self):
        """Test instantiating template with restore action."""
        template = Template(
            template_id="toggle",
            node_type=NodeType.LEAF_SWITCH,
            operation={
                "action": "click",
                "restore": {"action": "click"},
            },
        )
        instantiator = TemplateInstantiator()
        node = instantiator.instantiate(template, {"item_text": "WiFi"})

        assert node.operation.restore is not None
        assert node.operation.restore.action == "click"


class TestTemplateRegistry:
    """Tests for TemplateRegistry."""

    def test_builtin_templates_loaded(self):
        """Test that built-in templates are loaded."""
        registry = TemplateRegistry()
        templates = registry.list_templates()
        assert "menu_container" in templates
        assert "switch_leaf" in templates
        assert "slider_leaf" in templates

    def test_get_template(self):
        """Test getting a template by ID."""
        registry = TemplateRegistry()
        template = registry.get_template("menu_container")
        assert template is not None
        assert template.template_id == "menu_container"
        assert template.node_type == NodeType.CONTAINER

    def test_instantiate_from_registry(self):
        """Test instantiating a node from registry."""
        registry = TemplateRegistry()
        node = registry.instantiate(
            "switch_leaf",
            {"item_text": "Airplane Mode", "name": "Airplane Mode Switch"},
        )
        assert isinstance(node, TraversalNode)
        assert node.name == "Airplane Mode Switch"
        assert node.node_type == NodeType.LEAF_SWITCH

    def test_load_from_valid_json(self, tmp_path):
        """Test loading templates from JSON file."""
        template_data = {
            "templates": {
                "custom_container": {
                    "node_type": "container",
                    "operation": {"action": "click"},
                }
            }
        }
        json_file = tmp_path / "templates.json"
        with open(json_file, "w") as f:
            json.dump(template_data, f)

        registry = TemplateRegistry()
        registry.load_from_file(json_file)

        assert "custom_container" in registry.list_templates()

    def test_load_from_missing_file_raises_error(self, tmp_path):
        """Test loading from missing file raises error."""
        registry = TemplateRegistry()
        with pytest.raises(TemplateRegistryError, match="not found"):
            registry.load_from_file(tmp_path / "nonexistent.json")

    def test_load_from_invalid_json_raises_error(self, tmp_path):
        """Test loading from invalid JSON raises error."""
        json_file = tmp_path / "invalid.json"
        with open(json_file, "w") as f:
            f.write("not valid json {]")

        registry = TemplateRegistry()
        with pytest.raises(TemplateRegistryError, match="Invalid JSON"):
            registry.load_from_file(json_file)

    def test_load_json_missing_templates_key_raises_error(self, tmp_path):
        """Test JSON without 'templates' key raises error."""
        json_file = tmp_path / "no_templates.json"
        with open(json_file, "w") as f:
            json.dump({"other_key": {}}, f)

        registry = TemplateRegistry()
        with pytest.raises(TemplateRegistryError, match="templates"):
            registry.load_from_file(json_file)

    def test_validate_all(self):
        """Test validating all templates."""
        registry = TemplateRegistry()
        results = registry.validate_all()
        # Built-in templates should be valid
        assert isinstance(results, dict)


class TestDynamicMatcher:
    """Tests for DynamicMatcher."""

    def test_match_menu_item(self):
        """Test matching a menu item."""
        registry = TemplateRegistry()
        matcher = DynamicMatcher(registry)

        matcher.load_rules({
            "menu_rule": {
                "match_condition": {"type": "menu_item"},
                "child_template": "menu_container",
            }
        })

        menu_item = {"type": "menu_item", "text": "Settings", "index": 0}
        parent_node = registry.instantiate("menu_container", {"item_text": "Home"})

        result = matcher.match(menu_item, parent_node)
        assert result.matched
        assert result.rule_id == "menu_rule"
        assert result.template_id == "menu_container"
        assert result.action == MatchAction.GENERATE_CHILD

    def test_no_match(self):
        """Test when no rule matches."""
        registry = TemplateRegistry()
        matcher = DynamicMatcher(registry)

        matcher.load_rules({
            "menu_rule": {
                "match_condition": {"type": "menu_item"},
                "child_template": "menu_container",
            }
        })

        menu_item = {"type": "button", "text": "Submit"}
        parent_node = registry.instantiate("menu_container", {"item_text": "Home"})

        result = matcher.match(menu_item, parent_node)
        assert not result.matched

    def test_match_all(self):
        """Test matching multiple items."""
        registry = TemplateRegistry()
        matcher = DynamicMatcher(registry)

        matcher.load_rules({
            "menu_rule": {
                "match_condition": {"type": "menu_item"},
                "child_template": "menu_container",
            }
        })

        menu_items = [
            {"type": "menu_item", "text": "Settings", "index": 0},
            {"type": "button", "text": "Submit"},
            {"type": "menu_item", "text": "Display", "index": 1},
        ]
        parent_node = registry.instantiate("menu_container", {"item_text": "Home"})

        results = matcher.match_all(menu_items, parent_node)
        assert len(results) == 3
        assert results[0].matched
        assert not results[1].matched
        assert results[2].matched

    def test_instantiate_match(self):
        """Test instantiating node from match result."""
        registry = TemplateRegistry()
        matcher = DynamicMatcher(registry)

        matcher.load_rules({
            "switch_rule": {
                "match_condition": {"type": "switch"},
                "child_template": "switch_leaf",
            }
        })

        switch_item = {"type": "switch", "text": "WiFi", "index": 2}
        parent_node = registry.instantiate("menu_container", {"item_text": "Settings"})

        result = matcher.match(switch_item, parent_node)
        node = matcher.instantiate_match(result)

        assert isinstance(node, TraversalNode)
        assert node.node_type == NodeType.LEAF_SWITCH
        assert "WiFi" in node.node_id

    def test_match_with_text_pattern(self):
        """Test matching with text pattern condition."""
        registry = TemplateRegistry()
        matcher = DynamicMatcher(registry)

        matcher.load_rules({
            "brightness_rule": {
                "match_condition": {
                    "type": "slider",
                    "text_pattern": r"Brightness|亮度",
                },
                "child_template": "slider_leaf",
            }
        })

        slider_item = {"type": "slider", "text": "Brightness", "index": 0}
        parent_node = registry.instantiate("menu_container", {"item_text": "Display"})

        result = matcher.match(slider_item, parent_node)
        assert result.matched

    def test_match_with_index_range(self):
        """Test matching with index range condition."""
        registry = TemplateRegistry()
        matcher = DynamicMatcher(registry)

        matcher.load_rules({
            "first_item": {
                "match_condition": {"type": "menu_item", "max_index": 0},
                "child_template": "menu_container",
            }
        })

        first_item = {"type": "menu_item", "text": "First", "index": 0}
        parent_node = registry.instantiate("menu_container", {"item_text": "Root"})

        result = matcher.match(first_item, parent_node)
        assert result.matched

        # Second item should not match
        second_item = {"type": "menu_item", "text": "Second", "index": 1}
        result = matcher.match(second_item, parent_node)
        assert not result.matched

    def test_skip_action(self):
        """Test skip action in rule."""
        registry = TemplateRegistry()
        matcher = DynamicMatcher(registry)

        matcher.load_rules({
            "skip_disabled": {
                "match_condition": {"enabled": False},
                "child_template": "none",
                "action": "skip",
            }
        })

        disabled_item = {"type": "menu_item", "enabled": False}
        parent_node = registry.instantiate("menu_container", {"item_text": "Root"})

        result = matcher.match(disabled_item, parent_node)
        assert result.matched
        assert result.action == MatchAction.SKIP

    def test_match_history(self):
        """Test match history tracking."""
        registry = TemplateRegistry()
        matcher = DynamicMatcher(registry)

        matcher.load_rules({
            "rule1": {
                "match_condition": {"type": "menu_item"},
                "child_template": "menu_container",
            }
        })

        menu_item = {"type": "menu_item", "text": "Test", "index": 0}
        parent_node = registry.instantiate("menu_container", {"item_text": "Root"})

        matcher.match(menu_item, parent_node)

        history = matcher.get_match_history()
        assert len(history) == 1
        assert history[0]["rule_id"] == "rule1"

    def test_match_statistics(self):
        """Test match statistics."""
        registry = TemplateRegistry()
        matcher = DynamicMatcher(registry)

        matcher.load_rules({
            "menu_rule": {
                "match_condition": {"type": "menu_item"},
                "child_template": "menu_container",
            }
        })

        menu_items = [
            {"type": "menu_item", "text": "A", "index": 0},
            {"type": "menu_item", "text": "B", "index": 1},
        ]
        parent_node = registry.instantiate("menu_container", {"item_text": "Root"})

        matcher.match_all(menu_items, parent_node)

        stats = matcher.get_statistics()
        assert stats["total"] == 2
        assert "menu_rule" in stats["by_rule"]
        assert stats["by_rule"]["menu_rule"] == 2

    def test_clear_history(self):
        """Test clearing match history."""
        registry = TemplateRegistry()
        matcher = DynamicMatcher(registry)

        matcher.load_rules({
            "rule1": {
                "match_condition": {"type": "menu_item"},
                "child_template": "menu_container",
            }
        })

        menu_item = {"type": "menu_item", "text": "Test", "index": 0}
        parent_node = registry.instantiate("menu_container", {"item_text": "Root"})

        matcher.match(menu_item, parent_node)
        matcher.clear_history()

        assert len(matcher.get_match_history()) == 0
