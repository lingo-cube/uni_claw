"""Unit and integration tests for AI Capabilities.

Tests cover:
- types.py: Data types for all capabilities
- parse_to_plan.py: ParseToPlanCapability
- verify_page_type.py: VerifyPageTypeCapability
- screen_safety.py: ScreenSafetyCapability
- vision_analysis.py: VisionAnalysisCapability
- context_decision.py: ContextDecisionCapability
"""

import asyncio
from unittest.mock import AsyncMock, MagicMock, Mock, patch

import pytest

from src.ai.core.config import AIProviderConfig, RetryConfig
from src.ai.core.llm_client import LLMClient
from src.ai.core.validator import ResponseValidator, ValidationError
from src.ai.core.prompts import PromptRegistry
from src.ai.capabilities.parse_to_plan import ParseToPlanCapability
from src.ai.capabilities.verify_page_type import VerifyPageTypeCapability
from src.ai.capabilities.screen_safety import ScreenSafetyCapability
from src.ai.capabilities.vision_analysis import VisionAnalysisCapability
from src.ai.capabilities.context_decision import ContextDecisionCapability
from src.ai.capabilities.types import (
    TraversalPlan,
    TraversalNode,
    NodeOperation,
    NodeStrategy,
    PageTypeVerification,
    MismatchDetails,
    Suggestion,
    SafetyScreeningResult,
    SafetyEvaluation,
    PageLevelGuidance,
    ContextDecisionResult,
)
from src.ai.vision.mock_service import MockVisionService
from src.ai.vision.service import VisionService
from src.state.content_tree import (
    PageAnalysis,
    Direction,
    MenuInfo,
    MenuItem,
    MenuItemType,
    ExpectedAction,
    Coordinate,
)


# ============================================================================
# Tests for types.py
# ============================================================================

class TestNodeOperation:
    """Tests for NodeOperation dataclass."""

    def test_create_node_operation(self):
        """Test creating a NodeOperation."""
        op = NodeOperation(action="click", target={"by": "text", "value": "Settings"})
        assert op.action == "click"
        assert op.target == {"by": "text", "value": "Settings"}

    def test_node_operation_with_params(self):
        """Test NodeOperation with params."""
        op = NodeOperation(
            action="swipe",
            params={"direction": "down", "distance": 0.5}
        )
        assert op.params["direction"] == "down"


class TestNodeStrategy:
    """Tests for NodeStrategy dataclass."""

    def test_dynamic_match_strategy(self):
        """Test dynamic_match strategy."""
        strategy = NodeStrategy(
            type="dynamic_match",
            dynamic_rules={"match_by": "text"}
        )
        assert strategy.type == "dynamic_match"
        assert strategy.dynamic_rules["match_by"] == "text"

    def test_static_strategy(self):
        """Test static strategy."""
        strategy = NodeStrategy(
            type="static",
            static_children=["Item1", "Item2"]
        )
        assert strategy.static_children == ["Item1", "Item2"]


class TestTraversalNode:
    """Tests for TraversalNode dataclass."""

    def test_create_traversal_node(self):
        """Test creating a TraversalNode."""
        node = TraversalNode(
            node_id="root",
            name="Settings",
            node_type="container",
            operation=NodeOperation(action="click"),
            children_strategy=NodeStrategy(type="dynamic_match")
        )
        assert node.node_id == "root"
        assert node.name == "Settings"
        assert node.node_type == "container"


class TestTraversalPlan:
    """Tests for TraversalPlan dataclass."""

    def test_create_traversal_plan(self):
        """Test creating a TraversalPlan."""
        plan = TraversalPlan(
            entry_app="Settings",
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type="container",
                operation=NodeOperation(action="click"),
            ),
            mode="hybrid"
        )
        assert plan.entry_app == "Settings"
        assert plan.mode == "hybrid"
        assert plan.confidence == 1.0

    def test_traversal_plan_with_static_nodes(self):
        """Test TraversalPlan with static nodes."""
        static_node = TraversalNode(
            node_id="static1",
            name="Static",
            node_type="leaf",
            operation=NodeOperation(action="click"),
        )
        plan = TraversalPlan(
            entry_app="Music",
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type="container",
                operation=NodeOperation(action="click"),
            ),
            static_nodes=[static_node],
            mode="concrete"
        )
        assert len(plan.static_nodes) == 1
        assert plan.static_nodes[0].node_id == "static1"


class TestPageTypeVerification:
    """Tests for PageTypeVerification dataclass."""

    def test_create_verification_match(self):
        """Test creating a successful page type verification."""
        verification = PageTypeVerification(
            is_match=True,
            confidence=0.95,
            actual_type="menu_list",
            reasoning="Page has expected structure"
        )
        assert verification.is_match is True
        assert verification.confidence == 0.95
        assert verification.actual_type == "menu_list"

    def test_create_verification_mismatch(self):
        """Test creating a failed page type verification."""
        verification = PageTypeVerification(
            is_match=False,
            confidence=0.85,
            actual_type="dialog",
            reasoning="Dialog detected instead of menu",
            mismatch_details=MismatchDetails(
                unexpected_items=["OK button"]
            ),
            suggestion=Suggestion(action="close_popup", reason="Close dialog first")
        )
        assert verification.is_match is False
        assert verification.mismatch_details.unexpected_items == ["OK button"]
        assert verification.suggestion.action == "close_popup"


class TestSafetyEvaluation:
    """Tests for SafetyEvaluation dataclass."""

    def test_create_safe_evaluation(self):
        """Test creating a safe safety evaluation."""
        evaluation = SafetyEvaluation(
            name="Settings",
            safety_tag="safe",
            confidence=0.98,
            reason="Standard menu item"
        )
        assert evaluation.safety_tag == "safe"
        assert evaluation.confidence == 0.98

    def test_create_skip_evaluation(self):
        """Test creating a skip safety evaluation."""
        evaluation = SafetyEvaluation(
            name="Factory Reset",
            safety_tag="skip",
            confidence=1.0,
            reason="Destructive operation"
        )
        assert evaluation.safety_tag == "skip"


class TestSafetyScreeningResult:
    """Tests for SafetyScreeningResult dataclass."""

    def test_create_screening_result(self):
        """Test creating a safety screening result."""
        result = SafetyScreeningResult(
            evaluations=[
                SafetyEvaluation(name="Item1", safety_tag="safe", confidence=0.9, reason="OK"),
                SafetyEvaluation(name="Item2", safety_tag="skip", confidence=1.0, reason="Dangerous"),
            ],
            page_level_guidance=PageLevelGuidance(
                overall_safe_to_proceed=True,
                special_precautions=["Avoid Item2"]
            )
        )
        assert len(result.evaluations) == 2
        assert result.page_level_guidance.overall_safe_to_proceed is True


class TestContextDecisionResult:
    """Tests for ContextDecisionResult dataclass."""

    def test_create_success_decision(self):
        """Test creating a successful context decision."""
        decision = ContextDecisionResult(
            result="success",
            action="click",
            target={"by": "text", "value": "Settings"},
            reasoning="Navigate to Settings",
            confidence=0.92,
            safety_verified=True
        )
        assert decision.result == "success"
        assert decision.action == "click"
        assert decision.safety_verified is True

    def test_create_back_decision(self):
        """Test creating a back decision."""
        decision = ContextDecisionResult(
            result="success",
            action="back",
            target=None,
            reasoning="Return to previous page",
            confidence=0.95,
            safety_verified=True
        )
        assert decision.action == "back"
        assert decision.target is None


# ============================================================================
# Tests for ParseToPlanCapability
# ============================================================================

class TestParseToPlanCapability:
    """Tests for ParseToPlanCapability."""

    @pytest.fixture
    def config(self):
        """Create a test config."""
        return AIProviderConfig(api_key="test", retry=RetryConfig(max_attempts=1))

    @pytest.fixture
    def client(self, config):
        """Create a mock LLM client."""
        return AsyncMock(spec=LLMClient)

    @pytest.fixture
    def validator(self):
        """Create a mock validator."""
        val = ResponseValidator()
        # Register parser
        def parse_plan(data):
            return TraversalPlan(
                entry_app=data.get("entry_app"),
                root_node=TraversalNode(
                    node_id=data["root_node"]["node_id"],
                    name=data["root_node"]["name"],
                    node_type=data["root_node"]["node_type"],
                    operation=NodeOperation(**data["root_node"]["operation"]),
                ),
                mode=data.get("mode", "hybrid"),
            )
        val.register_parser("TraversalPlan", parse_plan)
        return val

    @pytest.fixture
    def prompt_registry(self, config):
        """Create a prompt registry."""
        return PromptRegistry(config)

    @pytest.fixture
    def capability(self, client, validator, config, prompt_registry):
        """Create the capability."""
        return ParseToPlanCapability(client, validator, config, prompt_registry)

    def test_prompt_keys(self, capability):
        """Test prompt key properties."""
        assert capability.system_prompt_key == "parse_task.system"
        assert capability.user_prompt_key == "parse_task.user"
        assert capability.response_type == "TraversalPlan"

    def test_response_schema(self, capability):
        """Test response schema structure."""
        schema = capability.response_schema
        assert schema["type"] == "object"
        assert "entry_app" in schema["properties"]
        assert "root_node" in schema["properties"]
        assert "mode" in schema["properties"]

    def test_prepare_input(self, capability):
        """Test prepare_input method."""
        result = capability.prepare_input("Go to Settings")
        assert result == {"instruction": "Go to Settings"}

    @pytest.mark.asyncio
    async def test_execute_success(self, capability, client):
        """Test successful execution."""
        # Mock LLM response
        client.call = AsyncMock(return_value={
            "entry_app": "Settings",
            "root_node": {
                "node_id": "root",
                "name": "Settings",
                "node_type": "container",
                "operation": {"action": "click"},
                "precondition": None,
                "children_strategy": {"type": "dynamic_match"},
                "error_policy": None,
            },
            "template_registry": "default",
            "mode": "hybrid",
        })

        result = await capability.execute_async("Go to Settings")

        assert isinstance(result, TraversalPlan)
        assert result.entry_app == "Settings"
        assert result.mode == "hybrid"


# ============================================================================
# Tests for VerifyPageTypeCapability
# ============================================================================

class TestVerifyPageTypeCapability:
    """Tests for VerifyPageTypeCapability."""

    @pytest.fixture
    def config(self):
        """Create a test config."""
        return AIProviderConfig(api_key="test", retry=RetryConfig(max_attempts=1))

    @pytest.fixture
    def client(self):
        """Create a mock LLM client."""
        return AsyncMock(spec=LLMClient)

    @pytest.fixture
    def validator(self):
        """Create a validator with parser."""
        val = ResponseValidator()
        def parse_verification(data):
            return PageTypeVerification(**data)
        val.register_parser("PageTypeVerification", parse_verification)
        return val

    @pytest.fixture
    def prompt_registry(self, config):
        """Create a prompt registry."""
        return PromptRegistry(config)

    @pytest.fixture
    def capability(self, client, validator, config, prompt_registry):
        """Create the capability."""
        return VerifyPageTypeCapability(client, validator, config, prompt_registry)

    @pytest.fixture
    def sample_page_analysis(self):
        """Create a sample PageAnalysis."""
        return PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[MenuInfo(name="DiLink", coordinate=Coordinate(x=0.08, y=0.12), active=True)],
            level2_dir=Direction.TOP,
            level2_menus=[MenuInfo(name="互联", coordinate=Coordinate(x=0.28, y=0.06), active=True)],
            current_path=["DiLink", "互联"],
            items=[
                MenuItem(
                    name="移动数据",
                    type=MenuItemType.MENU_ITEM,
                    expected_action=ExpectedAction.NAVIGATE,
                    coordinate=Coordinate(x=0.45, y=0.35),
                    expects_page_change=True,
                    expects_state_change=False,
                ),
            ],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=Coordinate(x=0.05, y=0.05),
            has_scroll=True,
            is_end_of_list=False,
        )

    def test_prompt_keys(self, capability):
        """Test prompt key properties."""
        assert capability.system_prompt_key == "verify_page.system"
        assert capability.user_prompt_key == "verify_page.user"
        assert capability.response_type == "PageTypeVerification"

    def test_prepare_input(self, capability, sample_page_analysis):
        """Test prepare_input method."""
        result = capability.prepare_input({
            "page_analysis": sample_page_analysis,
            "expected_type": "menu_list",
            "expected_page_name": "Connectivity",
        })

        assert result["expected_type"] == "menu_list"
        assert "DiLink" in result["level1_menus_summary"]
        assert "互联" in result["level2_menus_summary"]
        assert "移动数据" in result["elements_detail"]

    @pytest.mark.asyncio
    async def test_execute_success(self, capability, client, sample_page_analysis):
        """Test successful execution."""
        client.call = AsyncMock(return_value={
            "is_match": True,
            "confidence": 0.95,
            "actual_type": "menu_list",
            "reasoning": "Page structure matches expected type",
        })

        result = await capability.execute_async({
            "page_analysis": sample_page_analysis,
            "expected_type": "menu_list",
        })

        assert isinstance(result, PageTypeVerification)
        assert result.is_match is True
        assert result.actual_type == "menu_list"


# ============================================================================
# Tests for ScreenSafetyCapability
# ============================================================================

class TestScreenSafetyCapability:
    """Tests for ScreenSafetyCapability."""

    @pytest.fixture
    def config(self):
        """Create a test config."""
        return AIProviderConfig(api_key="test", retry=RetryConfig(max_attempts=1))

    @pytest.fixture
    def client(self):
        """Create a mock LLM client."""
        return AsyncMock(spec=LLMClient)

    @pytest.fixture
    def validator(self):
        """Create a validator with parser."""
        val = ResponseValidator()
        def parse_safety(data):
            evaluations = [SafetyEvaluation(**e) for e in data["evaluations"]]
            page_guidance = None
            if "page_level_guidance" in data and data["page_level_guidance"]:
                page_guidance = PageLevelGuidance(**data["page_level_guidance"])
            return SafetyScreeningResult(evaluations=evaluations, page_level_guidance=page_guidance)
        val.register_parser("SafetyScreeningResult", parse_safety)
        return val

    @pytest.fixture
    def prompt_registry(self, config):
        """Create a prompt registry."""
        return PromptRegistry(config)

    @pytest.fixture
    def capability(self, client, validator, config, prompt_registry):
        """Create the capability."""
        return ScreenSafetyCapability(client, validator, config, prompt_registry)

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
                MenuItem(name="Safe Item", type=MenuItemType.MENU_ITEM, expected_action=ExpectedAction.NAVIGATE, coordinate=Coordinate(x=0.5, y=0.3), expects_page_change=True, expects_state_change=False),
                MenuItem(name="Factory Reset", type=MenuItemType.BUTTON, expected_action=ExpectedAction.ACTION, coordinate=Coordinate(x=0.5, y=0.5), expects_page_change=False, expects_state_change=False),
            ],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        )

    def test_prompt_keys(self, capability):
        """Test prompt key properties."""
        assert capability.system_prompt_key == "screen_elements.system"
        assert capability.user_prompt_key == "screen_elements.user"
        assert capability.response_type == "SafetyScreeningResult"

    def test_prepare_input(self, capability, sample_page_analysis):
        """Test prepare_input method."""
        result = capability.prepare_input({
            "page_analysis": sample_page_analysis,
            "instruction": "Navigate to Settings",
            "page_type": "settings_group",
        })

        assert result["instruction"] == "Navigate to Settings"
        assert result["page_type"] == "settings_group"
        assert "Safe Item" in result["elements_list"]
        assert "Factory Reset" in result["elements_list"]

    @pytest.mark.asyncio
    async def test_execute_success(self, capability, client, sample_page_analysis):
        """Test successful execution."""
        client.call = AsyncMock(return_value={
            "evaluations": [
                {"name": "Safe Item", "safety_tag": "safe", "confidence": 0.98, "reason": "Safe"},
                {"name": "Factory Reset", "safety_tag": "skip", "confidence": 1.0, "reason": "Destructive"},
            ],
            "page_level_guidance": {
                "overall_safe_to_proceed": True,
                "special_precautions": ["Avoid Factory Reset"],
            },
        })

        result = await capability.execute_async({
            "page_analysis": sample_page_analysis,
            "instruction": "Test",
        })

        assert isinstance(result, SafetyScreeningResult)
        assert len(result.evaluations) == 2
        assert result.evaluations[0].safety_tag == "safe"
        assert result.evaluations[1].safety_tag == "skip"
        assert result.page_level_guidance.overall_safe_to_proceed is True


# ============================================================================
# Tests for VisionAnalysisCapability
# ============================================================================

class TestVisionAnalysisCapability:
    """Tests for VisionAnalysisCapability."""

    @pytest.fixture
    def mock_vision_service(self):
        """Create a mock vision service."""
        service = MockVisionService()
        service.add_response(PageAnalysis(
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
        ))
        return service

    @pytest.fixture
    def validator(self):
        """Create a validator."""
        return ResponseValidator()

    @pytest.fixture
    def capability(self, mock_vision_service, validator):
        """Create the capability."""
        return VisionAnalysisCapability(mock_vision_service, validator)

    def test_prompt_keys(self, capability):
        """Test prompt key properties."""
        assert capability.system_prompt_key == "vision_analysis.system"
        assert capability.user_prompt_key == "vision_analysis.user"
        assert capability.response_type == "PageAnalysis"

    def test_prepare_input(self, capability):
        """Test prepare_input returns empty dict."""
        result = capability.prepare_input(b"fake_image")
        assert result == {}

    @pytest.mark.asyncio
    async def test_execute_success(self, capability):
        """Test successful execution."""
        result = await capability.execute_async(b"fake_image")

        assert isinstance(result, PageAnalysis)
        assert result.level1_dir == Direction.LEFT

    @pytest.mark.asyncio
    async def test_execute_uses_vision_service(self, mock_vision_service, validator):
        """Test that execute uses the vision service."""
        capability = VisionAnalysisCapability(mock_vision_service, validator)

        await capability.execute_async(b"test_image")

        # The response should have been consumed from the queue
        assert len(mock_vision_service._responses) == 0


# ============================================================================
# Tests for ContextDecisionCapability
# ============================================================================

class TestContextDecisionCapability:
    """Tests for ContextDecisionCapability."""

    @pytest.fixture
    def config(self):
        """Create a test config."""
        return AIProviderConfig(api_key="test", retry=RetryConfig(max_attempts=1))

    @pytest.fixture
    def client(self):
        """Create a mock LLM client."""
        return AsyncMock(spec=LLMClient)

    @pytest.fixture
    def validator(self):
        """Create a validator with parser."""
        val = ResponseValidator()
        def parse_decision(data):
            return ContextDecisionResult(**data)
        val.register_parser("ContextDecisionResult", parse_decision)
        return val

    @pytest.fixture
    def prompt_registry(self, config):
        """Create a prompt registry."""
        return PromptRegistry(config)

    @pytest.fixture
    def capability(self, client, validator, config, prompt_registry):
        """Create the capability."""
        return ContextDecisionCapability(client, validator, config, prompt_registry)

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
                MenuItem(name="WiFi", type=MenuItemType.SWITCH, expected_action=ExpectedAction.TOGGLE, coordinate=Coordinate(x=0.5, y=0.3), expects_page_change=False, expects_state_change=True),
            ],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=Coordinate(x=0.05, y=0.05),
            has_scroll=False,
            is_end_of_list=False,
        )

    @pytest.fixture
    def sample_safety_result(self):
        """Create a sample safety screening result."""
        return SafetyScreeningResult(
            evaluations=[
                SafetyEvaluation(name="WiFi", safety_tag="safe", confidence=0.99, reason="Safe"),
            ],
            page_level_guidance=PageLevelGuidance(
                overall_safe_to_proceed=True,
                special_precautions=[],
            ),
        )

    def test_prompt_keys(self, capability):
        """Test prompt key properties."""
        assert capability.system_prompt_key == "make_decision.system"
        assert capability.user_prompt_key == "make_decision.user"
        assert capability.response_type == "ContextDecisionResult"

    def test_prepare_input(self, capability, sample_page_analysis, sample_safety_result):
        """Test prepare_input method."""
        result = capability.prepare_input({
            "page_analysis": sample_page_analysis,
            "safety_result": sample_safety_result,
            "context": {
                "node_stack": ["Root", "Settings"],
                "visited_pages": ["Home", "Settings"],
                "failed_nodes": [],
                "action_history": ["click", "navigate"],
            },
            "reason": "Need to explore WiFi settings",
        })

        assert result["reason"] == "Need to explore WiFi settings"
        assert "WiFi" in result["safe_elements"]
        assert "Root → Settings" in result["node_stack"]

    @pytest.mark.asyncio
    async def test_execute_success(self, capability, client, sample_page_analysis, sample_safety_result):
        """Test successful execution."""
        client.call = AsyncMock(return_value={
            "result": "success",
            "action": "click",
            "target": {"by": "text", "value": "WiFi"},
            "reasoning": "Click WiFi to explore",
            "confidence": 0.92,
            "safety_verified": True,
        })

        result = await capability.execute_async({
            "page_analysis": sample_page_analysis,
            "safety_result": sample_safety_result,
            "context": {},
            "reason": "Test",
        })

        assert isinstance(result, ContextDecisionResult)
        assert result.result == "success"
        assert result.action == "click"
        assert result.target["value"] == "WiFi"
        assert result.safety_verified is True

    @pytest.mark.asyncio
    async def test_execute_back_decision(self, capability, client, sample_page_analysis, sample_safety_result):
        """Test execution returns back decision."""
        client.call = AsyncMock(return_value={
            "result": "success",
            "action": "back",
            "target": None,
            "reasoning": "Return to previous",
            "confidence": 0.95,
            "safety_verified": True,
        })

        result = await capability.execute_async({
            "page_analysis": sample_page_analysis,
            "safety_result": sample_safety_result,
            "context": {},
            "reason": "Test",
        })

        assert result.action == "back"
        assert result.target is None


# ============================================================================
# Integration Tests
# ============================================================================

class TestCapabilityIntegration:
    """Integration tests for capabilities working together."""

    @pytest.fixture
    def config(self):
        """Create a test config."""
        return AIProviderConfig(api_key="test", retry=RetryConfig(max_attempts=1))

    @pytest.fixture
    def mock_client(self, config):
        """Create a mock LLM client."""
        client = AsyncMock(spec=LLMClient)
        return client

    @pytest.fixture
    def validator_with_parsers(self):
        """Create a validator with all parsers registered."""
        val = ResponseValidator()

        # TraversalPlan parser
        def parse_plan(data):
            return TraversalPlan(
                entry_app=data.get("entry_app"),
                root_node=TraversalNode(
                    node_id=data["root_node"]["node_id"],
                    name=data["root_node"]["name"],
                    node_type=data["root_node"]["node_type"],
                    operation=NodeOperation(**data["root_node"]["operation"]),
                ),
                mode=data.get("mode", "hybrid"),
            )
        val.register_parser("TraversalPlan", parse_plan)

        # PageTypeVerification parser
        def parse_verification(data):
            return PageTypeVerification(**data)
        val.register_parser("PageTypeVerification", parse_verification)

        # SafetyScreeningResult parser
        def parse_safety(data):
            evaluations = [SafetyEvaluation(**e) for e in data["evaluations"]]
            page_guidance = None
            if "page_level_guidance" in data and data["page_level_guidance"]:
                page_guidance = PageLevelGuidance(**data["page_level_guidance"])
            return SafetyScreeningResult(evaluations=evaluations, page_level_guidance=page_guidance)
        val.register_parser("SafetyScreeningResult", parse_safety)

        # ContextDecisionResult parser
        def parse_decision(data):
            return ContextDecisionResult(**data)
        val.register_parser("ContextDecisionResult", parse_decision)

        return val

    @pytest.fixture
    def prompt_registry(self, config):
        """Create a prompt registry."""
        return PromptRegistry(config)

    @pytest.mark.asyncio
    async def test_full_workflow_parse_to_decision(
        self,
        mock_client,
        validator_with_parsers,
        prompt_registry,
        config,
    ):
        """Test full workflow from parsing to decision making."""
        # Setup capabilities
        parse_cap = ParseToPlanCapability(mock_client, validator_with_parsers, config, prompt_registry)
        decision_cap = ContextDecisionCapability(mock_client, validator_with_parsers, config, prompt_registry)

        # Mock responses
        mock_client.call = AsyncMock(side_effect=[
            # Parse response
            {
                "entry_app": "Settings",
                "root_node": {
                    "node_id": "root",
                    "name": "Settings",
                    "node_type": "container",
                    "operation": {"action": "click"},
                    "precondition": None,
                    "children_strategy": {"type": "dynamic_match"},
                    "error_policy": None,
                },
                "template_registry": "default",
                "mode": "hybrid",
            },
            # Decision response
            {
                "result": "success",
                "action": "click",
                "target": {"by": "text", "value": "WiFi"},
                "reasoning": "Explore WiFi settings",
                "confidence": 0.9,
                "safety_verified": True,
            },
        ])

        # Execute parse
        plan = await parse_cap.execute_async("Go to WiFi Settings")
        assert plan.entry_app == "Settings"

        # Execute decision
        sample_page = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[
                MenuItem(name="WiFi", type=MenuItemType.SWITCH, expected_action=ExpectedAction.TOGGLE, coordinate=Coordinate(x=0.5, y=0.3), expects_page_change=False, expects_state_change=True),
            ],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        )
        safety_result = SafetyScreeningResult(
            evaluations=[SafetyEvaluation(name="WiFi", safety_tag="safe", confidence=0.99, reason="Safe")],
            page_level_guidance=PageLevelGuidance(overall_safe_to_proceed=True),
        )
        decision = await decision_cap.execute_async({
            "page_analysis": sample_page,
            "safety_result": safety_result,
            "context": {},
            "reason": "Test",
        })

        assert decision.action == "click"
        assert decision.target["value"] == "WiFi"
