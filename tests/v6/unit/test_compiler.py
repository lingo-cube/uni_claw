"""
Tests for V6.9 PlanCompiler (C-Series).

Tests cover C1-C12 compiler scenarios:
- C1-C4: Scope mapping (full, partial, target_only, validation)
- C5: Static path compilation
- C6-C9: Element handling mapping
- C10-C11: Navigation and completion override
- C12: Validation rules
"""

import pytest
from pathlib import Path
import sys

# Add project root to sys.path
sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from src.graph.node import (
    CompletionPolicy,
    CompletionPolicyType,
    NodeType,
    TraversalNode,
    ChildrenStrategy,
    ChildrenStrategyType,
    DynamicRule,
    ExitCondition,
    FallbackAction,
    IntentSlots,
)
from src.graph.plan import TraversalPlan
from src.graph.compiler import PlanCompiler, CompilerError


# ============================================================================
# C1-C4: Scope mapping tests
# ============================================================================


class TestC1_ScopeFullMapsToNone:
    """C1: Verify scope='full' maps to completion_policy.type=NONE."""

    def test_full_scope_maps_to_none(self):
        """WHEN compiling plan with scope='full',
        THEN completion_policy.type equals NONE.
        """
        intent = IntentSlots(scope="full", depth=2)

        compiler = PlanCompiler()
        policy = compiler._map_completion_policy(intent)

        assert policy.type == CompletionPolicyType.NONE


class TestC2_ScopePartialMapsToMaxSteps:
    """C2: Verify scope='partial' maps to completion_policy.type=MAX_STEPS."""

    def test_partial_scope_maps_to_max_steps(self):
        """WHEN compiling plan with scope='partial',
        THEN completion_policy.type equals MAX_STEPS.
        """
        intent = IntentSlots(scope="partial", depth=2)

        compiler = PlanCompiler()
        policy = compiler._map_completion_policy(intent)

        assert policy.type == CompletionPolicyType.MAX_STEPS
        assert policy.max_steps == 2  # depth maps to max_steps


class TestC3_ScopeTargetOnlyWithTarget:
    """C3: Verify scope='target_only' with target maps to TARGET_FOUND."""

    def test_target_only_with_target(self):
        """WHEN compiling plan with scope='target_only' and target,
        THEN completion_policy.type equals TARGET_FOUND and target_name is set.
        """
        intent = IntentSlots(scope="target_only", target="Brightness", depth=3)

        compiler = PlanCompiler()
        policy = compiler._map_completion_policy(intent)

        assert policy.type == CompletionPolicyType.TARGET_FOUND
        assert policy.target_name == "Brightness"


class TestC4_ScopeTargetOnlyWithoutTargetRaisesError:
    """C4: Verify scope='target_only' without target raises CompilerError."""

    def test_target_only_without_target_raises_error(self):
        """WHEN compiling plan with scope='target_only' and no target,
        THEN system raises CompilerError.
        """
        intent = IntentSlots(scope="target_only", target=None, depth=2)

        compiler = PlanCompiler()

        with pytest.raises(CompilerError, match="target.*required"):
            compiler._map_completion_policy(intent)


# ============================================================================
# C5: Static path compilation
# ============================================================================


class TestC5_StaticPathCompilation:
    """C5: Verify target_path creates STATIC node chain."""

    def test_target_path_creates_static_chain(self):
        """WHEN compiling plan with scope='target_path' and target='Settings/Display/Brightness',
        THEN children_strategy.type equals STATIC and 3 static_nodes are created.
        """
        intent = IntentSlots(scope="target_path", target="Settings/Display/Brightness", depth=3)

        compiler = PlanCompiler()
        plan = compiler.compile(intent, target_app="Settings")

        # Root should have STATIC strategy
        assert plan.root_node.children_strategy.type == ChildrenStrategyType.STATIC
        assert len(plan.root_node.static_nodes) == 3

        # Verify path concatenation
        assert plan.root_node.static_nodes[0].precondition.path == ["Settings"]
        assert plan.root_node.static_nodes[1].precondition.path == ["Settings", "Display"]
        assert plan.root_node.static_nodes[2].precondition.path == ["Settings", "Display", "Brightness"]


# ============================================================================
# C6-C9: Element handling mapping
# ============================================================================


class TestC6_ElementHandlingFullInteraction:
    """C6: Verify element_handling='full_interaction' creates 4 rules."""

    def test_full_interaction_creates_4_rules(self):
        """WHEN compiling plan with element_handling='full_interaction',
        THEN dynamic_rules contains 4 rules.
        """
        intent = IntentSlots(
            scope="full",
            element_handling="full_interaction",
            depth=2,
        )

        compiler = PlanCompiler()
        plan = compiler.compile(intent, target_app="test")

        rules = plan.root_node.children_strategy.dynamic_rules
        assert len(rules) == 4
        assert "menu_container" in rules
        assert "switch_leaf" in rules
        assert "slider_leaf" in rules
        assert "leaf_action" in rules


class TestC7_ElementHandlingMenuOnly:
    """C7: Verify element_handling='menu_only' creates only menu_container rule."""

    def test_menu_only_creates_menu_rule(self):
        """WHEN compiling plan with element_handling='menu_only',
        THEN dynamic_rules contains only menu_container rule.
        """
        intent = IntentSlots(
            scope="full",
            element_handling="menu_only",
            depth=2,
        )

        compiler = PlanCompiler()
        plan = compiler.compile(intent, target_app="test")

        rules = plan.root_node.children_strategy.dynamic_rules
        assert len(rules) == 1
        assert "menu_container" in rules


class TestC8_ElementHandlingSafeMode:
    """C8: Verify element_handling='safe_mode' creates rules with meta flag."""

    def test_safe_mode_creates_rules_with_meta(self):
        """WHEN compiling plan with element_handling='safe_mode',
        THEN dynamic_rules contains 4 rules and meta['safe_mode'] equals True.
        """
        intent = IntentSlots(
            scope="full",
            element_handling="safe_mode",
            depth=2,
        )

        compiler = PlanCompiler()
        plan = compiler.compile(intent, target_app="test")

        rules = plan.root_node.children_strategy.dynamic_rules
        assert len(rules) == 4

        # Check meta flag
        for rule in rules.values():
            if isinstance(rule, DynamicRule):
                assert rule.meta.get("safe_mode") is True


class TestC9_ElementHandlingReadOnly:
    """C9: Verify element_handling='read_only' creates only leaf_info rule."""

    def test_read_only_creates_info_rule(self):
        """WHEN compiling plan with element_handling='read_only',
        THEN dynamic_rules contains only leaf_info rule.
        """
        intent = IntentSlots(
            scope="full",
            element_handling="read_only",
            depth=2,
        )

        compiler = PlanCompiler()
        plan = compiler.compile(intent, target_app="test")

        rules = plan.root_node.children_strategy.dynamic_rules
        assert len(rules) == 1
        assert "leaf_info" in rules


# ============================================================================
# C10-C11: Navigation and completion override
# ============================================================================


class TestC10_NavigationBackMapsToBackFallback:
    """C10: Verify navigation='back' maps to exit_condition.fallback=BACK."""

    def test_navigation_back_maps_to_back_fallback(self):
        """WHEN compiling plan with navigation='back',
        THEN exit_condition.fallback equals BACK.
        """
        intent = IntentSlots(scope="full", navigation="back", depth=2)

        compiler = PlanCompiler()
        plan = compiler.compile(intent, target_app="test")

        assert plan.root_node.exit_condition.fallback == FallbackAction.BACK

    def test_no_navigation_maps_to_auto_escape(self):
        """WHEN compiling plan without navigation field,
        THEN exit_condition.fallback equals AUTO_ESCAPE.
        """
        intent = IntentSlots(scope="full", depth=2)  # No navigation specified

        compiler = PlanCompiler()
        plan = compiler.compile(intent, target_app="test")

        assert plan.root_node.exit_condition.fallback == FallbackAction.AUTO_ESCAPE


class TestC11_CompletionTimeoutOverridesScope:
    """C11: Verify completion='timeout' overrides scope-derived policy."""

    def test_timeout_overrides_full_scope(self):
        """WHEN compiling plan with scope='full' and completion='timeout',
        THEN completion_policy.type equals TIMEOUT (not NONE).
        """
        intent = IntentSlots(scope="full", completion="timeout", depth=2)

        compiler = PlanCompiler()
        plan = compiler.compile(intent, target_app="test")

        assert plan.completion_policy.type == CompletionPolicyType.TIMEOUT


# ============================================================================
# C12: Validation rules
# ============================================================================


class TestC12_ValidationRules:
    """C12: Verify PlanCompiler validates required fields."""

    def test_missing_target_app_raises_error(self):
        """WHEN compiling plan without target_app,
        THEN system raises CompilerError.
        """
        intent = IntentSlots(scope="full", depth=2)

        compiler = PlanCompiler()

        with pytest.raises(CompilerError, match="target_app.*required"):
            compiler.compile(intent, target_app=None)

    def test_invalid_scope_raises_error(self):
        """WHEN compiling plan with invalid scope value,
        THEN system raises CompilerError.
        """
        intent = IntentSlots(scope="invalid_scope", depth=2)

        compiler = PlanCompiler()

        with pytest.raises(CompilerError):
            compiler.compile(intent, target_app="test")

    def test_negative_depth_raises_error(self):
        """WHEN compiling plan with negative depth,
        THEN system raises CompilerError.
        """
        intent = IntentSlots(scope="full", depth=-1)

        compiler = PlanCompiler()

        with pytest.raises(CompilerError, match="depth.*positive"):
            compiler.compile(intent, target_app="test")


# ============================================================================
# Additional helper tests
# ============================================================================


class TestCompilerHelperMethods:
    """Additional tests for compiler helper methods."""

    def test_compiler_creates_valid_plan(self):
        """Verify compiler creates a valid TraversalPlan."""
        intent = IntentSlots(
            scope="full",
            element_handling="full_interaction",
            navigation="back",
            depth=3,
        )

        compiler = PlanCompiler()
        plan = compiler.compile(intent, target_app="Settings")

        # Verify basic plan structure
        assert plan is not None
        assert plan.entry_app == "Settings"
        assert plan.root_node is not None
        assert plan.completion_policy is not None

    def test_compiler_preserves_intent_slots(self):
        """Verify compiler preserves original IntentSlots in plan."""
        intent = IntentSlots(
            scope="partial",
            target="Display",
            depth=2,
        )

        compiler = PlanCompiler()
        plan = compiler.compile(intent, target_app="Settings")

        # IntentSlots should be preserved
        assert plan.intent_slots == intent
