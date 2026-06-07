"""
Tests for V6.9 Plan Compiler and Task Parser.

Tests cover:
- PlanCompiler mapping rules (scope, element_handling, navigation, completion)
- PlanCompiler validation (_validate_slots)
- TaskParser heuristic extraction
- Static path generation for target_path scope
"""

import pytest
from pathlib import Path
import sys

# Add project root to sys.path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from src.graph.compiler import PlanCompiler, CompilerError
from src.graph.node import (
    CompletionPolicyType,
    ChildrenStrategyType,
    ExitConditionType,
    FallbackAction,
    IntentSlots,
    NodeType,
)
from src.ai.task_parser import parse_task_to_slots


# ============================================================================
# Test PlanCompiler - Scope Mapping
# ============================================================================


class TestPlanCompilerScopeMapping:
    """Tests for scope → completion_policy mapping."""

    def test_full_scope_maps_to_none(self):
        """Test that 'full' scope maps to NONE completion policy."""
        slots = IntentSlots(target_app="settings", scope="full")
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        assert plan.completion_policy.type == CompletionPolicyType.NONE

    def test_partial_scope_maps_to_max_steps(self):
        """Test that 'partial' scope maps to MAX_STEPS completion policy."""
        slots = IntentSlots(target_app="settings", scope="partial")
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        assert plan.completion_policy.type == CompletionPolicyType.MAX_STEPS
        assert plan.completion_policy.max_steps == 50

    def test_target_only_scope_maps_to_target_found(self):
        """Test that 'target_only' scope maps to TARGET_FOUND completion policy."""
        slots = IntentSlots(target_app="settings", scope="target_only", target="WiFi")
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        assert plan.completion_policy.type == CompletionPolicyType.TARGET_FOUND
        assert plan.completion_policy.target_name == "WiFi"

    def test_target_path_scope_creates_static_strategy(self):
        """Test that 'target_path' scope creates STATIC children strategy."""
        slots = IntentSlots(target_app="settings", scope="target_path", target="WiFi/Network")
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        assert plan.root_node.children_strategy.type == ChildrenStrategyType.STATIC
        assert len(plan.static_nodes) > 0


# ============================================================================
# Test PlanCompiler - Element Handling Mapping
# ============================================================================


class TestPlanCompilerElementHandling:
    """Tests for element_handling → dynamic_rules template set mapping."""

    def test_full_interaction_maps_to_all_templates(self):
        """Test that 'full_interaction' includes all 4 templates."""
        slots = IntentSlots(target_app="settings", element_handling="full_interaction")
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        dynamic_rules = plan.root_node.children_strategy.dynamic_rules
        assert len(dynamic_rules) == 4
        # Verify template IDs are present
        template_ids = [rule.child_template for rule in dynamic_rules.values()]
        assert "menu_container" in template_ids
        assert "switch_leaf" in template_ids
        assert "slider_leaf" in template_ids
        assert "leaf_action" in template_ids

    def test_menu_only_maps_to_single_template(self):
        """Test that 'menu_only' includes only menu_container template."""
        slots = IntentSlots(target_app="settings", element_handling="menu_only")
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        dynamic_rules = plan.root_node.children_strategy.dynamic_rules
        assert len(dynamic_rules) == 1
        assert list(dynamic_rules.values())[0].child_template == "menu_container"

    def test_safe_mode_maps_to_all_templates_with_meta(self):
        """Test that 'safe_mode' includes all templates and sets safe_mode meta."""
        slots = IntentSlots(target_app="settings", element_handling="safe_mode")
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        dynamic_rules = plan.root_node.children_strategy.dynamic_rules
        assert len(dynamic_rules) == 4
        assert plan.root_node.meta.get("safe_mode") is True

    def test_read_only_maps_to_leaf_info_template(self):
        """Test that 'read_only' includes only leaf_info template."""
        slots = IntentSlots(target_app="settings", element_handling="read_only")
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        dynamic_rules = plan.root_node.children_strategy.dynamic_rules
        assert len(dynamic_rules) == 1
        assert list(dynamic_rules.values())[0].child_template == "leaf_info"


# ============================================================================
# Test PlanCompiler - Navigation Mapping
# ============================================================================


class TestPlanCompilerNavigation:
    """Tests for navigation → exit_condition.fallback mapping."""

    def test_navigation_back_maps_to_back_fallback(self):
        """Test that navigation='back' maps to BACK fallback."""
        slots = IntentSlots(target_app="settings", navigation="back")
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        assert plan.root_node.exit_condition.fallback == FallbackAction.BACK

    def test_navigation_default_maps_to_auto_escape(self):
        """Test that missing navigation maps to AUTO_ESCAPE fallback."""
        slots = IntentSlots(target_app="settings")
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        assert plan.root_node.exit_condition.fallback == FallbackAction.AUTO_ESCAPE


# ============================================================================
# Test PlanCompiler - Completion Override
# ============================================================================


class TestPlanCompilerCompletionOverride:
    """Tests for completion → completion_policy override mapping."""

    def test_completion_timeout_maps_to_timeout_policy(self):
        """Test that completion='timeout' maps to TIMEOUT completion policy."""
        slots = IntentSlots(target_app="settings", completion="timeout")
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        assert plan.completion_policy.type == CompletionPolicyType.TIMEOUT
        assert plan.completion_policy.timeout_seconds == 300

    def test_completion_steps_maps_to_max_steps_policy(self):
        """Test that completion='steps' maps to MAX_STEPS completion policy."""
        slots = IntentSlots(target_app="settings", completion="steps")
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        assert plan.completion_policy.type == CompletionPolicyType.MAX_STEPS
        assert plan.completion_policy.max_steps == 100


# ============================================================================
# Test PlanCompiler - Validation
# ============================================================================


class TestPlanCompilerValidation:
    """Tests for _validate_slots validation rules."""

    def test_missing_target_app_raises_error(self):
        """Test that missing target_app raises CompilerError."""
        slots = IntentSlots(target_app=None)
        compiler = PlanCompiler()
        with pytest.raises(CompilerError, match="target_app is required"):
            compiler.compile(slots)

    def test_target_only_without_target_raises_error(self):
        """Test that target_only scope without target raises CompilerError."""
        slots = IntentSlots(target_app="settings", scope="target_only", target=None)
        compiler = PlanCompiler()
        with pytest.raises(CompilerError, match="target is required"):
            compiler.compile(slots)

    def test_target_path_without_target_raises_error(self):
        """Test that target_path scope without target raises CompilerError."""
        slots = IntentSlots(target_app="settings", scope="target_path", target=None)
        compiler = PlanCompiler()
        with pytest.raises(CompilerError, match="target is required"):
            compiler.compile(slots)

    def test_invalid_depth_raises_error(self):
        """Test that invalid depth raises CompilerError."""
        slots = IntentSlots(target_app="settings", depth=0)
        compiler = PlanCompiler()
        with pytest.raises(CompilerError, match="Invalid depth"):
            compiler.compile(slots)

    def test_depth_exceeds_limit_raises_error(self):
        """Test that depth > 1000 raises CompilerError."""
        slots = IntentSlots(target_app="settings", depth=1001)
        compiler = PlanCompiler()
        with pytest.raises(CompilerError, match="Invalid depth"):
            compiler.compile(slots)

    def test_valid_slots_pass_validation(self):
        """Test that valid slots pass validation."""
        slots = IntentSlots(target_app="settings", scope="full", depth=5)
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        assert plan.entry_app == "settings"


# ============================================================================
# Test PlanCompiler - Static Path Generation
# ============================================================================


class TestPlanCompilerStaticPath:
    """Tests for static path generation for target_path scope."""

    def test_static_path_generates_correct_nodes(self):
        """Test that static path generates correct number of nodes."""
        slots = IntentSlots(
            target_app="settings",
            scope="target_path",
            target="Display/Brightness"
        )
        compiler = PlanCompiler()
        plan = compiler.compile(slots)
        # Should have 2 static nodes (Display, Brightness)
        assert len(plan.static_nodes) == 2

    def test_static_path_node_paths_are_correct(self):
        """Test that static path nodes have correct precondition paths."""
        slots = IntentSlots(
            target_app="settings",
            scope="target_path",
            target="Display/Brightness/Auto"
        )
        compiler = PlanCompiler()
        plan = compiler.compile(slots)

        # Get nodes in order
        nodes = list(plan.static_nodes.values())
        paths = [node.precondition.path for node in nodes if node.precondition]

        # First node: ["Display"]
        assert ["Display"] in paths
        # Second node: ["Display", "Brightness"]
        assert ["Display", "Brightness"] in paths
        # Third node: ["Display", "Brightness", "Auto"]
        assert ["Display", "Brightness", "Auto"] in paths

    def test_static_path_last_node_is_leaf(self):
        """Test that last node in static path is a leaf action."""
        slots = IntentSlots(
            target_app="settings",
            scope="target_path",
            target="Display/Brightness"
        )
        compiler = PlanCompiler()
        plan = compiler.compile(slots)

        # Last node should be LEAF_ACTION
        nodes = list(plan.static_nodes.values())
        last_node = nodes[-1]
        assert last_node.node_type == NodeType.LEAF_ACTION


# ============================================================================
# Test Task Parser
# ============================================================================


class TestTaskParser:
    """Tests for parse_task_to_slots heuristic extraction."""

    def test_extract_chinese_app_settings(self):
        """Test extracting '设置' as target_app."""
        slots = parse_task_to_slots("遍历设置应用")
        assert slots.target_app == "设置"

    def test_extract_chinese_app_display(self):
        """Test extracting '显示' as target_app."""
        # Use a task that only contains '显示' without '设置'
        slots = parse_task_to_slots("打开显示菜单")
        assert slots.target_app == "显示"

    def test_extract_english_app_settings(self):
        """Test extracting 'settings' as target_app."""
        slots = parse_task_to_slots("traverse settings app")
        assert slots.target_app == "settings"

    def test_extract_scope_from_search_keywords(self):
        """Test that search keywords extract target_only scope."""
        slots = parse_task_to_slots("遍历设置找到版本号")
        assert slots.scope == "target_only"

    def test_extract_target_from_find_keyword(self):
        """Test extracting target from '找到' keyword."""
        slots = parse_task_to_slots("在设置中找到版本号")
        assert slots.target == "版本号"

    def test_extract_target_from_search_keyword(self):
        """Test extracting target from '搜索' keyword."""
        slots = parse_task_to_slots("搜索WiFi选项")
        assert slots.target == "WiFi选项"

    def test_punctuation_stripping_from_target(self):
        """Test that punctuation is stripped from target."""
        slots = parse_task_to_slots("找到WiFi设置。")
        assert slots.target == "WiFi设置"

    def test_complete_chinese_task_extraction(self):
        """Test complete extraction from Chinese task."""
        slots = parse_task_to_slots("遍历设置找到版本号")
        assert slots.target_app == "设置"
        assert slots.scope == "target_only"
        assert slots.target == "版本号"

    def test_empty_task_returns_empty_slots(self):
        """Test that empty task returns empty IntentSlots."""
        slots = parse_task_to_slots("")
        assert slots.target_app is None
        assert slots.scope is None
        assert slots.target is None
