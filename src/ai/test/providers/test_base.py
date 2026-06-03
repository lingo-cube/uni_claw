"""Unit tests for AIProvider base class and AIResponse."""

import pytest
from src.ai.providers.base import AIProvider, AIResponse, AIProviderConfig, create_provider


class TestAIResponse:
    """Test AIResponse dataclass."""

    def test_response_creation(self):
        """Test creating an AIResponse."""
        response = AIResponse(
            content="Test response",
            provider_id="test",
            mode="text",
            input_tokens=10,
            output_tokens=20,
            latency_ms=100.0,
        )

        assert response.content == "Test response"
        assert response.provider_id == "test"
        assert response.mode == "text"
        assert response.input_tokens == 10
        assert response.output_tokens == 20

    def test_total_tokens(self):
        """Test total_tokens property."""
        response = AIResponse(
            content="Test",
            provider_id="test",
            mode="text",
            input_tokens=100,
            output_tokens=50,
            latency_ms=100.0,
        )

        assert response.total_tokens == 150

    def test_to_dict(self):
        """Test serialization to dict."""
        response = AIResponse(
            content="Test",
            provider_id="test",
            mode="text",
            input_tokens=10,
            output_tokens=20,
            latency_ms=100.0,
            model="test-model",
        )

        data = response.to_dict()

        assert data["content"] == "Test"
        assert data["provider_id"] == "test"
        assert data["input_tokens"] == 10
        assert data["output_tokens"] == 20
        assert data["total_tokens"] == 30
        assert data["model"] == "test-model"

    def test_from_dict(self):
        """Test deserialization from dict."""
        data = {
            "content": "Test",
            "provider_id": "test",
            "mode": "text",
            "input_tokens": 10,
            "output_tokens": 20,
            "latency_ms": 100.0,
        }

        response = AIResponse.from_dict(data)

        assert response.content == "Test"
        assert response.provider_id == "test"

    def test_response_with_error(self):
        """Test response with error state."""
        response = AIResponse(
            content="",
            provider_id="test",
            mode="text",
            input_tokens=0,
            output_tokens=0,
            latency_ms=50.0,
            success=False,
            error_message="API error",
        )

        assert response.success is False
        assert response.error_message == "API error"


class TestAIProviderConfig:
    """Test AIProviderConfig dataclass."""

    def test_config_creation(self):
        """Test creating a valid config."""
        config = AIProviderConfig(
            api_key="test-key",
            model="test-model",
            base_url="https://api.example.com",
        )

        assert config.api_key == "test-key"
        assert config.model == "test-model"
        assert config.base_url == "https://api.example.com"

    def test_config_defaults(self):
        """Test default values."""
        config = AIProviderConfig(
            api_key="test-key",
            model="test-model",
            base_url="https://api.example.com",
        )

        assert config.max_concurrent_requests == 4
        assert config.request_timeout == 30.0

    def test_config_validation_api_key(self):
        """Test validation for missing api_key."""
        with pytest.raises(ValueError, match="api_key is required"):
            AIProviderConfig(
                api_key="",
                model="test-model",
                base_url="https://api.example.com",
            )

    def test_config_validation_model(self):
        """Test validation for missing model."""
        with pytest.raises(ValueError, match="model is required"):
            AIProviderConfig(
                api_key="test-key",
                model="",
                base_url="https://api.example.com",
            )

    def test_config_validation_base_url(self):
        """Test validation for missing base_url."""
        with pytest.raises(ValueError, match="base_url is required"):
            AIProviderConfig(
                api_key="test-key",
                model="test-model",
                base_url="",
            )

    def test_config_validation_max_concurrent(self):
        """Test validation for invalid max_concurrent_requests."""
        with pytest.raises(ValueError, match="max_concurrent_requests must be positive"):
            AIProviderConfig(
                api_key="test-key",
                model="test-model",
                base_url="https://api.example.com",
                max_concurrent_requests=0,
            )

    def test_config_validation_timeout(self):
        """Test validation for invalid timeout."""
        with pytest.raises(ValueError, match="request_timeout must be positive"):
            AIProviderConfig(
                api_key="test-key",
                model="test-model",
                base_url="https://api.example.com",
                request_timeout=-1.0,
            )


class MockAIProvider(AIProvider):
    """Mock implementation for testing."""

    @property
    def provider_id(self) -> str:
        return "mock"

    @property
    def supported_modes(self) -> list:
        return ["text", "vision"]

    async def complete_text(self, prompt, schema=None, max_tokens=2048, **kwargs):
        from src.ai.providers.base import AIResponse
        return AIResponse(
            content=f"Mock: {prompt}",
            provider_id="mock",
            mode="text",
            input_tokens=len(prompt.split()),
            output_tokens=10,
            latency_ms=50.0,
        )

    async def complete_vision(self, prompt, image_data, schema=None, max_tokens=4096, **kwargs):
        from src.ai.providers.base import AIResponse
        return AIResponse(
            content=f"Mock vision: {len(image_data)} bytes",
            provider_id="mock",
            mode="vision",
            input_tokens=100,
            output_tokens=20,
            latency_ms=100.0,
        )

    async def complete_multimodal(self, prompt, image_data, additional_context=None, schema=None, max_tokens=4096, **kwargs):
        from src.ai.providers.base import AIResponse
        return AIResponse(
            content=f"Mock multimodal",
            provider_id="mock",
            mode="multimodal",
            input_tokens=150,
            output_tokens=30,
            latency_ms=150.0,
        )


class TestAIProvider:
    """Test AIProvider abstract base class."""

    def test_provider_initialization(self):
        """Test provider initialization."""
        config = AIProviderConfig(
            api_key="test-key",
            model="test-model",
            base_url="https://api.example.com",
        )

        provider = MockAIProvider(config)

        assert provider.config == config
        assert provider.provider_id == "mock"
        assert provider.supported_modes == ["text", "vision"]

    def test_semaphore_initialization(self):
        """Test that semaphore is initialized correctly."""
        config = AIProviderConfig(
            api_key="test-key",
            model="test-model",
            base_url="https://api.example.com",
            max_concurrent_requests=5,
        )

        provider = MockAIProvider(config)

        assert provider._semaphore._value == 5

    def test_check_mode_supported_pass(self):
        """Test mode check with supported mode."""
        provider = MockAIProvider(
            AIProviderConfig(
                api_key="test-key",
                model="test-model",
                base_url="https://api.example.com",
            )
        )

        # Should not raise
        provider._check_mode_supported("text")

    def test_check_mode_supported_fail(self):
        """Test mode check with unsupported mode."""
        provider = MockAIProvider(
            AIProviderConfig(
                api_key="test-key",
                model="test-model",
                base_url="https://api.example.com",
            )
        )

        with pytest.raises(NotImplementedError, match="does not support multimodal"):
            provider._check_mode_supported("multimodal")

    @pytest.mark.asyncio
    async def test_complete_text_mock(self):
        """Test text completion with mock provider."""
        provider = MockAIProvider(
            AIProviderConfig(
                api_key="test-key",
                model="test-model",
                base_url="https://api.example.com",
            )
        )

        response = await provider.complete_text("Test prompt")

        assert "Mock: Test prompt" in response.content
        assert response.mode == "text"

    @pytest.mark.asyncio
    async def test_complete_vision_mock(self):
        """Test vision completion with mock provider."""
        provider = MockAIProvider(
            AIProviderConfig(
                api_key="test-key",
                model="test-model",
                base_url="https://api.example.com",
            )
        )

        response = await provider.complete_vision("Test", b"fake_image")

        assert "vision" in response.content.lower()
        assert response.mode == "vision"

    def test_get_token_estimate_text(self):
        """Test token estimation for text mode."""
        provider = MockAIProvider(
            AIProviderConfig(
                api_key="test-key",
                model="test-model",
                base_url="https://api.example.com",
            )
        )

        estimate = provider.get_token_estimate("text", 100)

        assert estimate["input"] == 100
        assert estimate["output"] == 50
        assert estimate["total"] == 150

    def test_get_token_estimate_vision(self):
        """Test token estimation for vision mode."""
        provider = MockAIProvider(
            AIProviderConfig(
                api_key="test-key",
                model="test-model",
                base_url="https://api.example.com",
            )
        )

        estimate = provider.get_token_estimate("vision", 100)

        # Vision requires more input tokens
        assert estimate["input"] == 200
        assert estimate["output"] == 100

    def test_get_performance_rating(self):
        """Test performance rating."""
        provider = MockAIProvider(
            AIProviderConfig(
                api_key="test-key",
                model="test-model",
                base_url="https://api.example.com",
            )
        )

        rating = provider.get_performance_rating("text")

        assert "latency" in rating
        assert "quality" in rating
        assert "efficiency" in rating
        assert all(0 <= v <= 1 for v in rating.values())

    @pytest.mark.asyncio
    async def test_health_check_success(self):
        """Test health check with successful response."""
        provider = MockAIProvider(
            AIProviderConfig(
                api_key="test-key",
                model="test-model",
                base_url="https://api.example.com",
            )
        )

        is_healthy = await provider.health_check()

        assert is_healthy is True

    @pytest.mark.asyncio
    async def test_health_check_failure(self):
        """Test health check with failed response."""

        class FailingMockProvider(MockAIProvider):
            async def complete_text(self, prompt, schema=None, max_tokens=2048, **kwargs):
                from src.ai.providers.base import AIResponse
                return AIResponse(
                    content="",
                    provider_id="mock",
                    mode="text",
                    input_tokens=0,
                    output_tokens=0,
                    latency_ms=50.0,
                    success=False,
                )

        provider = FailingMockProvider(
            AIProviderConfig(
                api_key="test-key",
                model="test-model",
                base_url="https://api.example.com",
            )
        )

        is_healthy = await provider.health_check()

        assert is_healthy is False


class TestCreateProvider:
    """Test create_provider factory function."""

    def test_create_deepseek_provider(self):
        """Test creating DeepSeek provider."""
        config = AIProviderConfig(
            api_key="test-key",
            model="deepseek-v4-flash",
            base_url="https://api.deepseek.com/v1",
        )

        provider = create_provider("deepseek", config)

        assert provider.provider_id == "deepseek"
        assert provider.supported_modes == ["text"]

    def test_create_claude_provider(self):
        """Test creating Claude provider."""
        config = AIProviderConfig(
            api_key="test-key",
            model="claude-3-5-sonnet-20241022",
            base_url="https://api.anthropic.com/v1",
        )

        provider = create_provider("claude", config)

        assert provider.provider_id == "claude"
        assert "text" in provider.supported_modes
        assert "vision" in provider.supported_modes

    def test_create_mimo_provider(self):
        """Test creating MiMo provider."""
        config = AIProviderConfig(
            api_key="test-key",
            model="mimo-v2.5",
            base_url="https://token-plan-cn.xiaomimimo.com/anthropic",
        )

        provider = create_provider("mimo", config)

        assert provider.provider_id == "mimo"
        assert "vision" in provider.supported_modes
        assert "multimodal" in provider.supported_modes

    def test_create_provider_invalid(self):
        """Test creating invalid provider."""
        config = AIProviderConfig(
            api_key="test-key",
            model="test-model",
            base_url="https://api.example.com",
        )

        with pytest.raises(ValueError, match="Unknown provider type"):
            create_provider("invalid", config)

    def test_create_provider_case_insensitive(self):
        """Test that provider type is case-insensitive."""
        config = AIProviderConfig(
            api_key="test-key",
            model="deepseek-v4-flash",
            base_url="https://api.deepseek.com/v1",
        )

        provider = create_provider("DeepSeek", config)

        assert provider.provider_id == "deepseek"
