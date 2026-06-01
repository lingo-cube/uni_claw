"""Parse to plan capability - instruction parsing."""

from typing import Any, Dict, Optional

from ..core.capability import BaseCapability
from ..core.config import AIProviderConfig
from ..core.llm_client import LLMClient
from ..core.validator import ResponseValidator
from ..core.prompts import PromptRegistry
from .types import TraversalPlan


class ParseToPlanCapability(BaseCapability[str, TraversalPlan]):
    """Capability to parse natural language instructions into traversal plans."""

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
        return "parse_task.system"

    @property
    def user_prompt_key(self) -> str:
        return "parse_task.user"

    @property
    def response_schema(self) -> Dict:
        """Response schema for parse task capability.

        Updated to support:
        - entry_policy: Entry strategy configuration
        - exit_condition: Multiple exit types (all_children_visited, target_found, etc.)
        - meta: Runtime constraints (max_depth, visited_pages)
        """
        return {
            "type": "object",
            "properties": {
                "entry_app": {"type": ["string", "null"]},
                "entry_policy": {
                    "type": "object",
                    "properties": {
                        "strategy": {"type": "string"},
                        "fallback": {"type": "string"}
                    },
                    "required": ["strategy"]
                },
                "root_node": {
                    "type": "object",
                    "properties": {
                        "node_id": {"type": "string"},
                        "name": {"type": "string"},
                        "node_type": {"type": "string"},
                        "operation": {"type": "object"},
                        "precondition": {"type": ["object", "null"]},
                        "children_strategy": {"type": "object"},
                        "exit_condition": {
                            "type": "object",
                            "properties": {
                                "type": {
                                    "type": "string",
                                    "enum": [
                                        "all_children_visited",
                                        "target_found",
                                        "single_level",
                                        "depth_limited",
                                        "timeout_or_complete"
                                    ]
                                },
                                "fallback": {
                                    "type": "string",
                                    "enum": ["back_to_parent", "stay_on_page", "return_to_root"]
                                },
                                "target_name": {"type": "string"},
                                "max_depth": {"type": "integer"},
                                "timeout_seconds": {"type": "integer"}
                            },
                            "required": ["type", "fallback"]
                        },
                        "error_policy": {"type": ["null", "object"]},
                        "meta": {
                            "type": "object",
                            "properties": {
                                "max_depth": {"type": "integer"},
                                "visited_pages": {"type": "array", "items": {"type": "string"}},
                                "visited_pages_scope": {"type": "string", "enum": ["traversal", "global"]}
                            },
                            "required": ["max_depth", "visited_pages"]
                        }
                    },
                    "required": [
                        "node_id", "name", "node_type", "operation",
                        "precondition", "children_strategy",
                        "exit_condition", "error_policy", "meta"
                    ],
                },
                "static_nodes": {
                    "type": "array",
                    "items": {"type": "object"},
                },
                "template_registry": {"type": "string"},
                "mode": {"type": "string", "enum": ["hybrid", "concrete", "dynamic"]},
            },
            "required": ["entry_app", "root_node", "template_registry", "mode"],
        }

    @property
    def response_type(self) -> str:
        return "TraversalPlan"

    def prepare_input(self, raw_input: str) -> Dict:
        """Prepare input variables from instruction string.

        Args:
            raw_input: Natural language instruction

        Returns:
            Variables dict for prompt template
        """
        return {"instruction": raw_input}


__all__ = ["ParseToPlanCapability"]
