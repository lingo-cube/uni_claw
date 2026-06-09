"""No-op AI Advisor implementation.

This module provides the default AI advisor implementation that returns
safe default values, ensuring existing functionality is not affected.
"""

from typing import Optional, Tuple

from ..models.traversal_context import TraversalContext
from ..models.content_models import PageAnalysis
from .advisor import AIStrategyAdvisor
from .ai_types import DecisionResult, ContainerInference


class NoOpAIAdvisor(AIStrategyAdvisor):
    """No-op implementation of AIStrategyAdvisor.

    This implementation returns safe default values for all methods,
    ensuring that existing traversal behavior is not affected when AI
    features are disabled.
    """

    def infer_container_type(
        self, ui: PageAnalysis, context: TraversalContext
    ) -> ContainerInference:
        """Return unknown container type with zero confidence.

        Args:
            ui: Current page analysis (ignored)
            context: Current traversal context (ignored)

        Returns:
            ContainerInference with UNKNOWN type and 0.0 confidence
        """
        return ContainerInference(
            container_type="UNKNOWN",
            confidence=0.0,
            matched_template=None,
        )

    def decide_next_action(
        self,
        goal: str,
        ui: PageAnalysis,
        context: TraversalContext,
    ) -> Tuple[DecisionResult, Optional[dict]]:
        """Return UNSURE to let rule engine handle decisions.

        Args:
            goal: The goal to achieve (ignored)
            ui: Current page analysis (ignored)
            context: Current traversal context (ignored)

        Returns:
            Tuple of (UNSURE, None) to defer to rule engine
        """
        return DecisionResult.UNSURE, None

    def handle_exception(
        self,
        exception: dict,
        ui: PageAnalysis,
        context: TraversalContext,
    ) -> Tuple[DecisionResult, Optional[dict]]:
        """Return GIVE_UP to terminate traversal on exceptions.

        Args:
            exception: Exception context (ignored)
            ui: Current page analysis (ignored)
            context: Current traversal context (ignored)

        Returns:
            Tuple of (GIVE_UP, None) to terminate traversal
        """
        return DecisionResult.GIVE_UP, None


__all__ = ["NoOpAIAdvisor"]
