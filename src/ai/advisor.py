"""AI Strategy Advisor abstract interface."""

from abc import ABC, abstractmethod
from typing import Optional, Tuple

from ..context.traversal_context import TraversalContext
from ..state.content_tree import PageAnalysis
from .ai_types import DecisionResult, ContainerInference


class AIStrategyAdvisor(ABC):
    """Abstract base class for AI strategy advisors.

    This class defines the interface for AI-powered decision making during
    UI traversal. Implementations can use real LLMs (Phase 3+) or mock/test
    implementations for development and testing.
    """

    @abstractmethod
    def infer_container_type(
        self, ui: PageAnalysis, context: TraversalContext
    ) -> ContainerInference:
        """Infer the container type of the current page.

        Args:
            ui: Current page analysis
            context: Current traversal context

        Returns:
            ContainerInference with type, confidence, and matched template
        """

    @abstractmethod
    def decide_next_action(
        self,
        goal: str,
        ui: PageAnalysis,
        context: TraversalContext,
    ) -> Tuple[DecisionResult, Optional[dict]]:
        """Decide the next action to achieve a goal.

        Args:
            goal: The goal to achieve (e.g., "return_to_root", "close_popup")
            ui: Current page analysis
            context: Current traversal context

        Returns:
            Tuple of (DecisionResult, Optional[node_data]) where node_data
            contains the information needed to create a TraversalNode
        """

    @abstractmethod
    def handle_exception(
        self,
        exception: dict,
        ui: PageAnalysis,
        context: TraversalContext,
    ) -> Tuple[DecisionResult, Optional[dict]]:
        """Handle an exception during traversal.

        Args:
            exception: Exception context dict with type, message, etc.
            ui: Current page analysis
            context: Current traversal context

        Returns:
            Tuple of (DecisionResult, Optional[node_data]) where node_data
            contains recovery action information
        """


__all__ = ["AIStrategyAdvisor"]
