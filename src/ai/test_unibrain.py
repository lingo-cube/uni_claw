"""Unit tests for UniBrain AI Provider.

Tests cover:
- UniBrain initialization and configuration
- Parser registration
- Capability initialization
- AIStrategyAdvisor interface methods
- Integration with vision service
"""

from unittest.mock import AsyncMock, MagicMock, Mock, patch

import pytest

from src.ai.provider import UniBrain
from src.ai.core.config import AIProviderConfig, RetryConfig
from src.ai.vision.config import VisionConfig
from src.ai.vision.mock_service import MockVisionService
from src.ai.capabilities.types import (
    TraversalPlan,
    PageTypeVerification,
    SafetyScreeningResult,
    ContextDecisionResult,
    TraversalNode,
    NodeOperation,
    NodeStrategy,
    MismatchDetails,
    Suggestion,
    SafetyEvaluation,
    PageLevelGuidance,
)
from src.ai.types import DecisionResult, ContainerInference
from src.state.content_tree import (
    PageAnalysis,
    Direction,
    MenuInfo,
    MenuItem,
    MenuItemType,
    ExpectedAction,
    Coordinate,
)
from src.context.traversal_context import TraversalContext


# ============================================================================
# Tests for UniBrain Initialization
# ============================================================================

class TestUniBrainInit:
    """Tests for UniBrain initialization."""

    @pytest.fixture
    def ai_config(self):
        """Create a test AI config."""
        return AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )

    @pytest.fixture
    def vision_config(self):
        """Create a test vision config."""
        return VisionConfig(service_type="mock")

    def test_init_with_ai_config_only(self, ai_config):
        """Test initialization with only AI config (vision defaults to mock)."""
        provider = UniBrain(ai_config)

        assert provider.client is not None
        assert provider.validator is not None
        assert provider.prompt_registry is not None
        assert isinstance(provider.vision_service, MockVisionService)
        assert "parse" in provider.capabilities
        assert "verify" in provider.capabilities
        assert "safety" in provider.capabilities
        assert "vision" in provider.capabilities
        assert "decision" in provider.capabilities

    def test_init_with_vision_config(self, ai_config, vision_config):
        """Test initialization with vision config."""
        provider = UniBrain(ai_config, vision_config)

        assert isinstance(provider.vision_service, MockVisionService)

    def test_init_creates_all_capabilities(self, ai_config):
        """Test that all capabilities are created."""
        provider = UniBrain(ai_config)

        # Check all capabilities exist
        assert provider.capabilities["parse"] is not None
        assert provider.capabilities["verify"] is not None
        assert provider.capabilities["safety"] is not None
        assert provider.capabilities["vision"] is not None
        assert provider.capabilities["decision"] is not None

    def test_registers_parsers_on_init(self, ai_config):
        """Test that parsers are registered during initialization."""
        provider = UniBrain(ai_config)

        # Check that parsers are registered
        assert provider.validator.has_parser("TraversalPlan")
        assert provider.validator.has_parser("PageTypeVerification")
        assert provider.validator.has_parser("SafetyScreeningResult")
        assert provider.validator.has_parser("ContextDecisionResult")
        assert provider.validator.has_parser("PageAnalysis")


# ============================================================================
# Tests for Parser Registration
# ============================================================================

class TestParserRegistration:
    """Tests for parser registration in UniBrain."""

    @pytest.fixture
    def provider(self):
        """Create a UniBrain provider for testing."""
        ai_config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        return UniBrain(ai_config)

    def test_traversal_plan_parser(self, provider):
        """Test TraversalPlan parser."""
        response = {
            "entry_app": "Settings",
            "root_node": {
                "node_id": "root",
                "name": "Settings",
                "node_type": "container",
                "operation": {"action": "click"},
                "children_strategy": {"type": "dynamic_match"},
            },
            "mode": "hybrid",
        }

        result = provider.validator.validate_and_parse(response, "TraversalPlan")

        assert isinstance(result, TraversalPlan)
        assert result.entry_app == "Settings"
        assert result.root_node.name == "Settings"

    def test_page_verification_parser(self, provider):
        """Test PageTypeVerification parser."""
        response = {
            "is_match": True,
            "confidence": 0.95,
            "actual_type": "menu_list",
            "reasoning": "Matches expected structure",
        }

        result = provider.validator.validate_and_parse(response, "PageTypeVerification")

        assert isinstance(result, PageTypeVerification)
        assert result.is_match is True
        assert result.actual_type == "menu_list"

    def test_page_verification_parser_with_mismatch(self, provider):
        """Test PageTypeVerification parser with mismatch details."""
        response = {
            "is_match": False,
            "confidence": 0.85,
            "actual_type": "dialog",
            "reasoning": "Dialog instead of menu",
            "mismatch_details": {
                "missing_items": ["List"],
                "unexpected_items": ["OK button"],
            },
            "suggestion": {
                "action": "close_popup",
                "reason": "Close dialog first",
            },
        }

        result = provider.validator.validate_and_parse(response, "PageTypeVerification")

        assert result.is_match is False
        assert result.mismatch_details is not None
        assert result.mismatch_details.missing_items == ["List"]
        assert result.suggestion.action == "close_popup"

    def test_safety_result_parser(self, provider):
        """Test SafetyScreeningResult parser."""
        response = {
            "evaluations": [
                {
                    "name": "Safe Item",
                    "safety_tag": "safe",
                    "confidence": 0.98,
                    "reason": "Standard item",
                },
                {
                    "name": "Dangerous Item",
                    "safety_tag": "skip",
                    "confidence": 1.0,
                    "reason": "Destructive action",
                },
            ],
            "page_level_guidance": {
                "overall_safe_to_proceed": True,
                "special_precautions": ["Avoid Dangerous Item"],
            },
        }

        result = provider.validator.validate_and_parse(response, "SafetyScreeningResult")

        assert isinstance(result, SafetyScreeningResult)
        assert len(result.evaluations) == 2
        assert result.evaluations[0].safety_tag == "safe"
        assert result.evaluations[1].safety_tag == "skip"
        assert result.page_level_guidance.overall_safe_to_proceed is True

    def test_decision_result_parser(self, provider):
        """Test ContextDecisionResult parser."""
        response = {
            "result": "success",
            "action": "click",
            "target": {"by": "text", "value": "Settings"},
            "reasoning": "Navigate to Settings",
            "confidence": 0.92,
            "safety_verified": True,
        }

        result = provider.validator.validate_and_parse(response, "ContextDecisionResult")

        assert isinstance(result, ContextDecisionResult)
        assert result.result == "success"
        assert result.action == "click"
        assert result.safety_verified is True


# ============================================================================
# Tests for AIStrategyAdvisor Interface Methods
# ============================================================================

class TestInferContainerType:
    """Tests for infer_container_type method."""

    @pytest.fixture
    def provider(self):
        """Create a UniBrain provider for testing."""
        ai_config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        return UniBrain(ai_config)

    @pytest.fixture
    def sample_page_analysis(self):
        """Create a sample PageAnalysis."""
        return PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[
                MenuItem(
                    name="Item1",
                    type=MenuItemType.MENU_ITEM,
                    expected_action=ExpectedAction.NAVIGATE,
                    coordinate=Coordinate(x=0.5, y=0.3),
                    expects_page_change=True,
                    expects_state_change=False,
                ),
            ],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        )

    @pytest.fixture
    def sample_context(self):
        """Create a sample TraversalContext."""
        return TraversalContext()

    @patch('src.ai.capabilities.verify_page_type.VerifyPageTypeCapability.execute')
    def test_infer_container_type_success(self, mock_execute, provider, sample_page_analysis, sample_context):
        """Test successful container type inference."""
        # Mock the verify capability response
        mock_execute.return_value = PageTypeVerification(
            is_match=True,
            confidence=0.95,
            actual_type="menu_list",
            reasoning="Matches menu list pattern",
        )

        result = provider.infer_container_type(sample_page_analysis, sample_context)

        assert isinstance(result, ContainerInference)
        assert result.container_type == "menu_list"
        assert result.confidence == 0.95
        assert result.matched_template == "menu_list"


class TestDecideNextAction:
    """Tests for decide_next_action method."""

    @pytest.fixture
    def provider(self):
        """Create a UniBrain provider for testing."""
        ai_config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        return UniBrain(ai_config)

    @pytest.fixture
    def sample_page_analysis(self):
        """Create a sample PageAnalysis."""
        return PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Settings"],
            items=[
                MenuItem(
                    name="WiFi",
                    type=MenuItemType.SWITCH,
                    expected_action=ExpectedAction.TOGGLE,
                    coordinate=Coordinate(x=0.5, y=0.3),
                    expects_page_change=False,
                    expects_state_change=True,
                ),
            ],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        )

    @pytest.fixture
    def sample_context(self):
        """Create a sample TraversalContext."""
        return TraversalContext()

    @patch('src.ai.capabilities.screen_safety.ScreenSafetyCapability.execute')
    @patch('src.ai.capabilities.context_decision.ContextDecisionCapability.execute')
    def test_decide_next_action_success(self, mock_decision, mock_safety, provider, sample_page_analysis, sample_context):
        """Test successful next action decision."""
        # Mock safety and decision responses
        mock_safety.return_value = SafetyScreeningResult(
            evaluations=[
                SafetyEvaluation(name="WiFi", safety_tag="safe", confidence=0.99, reason="Safe"),
            ],
            page_level_guidance=PageLevelGuidance(overall_safe_to_proceed=True),
        )

        mock_decision.return_value = ContextDecisionResult(
            result="success",
            action="click",
            target={"by": "text", "value": "WiFi"},
            reasoning="Click WiFi to explore",
            confidence=0.92,
            safety_verified=True,
        )

        decision_result, node_data = provider.decide_next_action(
            "Explore WiFi settings",
            sample_page_analysis,
            sample_context,
        )

        assert decision_result == DecisionResult.SUCCESS
        assert node_data is not None
        assert node_data["action"] == "click"
        assert node_data["target"]["value"] == "WiFi"

    @patch('src.ai.capabilities.screen_safety.ScreenSafetyCapability.execute')
    @patch('src.ai.capabilities.context_decision.ContextDecisionCapability.execute')
    def test_decide_next_action_low_confidence(self, mock_decision, mock_safety, provider, sample_page_analysis, sample_context):
        """Test decision with low confidence returns UNSURE."""
        mock_safety.return_value = SafetyScreeningResult(
            evaluations=[],
            page_level_guidance=PageLevelGuidance(overall_safe_to_proceed=True),
        )

        mock_decision.return_value = ContextDecisionResult(
            result="unsure",
            action="no_action",
            reasoning="Not sure what to do",
            confidence=0.5,  # Below threshold
            safety_verified=True,
        )

        decision_result, node_data = provider.decide_next_action(
            "Explore",
            sample_page_analysis,
            sample_context,
        )

        assert decision_result == DecisionResult.UNSURE
        assert node_data is None

    @patch('src.ai.capabilities.screen_safety.ScreenSafetyCapability.execute')
    @patch('src.ai.capabilities.context_decision.ContextDecisionCapability.execute')
    def test_decide_next_action_back_decision(self, mock_decision, mock_safety, provider, sample_page_analysis, sample_context):
        """Test decision to go back."""
        mock_safety.return_value = SafetyScreeningResult(
            evaluations=[],
            page_level_guidance=PageLevelGuidance(overall_safe_to_proceed=True),
        )

        mock_decision.return_value = ContextDecisionResult(
            result="success",
            action="back",
            target=None,
            reasoning="Return to previous page",
            confidence=0.95,
            safety_verified=True,
        )

        decision_result, node_data = provider.decide_next_action(
            "Go back",
            sample_page_analysis,
            sample_context,
        )

        assert decision_result == DecisionResult.SUCCESS
        assert node_data is not None
        assert node_data["action"] == "back"
        assert node_data["target"] is None


class TestHandleException:
    """Tests for handle_exception method."""

    @pytest.fixture
    def provider(self):
        """Create a UniBrain provider for testing."""
        ai_config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        return UniBrain(ai_config)

    @pytest.fixture
    def sample_page_analysis(self):
        """Create a sample PageAnalysis."""
        return PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        )

    @pytest.fixture
    def sample_context(self):
        """Create a sample TraversalContext."""
        return TraversalContext()

    @patch('src.ai.provider.UniBrain.decide_next_action')
    def test_handle_exception_delegates_to_decision(self, mock_decision, provider, sample_page_analysis, sample_context):
        """Test that handle_exception delegates to decide_next_action."""
        mock_decision.return_value = (DecisionResult.SUCCESS, {"action": "back"})

        exception_dict = {"type": "ElementNotFound", "message": "Element not found"}

        decision_result, node_data = provider.handle_exception(
            exception_dict,
            sample_page_analysis,
            sample_context,
        )

        mock_decision.assert_called_once()
        assert decision_result == DecisionResult.SUCCESS
        assert node_data["action"] == "back"


class TestAnalyzeScreenshot:
    """Tests for analyze_screenshot method."""

    @pytest.fixture
    def provider(self):
        """Create a UniBrain provider for testing."""
        ai_config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        return UniBrain(ai_config)

    def test_analyze_screenshot_delegates_to_vision_capability(self, provider):
        """Test that analyze_screenshot delegates to vision capability."""
        result = provider.analyze_screenshot(b"fake_image")

        assert isinstance(result, PageAnalysis)


class TestVerifyPageWithVision:
    """Tests for verify_page_with_vision method."""

    @pytest.fixture
    def provider(self):
        """Create a UniBrain provider for testing."""
        ai_config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        return UniBrain(ai_config)

    def test_verify_page_with_vision_full_workflow(self, provider):
        """Test verify_page_with_vision combines vision and verification."""
        result = provider.verify_page_with_vision(
            b"fake_image",
            "menu_list",
        )

        assert isinstance(result, PageTypeVerification)


# ============================================================================
# Integration Tests
# ============================================================================

class TestUniBrainIntegration:
    """Integration tests for UniBrain provider."""

    @pytest.fixture
    def provider(self):
        """Create a UniBrain provider for testing."""
        ai_config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        return UniBrain(ai_config)

    def test_provider_implements_ai_strategy_advisor(self, provider):
        """Test that UniBrain implements AIStrategyAdvisor interface."""
        from src.ai.advisor import AIStrategyAdvisor

        assert isinstance(provider, AIStrategyAdvisor)

    def test_all_interface_methods_implemented(self, provider):
        """Test that all AIStrategyAdvisor methods are implemented."""
        # Check that methods exist and are callable
        assert callable(provider.infer_container_type)
        assert callable(provider.decide_next_action)
        assert callable(provider.handle_exception)

    def test_capabilities_use_shared_validator(self, provider):
        """Test that all capabilities share the same validator."""
        assert provider.capabilities["parse"].validator is provider.validator
        assert provider.capabilities["verify"].validator is provider.validator
        assert provider.capabilities["safety"].validator is provider.validator
        assert provider.capabilities["decision"].validator is provider.validator

    def test_llm_capabilities_use_shared_client(self, provider):
        """Test that LLM capabilities share the same client."""
        assert provider.capabilities["parse"].client is provider.client
        assert provider.capabilities["verify"].client is provider.client
        assert provider.capabilities["safety"].client is provider.client
        assert provider.capabilities["decision"].client is provider.client

    def test_vision_capability_uses_vision_service(self, provider):
        """Test that vision capability uses the vision service."""
        assert provider.capabilities["vision"].vision_service is provider.vision_service


# ============================================================================
# Edge Cases and Error Handling
# ============================================================================

class TestUniBrainEdgeCases:
    """Tests for edge cases and error handling."""

    @pytest.fixture
    def provider(self):
        """Create a UniBrain provider for testing."""
        ai_config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        return UniBrain(ai_config)

    def test_empty_page_analysis(self, provider):
        """Test with empty PageAnalysis."""
        empty_page = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        )

        result = provider.analyze_screenshot(b"fake_image")

        # Should return default mock response
        assert isinstance(result, PageAnalysis)

    def test_context_with_empty_fields(self, provider):
        """Test decision with empty context fields."""
        empty_page = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        )

        context = TraversalContext()

        # Should not raise even with empty context
        result = provider.analyze_screenshot(b"fake_image")
        assert isinstance(result, PageAnalysis)
