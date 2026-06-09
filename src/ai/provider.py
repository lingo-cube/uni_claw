"""UniBrain - AI Provider for uni-claw framework.

This is the refactored implementation using:
- Provider abstraction layer (providers/)
- PromptManager (prompts/)
- TraceIntegration (trace/)
- Declarative routing configuration (ai_providers.yaml)
"""

import logging
import os
import json
from typing import Dict, Optional, Tuple, Any
from pathlib import Path
from dataclasses import dataclass

from .advisor import AIStrategyAdvisor
from .providers.base import AIProvider, AIResponse, AIProviderConfig, create_provider
from .prompts.manager import PromptManager
from .trace.integration import TraceIntegration, SpanContext
from ..models.traversal_context import TraversalContext
from ..state.content_tree import PageAnalysis
from .ai_types import DecisionResult, ContainerInference, PageTypeVerification

logger = logging.getLogger(__name__)


class UniBrainConfig:
    """Configuration for UniBrain.

    Attributes:
        routing_config_path: Path to the provider routing config
        prompt_dir: Directory containing prompt templates
        enable_trace: Enable trace integration
        default_provider: Default provider if routing fails
        enable_metrics: Enable metrics collection (legacy, for compatibility)
        enable_archiving: Enable failure archiving (legacy, for compatibility)
    """
    def __init__(
        self,
        routing_config_path: str = "config/ai_providers.yaml",
        prompt_dir: str = "src/ai/prompts",
        enable_trace: bool = True,
        default_provider: str = "deepseek",
        enable_metrics: bool = True,
        enable_archiving: bool = True,
    ):
        self.routing_config_path = routing_config_path
        self.prompt_dir = prompt_dir
        self.enable_trace = enable_trace
        self.default_provider = default_provider
        self.enable_metrics = enable_metrics
        self.enable_archiving = enable_archiving


class UniBrain(AIStrategyAdvisor):
    """UniBrain AI Provider - refactored with new architecture.

    This provider uses:
    - Provider abstraction for unified AI access
    - PromptManager for centralized prompt management
    - TraceIntegration for automatic tracing
    - Declarative routing configuration

    The 5 core capabilities are:
    1. analyze_visual - Screenshot analysis (Claude/MiMo)
    2. parse_instruction - Instruction parsing (DeepSeek)
    3. verify_page_type - Page type verification (DeepSeek)
    4. decide_next_action - Context decision (DeepSeek)
    5. screen_safety - Safety screening (DeepSeek)

    For backward compatibility, the constructor accepts the old parameters
    but now uses the new architecture internally.
    """

    def __init__(
        self,
        ai_config=None,
        vision_config=None,
        enable_metrics: bool = True,
        enable_archiving: bool = True,
        config: Optional[UniBrainConfig] = None,
        providers: Optional[Dict[str, AIProvider]] = None,
    ):
        """Initialize UniBrain with new architecture.

        Args:
            ai_config: Legacy parameter (for backward compatibility)
            vision_config: Legacy parameter (for backward compatibility)
            enable_metrics: Enable metrics collection
            enable_archiving: Enable failure archiving
            config: Optional UniBrainConfig (new way)
            providers: Optional pre-configured providers (for testing)
        """
        # Use new config if provided, otherwise create default
        if config is None:
            config = UniBrainConfig(
                enable_trace=True,
                enable_metrics=enable_metrics,
                enable_archiving=enable_archiving,
            )
        else:
            # Update config with legacy parameters if not set
            config.enable_metrics = enable_metrics
            config.enable_archiving = enable_archiving

        self.config = config

        # Store legacy config for backward compatibility
        self.ai_config = ai_config
        self.vision_config = vision_config

        # Load providers
        if providers:
            self.providers = providers
            logger.info(f"Using {len(providers)} pre-configured providers")
        else:
            self.providers = self._load_providers_from_config()
            logger.info(f"Loaded {len(self.providers)} providers from config")

        # Initialize prompt manager
        self.prompt_manager = PromptManager(self.config.prompt_dir)
        logger.info(f"Initialized PromptManager with {len(self.prompt_manager.list_capabilities())} capabilities")

        # Initialize trace integration
        self.trace_integration = TraceIntegration(enable_auto=self.config.enable_trace)

        # Initialize metrics (legacy compatibility)
        self.metrics = None  # Could be integrated with trace_integration later
        self.archiver = None  # Legacy archiving, could be added

        # Load routing configuration
        self.routing_config = self._load_routing_config()
        self._capability_provider_map = self.routing_config.get("routing", {})
        logger.info(f"Loaded routing configuration for {len(self._capability_provider_map)} capabilities")

        # For backward compatibility, maintain capabilities dict interface
        self.capabilities = {
            "parse": self,  # Self-reference for compatibility
            "verify": self,
            "safety": self,
            "vision": self,
            "decision": self,
        }

        logger.info("UniBrain initialized successfully with new architecture")

    def _load_routing_config(self) -> Dict:
        """Load provider routing configuration with local overrides.

        Returns:
            Dict with routing configuration
        """
        from src.ai.providers.config import load_routing_config_with_local

        config_path = self.config.routing_config_path or "config/ai_providers.yaml"

        if not Path(config_path).exists():
            logger.warning(f"Routing config not found: {config_path}, using defaults")
            return {
                "providers": {},
                "routing": {},
                "defaults": {
                    "default_provider": self.config.default_provider,
                },
            }

        try:
            # Load config with local overrides
            config = load_routing_config_with_local(
                config_path=config_path,
                local_config_path="config/ai_providers.local.yaml"
            )
            logger.info(f"Loaded routing config from {config_path} (with local overrides)")
            return config or {}
        except Exception as e:
            logger.error(f"Failed to load routing config: {e}")
            return {
                "providers": {},
                "routing": {},
                "defaults": {
                    "default_provider": self.config.default_provider,
                },
            }

    def _resolve_env_var(self, value: str) -> str:
        """Resolve environment variable in config value.

        Args:
            value: Value that may contain ${VAR_NAME} pattern

        Returns:
            Resolved value
        """
        if isinstance(value, str) and value.startswith("${") and value.endswith("}"):
            var_name = value[2:-1]
            return os.getenv(var_name, value)
        return value

    def _load_providers_from_config(self) -> Dict[str, AIProvider]:
        """Load providers from routing configuration.

        Returns:
            Dict mapping provider IDs to provider instances
        """
        providers = {}
        routing_config = self._load_routing_config()

        for provider_id, provider_config in routing_config.get("providers", {}).items():
            try:
                # Resolve environment variables in config
                api_key = self._resolve_env_var(provider_config["config"]["api_key"])
                model = provider_config["config"]["model"]
                base_url = provider_config["config"]["base_url"]

                # Create provider config
                ai_config = AIProviderConfig(
                    api_key=api_key,
                    model=model,
                    base_url=base_url,
                )

                # Create provider instance
                provider = create_provider(provider_id, ai_config)

                providers[provider_id] = provider
                logger.info(f"Loaded provider: {provider_id}")

            except Exception as e:
                logger.error(f"Failed to load provider {provider_id}: {e}")

        return providers

    def _select_provider(self, capability: str) -> AIProvider:
        """Select provider for a capability using routing config.

        Args:
            capability: Capability name

        Returns:
            AIProvider instance

        Raises:
            RuntimeError: If no provider is configured/available
        """
        provider_id = self._capability_provider_map.get(capability)

        if not provider_id:
            logger.warning(f"No provider configured for {capability}, using default")
            provider_id = self.config.default_provider

        if provider_id not in self.providers:
            raise RuntimeError(
                f"Provider '{provider_id}' not found. Available: {list(self.providers.keys())}"
            )

        return self.providers[provider_id]

    async def _execute_capability(
        self,
        capability: str,
        mode: str,
        prompt_kwargs: Dict[str, Any],
        image_data: Optional[bytes] = None,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
    ) -> AIResponse:
        """Execute a capability using the routed provider.

        Args:
            capability: Capability name
            mode: Call mode (text, vision, multimodal)
            prompt_kwargs: Variables for prompt template
            image_data: Optional image data for vision/multimodal
            schema: Optional JSON schema for structured output
            max_tokens: Maximum output tokens

        Returns:
            AIResponse from the provider
        """
        provider = self._select_provider(capability)

        # Get prompt template
        template = self.prompt_manager.get_prompt(capability)
        formatted_prompt = template.format(**prompt_kwargs)

        # Start trace span
        span = self.trace_integration.start_span(
            operation=f"unibrain.{capability}",
            tags={
                "capability": capability,
                "provider_id": provider.provider_id,
                "mode": mode,
            },
        )

        try:
            # Execute based on mode
            if mode == "text":
                response = await provider.complete_text(
                    prompt=formatted_prompt,
                    schema=schema,
                    max_tokens=max_tokens,
                )
            elif mode == "vision":
                if not image_data:
                    raise ValueError(f"image_data required for vision mode")
                response = await provider.complete_vision(
                    prompt=formatted_prompt,
                    image_data=image_data,
                    schema=schema,
                    max_tokens=max_tokens,
                )
            elif mode == "multimodal":
                if not image_data:
                    raise ValueError(f"image_data required for multimodal mode")
                response = await provider.complete_multimodal(
                    prompt=formatted_prompt,
                    image_data=image_data,
                    additional_context=prompt_kwargs.get("additional_context"),
                    schema=schema,
                    max_tokens=max_tokens,
                )
            else:
                raise ValueError(f"Unknown mode: {mode}")

            # Record metrics
            self.trace_integration.record_metrics(
                capability=capability,
                provider_id=provider.provider_id,
                latency_ms=response.latency_ms,
                tokens={"input": response.input_tokens, "output": response.output_tokens},
                success=response.success,
            )

            self.trace_integration.finish_span(span, result=response.content)

            return response

        except Exception as e:
            self.trace_integration.finish_span(span, error=e)
            raise

    # ============================================================================
    # AIStrategyAdvisor Interface Implementation
    # ============================================================================

    def infer_container_type(
        self, ui: PageAnalysis, context: TraversalContext
    ) -> ContainerInference:
        """Infer container type using AI.

        Args:
            ui: Current page analysis
            context: Current traversal context

        Returns:
            ContainerInference with type and confidence
        """
        import asyncio

        # For now, use simple logic based on layout_type
        # This could be enhanced with AI verification later
        return ContainerInference(
            container_type=ui.layout_type or "unknown",
            confidence=0.8 if ui.layout_type else 0.5,
            matched_template=ui.layout_type or "unknown",
        )

    def decide_next_action(
        self,
        goal: str,
        ui: PageAnalysis,
        context: TraversalContext,
    ) -> Tuple[DecisionResult, Optional[dict]]:
        """Decide next action using AI.

        Args:
            goal: The goal to achieve
            ui: Current page analysis
            context: Current traversal context

        Returns:
            Tuple of (DecisionResult, optional node_data)
        """
        import asyncio

        try:
            # Execute decide_next_action capability
            response = asyncio.run(self._execute_capability(
                capability="decide_next_action",
                mode="text",
                prompt_kwargs={
                    "goal": goal,
                    "page_analysis": ui.model_dump_json() if hasattr(ui, 'model_dump_json') else str(ui),
                    "context": str(context),
                },
                schema={
                    "type": "object",
                    "properties": {
                        "result": {"type": "string"},
                        "action": {"type": "string"},
                        "target": {"type": "string"},
                        "params": {"type": "object"},
                        "reasoning": {"type": "string"},
                        "confidence": {"type": "number"},
                    },
                },
            ))

            # Parse response
            result_data = json.loads(response.content)
            decision_result = DecisionResult.from_string(result_data.get("result", "unknown"))

            node_data = None
            if decision_result == DecisionResult.SUCCESS and result_data.get("action"):
                node_data = {
                    "action": result_data["action"],
                    "target": result_data.get("target"),
                    "params": result_data.get("params"),
                    "reasoning": result_data.get("reasoning"),
                }

            return decision_result, node_data

        except Exception as e:
            logger.error(f"decide_next_action failed: {e}")
            return DecisionResult.UNSURE, None

    def handle_exception(
        self,
        exception: dict,
        ui: PageAnalysis,
        context: TraversalContext,
    ) -> Tuple[DecisionResult, Optional[dict]]:
        """Handle exception using AI.

        Args:
            exception: Exception context dict
            ui: Current page analysis
            context: Current traversal context

        Returns:
            Tuple of (DecisionResult, optional recovery node_data)
        """
        # Convert exception to recovery goal
        recovery_goal = f"Recover from: {exception.get('type', 'Unknown error')}"
        return self.decide_next_action(recovery_goal, ui, context)

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """Analyze screenshot using Vision Service.

        Args:
            image_data: PNG image bytes

        Returns:
            PageAnalysis with detected elements
        """
        import asyncio

        try:
            response = asyncio.run(self._execute_capability(
                capability="analyze_visual",
                mode="vision",
                prompt_kwargs={
                    "image_description": "Vehicle infotainment system screenshot",
                    "context_info": "{}",
                },
                image_data=image_data,
                max_tokens=4096,
            ))

            # Parse response as PageAnalysis
            result_data = json.loads(response.content)
            return PageAnalysis(**result_data)

        except Exception as e:
            logger.error(f"analyze_screenshot failed: {e}")
            raise

    def verify_page_with_vision(
        self,
        image_data: bytes,
        expected_type: str,
    ) -> PageTypeVerification:
        """Verify page type using vision analysis.

        Args:
            image_data: PNG image bytes
            expected_type: Expected page type

        Returns:
            PageTypeVerification result
        """
        import asyncio

        try:
            response = asyncio.run(self._execute_capability(
                capability="verify_page_type",
                mode="vision",
                prompt_kwargs={
                    "expected_type": expected_type,
                    "context_info": "{}",
                },
                image_data=image_data,
                max_tokens=2048,
            ))

            result_data = json.loads(response.content)
            return PageTypeVerification(**result_data)

        except Exception as e:
            logger.error(f"verify_page_with_vision failed: {e}")
            raise

    # ============================================================================
    # Legacy Compatibility Methods
    # ============================================================================

    def get_metrics_summary(self) -> Optional[Dict]:
        """Get metrics summary (legacy compatibility).

        Returns:
            Dict with metrics summary or None if metrics disabled
        """
        # Could integrate with trace_integration later
        return None

    def get_latency_stats(self, capability: str) -> Optional[Dict]:
        """Get latency statistics for a capability (legacy compatibility).

        Args:
            capability: Capability name

        Returns:
            Dict with latency stats or None if metrics disabled
        """
        # Could integrate with trace_integration later
        return None

    def get_failure_summary(self) -> Optional[Dict]:
        """Get failure summary (legacy compatibility).

        Returns:
            Dict with failure summary or None if archiving disabled
        """
        # Legacy archiving not implemented in new architecture
        return None

    def get_failures(self, capability: Optional[str] = None, limit: int = 100) -> list:
        """Get failure records (legacy compatibility).

        Args:
            capability: Optional capability name to filter by
            limit: Maximum records to return

        Returns:
            List of failure records
        """
        # Legacy archiving not implemented in new architecture
        return []


__all__ = ["UniBrain", "UniBrainConfig"]
