"""Tests for UniBrain (refactored architecture)."""

import pytest
from src.ai.provider import UniBrain, UniBrainConfig


class TestUniBrainConfig:
    """Test UniBrain configuration."""

    def test_config_creation(self):
        """Test creating configuration."""
        config = UniBrainConfig()
        assert config.routing_config_path == "config/ai_providers.yaml"
        assert config.prompt_dir == "src/ai/prompts"
        assert config.enable_trace is True
        assert config.default_provider == "deepseek"
        assert config.enable_metrics is True
        assert config.enable_archiving is True

    def test_config_custom_values(self):
        """Test configuration with custom values."""
        config = UniBrainConfig(
            routing_config_path="custom/config.yaml",
            prompt_dir="custom/prompts",
            enable_trace=False,
            default_provider="claude",
        )
        assert config.routing_config_path == "custom/config.yaml"
        assert config.prompt_dir == "custom/prompts"
        assert config.enable_trace is False
        assert config.default_provider == "claude"


class TestUniBrain:
    """Test UniBrain implementation."""

    @pytest.fixture
    def mock_providers(self):
        """Create mock providers for testing."""
        from src.ai.providers.base import AIProvider, AIResponse, AIProviderConfig
        import asyncio

        class MockProvider(AIProvider):
            @property
            def provider_id(self):
                return "mock"

            @property
            def supported_modes(self):
                return ["text", "vision", "multimodal"]

            async def complete_text(self, prompt, schema=None, max_tokens=2048, **kwargs):
                return AIResponse(
                    content='{"result": "success", "action": "click", "target": "button1"}',
                    provider_id="mock",
                    mode="text",
                    input_tokens=10,
                    output_tokens=20,
                    latency_ms=50,
                )

            async def complete_vision(self, prompt, image_data, schema=None, max_tokens=4096, **kwargs):
                return AIResponse(
                    content='{"elements": [], "layout": {"type": "vertical_list"}}',
                    provider_id="mock",
                    mode="vision",
                    input_tokens=100,
                    output_tokens=200,
                    latency_ms=100,
                )

            async def complete_multimodal(self, prompt, image_data, additional_context=None, schema=None, max_tokens=4096, **kwargs):
                return AIResponse(
                    content='{"result": "success"}',
                    provider_id="mock",
                    mode="multimodal",
                    input_tokens=150,
                    output_tokens=100,
                    latency_ms=120,
                )

        return {"mock": MockProvider(AIProviderConfig(api_key="test", model="test", base_url="http://test"))}

    @pytest.fixture
    def unibrain(self, mock_providers):
        """Create UniBrain instance with mock providers."""
        config = UniBrainConfig(
            enable_trace=False,  # Disable trace for faster tests
            default_provider="mock",  # Use mock as default for tests
            routing_config_path="nonexistent.yaml",  # Use non-existent config to avoid loading routing
        )
        unibrain = UniBrain(config=config, providers=mock_providers)
        # Set up custom routing for tests
        unibrain._capability_provider_map = {"decide_next_action": "mock"}
        return unibrain

    def test_initialization(self, unibrain):
        """Test UniBrain initialization."""
        assert unibrain is not None
        assert len(unibrain.providers) == 1
        assert "mock" in unibrain.providers
        assert unibrain.prompt_manager is not None
        assert unibrain.trace_integration is not None

    def test_select_provider(self, unibrain):
        """Test provider selection."""
        # Test with known capability
        provider = unibrain._select_provider("decide_next_action")
        assert provider.provider_id == "mock"  # Configured in routing

        # Test with unknown capability (should use default)
        provider = unibrain._select_provider("unknown_capability")
        assert provider.provider_id == "mock"  # Default provider for tests

    def test_select_provider_missing_provider(self, unibrain):
        """Test provider selection with missing provider."""
        # Mock routing to use a non-existent provider
        unibrain._capability_provider_map = {"test": "nonexistent"}

        with pytest.raises(RuntimeError, match="Provider 'nonexistent' not found"):
            unibrain._select_provider("test")

    def test_resolve_env_var(self, unibrain):
        """Test environment variable resolution."""
        import os

        os.environ["TEST_VAR"] = "test_value"
        result = unibrain._resolve_env_var("${TEST_VAR}")
        assert result == "test_value"

        # Test with non-existent env var (returns original)
        result = unibrain._resolve_env_var("${NONEXISTENT}")
        assert result == "${NONEXISTENT}"

        # Test with plain string
        result = unibrain._resolve_env_var("plain_string")
        assert result == "plain_string"

    def test_backward_compatibility_interface(self, unibrain):
        """Test that UniBrain maintains backward compatibility."""
        # Should have capabilities dict
        assert "parse" in unibrain.capabilities
        assert "verify" in unibrain.capabilities
        assert "safety" in unibrain.capabilities
        assert "vision" in unibrain.capabilities
        assert "decision" in unibrain.capabilities

        # Should have legacy methods
        assert hasattr(unibrain, "get_metrics_summary")
        assert hasattr(unibrain, "get_latency_stats")
        assert hasattr(unibrain, "get_failure_summary")
