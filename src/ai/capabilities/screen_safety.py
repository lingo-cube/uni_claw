"""Screen safety capability."""

from typing import Any, Dict, Optional

from ..core.capability import BaseCapability
from ..core.config import AIProviderConfig
from ..core.llm_client import LLMClient
from ..core.validator import ResponseValidator
from ..core.prompts import PromptRegistry
from ...state.content_tree import PageAnalysis
from .types import SafetyScreeningResult


class ScreenSafetyCapability(BaseCapability[Dict, SafetyScreeningResult]):
    """Capability to screen elements for safety before interaction."""

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
        return "screen_elements.system"

    @property
    def user_prompt_key(self) -> str:
        return "screen_elements.user"

    @property
    def response_schema(self) -> Dict:
        return {
            "type": "object",
            "properties": {
                "evaluations": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name": {"type": "string"},
                            "safety_tag": {"type": "string", "enum": ["safe", "caution", "skip", "unknown"]},
                            "confidence": {"type": "number", "minimum": 0, "maximum": 1},
                            "reason": {"type": "string"},
                            "context_dependency": {"type": "string"},
                            "task_relevance": {"type": "string"},
                        },
                        "required": ["name", "safety_tag", "confidence", "reason"],
                    },
                },
                "page_level_guidance": {
                    "type": "object",
                    "properties": {
                        "overall_safe_to_proceed": {"type": "boolean"},
                        "recommended_max_parallel": {"type": "integer"},
                        "special_precautions": {"type": "array", "items": {"type": "string"}},
                        "task_suitability": {"type": "string"},
                    },
                },
            },
            "required": ["evaluations"],
        }

    @property
    def response_type(self) -> str:
        return "SafetyScreeningResult"

    def prepare_input(self, raw_input: Dict) -> Dict:
        """Prepare input variables from page analysis and instruction.

        Args:
            raw_input: Dict containing page_analysis, instruction, and other context

        Returns:
            Variables dict for prompt template
        """
        page_analysis: PageAnalysis = raw_input.get("page_analysis")
        instruction = raw_input.get("instruction", "")

        # Format elements for prompt
        elements_list = "\\n".join([
            f"{item.name}|{item.type.value}|{item.expected_action.value}|[{item.coordinate.x},{item.coordinate.y}]"
            for item in page_analysis.items
        ])

        return {
            "instruction": instruction,
            "current_path": str(page_analysis.current_path),
            "page_type": raw_input.get("page_type", "unknown"),
            "is_popup": str(page_analysis.is_popup),
            "elements_list": elements_list,
        }


__all__ = ["ScreenSafetyCapability"]
