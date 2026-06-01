"""Context decision capability."""

from typing import Any, Dict, Optional

from ..core.capability import BaseCapability
from ..core.config import AIProviderConfig
from ..core.llm_client import LLMClient
from ..core.validator import ResponseValidator
from ..core.prompts import PromptRegistry
from ...state.content_tree import PageAnalysis
from .types import ContextDecisionResult


class ContextDecisionCapability(BaseCapability[Dict, ContextDecisionResult]):
    """Capability to make next-action decisions based on context."""

    def __init__(
        self,
        client: LLMClient,
        validator: ResponseValidator,
        config: AIProviderConfig,
        prompt_registry: PromptRegistry,
        metrics: Optional[Any] = None,
        archiver: Optional[Any] = None,
    ):
        """Initialize the capability.

        Args:
            client: LLM client for API calls
            validator: Response validator for parsing
            config: AI provider configuration
            prompt_registry: Prompt registry for template access
            metrics: Optional AIMetrics collector
            archiver: Optional FailureArchiver
        """
        super().__init__(client, validator, config, prompt_registry, metrics, archiver)

    @property
    def system_prompt_key(self) -> str:
        return "make_decision.system"

    @property
    def user_prompt_key(self) -> str:
        return "make_decision.user"

    @property
    def response_schema(self) -> Dict:
        return {
            "type": "object",
            "properties": {
                "result": {"type": "string", "enum": ["success", "unsure", "give_up", "wait", "safe_mode"]},
                "action": {"type": "string", "enum": ["click", "back", "swipe", "scroll_down", "wait", "skip", "no_action"]},
                "target": {
                    "type": ["object", "null"],
                    "properties": {
                        "by": {"type": "string", "enum": ["text", "coordinate"]},
                        "value": {"type": "string"},
                    },
                },
                "params": {"type": ["object", "null"]},
                "reasoning": {"type": "string"},
                "confidence": {"type": "number", "minimum": 0, "maximum": 1},
                "safety_verified": {"type": "boolean"},
            },
            "required": ["result", "action", "reasoning", "confidence", "safety_verified"],
        }

    @property
    def response_type(self) -> str:
        return "ContextDecisionResult"

    def prepare_input(self, raw_input: Dict) -> Dict:
        """Prepare input variables from context and page analysis.

        Args:
            raw_input: Dict containing goal, page_analysis, context, and safety screening

        Returns:
            Variables dict for prompt template
        """
        page_analysis: PageAnalysis = raw_input.get("page_analysis")
        safety_result = raw_input.get("safety_result")
        context = raw_input.get("context", {})

        # Format elements for prompt
        elements_detail = "\\n".join([
            f"{item.name}|{item.type.value}|{item.expected_action.value}|[{item.coordinate.x},{item.coordinate.y}]"
            for item in page_analysis.items
        ])

        # Extract safety info
        if safety_result and safety_result.page_level_guidance:
            safe_elements = [e.name for e in safety_result.evaluations if e.safety_tag == "safe"]
            caution_elements = [e.name for e in safety_result.evaluations if e.safety_tag == "caution"]
            skip_elements = [e.name for e in safety_result.evaluations if e.safety_tag == "skip"]
        else:
            safe_elements = []
            caution_elements = []
            skip_elements = []

        return {
            "reason": raw_input.get("reason", "Decision needed"),
            "current_path": str(page_analysis.current_path),
            "is_popup": str(page_analysis.is_popup),
            "popup_info": str(page_analysis.popup_info) if page_analysis.popup_info else "None",
            "elements_detail": elements_detail,
            "overall_safe_to_proceed": str(safety_result.page_level_guidance.overall_safe_to_proceed) if safety_result and safety_result.page_level_guidance else "True",
            "safe_elements": ", ".join(safe_elements),
            "caution_elements": ", ".join(caution_elements),
            "skip_elements": ", ".join(skip_elements),
            "special_precautions": "; ".join(safety_result.page_level_guidance.special_precautions) if safety_result and safety_result.page_level_guidance else "",
            "node_stack": " → ".join(context.get("node_stack", [])),
            "visited_pages": ", ".join(context.get("visited_pages", [])),
            "failed_nodes": ", ".join(context.get("failed_nodes", [])),
            "action_history": " → ".join(context.get("action_history", [])),
            "extra": context.get("extra", ""),
        }


__all__ = ["ContextDecisionCapability"]
