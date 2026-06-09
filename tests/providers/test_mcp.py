"""Tests for MCPProvider."""

import pytest
import base64
from unittest.mock import AsyncMock, MagicMock, patch

from src.ai.providers.mcp import MCPProvider
from src.ai.providers.base import AIProviderConfig, AIResponse


@pytest.fixture
def mcp_config():
    """Create test MCP configuration."""
    return AIProviderConfig(
        api_key="not_required",
        model="mcp-vision-4.5v",
        base_url="http://localhost:8080",
        max_concurrent_requests=4,
        request_timeout=30.0,
    )


@pytest.fixture
def mcp_provider(mcp_config):
    """Create MCPProvider instance."""
    return MCPProvider(mcp_config)


def test_provider_properties(mcp_provider):
    """Test provider basic properties."""
    assert mcp_provider.provider_id == "mcp"
    assert "vision" in mcp_provider.supported_modes
    assert "multimodal" in mcp_provider.supported_modes
    assert "text" not in mcp_provider.supported_modes


def test_token_estimation(mcp_provider):
    """Test token estimation."""
    # Vision mode should estimate higher tokens due to image
    vision_estimate = mcp_provider.get_token_estimate("vision", avg_request_tokens=500)
    assert vision_estimate["input"] > 500  # Should include image tokens
    assert vision_estimate["output"] > 0
    assert vision_estimate["total"] > 1000

    # Multimodal should be similar to vision
    multimodal_estimate = mcp_provider.get_token_estimate("multimodal")
    assert multimodal_estimate["input"] > 500


def test_performance_rating(mcp_provider):
    """Test performance ratings."""
    vision_rating = mcp_provider.get_performance_rating("vision")
    assert 0 <= vision_rating["latency"] <= 1
    assert 0 <= vision_rating["quality"] <= 1
    assert 0 <= vision_rating["efficiency"] <= 1

    # Text mode should return zeros
    text_rating = mcp_provider.get_performance_rating("text")
    assert text_rating["latency"] == 0
    assert text_rating["quality"] == 0
    assert text_rating["efficiency"] == 0


@pytest.mark.asyncio
async def test_complete_text_not_supported(mcp_provider):
    """Test that complete_text raises NotImplementedError."""
    with pytest.raises(NotImplementedError):
        await mcp_provider.complete_text("test prompt")


@pytest.mark.asyncio
async def test_complete_vision_success(mcp_provider):
    """Test successful vision analysis."""
    # Mock the HTTP request
    mock_response = {
        "content": "This is a test image description.",
    }

    with patch.object(
        mcp_provider, "_make_request", new=AsyncMock(return_value=mock_response)
    ):
        response = await mcp_provider.complete_vision(
            prompt="Describe this image",
            image_data=b"fake_image_data",
            max_tokens=1000,
        )

        assert response.success is True
        assert response.provider_id == "mcp"
        assert response.mode == "vision"
        assert response.content == "This is a test image description."
        assert response.input_tokens > 0
        assert response.output_tokens > 0


@pytest.mark.asyncio
async def test_complete_vision_with_dict_response(mcp_provider):
    """Test vision analysis with dict response."""
    # Mock response with result field
    mock_response = {
        "result": "Analysis result",
    }

    with patch.object(
        mcp_provider, "_make_request", new=AsyncMock(return_value=mock_response)
    ):
        response = await mcp_provider.complete_vision(
            prompt="Analyze this",
            image_data=b"fake_image_data",
        )

        assert response.content == "Analysis result"


@pytest.mark.asyncio
async def test_complete_vision_error(mcp_provider):
    """Test vision analysis with error."""
    # Mock HTTP error
    with patch.object(
        mcp_provider, "_make_request", new=AsyncMock(side_effect=RuntimeError("API error"))
    ):
        with pytest.raises(RuntimeError, match="MCP vision request failed"):
            await mcp_provider.complete_vision(
                prompt="Describe this",
                image_data=b"fake_image_data",
            )


@pytest.mark.asyncio
async def test_complete_multimodal(mcp_provider):
    """Test multimodal completion."""
    mock_response = {
        "content": "Multimodal analysis result",
    }

    with patch.object(
        mcp_provider, "_make_request", new=AsyncMock(return_value=mock_response)
    ):
        response = await mcp_provider.complete_multimodal(
            prompt="Analyze with context",
            image_data=b"fake_image_data",
            additional_context={"user_id": "123", "session": "abc"},
            max_tokens=2000,
        )

        assert response.success is True
        assert response.mode == "multimodal"
        assert "Context:" in response.content or "Multimodal" in response.content


@pytest.mark.asyncio
async def test_complete_multimodal_without_context(mcp_provider):
    """Test multimodal without additional context."""
    mock_response = {
        "content": "Simple analysis",
    }

    with patch.object(
        mcp_provider, "_make_request", new=AsyncMock(return_value=mock_response)
    ):
        response = await mcp_provider.complete_multimodal(
            prompt="Analyze",
            image_data=b"fake_image_data",
        )

        assert response.mode == "multimodal"


@pytest.mark.asyncio
async def test_check_mode_supported_text(mcp_provider):
    """Test that text mode check raises error."""
    from src.ai.providers.base import AIProvider

    # Text mode should raise NotImplementedError
    with pytest.raises(NotImplementedError):
        await mcp_provider.complete_text("test")


def test_config_validation():
    """Test configuration validation."""
    # Valid config
    config = AIProviderConfig(
        api_key="test",
        model="mcp-vision",
        base_url="http://localhost:8080",
    )
    provider = MCPProvider(config)
    assert provider.config.api_key == "test"
    assert provider.config.model == "mcp-vision"


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
