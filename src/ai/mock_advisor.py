"""Mock AI Advisor for testing.

This module provides a mock implementation of AIStrategyAdvisor that
returns predefined values, useful for unit and integration tests.
"""

from typing import Optional, Tuple, Callable

from ..context.traversal_context import TraversalContext
from ..state.content_tree import PageAnalysis
from .advisor import AIStrategyAdvisor
from .ai_types import DecisionResult, ContainerInference


class MockAIAdvisor(AIStrategyAdvisor):
    """Mock implementation of AIStrategyAdvisor for testing.

    This advisor returns predefined values, allowing tests to verify
    AI integration without calling actual LLM services.

    Args:
        container_inference: Predefined ContainerInference to return
        decision_result: Predefined DecisionResult for decide_next_action
        decision_node: Predefined node data for decide_next_action
        exception_result: Predefined DecisionResult for handle_exception
        exception_node: Predefined node data for handle_exception
    """

    def __init__(
        self,
        container_inference: Optional[ContainerInference] = None,
        decision_result: DecisionResult = DecisionResult.SUCCESS,
        decision_node: Optional[dict] = None,
        exception_result: DecisionResult = DecisionResult.SUCCESS,
        exception_node: Optional[dict] = None,
    ):
        """Initialize mock advisor with predefined responses.

        Args:
            container_inference: Predefined container inference result
            decision_result: Predefined decision result
            decision_node: Predefined node data for decisions
            exception_result: Predefined exception result
            exception_node: Predefined node data for exceptions
        """
        self._container_inference = container_inference or ContainerInference(
            container_type="MOCK_CONTAINER",
            confidence=1.0,
            matched_template="mock_template",
        )
        self._decision_result = decision_result
        self._decision_node = decision_node
        self._exception_result = exception_result
        self._exception_node = exception_node

        # Optional custom callbacks for dynamic behavior
        self._infer_callback: Optional[Callable] = None
        self._decide_callback: Optional[Callable] = None
        self._handle_callback: Optional[Callable] = None

        # Track call counts
        self.infer_count = 0
        self.decide_count = 0
        self.handle_count = 0

    def set_infer_callback(self, callback: Callable) -> None:
        """Set a custom callback for infer_container_type.

        Args:
            callback: Function that takes (ui, context) and returns ContainerInference
        """
        self._infer_callback = callback

    def set_decide_callback(self, callback: Callable) -> None:
        """Set a custom callback for decide_next_action.

        Args:
            callback: Function that takes (goal, ui, context) and returns (result, node)
        """
        self._decide_callback = callback

    def set_handle_callback(self, callback: Callable) -> None:
        """Set a custom callback for handle_exception.

        Args:
            callback: Function that takes (exception, ui, context) and returns (result, node)
        """
        self._handle_callback = callback

    def infer_container_type(
        self, ui: PageAnalysis, context: TraversalContext
    ) -> ContainerInference:
        """Return predefined or callback-generated container inference.

        Args:
            ui: Current page analysis
            context: Current traversal context

        Returns:
            Predefined ContainerInference result
        """
        self.infer_count += 1
        if self._infer_callback:
            return self._infer_callback(ui, context)
        return self._container_inference

    def decide_next_action(
        self,
        goal: str,
        ui: PageAnalysis,
        context: TraversalContext,
    ) -> Tuple[DecisionResult, Optional[dict]]:
        """Return predefined or callback-generated decision.

        Args:
            goal: The goal to achieve
            ui: Current page analysis
            context: Current traversal context

        Returns:
            Predefined decision result and node data
        """
        self.decide_count += 1
        if self._decide_callback:
            return self._decide_callback(goal, ui, context)
        return self._decision_result, self._decision_node

    def handle_exception(
        self,
        exception: dict,
        ui: PageAnalysis,
        context: TraversalContext,
    ) -> Tuple[DecisionResult, Optional[dict]]:
        """Return predefined or callback-generated exception handling.

        Args:
            exception: Exception context
            ui: Current page analysis
            context: Current traversal context

        Returns:
            Predefined exception result and node data
        """
        self.handle_count += 1
        if self._handle_callback:
            return self._handle_callback(exception, ui, context)
        return self._exception_result, self._exception_node

    def reset_counts(self) -> None:
        """Reset call count tracking."""
        self.infer_count = 0
        self.decide_count = 0
        self.handle_count = 0


__all__ = ["MockAIAdvisor"]
