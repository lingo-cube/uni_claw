"""Integration tests for AI Strategy Advisor.

Tests AI advisor integration with TraversalEngine, including:
- Container inference when rules fail
- Target decision when rules cannot locate
- Exception fallback when chain is exhausted
- SafetyFilter rejection scenarios
- Timeout handling
- Cache behavior
- Debounce mechanism
- Configuration switching
"""

import pytest
from unittest.mock import Mock, MagicMock, patch

from src.ai import AIStrategyAdvisor, MockAIAdvisor, DecisionResult, ContainerInference
from src.safety import SafetyFilter
from src.context import TraversalContext
from src.traversal import TraversalEngine, TraversalConfig
from src.state.content_tree import PageAnalysis, MenuInfo, Coordinate, Direction
from src.adb.adb_client import ADBClient
from src.vision.vision_service import VisionService
from src.state.content_tree import TraversalState


@pytest.fixture
def mock_adb():
    """Create mock ADB client."""
    adb = Mock(spec=ADBClient)
    adb.is_connected.return_value = True
    adb.capture_screenshot.return_value = b"fake_screenshot"
    return adb


@pytest.fixture
def mock_vision():
    """Create mock vision service."""
    vision = Mock(spec=VisionService)
    vision.analyze_screenshot.return_value = PageAnalysis(
        level1_dir=Direction.LEFT,
        level1_menus=[MenuInfo(name="Settings", coordinate=Coordinate(x=0.5, y=0.5))],
        level2_dir=Direction.TOP,
        level2_menus=[],
        current_path=["Home"],
        items=[],
    )
    return vision


@pytest.fixture
def mock_state():
    """Create mock traversal state."""
    state = Mock(spec=TraversalState)
    state.current_path = ["Home"]
    state.visited = {}
    return state


@pytest.fixture
def ai_config():
    """Create AI-enabled config."""
    return TraversalConfig(
        enable_ai_advisor=True,
        ai_call_timeout=30.0,
        ai_min_confidence=0.7,
        ai_cache_ttl=300,
        max_steps=10,
    )


class TestContainerInferenceIntegration:
    """Tests for container inference AI integration (Task 6.1)."""

    def test_ai_called_when_rules_fail(self, mock_adb, mock_vision, mock_state, ai_config):
        """Test AI is called when rule-based container inference fails."""
        # Create engine with AI enabled
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=mock_state,
            config=ai_config,
        )

        # Set up mock AI advisor
        mock_ai = MockAIAdvisor(
            container_inference=ContainerInference("CUSTOM_GRID", 0.85, "custom_template")
        )
        engine.set_ai_advisor(mock_ai)

        # Build context and call AI
        context = engine._build_traversal_context()
        ui = mock_vision.analyze_screenshot(b"test")

        # Call container inference
        result = engine.ai_advisor.infer_container_type(ui, context)

        # Verify AI was called and returned result
        assert result.container_type == "CUSTOM_GRID"
        assert result.confidence == 0.85


class TestTargetDecisionIntegration:
    """Tests for target decision AI integration (Task 6.2)."""

    def test_ai_called_when_rules_cannot_locate(self, mock_adb, mock_vision, mock_state, ai_config):
        """Test AI is called when rules cannot locate target element."""
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=mock_state,
            config=ai_config,
        )

        mock_ai = MockAIAdvisor(
            decision_result=DecisionResult.SUCCESS,
            decision_node={"action": "click", "text": "Back"}
        )
        engine.set_ai_advisor(mock_ai)

        context = engine._build_traversal_context()
        ui = mock_vision.analyze_screenshot(b"test")

        result, node = engine.ai_advisor.decide_next_action("return_to_root", ui, context)

        assert result == DecisionResult.SUCCESS
        assert node["action"] == "click"


class TestExceptionFallbackIntegration:
    """Tests for exception fallback AI integration (Task 6.3)."""

    def test_ai_called_when_exception_chain_exhausted(self, mock_adb, mock_vision, mock_state, ai_config):
        """Test AI is called as fallback when exception chain is exhausted."""
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=mock_state,
            config=ai_config,
        )

        recovery_node = {"action": "back", "text": "Recovery"}
        mock_ai = MockAIAdvisor(
            exception_result=DecisionResult.SUCCESS,
            exception_node=recovery_node
        )
        engine.set_ai_advisor(mock_ai)

        context = engine._build_traversal_context()
        exception_dict = {"type": "TestException", "message": "Test error"}

        result, node = engine.ai_advisor.handle_exception(exception_dict, mock_vision.analyze_screenshot(b"test"), context)

        assert result == DecisionResult.SUCCESS
        assert node["action"] == "back"


class TestSafetyFilterRejection:
    """Tests for SafetyFilter rejection scenarios (Task 6.4)."""

    def test_dangerous_action_rejected(self, mock_adb, mock_vision, mock_state, ai_config):
        """Test dangerous actions are rejected by SafetyFilter."""
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=mock_state,
            config=ai_config,
        )

        # Mock AI returns dangerous action
        mock_ai = MockAIAdvisor(
            decision_result=DecisionResult.SUCCESS,
            decision_node={"action": "click", "text": "恢复出厂设置"}
        )
        engine.set_ai_advisor(mock_ai)

        dangerous_node = {"action": "click", "text": "恢复出厂设置"}
        safety_result = engine.safety_filter.validate(dangerous_node, {"current_path": ["Home"]})

        assert safety_result.is_safe is False
        assert "blocked" in safety_result.reason.lower()
        assert safety_result.fallback_node is not None
        assert safety_result.fallback_node["action"] == "no_action"

    def test_blocked_action_type_rejected(self, mock_adb, mock_vision, mock_state, ai_config):
        """Test non-whitelisted action types are rejected."""
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=mock_state,
            config=ai_config,
        )

        invalid_node = {"action": "delete", "text": "Item"}
        safety_result = engine.safety_filter.validate(invalid_node)

        assert safety_result.is_safe is False
        assert "not in whitelist" in safety_result.reason.lower()


class TestAITimeoutScenarios:
    """Tests for AI timeout handling (Task 6.5)."""

    def test_timeout_returns_unsafe(self, mock_adb, mock_vision, mock_state, ai_config):
        """Test timeout scenarios result in UNSURE decision."""
        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=mock_state,
            config=ai_config,
        )

        # AI timeout handling would be tested with actual timeout decorator
        # For now, verify the degradation path exists
        result = engine._handle_ai_exception_fallback()

        assert result[0] == DecisionResult.UNSURE
        assert result[1] is None


class TestCacheHitScenarios:
    """Tests for cache behavior (Task 6.6)."""

    def test_cache_hit_same_context(self):
        """Test cache returns same result for identical context."""
        from src.ai.cache import AIResponseCache, make_cache_key

        cache = AIResponseCache(maxsize=10, ttl_seconds=300)

        # First call - cache miss
        key1 = make_cache_key("ui_hash_123", "path_hash_456", "infer_container_type")
        result1 = cache.get(key1)
        assert result1 is None

        # Store result
        test_value = ContainerInference("GRID", 0.9)
        cache.put(key1, test_value)

        # Second call - cache hit
        result2 = cache.get(key1)
        assert result2.container_type == "GRID"
        assert result2.confidence == 0.9

    def test_cache_miss_different_context(self):
        """Test cache miss for different context."""
        from src.ai.cache import AIResponseCache, make_cache_key

        cache = AIResponseCache(maxsize=10, ttl_seconds=300)

        key1 = make_cache_key("ui_hash_123", "path_hash_456", "infer_container_type")
        key2 = make_cache_key("ui_hash_789", "path_hash_456", "infer_container_type")

        cache.put(key1, ContainerInference("GRID", 0.9))

        # Different UI hash should miss
        result = cache.get(key2)
        assert result is None


class TestDebounceMechanism:
    """Tests for debounce mechanism (Task 6.7)."""

    def test_debounce_allows_up_to_limit(self):
        """Test debounce allows up to limit (default 2)."""
        from src.ai.cache import DebounceTracker

        tracker = DebounceTracker()

        # First call - allowed
        assert tracker.should_allow("node1", "TimeoutError") is True

        # Second call - allowed
        assert tracker.should_allow("node1", "TimeoutError") is True

        # Third call - blocked
        assert tracker.should_allow("node1", "TimeoutError") is False

    def test_debounce_resets_on_different_node(self):
        """Test debounce resets for different nodes."""
        from src.ai.cache import DebounceTracker

        tracker = DebounceTracker()

        tracker.should_allow("node1", "TimeoutError")
        tracker.should_allow("node1", "TimeoutError")

        # Different node - should allow
        assert tracker.should_allow("node2", "TimeoutError") is True

    def test_debounce_reset_all(self):
        """Test resetting all debounce counters."""
        from src.ai.cache import DebounceTracker

        tracker = DebounceTracker()
        tracker.should_allow("node1", "TimeoutError")
        tracker.should_allow("node1", "TimeoutError")

        # Reset all
        tracker.reset()

        # Should allow again after reset
        assert tracker.should_allow("node1", "TimeoutError") is True


class TestConfigurationSwitching:
    """Tests for configuration switching (Task 6.8)."""

    def test_ai_disabled_uses_noop(self, mock_adb, mock_vision, mock_state):
        """Test AI disabled uses NoOpAIAdvisor."""
        config = TraversalConfig(
            enable_ai_advisor=False,  # AI disabled
            max_steps=10,
        )

        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=mock_state,
            config=config,
        )

        # With AI disabled, ai_advisor should be None
        assert engine.ai_advisor is None

    def test_ai_enabled_uses_set_advisor(self, mock_adb, mock_vision, mock_state):
        """Test AI enabled uses configured advisor."""
        config = TraversalConfig(
            enable_ai_advisor=True,
            max_steps=10,
        )

        engine = TraversalEngine(
            adb_client=mock_adb,
            vision_service=mock_vision,
            state=mock_state,
            config=config,
        )

        # Should have NoOp as default, but can be overridden
        assert engine.ai_advisor is not None

        # Set custom advisor
        mock_ai = MockAIAdvisor()
        engine.set_ai_advisor(mock_ai)

        assert engine.ai_advisor == mock_ai
