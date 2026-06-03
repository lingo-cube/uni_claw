"""Integration tests for UniBrain with TraversalEngine and async/sync execution.

Tests cover:
- Task 5.4: Integration tests with TraversalEngine
- Task 5.5: Async/sync execution wrapper compatibility
"""

import asyncio
from unittest.mock import AsyncMock, MagicMock, Mock, patch
from threading import Thread
import time

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
    SafetyEvaluation,
    PageLevelGuidance,
)
from src.ai.types import DecisionResult, ContainerInference
from src.traversal.traversal_engine import TraversalEngine, TraversalConfig
from src.context.traversal_context import TraversalContext
from src.state.content_tree import (
    PageAnalysis,
    TraversalState,
    Direction,
    MenuInfo,
    MenuItem,
    MenuItemType,
    ExpectedAction,
    Coordinate,
)
from src.adb.adb_client import ADBClient
from src.vision.vision_service import VisionService


# ============================================================================
# Fixtures
# ============================================================================

@pytest.fixture
def ai_config():
    """Create a test AI config."""
    return AIProviderConfig(
        api_key="test-key",
        retry=RetryConfig(max_attempts=1)
    )


@pytest.fixture
def vision_config():
    """Create a test vision config."""
    return VisionConfig(service_type="mock")


@pytest.fixture
def unibrain(ai_config, vision_config):
    """Create a UniBrain provider for testing."""
    return UniBrain(ai_config, vision_config)


@pytest.fixture
def mock_adb():
    """Create a mock ADB client."""
    adb = MagicMock(spec=ADBClient)
    adb.get_screenshot = MagicMock(return_value=b"fake_screenshot")
    adb.tap = MagicMock()
    adb.back = MagicMock()
    return adb


@pytest.fixture
def mock_vision():
    """Create a mock vision service."""
    vision = MagicMock(spec=VisionService)
    vision.analyze_screenshot = MagicMock(return_value=PageAnalysis(
        level1_dir=Direction.LEFT,
        level1_menus=[],
        level2_dir=Direction.TOP,
        level2_menus=[],
        current_path=[],
        items=[
            MenuItem(
                name="TestItem",
                type=MenuItemType.MENU_ITEM,
                expected_action=ExpectedAction.NAVIGATE,
                coordinate=Coordinate(x=0.5, y=0.5),
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
    ))
    return vision


@pytest.fixture
def traversal_state():
    """Create a TraversalState for testing."""
    return TraversalState(root_name="TestRoot")


@pytest.fixture
def traversal_config():
    """Create a TraversalConfig with AI advisor enabled."""
    return TraversalConfig(
        enable_ai_advisor=True,
        enable_exception_handling=False,
        use_graph_mode=False,
    )


# ============================================================================
# Task 5.4: Integration Tests with TraversalEngine
# ============================================================================

class TestTraversalEngineIntegration:
    """Integration tests for UniBrain with TraversalEngine."""

    def test_set_unibrain_as_advisor(self, unibrain, mock_adb, mock_vision, traversal_state, traversal_config):
        """Test setting UniBrain as AI advisor in TraversalEngine."""
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=traversal_state,
            config=traversal_config,
        )

        # Set UniBrain as the advisor
        engine.set_ai_advisor(unibrain)

        assert engine.ai_advisor is unibrain
        assert isinstance(engine.ai_advisor, UniBrain)

    def test_traversal_context_building(self, unibrain, mock_adb, mock_vision, traversal_state, traversal_config):
        """Test that TraversalEngine can build context for AI calls."""
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=traversal_state,
            config=traversal_config,
        )
        engine.set_ai_advisor(unibrain)

        # Build traversal context
        context = engine._build_traversal_context()

        assert isinstance(context, TraversalContext)
        assert context.current_path is not None
        assert context.visited_pages is not None

    @patch('src.ai.capabilities.verify_page_type.VerifyPageTypeCapability.execute')
    def test_infer_container_type_integration(self, mock_verify, unibrain, mock_adb, mock_vision, traversal_state, traversal_config):
        """Test container type inference through TraversalEngine."""
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=traversal_state,
            config=traversal_config,
        )
        engine.set_ai_advisor(unibrain)

        # Mock verification response
        mock_verify.return_value = PageTypeVerification(
            is_match=True,
            confidence=0.95,
            actual_type="menu_list",
            reasoning="Matches menu list pattern",
        )

        # Get page analysis
        page_analysis = mock_vision.analyze_screenshot(b"test")

        # Infer container type
        context = engine._build_traversal_context()
        result = engine.ai_advisor.infer_container_type(page_analysis, context)

        assert isinstance(result, ContainerInference)
        assert result.container_type == "menu_list"
        assert result.confidence == 0.95

    @patch('src.ai.capabilities.screen_safety.ScreenSafetyCapability.execute')
    @patch('src.ai.capabilities.context_decision.ContextDecisionCapability.execute')
    def test_decide_next_action_integration(self, mock_decision, mock_safety, unibrain, mock_adb, mock_vision, traversal_state, traversal_config):
        """Test next action decision through TraversalEngine."""
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=traversal_state,
            config=traversal_config,
        )
        engine.set_ai_advisor(unibrain)

        # Mock safety and decision responses
        mock_safety.return_value = SafetyScreeningResult(
            evaluations=[
                SafetyEvaluation(name="TestItem", safety_tag="safe", confidence=0.99, reason="Safe"),
            ],
            page_level_guidance=PageLevelGuidance(overall_safe_to_proceed=True),
        )

        mock_decision.return_value = ContextDecisionResult(
            result="success",
            action="click",
            target={"by": "text", "value": "TestItem"},
            reasoning="Click to explore",
            confidence=0.92,
            safety_verified=True,
        )

        # Get page analysis
        page_analysis = mock_vision.analyze_screenshot(b"test")

        # Decide next action
        context = engine._build_traversal_context()
        result, node_data = engine.ai_advisor.decide_next_action(
            "Explore",
            page_analysis,
            context,
        )

        assert result == DecisionResult.SUCCESS
        assert node_data is not None
        assert node_data["action"] == "click"

    def test_ai_cache_integration(self, unibrain, mock_adb, mock_vision, traversal_state, traversal_config):
        """Test that AI cache is used with UniBrain."""
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=traversal_state,
            config=traversal_config,
        )
        engine.set_ai_advisor(unibrain)

        assert engine.ai_cache is not None
        assert engine.debounce_tracker is not None

    def test_safety_filter_integration(self, unibrain, mock_adb, mock_vision, traversal_state, traversal_config):
        """Test that safety filter is initialized with UniBrain."""
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=traversal_state,
            config=traversal_config,
        )
        engine.set_ai_advisor(unibrain)

        assert engine.safety_filter is not None

    def test_ai_advisor_disabled_when_config_false(self, mock_adb, mock_vision, traversal_state):
        """Test that AI advisor is not initialized when disabled in config."""
        config = TraversalConfig(enable_ai_advisor=False)
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=traversal_state,
            config=config,
        )

        assert engine.ai_advisor is None
        assert engine.safety_filter is None
        assert engine.ai_cache is None


# ============================================================================
# Task 5.5: Async/Sync Execution Wrapper Compatibility
# ============================================================================

class TestAsyncSyncWrapper:
    """Tests for async/sync execution wrapper compatibility."""

    @pytest.fixture
    def sync_unibrain(self):
        """Create a UniBrain with sync-compatible config."""
        config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        vision = VisionConfig(service_type="mock")
        return UniBrain(config, vision)

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
                    name="TestItem",
                    type=MenuItemType.MENU_ITEM,
                    expected_action=ExpectedAction.NAVIGATE,
                    coordinate=Coordinate(x=0.5, y=0.5),
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

    def test_sync_execute_returns_result(self, sync_unibrain, sample_page_analysis):
        """Test that sync execute method returns valid results."""
        # Vision analysis is synchronous
        result = sync_unibrain.analyze_screenshot(b"test_image")

        assert isinstance(result, PageAnalysis)

    def test_capability_sync_wrapper(self, sync_unibrain):
        """Test that capabilities can be called synchronously."""
        capability = sync_unibrain.capabilities["vision"]

        # This should work synchronously
        result = capability.execute(b"test_image")

        assert isinstance(result, PageAnalysis)

    def test_multiple_sync_calls_sequential(self, sync_unibrain):
        """Test multiple sequential sync calls work correctly."""
        results = []

        for i in range(3):
            result = sync_unibrain.analyze_screenshot(b"test_image")
            results.append(result)

        assert len(results) == 3
        assert all(isinstance(r, PageAnalysis) for r in results)

    def test_sync_and_async_methods_same_result(self, sync_unibrain):
        """Test that sync and async methods produce equivalent results."""
        capability = sync_unibrain.capabilities["vision"]

        # Sync call
        sync_result = capability.execute(b"test_image")

        # Async call (simulate with run_until_complete)
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        try:
            async_result = loop.run_until_complete(capability.execute_async(b"test_image"))
        finally:
            loop.close()

        # Both should return PageAnalysis
        assert isinstance(sync_result, PageAnalysis)
        assert isinstance(async_result, PageAnalysis)

    def test_sync_call_in_thread_context(self, sync_unibrain):
        """Test that sync calls work from different thread contexts."""
        results = []
        exception_occurred = False

        def worker():
            try:
                result = sync_unibrain.analyze_screenshot(b"test_image")
                results.append(result)
            except Exception as e:
                nonlocal exception_occurred
                exception_occurred = True

        # Run in thread
        thread = Thread(target=worker)
        thread.start()
        thread.join(timeout=5)

        assert not exception_occurred, "Exception occurred in thread"
        assert len(results) == 1
        assert isinstance(results[0], PageAnalysis)

    def test_capability_execute_is_callable_sync(self, sync_unibrain):
        """Test that all capability execute methods can be called synchronously."""
        # All capabilities should have an execute method
        for cap_name, capability in sync_unibrain.capabilities.items():
            assert hasattr(capability, 'execute')
            assert callable(capability.execute)

    def test_vision_capability_sync_interface(self, sync_unibrain):
        """Test that vision capability has proper sync interface."""
        vision_cap = sync_unibrain.capabilities["vision"]

        # Should have both sync and async methods
        assert hasattr(vision_cap, 'execute')
        assert hasattr(vision_cap, 'execute_async')

        # Sync execute should be callable
        assert callable(vision_cap.execute)
        assert callable(vision_cap.execute_async)


# ============================================================================
# Combined Integration Tests
# ============================================================================

class TestCombinedIntegration:
    """Combined integration tests for full workflow."""

    @patch('src.ai.capabilities.verify_page_type.VerifyPageTypeCapability.execute')
    @patch('src.ai.capabilities.screen_safety.ScreenSafetyCapability.execute')
    @patch('src.ai.capabilities.context_decision.ContextDecisionCapability.execute')
    def test_full_decision_workflow(
        self,
        mock_decision,
        mock_safety,
        mock_verify,
        unibrain,
        mock_adb,
        mock_vision,
        traversal_state,
        traversal_config,
    ):
        """Test full workflow from vision analysis to decision making."""
        # Setup engine with UniBrain
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=traversal_state,
            config=traversal_config,
        )
        engine.set_ai_advisor(unibrain)

        # Mock responses
        mock_verify.return_value = PageTypeVerification(
            is_match=True,
            confidence=0.95,
            actual_type="menu_list",
            reasoning="Matches",
        )

        mock_safety.return_value = SafetyScreeningResult(
            evaluations=[SafetyEvaluation(name="Item", safety_tag="safe", confidence=0.99, reason="OK")],
            page_level_guidance=PageLevelGuidance(overall_safe_to_proceed=True),
        )

        mock_decision.return_value = ContextDecisionResult(
            result="success",
            action="click",
            target={"by": "text", "value": "Item"},
            reasoning="Click item",
            confidence=0.92,
            safety_verified=True,
        )

        # Full workflow
        page_analysis = mock_vision.analyze_screenshot(b"test")
        context = engine._build_traversal_context()

        # Infer container
        container = engine.ai_advisor.infer_container_type(page_analysis, context)
        assert container.container_type == "menu_list"

        # Decide action
        decision, node_data = engine.ai_advisor.decide_next_action(
            "Explore",
            page_analysis,
            context,
        )
        assert decision == DecisionResult.SUCCESS
        assert node_data["action"] == "click"
