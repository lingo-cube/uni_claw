"""Unit tests for AI Strategy Advisor."""

import pytest

from src.ai.advisor import AIStrategyAdvisor
from src.ai.noop_advisor import NoOpAIAdvisor
from src.ai.mock_advisor import MockAIAdvisor
from src.ai.types import DecisionResult, ContainerInference
from src.context.traversal_context import TraversalContext
from src.state.content_tree import PageAnalysis, MenuInfo, Coordinate, Direction


@pytest.fixture
def sample_page_analysis():
    """Create a sample PageAnalysis for testing."""
    return PageAnalysis(
        level1_dir=Direction.LEFT,
        level1_menus=[MenuInfo(name="Settings", coordinate=Coordinate(x=0.5, y=0.5))],
        level2_dir=Direction.TOP,
        level2_menus=[],
        current_path=["Home", "Settings"],
        items=[],
    )


@pytest.fixture
def sample_context():
    """Create a sample TraversalContext for testing."""
    return TraversalContext(
        node_stack=["root", "settings"],
        current_path=["Home", "Settings"],
        visited_pages={"Home", "Settings"},
    )


class TestAIStrategyAdvisor:
    """Tests for AIStrategyAdvisor abstract interface."""

    def test_is_abstract(self):
        """Test AIStrategyAdvisor cannot be instantiated directly."""
        with pytest.raises(TypeError):
            AIStrategyAdvisor()

    def test_required_methods(self):
        """Test that required abstract methods are defined."""
        # Check abstract methods exist
        abstract_methods = AIStrategyAdvisor.__abstractmethods__
        assert "infer_container_type" in abstract_methods
        assert "decide_next_action" in abstract_methods
        assert "handle_exception" in abstract_methods


class TestNoOpAIAdvisor:
    """Tests for NoOpAIAdvisor."""

    def test_infer_container_type_returns_unknown(self, sample_page_analysis, sample_context):
        """Test NoOpAIAdvisor returns UNKNOWN with zero confidence."""
        advisor = NoOpAIAdvisor()
        result = advisor.infer_container_type(sample_page_analysis, sample_context)
        assert result.container_type == "UNKNOWN"
        assert result.confidence == 0.0
        assert result.matched_template is None

    def test_decide_next_action_returns_unsure(self, sample_page_analysis, sample_context):
        """Test NoOpAIAdvisor returns UNSURE with no node."""
        advisor = NoOpAIAdvisor()
        result, node = advisor.decide_next_action(
            "return_to_root", sample_page_analysis, sample_context
        )
        assert result == DecisionResult.UNSURE
        assert node is None

    def test_handle_exception_returns_give_up(self, sample_page_analysis, sample_context):
        """Test NoOpAIAdvisor returns GIVE_UP with no node."""
        advisor = NoOpAIAdvisor()
        exception = {"type": "ValueError", "message": "Test error"}
        result, node = advisor.handle_exception(
            exception, sample_page_analysis, sample_context
        )
        assert result == DecisionResult.GIVE_UP
        assert node is None


class TestMockAIAdvisor:
    """Tests for MockAIAdvisor."""

    def test_default_infer_container_type(self, sample_page_analysis, sample_context):
        """Test MockAIAdvisor default container inference."""
        advisor = MockAIAdvisor()
        result = advisor.infer_container_type(sample_page_analysis, sample_context)
        assert result.container_type == "MOCK_CONTAINER"
        assert result.confidence == 1.0
        assert result.matched_template == "mock_template"

    def test_default_decide_next_action(self, sample_page_analysis, sample_context):
        """Test MockAIAdvisor default decision."""
        advisor = MockAIAdvisor()
        result, node = advisor.decide_next_action(
            "return_to_root", sample_page_analysis, sample_context
        )
        assert result == DecisionResult.SUCCESS
        assert node is None  # Default is None

    def test_default_handle_exception(self, sample_page_analysis, sample_context):
        """Test MockAIAdvisor default exception handling."""
        advisor = MockAIAdvisor()
        exception = {"type": "ValueError", "message": "Test error"}
        result, node = advisor.handle_exception(
            exception, sample_page_analysis, sample_context
        )
        assert result == DecisionResult.SUCCESS
        assert node is None

    def test_custom_container_inference(self, sample_page_analysis, sample_context):
        """Test MockAIAdvisor with custom container inference."""
        custom_inference = ContainerInference("CUSTOM", 0.75, "custom_template")
        advisor = MockAIAdvisor(container_inference=custom_inference)
        result = advisor.infer_container_type(sample_page_analysis, sample_context)
        assert result.container_type == "CUSTOM"
        assert result.confidence == 0.75

    def test_custom_decision(self, sample_page_analysis, sample_context):
        """Test MockAIAdvisor with custom decision."""
        custom_node = {"action": "click", "text": "Back"}
        advisor = MockAIAdvisor(
            decision_result=DecisionResult.UNSURE,
            decision_node=custom_node,
        )
        result, node = advisor.decide_next_action(
            "return_to_root", sample_page_analysis, sample_context
        )
        assert result == DecisionResult.UNSURE
        assert node == custom_node

    def test_call_count_tracking(self, sample_page_analysis, sample_context):
        """Test MockAIAdvisor tracks method calls."""
        advisor = MockAIAdvisor()
        assert advisor.infer_count == 0
        assert advisor.decide_count == 0
        assert advisor.handle_count == 0

        advisor.infer_container_type(sample_page_analysis, sample_context)
        advisor.decide_next_action("test", sample_page_analysis, sample_context)
        advisor.handle_exception({}, sample_page_analysis, sample_context)

        assert advisor.infer_count == 1
        assert advisor.decide_count == 1
        assert advisor.handle_count == 1

    def test_reset_counts(self, sample_page_analysis, sample_context):
        """Test reset_counts clears tracking."""
        advisor = MockAIAdvisor()
        advisor.infer_container_type(sample_page_analysis, sample_context)
        advisor.reset_counts()
        assert advisor.infer_count == 0

    def test_infer_callback(self, sample_page_analysis, sample_context):
        """Test custom callback for infer_container_type."""
        advisor = MockAIAdvisor()
        custom_inference = ContainerInference("CALLBACK", 0.9)

        def callback(ui, ctx):
            return custom_inference

        advisor.set_infer_callback(callback)
        result = advisor.infer_container_type(sample_page_analysis, sample_context)
        assert result.container_type == "CALLBACK"

    def test_decide_callback(self, sample_page_analysis, sample_context):
        """Test custom callback for decide_next_action."""
        advisor = MockAIAdvisor()
        custom_node = {"action": "back"}

        def callback(goal, ui, ctx):
            return DecisionResult.SUCCESS, custom_node

        advisor.set_decide_callback(callback)
        result, node = advisor.decide_next_action(
            "test", sample_page_analysis, sample_context
        )
        assert node == custom_node

    def test_handle_callback(self, sample_page_analysis, sample_context):
        """Test custom callback for handle_exception."""
        advisor = MockAIAdvisor()
        recovery_node = {"action": "no_action"}

        def callback(exc, ui, ctx):
            return DecisionResult.UNSURE, recovery_node

        advisor.set_handle_callback(callback)
        result, node = advisor.handle_exception(
            {}, sample_page_analysis, sample_context
        )
        assert node == recovery_node
