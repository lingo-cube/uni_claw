"""Verify page type capability."""

from typing import Any, Dict, Optional

from ..core.capability import BaseCapability
from ..core.config import AIProviderConfig
from ..core.llm_client import LLMClient
from ..core.validator import ResponseValidator
from ..core.prompts import PromptRegistry
from ...state.content_tree import PageAnalysis
from .types import PageTypeVerification


class VerifyPageTypeCapability(BaseCapability[Dict, PageTypeVerification]):
    """Capability to verify if current page matches expected type."""

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
        return "verify_page.system"

    @property
    def user_prompt_key(self) -> str:
        return "verify_page.user"

    @property
    def response_schema(self) -> Dict:
        return {
            "type": "object",
            "properties": {
                "is_match": {"type": "boolean"},
                "confidence": {"type": "number", "minimum": 0, "maximum": 1},
                "actual_type": {
                    "type": "string",
                    "enum": ["menu_list", "settings_group", "dialog", "home_desktop", "leaf_page", "unknown"],
                },
                "reasoning": {"type": "string"},
                "mismatch_details": {
                    "type": "object",
                    "properties": {
                        "missing_items": {"type": "array", "items": {"type": "string"}},
                        "unexpected_items": {"type": "array", "items": {"type": "string"}},
                        "type_conflict": {"type": "string"},
                    },
                },
                "suggestion": {
                    "type": "object",
                    "properties": {
                        "action": {"type": "string", "enum": ["back", "retry", "skip", "close_popup", "renavigate"]},
                        "target": {"type": ["string", "null"]},
                        "reason": {"type": "string"},
                    },
                },
            },
            "required": ["is_match", "confidence", "actual_type", "reasoning"],
        }

    @property
    def response_type(self) -> str:
        return "PageTypeVerification"

    def prepare_input(self, raw_input: Dict) -> Dict:
        """Prepare input variables from page analysis and expected type.

        Args:
            raw_input: Dict containing page_analysis, expected_type, and other context

        Returns:
            Variables dict for prompt template
        """
        page_analysis: PageAnalysis = raw_input.get("page_analysis")
        expected_type = raw_input.get("expected_type", "auto_detect")

        # Format elements for prompt
        elements_detail = "\\n".join([
            f"{item.name}|{item.type.value}|{item.expected_action.value}|[{item.coordinate.x},{item.coordinate.y}]"
            for item in page_analysis.items
        ])

        level1_summary = ", ".join([m.name for m in page_analysis.level1_menus])
        level2_summary = ", ".join([m.name for m in page_analysis.level2_menus])

        return {
            "expected_type": expected_type,
            "expected_page_name": raw_input.get("expected_page_name", ""),
            "required_items": raw_input.get("required_items", ""),
            "current_path": str(page_analysis.current_path),
            "is_popup": str(page_analysis.is_popup),
            "level1_menus_summary": level1_summary,
            "level2_menus_summary": level2_summary,
            "elements_detail": elements_detail,
        }


__all__ = ["VerifyPageTypeCapability"]
