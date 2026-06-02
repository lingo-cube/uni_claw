"""Vision service factory for creating vision service instances.

This module provides a factory for creating vision services with different
modes: legacy, flattened, and dual. It uses different AI providers:
- MiMo-CC (Anthropic protocol) for multimodal vision analysis
- DeepSeek for text-based logical assembly
"""

import logging
from typing import Optional, Union

from src.ai.vision.legacy_vision_service import LegacyVisionService
from src.ai.vision.flattened_vision_service import FlattenedVisionService
from src.ai.vision.multimodal_analyzer import ClaudeMultimodalAnalyzer
from src.ai.vision.page_analysis_assembler import DeepSeekPageAnalysisAssembler
from src.ai.vision.cache import InMemoryScreenCache, InMemoryPageAnalysisCache


logger = logging.getLogger(__name__)


class VisionServiceFactory:
    """Factory for creating vision service instances.

    Supports three modes:
    - legacy: Original one-step vision service
    - flattened: New two-step pipeline service (MiMo-CC + DeepSeek)
    - dual: Experimental - runs both in parallel for comparison

    Provider configuration:
    - Vision (multimodal): MiMo-CC using Anthropic protocol
    - Assembly (text): DeepSeek API
    """

    @staticmethod
    def create(
        mode: str = None,
        multimodal_provider=None,
        text_provider=None,
        config: Optional[dict] = None,
    ) -> Union[LegacyVisionService, FlattenedVisionService]:
        """Create a vision service instance.

        Args:
            mode: Service mode - "legacy", "flattened", or "dual".
                   If None, reads from Settings.vision.mode
            multimodal_provider: AI provider for vision analysis (MiMo-CC)
            text_provider: AI provider for text assembly (DeepSeek)
            config: Optional configuration dictionary

        Returns:
            Vision service instance

        Raises:
            ValueError: If mode is invalid
            RuntimeError: If required dependencies are missing
        """
        if config is None:
            from src.config.settings import get_settings
            settings = get_settings()
            # Build config from settings
            config = {
                'multimodal_model': settings.vision.multimodal_model,
                'text_model': settings.vision.text_model,
                'enable_cache': settings.vision.enable_cache,
                'screen_cache_ttl': settings.vision.screen_cache_ttl,
                'page_analysis_cache_ttl': settings.vision.page_analysis_cache_ttl,
                'cache_max_size': settings.vision.cache_max_size,
                'enable_fallback': settings.vision.enable_fallback,
            }
            if mode is None:
                mode = settings.vision.mode

        if mode == "legacy":
            return VisionServiceFactory._create_legacy(config)
        elif mode == "flattened":
            return VisionServiceFactory._create_flattened(
                multimodal_provider,
                text_provider,
                config,
            )
        elif mode == "dual":
            return VisionServiceFactory._create_dual(
                multimodal_provider,
                text_provider,
                config,
            )
        else:
            raise ValueError(
                f"Invalid mode: {mode}. Must be 'legacy', 'flattened', or 'dual'"
            )

    @staticmethod
    def _create_legacy(config: dict) -> LegacyVisionService:
        """Create legacy vision service.

        Args:
            config: Configuration dictionary

        Returns:
            LegacyVisionService instance
        """
        logger.info("Creating legacy vision service")
        return LegacyVisionService()

    @staticmethod
    def _create_flattened(
        multimodal_provider,
        text_provider,
        config: dict,
    ) -> FlattenedVisionService:
        """Create flattened two-step pipeline vision service.

        Uses two different providers:
        - multimodal_provider (MiMo-CC): For vision analysis
        - text_provider (DeepSeek): For logical assembly

        Args:
            multimodal_provider: AI provider for vision (supports images)
            text_provider: AI provider for text assembly
            config: Configuration dictionary

        Returns:
            FlattenedVisionService instance
        """
        logger.info("Creating flattened vision service (MiMo-CC + DeepSeek)")

        # Get configuration values
        multimodal_model = config.get('multimodal_model', 'mimo-v2.5')
        text_model = config.get('text_model', 'deepseek-v4-flash')
        enable_cache = config.get('enable_cache', True)
        screen_cache_ttl = config.get('screen_cache_ttl', 300)
        page_analysis_cache_ttl = config.get('page_analysis_cache_ttl', 600)
        cache_max_size = config.get('cache_max_size', 1000)
        enable_fallback = config.get('enable_fallback', True)

        # Create multimodal analyzer (for vision)
        if multimodal_provider is None:
            multimodal_provider = VisionServiceFactory._create_multimodal_provider(
                config.get('mimo_api_key'),
                config.get('mimo_base_url'),
                multimodal_model,
            )

        multimodal_analyzer = ClaudeMultimodalAnalyzer(
            ai_provider=multimodal_provider,
            model=multimodal_model,
        )

        # Create page assembler (for text)
        if text_provider is None:
            text_provider = VisionServiceFactory._create_text_provider(
                config.get('deepseek_api_key'),
                text_model,
            )

        assembler = DeepSeekPageAnalysisAssembler(
            ai_provider=text_provider,
            model=text_model,
        )

        # Create caches
        screen_cache = None
        page_analysis_cache = None
        if enable_cache:
            screen_cache = InMemoryScreenCache(
                ttl=screen_cache_ttl,
                max_size=cache_max_size,
            )
            page_analysis_cache = InMemoryPageAnalysisCache(
                ttl=page_analysis_cache_ttl,
                max_size=cache_max_size,
            )
            logger.info(
                f"Caching enabled: screen_ttl={screen_cache_ttl}s, "
                f"page_ttl={page_analysis_cache_ttl}s"
            )

        # Create legacy service for fallback
        legacy_service = None
        if enable_fallback:
            legacy_service = LegacyVisionService()
            logger.info("Fallback to legacy service enabled")

        return FlattenedVisionService(
            multimodal_analyzer=multimodal_analyzer,
            assembler=assembler,
            screen_cache=screen_cache,
            page_analysis_cache=page_analysis_cache,
            legacy_service=legacy_service,
        )

    @staticmethod
    def _create_dual(
        multimodal_provider,
        text_provider,
        config: dict,
    ):
        """Create dual-mode vision service (experimental).

        Args:
            multimodal_provider: AI provider for vision
            text_provider: AI provider for text
            config: Configuration dictionary

        Returns:
            Dual mode service instance (placeholder for now)
        """
        logger.warning("Dual mode is not yet implemented, using flattened mode")
        return VisionServiceFactory._create_flattened(
            multimodal_provider,
            text_provider,
            config,
        )

    @staticmethod
    def _create_multimodal_provider(api_key: str, base_url: str, model: str):
        """Create multimodal provider (MiMo-CC with Anthropic protocol).

        Args:
            api_key: API key for MiMo-CC
            base_url: Base URL for MiMo-CC
            model: Model name

        Returns:
            MultimodalClient instance

        Raises:
            RuntimeError: If API key is missing
        """
        from src.ai.core.multimodal_client import MultimodalClient
        from src.ai.core.config import AIProviderConfig

        if not api_key:
            # Try to get from settings
            from src.config.settings import get_settings
            settings = get_settings()
            api_key = settings.mimo_api_key or settings.anthropic_api_key
            base_url = getattr(settings, 'mimo_cc_base_url',
                           'https://token-plan-cn.xiaomimimo.com/anthropic')
            model = getattr(settings, 'mimo_cc_model', model)

        if not api_key:
            raise RuntimeError(
                "No API key configured for multimodal provider. "
                "Please set MIMO_API_KEY or ANTHROPIC_API_KEY."
            )

        ai_config = AIProviderConfig(
            api_key=api_key,
            model=model,
            base_url=base_url or 'https://token-plan-cn.xiaomimimo.com/anthropic',
        )
        return MultimodalClient(ai_config)

    @staticmethod
    def _create_text_provider(api_key: str, model: str):
        """Create text provider (DeepSeek).

        Args:
            api_key: API key for DeepSeek
            model: Model name

        Returns:
            LLMClient instance

        Raises:
            RuntimeError: If API key is missing
        """
        from src.ai.core.llm_client import LLMClient
        from src.ai.core.config import AIProviderConfig

        if not api_key:
            # Try to get from settings
            from src.config.settings import get_settings
            settings = get_settings()
            api_key = settings.deepseek_api_key
            model = model or 'deepseek-v4-flash'

        if not api_key:
            raise RuntimeError(
                "No API key configured for text provider. "
                "Please set DEEPSEEK_API_KEY."
            )

        ai_config = AIProviderConfig(
            api_key=api_key,
            model=model,
            base_url='https://api.deepseek.com/v1',
        )
        return LLMClient(ai_config)

    @staticmethod
    def create_from_settings(settings=None) -> FlattenedVisionService:
        """Create service from project settings (convenience method).

        Args:
            settings: Optional Settings object (loads if None)

        Returns:
            FlattenedVisionService instance

        Raises:
            RuntimeError: If required API keys are missing
        """
        if settings is None:
            from src.config.settings import get_settings
            settings = get_settings()

        # Create providers from settings
        multimodal_provider = VisionServiceFactory._create_multimodal_provider(
            api_key=settings.mimo_api_key or settings.anthropic_api_key,
            base_url=settings.mimo_cc_base_url,
            model=settings.mimo_cc_model or settings.vision.multimodal_model,
        )

        text_provider = VisionServiceFactory._create_text_provider(
            api_key=settings.deepseek_api_key,
            model=settings.vision.text_model,
        )

        # Build config
        config = {
            'multimodal_model': settings.mimo_cc_model or settings.vision.multimodal_model,
            'text_model': settings.vision.text_model,
            'enable_cache': settings.vision.enable_cache,
            'screen_cache_ttl': settings.vision.screen_cache_ttl,
            'page_analysis_cache_ttl': settings.vision.page_analysis_cache_ttl,
            'cache_max_size': settings.vision.cache_max_size,
            'enable_fallback': settings.vision.enable_fallback,
            'mimo_api_key': settings.mimo_api_key,
            'deepseek_api_key': settings.deepseek_api_key,
        }

        return VisionServiceFactory._create_flattened(
            multimodal_provider,
            text_provider,
            config,
        )


__all__ = ["VisionServiceFactory"]
