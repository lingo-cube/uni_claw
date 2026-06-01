"""Tests for AI types models.

This module tests the models from src/ai/types.py and
src/ai/capabilities/types.py including:
- DecisionResult enum
- ContainerInference
- NodeOperation
- NodeStrategy
- TraversalPlan
- PageTypeVerification
- MismatchDetails
- Suggestion
- SafetyEvaluation
- PageLevelGuidance
- SafetyScreeningResult
- ContextDecisionResult
"""

import pytest
from src.ai.types import DecisionResult, ContainerInference
from src.ai.capabilities.types import (
    NodeOperation,
    NodeStrategy,
    TraversalPlan,
    PageTypeVerification,
    MismatchDetails,
    Suggestion,
    SafetyEvaluation,
    PageLevelGuidance,
    SafetyScreeningResult,
    ContextDecisionResult,
)


class TestDecisionResult:
    """Tests for DecisionResult enum."""

    def test_decision_result_values(self):
        """Test DecisionResult has correct values."""
        assert DecisionResult.SUCCESS.value == "success"
        assert DecisionResult.UNSURE.value == "unsure"
        assert DecisionResult.GIVE_UP.value == "give_up"

    def test_decision_result_values_method(self):
        """Test DecisionResult.values() method."""
        values = DecisionResult.values()
        assert len(values) == 3
        assert "success" in values

    def test_decision_result_from_value(self):
        """Test DecisionResult.from_value() method."""
        result = DecisionResult.from_value("success")
        assert result == DecisionResult.SUCCESS

    def test_decision_result_is_valid(self):
        """Test DecisionResult.is_valid() method."""
        assert DecisionResult.is_valid("unsure") is True
        assert DecisionResult.is_valid("invalid") is False


class TestContainerInference:
    """Tests for ContainerInference model."""

    def test_basic_creation(self):
        """Test basic ContainerInference creation."""
        inference = ContainerInference(
            container_type="GRID_MENU",
            confidence=0.85,
        )
        assert inference.container_type == "GRID_MENU"
        assert inference.confidence == 0.85

    def test_with_matched_template(self):
        """Test ContainerInference with matched template."""
        inference = ContainerInference(
            container_type="LIST_MENU",
            confidence=0.9,
            matched_template="list_template_v1",
        )
        assert inference.matched_template == "list_template_v1"

    def test_optional_matched_template(self):
        """Test ContainerInference with optional matched_template."""
        inference = ContainerInference(
            container_type="UNKNOWN",
            confidence=0.0,
        )
        assert inference.matched_template is None

    def test_confidence_validation_valid(self):
        """Test confidence validation for valid values."""
        # Edge cases
        ContainerInference("TEST", 0.0)
        ContainerInference("TEST", 0.5)
        ContainerInference("TEST", 1.0)

    def test_confidence_validation_invalid(self):
        """Test confidence validation rejects invalid values."""
        with pytest.raises(ValueError, match="Confidence must be between 0 and 1"):
            ContainerInference("TEST", -0.1)

        with pytest.raises(ValueError, match="Confidence must be between 0 and 1"):
            ContainerInference("TEST", 1.1)

    def test_frozen_immutability(self):
        """Test ContainerInference is frozen (immutable)."""
        inference = ContainerInference("TEST", 0.5)
        with pytest.raises(Exception):  # FrozenInstanceError
            inference.confidence = 0.8


class TestNodeOperation:
    """Tests for NodeOperation model."""

    def test_creation(self):
        """Test creating NodeOperation."""
        op = NodeOperation(action="click")
        assert op.action == "click"

    def test_with_target(self):
        """Test NodeOperation with target."""
        op = NodeOperation(
            action="click",
            target={"by": "text", "value": "Settings"},
        )
        assert op.target["by"] == "text"

    def test_with_params(self):
        """Test NodeOperation with parameters."""
        op = NodeOperation(
            action="input_text",
            params={"text": "Test Input"},
        )
        assert op.params["text"] == "Test Input"


class TestNodeStrategy:
    """Tests for NodeStrategy model."""

    def test_creation(self):
        """Test creating NodeStrategy."""
        strategy = NodeStrategy(type="dynamic_match")
        assert strategy.type == "dynamic_match"

    def test_with_dynamic_rules(self):
        """Test NodeStrategy with dynamic rules."""
        strategy = NodeStrategy(
            type="dynamic_match",
            dynamic_rules={
                "menu_rule": {
                    "match_condition": {"type": "menu_item"},
                    "child_template": "menu_container",
                }
            },
        )
        assert "menu_rule" in strategy.dynamic_rules


class TestTraversalPlan:
    """Tests for TraversalPlan model."""

    def test_creation(self):
        """Test creating TraversalPlan."""
        root_op = NodeOperation(action="no_action")
        root_node = {
            "node_id": "home",
            "name": "Home",
            "node_type": "container",
            "operation": {"action": "no_action"},
            "children_strategy": {"type": "dynamic_match"},
        }

        plan = TraversalPlan(
            entry_app="com.test.app",
            root_node=root_node,
        )
        assert plan.entry_app == "com.test.app"

    def test_with_static_nodes(self):
        """Test TraversalPlan with static nodes."""
        root_node = {
            "node_id": "root",
            "name": "Root",
            "node_type": "container",
            "operation": {"action": "no_action"},
        }

        plan = TraversalPlan(
            entry_app="com.test.app",
            root_node=root_node,
            static_nodes=[
                {
                    "node_id": "settings",
                    "name": "Settings",
                    "node_type": "container",
                    "operation": {"action": "navigate"},
                }
            ],
        )
        assert len(plan.static_nodes) == 1

    def test_with_mode(self):
        """Test TraversalPlan with different modes."""
        root_node = {
            "node_id": "root",
            "name": "Root",
            "node_type": "container",
            "operation": {"action": "no_action"},
        }

        plan = TraversalPlan(
            entry_app="com.test.app",
            root_node=root_node,
            mode="concrete",
        )
        assert plan.mode == "concrete"


class TestMismatchDetails:
    """Tests for MismatchDetails model."""

    def test_creation(self):
        """Test creating MismatchDetails."""
        details = MismatchDetails(
            missing_items=["back_button", "menu_bar"],
            unexpected_items=["ad_banner"],
        )
        assert "back_button" in details.missing_items
        assert "ad_banner" in details.unexpected_items

    def test_empty_details(self):
        """Test MismatchDetails with empty lists."""
        details = MismatchDetails()
        assert len(details.missing_items) == 0
        assert len(details.unexpected_items) == 0


class TestSuggestion:
    """Tests for Suggestion model."""

    def test_creation(self):
        """Test creating Suggestion."""
        suggestion = Suggestion(
            action="back",
            target=None,
            reason="Go back to previous page",
        )
        assert suggestion.action == "back"
        assert suggestion.reason == "Go back to previous page"

    def test_with_target(self):
        """Test Suggestion with target."""
        suggestion = Suggestion(
            action="click",
            target="Close Button",
            reason="Close the dialog",
        )
        assert suggestion.target == "Close Button"


class TestPageTypeVerification:
    """Tests for PageTypeVerification model."""

    def test_match(self):
        """Test PageTypeVerification with match."""
        verification = PageTypeVerification(
            is_match=True,
            confidence=0.95,
            actual_type="menu_list",
        )
        assert verification.is_match is True
        assert verification.actual_type == "menu_list"

    def test_mismatch_with_details(self):
        """Test PageTypeVerification with mismatch and details."""
        details = MismatchDetails(
            missing_items=["back_button"],
            unexpected_items=["close_button"],
        )
        suggestion = Suggestion(action="close_popup")

        verification = PageTypeVerification(
            is_match=False,
            confidence=0.8,
            actual_type="dialog",
            mismatch_details=details,
            suggestion=suggestion,
        )
        assert verification.is_match is False
        assert verification.actual_type == "dialog"
        assert verification.suggestion.action == "close_popup"


class TestSafetyEvaluation:
    """Tests for SafetyEvaluation model."""

    def test_creation(self):
        """Test creating SafetyEvaluation."""
        evaluation = SafetyEvaluation(
            name="Factory Reset",
            safety_tag="skip",
            confidence=1.0,
            reason="Destructive operation",
        )
        assert evaluation.name == "Factory Reset"
        assert evaluation.safety_tag == "skip"

    def test_with_optional_fields(self):
        """Test SafetyEvaluation with optional fields."""
        evaluation = SafetyEvaluation(
            name="Clear Cache",
            safety_tag="caution",
            confidence=0.8,
            reason="May affect performance",
            context_dependency="Check if cache needed",
            task_relevance="Generally safe",
        )
        assert evaluation.context_dependency == "Check if cache needed"


class TestPageLevelGuidance:
    """Tests for PageLevelGuidance model."""

    def test_creation(self):
        """Test creating PageLevelGuidance."""
        guidance = PageLevelGuidance(
            overall_safe_to_proceed=True,
            recommended_max_parallel=3,
        )
        assert guidance.overall_safe_to_proceed is True
        assert guidance.recommended_max_parallel == 3

    def test_with_precautions(self):
        """Test PageLevelGuidance with precautions."""
        guidance = PageLevelGuidance(
            overall_safe_to_proceed=True,
            special_precautions=["Skip factory reset", "Verify payments"],
        )
        assert len(guidance.special_precautions) == 2


class TestSafetyScreeningResult:
    """Tests for SafetyScreeningResult model."""

    def test_creation(self):
        """Test creating SafetyScreeningResult."""
        evaluations = [
            SafetyEvaluation(
                name="WiFi Toggle",
                safety_tag="safe",
                confidence=1.0,
                reason="Safe toggle",
            )
        ]

        result = SafetyScreeningResult(evaluations=evaluations)
        assert len(result.evaluations) == 1

    def test_with_page_guidance(self):
        """Test SafetyScreeningResult with page guidance."""
        evaluations = [
            SafetyEvaluation(
                name="Item1",
                safety_tag="safe",
                confidence=0.9,
                reason="Safe",
            )
        ]
        guidance = PageLevelGuidance(overall_safe_to_proceed=True)

        result = SafetyScreeningResult(
            evaluations=evaluations,
            page_level_guidance=guidance,
        )
        assert result.page_level_guidance is not None


class TestContextDecisionResult:
    """Tests for ContextDecisionResult model."""

    def test_creation(self):
        """Test creating ContextDecisionResult."""
        result = ContextDecisionResult(
            result="success",
            action="click",
            target={"by": "text", "value": "Settings"},
        )
        assert result.result == "success"
        assert result.action == "click"

    def test_with_confidence(self):
        """Test ContextDecisionResult with confidence."""
        result = ContextDecisionResult(
            result="success",
            action="click",
            confidence=0.95,
        )
        assert result.confidence == 0.95

    def test_safety_verified(self):
        """Test ContextDecisionResult with safety verification."""
        result = ContextDecisionResult(
            result="success",
            action="click",
            safety_verified=True,
        )
        assert result.safety_verified is True

    def test_with_params(self):
        """Test ContextDecisionResult with parameters."""
        result = ContextDecisionResult(
            result="success",
            action="input_text",
            params={"text": "test"},
        )
        assert result.params["text"] == "test"
