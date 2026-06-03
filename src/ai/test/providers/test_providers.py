"""Unit tests for Provider implementations (DeepSeek, Claude, MiMo)."""

import pytest
from src.ai.providers.base import AIProviderConfig
from src.ai.providers import DeepSeekProvider, ClaudeProvider, MiMoProvider


class TestDeepSeekProvider:
    """Test DeepSeek provider implementation."""

    @pytest.fixture
    def config(self):
        """DeepSeek configuration."""
        return AIProviderConfig(
            api_key="test-deepseek-key",
            model="deepseek-v4-flash",
            base_url="https://api.deepseek.com/v1",
            max_concurrent_requests=2,
        )

    @pytest.fixture
    def provider(self, config):
        """DeepSeek provider instance."""
        return DeepSeekProvider(config)

    def test_provider_id(self, provider):
        """Test provider ID."""
        assert provider.provider_id == "deepseek"

    def test_supported_modes(self, provider):
        """Test supported modes - text only."""
        assert provider.supported_modes == ["text"]

    def test_config_assigned(self, provider, config):
        """Test configuration is assigned."""
        assert provider.config == config

    @pytest.mark.asyncio
    async def test_complete_text_not_implemented(self, provider):
        """Test that complete_text raises error for real calls without API."""
        # This would make a real API call, which we don't want in unit tests
        # So we just check that the method exists and has correct signature
        import inspect
        sig = inspect.signature(provider.complete_text)
        assert "prompt" in sig.parameters
        assert "schema" in sig.parameters
        assert "max_tokens" in sig.parameters

    @pytest.mark.asyncio
    async def test_complete_vision_not_supported(self, provider):
        """Test that vision mode is not supported."""
        with pytest.raises(NotImplementedError, match="does not support vision"):
            await provider.complete_vision("test", b"image_data")

    @pytest.mark.asyncio
    async def test_complete_multimodal_not_supported(self, provider):
        """Test that multimodal mode is not supported."""
        with pytest.raises(NotImplementedError, match="does not support multimodal"):
            await provider.complete_multimodal("test", b"image_data")

    def test_get_token_estimate(self, provider):
        """Test token estimation for text mode."""
        estimate = provider.get_token_estimate("text", 100)

        assert estimate["input"] == 100
        assert estimate["output"] == 50
        assert estimate["total"] == 150

    def test_get_token_estimate_vision_fails(self, provider):
        """Test that token estimation fails for unsupported modes."""
        with pytest.raises(NotImplementedError):
            provider.get_token_estimate("vision")

    def test_get_performance_rating(self, provider):
        """Test performance rating."""
        rating = provider.get_performance_rating("text")

        assert rating["latency"] == 0.8
        assert rating["quality"] == 0.7
        assert rating["efficiency"] == 0.9

    def test_get_performance_rating_unsupported_mode(self, provider):
        """Test performance rating for unsupported mode."""
        rating = provider.get_performance_rating("vision")

        assert rating["latency"] == 0.0
        assert rating["quality"] == 0.0
        assert rating["efficiency"] == 0.0


class TestClaudeProvider:
    """Test Claude provider implementation."""

    @pytest.fixture
    def config(self):
        """Claude configuration."""
        return AIProviderConfig(
            api_key="test-anthropic-key",
            model="claude-3-5-sonnet-20241022",
            base_url="https://api.anthropic.com/v1",
            max_concurrent_requests=3,
        )

    @pytest.fixture
    def provider(self, config):
        """Claude provider instance."""
        return ClaudeProvider(config)

    def test_provider_id(self, provider):
        """Test provider ID."""
        assert provider.provider_id == "claude"

    def test_supported_modes(self, provider):
        """Test supported modes - all three."""
        assert "text" in provider.supported_modes
        assert "vision" in provider.supported_modes
        assert "multimodal" in provider.supported_modes
        assert len(provider.supported_modes) == 3

    @pytest.mark.asyncio
    async def test_method_signatures(self, provider):
        """Test that all methods have correct signatures."""
        import inspect

        # Check complete_text signature
        text_sig = inspect.signature(provider.complete_text)
        assert "prompt" in text_sig.parameters
        assert "schema" in text_sig.parameters
        assert "max_tokens" in text_sig.parameters

        # Check complete_vision signature
        vision_sig = inspect.signature(provider.complete_vision)
        assert "prompt" in vision_sig.parameters
        assert "image_data" in vision_sig.parameters
        assert "schema" in vision_sig.parameters

        # Check complete_multimodal signature
        multimodal_sig = inspect.signature(provider.complete_multimodal)
        assert "prompt" in multimodal_sig.parameters
        assert "image_data" in multimodal_sig.parameters
        assert "additional_context" in multimodal_sig.parameters

    def test_get_token_estimate(self, provider):
        """Test token estimation for all modes."""
        # Text mode
        text_estimate = provider.get_token_estimate("text", 100)
        assert text_estimate["input"] == 100
        assert text_estimate["output"] == 50

        # Vision mode - more input tokens
        vision_estimate = provider.get_token_estimate("vision", 100)
        assert vision_estimate["input"] == 200
        assert vision_estimate["output"] == 100

        # Multimodal mode
        multimodal_estimate = provider.get_token_estimate("multimodal", 100)
        assert multimodal_estimate["input"] == 200
        assert multimodal_estimate["output"] == 100

    def test_get_performance_rating(self, provider):
        """Test performance rating for Claude."""
        rating = provider.get_performance_rating("text")

        assert rating["latency"] == 0.6
        assert rating["quality"] == 0.95
        assert rating["efficiency"] == 0.6

    def test_get_performance_rating_all_modes(self, provider):
        """Test performance rating is consistent across modes."""
        text_rating = provider.get_performance_rating("text")
        vision_rating = provider.get_performance_rating("vision")
        multimodal_rating = provider.get_performance_rating("multimodal")

        # All should have the same ratings for Claude
        assert text_rating == vision_rating == multimodal_rating


class TestMiMoProvider:
    """Test MiMo provider implementation."""

    @pytest.fixture
    def config(self):
        """MiMo configuration."""
        return AIProviderConfig(
            api_key="test-mimo-key",
            model="mimo-v2.5",
            base_url="https://token-plan-cn.xiaomimimo.com/anthropic",
            max_concurrent_requests=4,
        )

    @pytest.fixture
    def provider(self, config):
        """MiMo provider instance."""
        return MiMoProvider(config)

    def test_provider_id(self, provider):
        """Test provider ID."""
        assert provider.provider_id == "mimo"

    def test_supported_modes(self, provider):
        """Test supported modes - vision and multimodal."""
        assert "vision" in provider.supported_modes
        assert "multimodal" in provider.supported_modes
        assert "text" not in provider.supported_modes
        assert len(provider.supported_modes) == 2

    @pytest.mark.asyncio
    async def test_complete_text_not_supported(self, provider):
        """Test that text mode is not supported."""
        with pytest.raises(NotImplementedError, match="does not support text"):
            await provider.complete_text("test")

    def test_get_token_estimate(self, provider):
        """Test token estimation for MiMo."""
        # Vision mode
        vision_estimate = provider.get_token_estimate("vision", 100)
        assert vision_estimate["input"] == 200
        assert vision_estimate["output"] == 50
        assert vision_estimate["total"] == 250

    def test_get_performance_rating(self, provider):
        """Test performance rating for MiMo."""
        rating = provider.get_performance_rating("vision")

        assert rating["latency"] == 0.7
        assert rating["quality"] == 0.9
        assert rating["efficiency"] == 0.8

    def test_get_performance_rating_unsupported_mode(self, provider):
        """Test performance rating for unsupported mode."""
        rating = provider.get_performance_rating("text")

        assert rating["latency"] == 0.0
        assert rating["quality"] == 0.0
        assert rating["efficiency"] == 0.0


class TestProviderIntegration:
    """Integration tests across all providers."""

    @pytest.fixture
    def all_providers(self):
        """All provider instances with their configs."""
        return {
            "deepseek": DeepSeekProvider(
                AIProviderConfig(
                    api_key="test-deepseek-key",
                    model="deepseek-v4-flash",
                    base_url="https://api.deepseek.com/v1",
                )
            ),
            "claude": ClaudeProvider(
                AIProviderConfig(
                    api_key="test-anthropic-key",
                    model="claude-3-5-sonnet-20241022",
                    base_url="https://api.anthropic.com/v1",
                )
            ),
            "mimo": MiMoProvider(
                AIProviderConfig(
                    api_key="test-mimo-key",
                    model="mimo-v2.5",
                    base_url="https://token-plan-cn.xiaomimimo.com/anthropic",
                )
            ),
        }

    def test_all_providers_have_valid_ids(self, all_providers):
        """Test all providers have valid provider IDs."""
        for name, provider in all_providers.items():
            assert provider.provider_id == name.lower()

    def test_all_providers_have_supported_modes(self, all_providers):
        """Test all providers define supported modes."""
        for name, provider in all_providers.items():
            assert isinstance(provider.supported_modes, list)
            assert len(provider.supported_modes) > 0
            assert all(mode in ["text", "vision", "multimodal"] for mode in provider.supported_modes)

    def test_all_providers_have_performance_ratings(self, all_providers):
        """Test all providers can provide performance ratings."""
        for name, provider in all_providers.items():
            for mode in provider.supported_modes:
                rating = provider.get_performance_rating(mode)
                assert "latency" in rating
                assert "quality" in rating
                assert "efficiency" in rating
                assert all(0 <= v <= 1 for v in rating.values())

    def test_all_providers_have_token_estimates(self, all_providers):
        """Test all providers can provide token estimates."""
        for name, provider in all_providers.items():
            for mode in provider.supported_modes:
                estimate = provider.get_token_estimate(mode, 100)
                assert "input" in estimate
                assert "output" in estimate
                assert "total" in estimate
                assert estimate["total"] == estimate["input"] + estimate["output"]

    @pytest.mark.asyncio
    async def test_provider_mode_compatibility(self):
        """Test that providers correctly handle their supported modes."""
        providers = {
            "deepseek": ("text",),
            "claude": ("text", "vision", "multimodal"),
            "mimo": ("vision", "multimodal"),
        }

        for provider_name, (provider, modes) in {
            "deepseek": (DeepSeekProvider(AIProviderConfig(api_key="k", model="m", base_url="u")), ("text",)),
            "claude": (ClaudeProvider(AIProviderConfig(api_key="k", model="m", base_url="u")), ("text", "vision", "multimodal")),
            "mimo": (MiMoProvider(AIProviderConfig(api_key="k", model="m", base_url="u")), ("vision", "multimodal")),
        }.items():
            assert provider.supported_modes == list(modes)

            # Check that unsupported modes raise NotImplementedError
            unsupported = {"text", "vision", "multimodal"} - set(modes)
            for mode in unsupported:
                if mode == "text":
                    with pytest.raises(NotImplementedError):
                        await provider.complete_text("test")
                elif mode == "vision":
                    with pytest.raises(NotImplementedError):
                        await provider.complete_vision("test", b"data")
                elif mode == "multimodal":
                    with pytest.raises(NotImplementedError):
                        await provider.complete_multimodal("test", b"data")
