"""Unit tests for VisionServiceFactory."""

import json

import pytest
from unittest.mock import Mock, MagicMock, patch

from src.ai.vision.vision_service_factory import VisionServiceFactory
from src.ai.vision.legacy_vision_service import LegacyVisionService
from src.ai.vision.flattened_vision_service import FlattenedVisionService
from src.ai.vision.multimodal_analyzer import ClaudeMultimodalAnalyzer
from src.ai.vision.page_analysis_assembler import DeepSeekPageAnalysisAssembler
from src.ai.vision.cache import InMemoryScreenCache, InMemoryPageAnalysisCache


class MockAIProvider:
    """Mock AI provider for testing."""

    def __init__(self, response_content: str = None):
        self.response_content = response_content or self._default_response()
        self.call_count = 0

    def complete(self, prompt, image_data=None, model=None, response_format=None):
        """Mock complete method."""
        self.call_count += 1
        return MagicMock(
            content=self.response_content,
            usage=MagicMock(input_tokens=100, output_tokens=200)
        )

    def _default_response(self) -> str:
        """Return default mock response."""
        return json.dumps({
            'elements': [],
            'screen_hints': {},
        })


class TestVisionServiceFactoryCreate:
    """Tests for VisionServiceFactory.create() method."""

    def test_create_legacy_mode(self):
        """Test creating legacy vision service."""
        service = VisionServiceFactory.create(mode="legacy")

        assert isinstance(service, LegacyVisionService)

    def test_create_flattened_mode_with_provider(self):
        """Test creating flattened vision service with AI provider."""
        provider = MockAIProvider()
        config = {
            'multimodal_model': 'claude-3-5-sonnet-20241022',
            'text_model': 'deepseek-v4-flash',
        }

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config=config,
        )

        assert isinstance(service, FlattenedVisionService)
        assert service.multimodal_analyzer is not None
        assert service.assembler is not None

    def test_create_flattened_mode_without_provider_raises_error(self):
        """Test that creating flattened mode without provider raises error."""
        with pytest.raises(RuntimeError, match="ai_provider is required"):
            VisionServiceFactory.create(mode="flattened", ai_provider=None)

    def test_create_dual_mode_falls_back_to_flattened(self):
        """Test that dual mode falls back to flattened mode."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="dual",
            ai_provider=provider,
        )

        # Dual mode not yet implemented, should return flattened
        assert isinstance(service, FlattenedVisionService)

    def test_create_invalid_mode_raises_error(self):
        """Test that invalid mode raises ValueError."""
        provider = MockAIProvider()

        with pytest.raises(ValueError, match="Invalid mode"):
            VisionServiceFactory.create(mode="invalid", ai_provider=provider)

    def test_create_with_none_config_uses_defaults(self):
        """Test that None config uses default values."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config=None,
        )

        assert isinstance(service, FlattenedVisionService)
        assert service.screen_cache is not None  # Cache enabled by default
        assert service.page_analysis_cache is not None


class TestCreateLegacy:
    """Tests for _create_legacy() method."""

    def test_create_legacy_returns_legacy_service(self):
        """Test that _create_legacy returns LegacyVisionService."""
        provider = MockAIProvider()
        config = {}

        service = VisionServiceFactory._create_legacy(provider, config)

        assert isinstance(service, LegacyVisionService)

    def test_create_legacy_ignores_provider(self):
        """Test that _create_legacy doesn't use the provider."""
        provider = MockAIProvider()
        config = {}

        service = VisionServiceFactory._create_legacy(provider, config)

        # Provider should not be used
        assert provider.call_count == 0


class TestCreateFlattened:
    """Tests for _create_flattened() method."""

    def test_create_flattened_with_defaults(self):
        """Test creating flattened service with default config."""
        provider = MockAIProvider()
        config = {}

        service = VisionServiceFactory._create_flattened(provider, config)

        assert isinstance(service, FlattenedVisionService)
        assert isinstance(service.multimodal_analyzer, ClaudeMultimodalAnalyzer)
        assert isinstance(service.assembler, DeepSeekPageAnalysisAssembler)
        assert service.multimodal_analyzer.model == 'claude-3-5-sonnet-20241022'

    def test_create_flattened_with_custom_models(self):
        """Test creating flattened service with custom models."""
        provider = MockAIProvider()
        config = {
            'multimodal_model': 'claude-3-opus-20240229',
            'text_model': 'deepseek-v4',
        }

        service = VisionServiceFactory._create_flattened(provider, config)

        assert service.multimodal_analyzer.model == 'claude-3-opus-20240229'
        assert service.assembler.model == 'deepseek-v4'

    def test_create_flattened_cache_disabled(self):
        """Test creating flattened service with cache disabled."""
        provider = MockAIProvider()
        config = {'enable_cache': False}

        service = VisionServiceFactory._create_flattened(provider, config)

        assert service.screen_cache is None
        assert service.page_analysis_cache is None

    def test_create_flattened_cache_enabled_with_defaults(self):
        """Test creating flattened service with cache enabled using defaults."""
        provider = MockAIProvider()
        config = {'enable_cache': True}

        service = VisionServiceFactory._create_flattened(provider, config)

        assert isinstance(service.screen_cache, InMemoryScreenCache)
        assert isinstance(service.page_analysis_cache, InMemoryPageAnalysisCache)

    def test_create_flattened_cache_custom_ttl(self):
        """Test creating flattened service with custom cache TTL."""
        provider = MockAIProvider()
        config = {
            'enable_cache': True,
            'screen_cache_ttl': 600,
            'page_analysis_cache_ttl': 1200,
        }

        service = VisionServiceFactory._create_flattened(provider, config)

        assert service.screen_cache.ttl == 600
        assert service.page_analysis_cache.ttl == 1200

    def test_create_flattened_cache_custom_max_size(self):
        """Test creating flattened service with custom cache max size."""
        provider = MockAIProvider()
        config = {
            'enable_cache': True,
            'cache_max_size': 500,
        }

        service = VisionServiceFactory._create_flattened(provider, config)

        assert service.screen_cache.max_size == 500
        assert service.page_analysis_cache.max_size == 500

    def test_create_flattened_fallback_enabled(self):
        """Test creating flattened service with fallback enabled."""
        provider = MockAIProvider()
        config = {'enable_fallback': True}

        service = VisionServiceFactory._create_flattened(provider, config)

        assert service.legacy_service is not None
        assert isinstance(service.legacy_service, LegacyVisionService)

    def test_create_flattened_fallback_disabled(self):
        """Test creating flattened service with fallback disabled."""
        provider = MockAIProvider()
        config = {'enable_fallback': False}

        service = VisionServiceFactory._create_flattened(provider, config)

        assert service.legacy_service is None

    def test_create_flattened_full_config(self):
        """Test creating flattened service with full custom config."""
        provider = MockAIProvider()
        config = {
            'multimodal_model': 'claude-3-opus-20240229',
            'text_model': 'deepseek-v4',
            'enable_cache': True,
            'screen_cache_ttl': 400,
            'page_analysis_cache_ttl': 800,
            'cache_max_size': 2000,
            'enable_fallback': True,
        }

        service = VisionServiceFactory._create_flattened(provider, config)

        # Verify all config values applied
        assert isinstance(service, FlattenedVisionService)
        assert service.multimodal_analyzer.model == 'claude-3-opus-20240229'
        assert service.assembler.model == 'deepseek-v4'
        assert service.screen_cache.ttl == 400
        assert service.page_analysis_cache.ttl == 800
        assert service.screen_cache.max_size == 2000
        assert service.page_analysis_cache.max_size == 2000
        assert service.legacy_service is not None


class TestCreateDual:
    """Tests for _create_dual() method."""

    def test_create_dual_returns_flattened(self):
        """Test that _create_dual returns flattened service (not yet implemented)."""
        provider = MockAIProvider()
        config = {}

        service = VisionServiceFactory._create_dual(provider, config)

        # Should return flattened until dual mode is implemented
        assert isinstance(service, FlattenedVisionService)

    def test_create_dual_with_config(self):
        """Test _create_dual passes config to flattened."""
        provider = MockAIProvider()
        config = {
            'enable_cache': False,
            'enable_fallback': False,
        }

        service = VisionServiceFactory._create_dual(provider, config)

        assert isinstance(service, FlattenedVisionService)
        assert service.screen_cache is None
        assert service.legacy_service is None


class TestConfigurationDefaults:
    """Tests for configuration default values."""

    def test_default_multimodal_model(self):
        """Test default multimodal model is claude-3-5-sonnet."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={},
        )

        assert service.multimodal_analyzer.model == 'claude-3-5-sonnet-20241022'

    def test_default_text_model(self):
        """Test default text model is deepseek-v4-flash."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={},
        )

        assert service.assembler.model == 'deepseek-v4-flash'

    def test_default_cache_enabled(self):
        """Test default cache is enabled."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={},
        )

        assert service.screen_cache is not None
        assert service.page_analysis_cache is not None

    def test_default_cache_ttl(self):
        """Test default cache TTL values."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={},
        )

        assert service.screen_cache.ttl == 300
        assert service.page_analysis_cache.ttl == 600

    def test_default_cache_max_size(self):
        """Test default cache max size."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={},
        )

        assert service.screen_cache.max_size == 1000
        assert service.page_analysis_cache.max_size == 1000

    def test_default_fallback_enabled(self):
        """Test default fallback is enabled."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={},
        )

        assert service.legacy_service is not None
