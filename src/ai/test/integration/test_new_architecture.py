"""Integration tests for new AI architecture.

Tests verify:
- Provider routing with configuration
- Prompt manager integration
- Trace integration
- End-to-end capability execution
"""

import pytest
from pathlib import Path
from unittest.mock import AsyncMock, Mock, patch

from src.ai.providers import (
    AIProvider,
    AIResponse,
    AIProviderConfig,
    create_provider,
    DeepSeekProvider,
    ClaudeProvider,
)
from src.ai.providers.config import (
    load_routing_config,
    get_provider_routing,
    get_provider_config,
    resolve_env_var,
    validate_config,
)
from src.ai.prompts import PromptManager, PromptTemplate
from src.ai.trace import TraceIntegration, SpanContext
from src.models.content_models import PageAnalysis


class TestProviderRouting:
    """Test provider routing from configuration."""

    @pytest.fixture
    def routing_config_path(self):
        """Path to the routing configuration."""
        return Path(__file__).parent.parent.parent.parent.parent / "config" / "ai_providers.yaml"

    def test_load_routing_config(self, routing_config_path):
        """Test loading routing configuration."""
        if not routing_config_path.exists():
            pytest.skip(f"Config file not found: {routing_config_path}")

        config = load_routing_config(str(routing_config_path))

        assert "providers" in config
        assert "routing" in config
        assert "deepseek" in config["providers"]
        assert "claude" in config["providers"]

    def test_provider_routing_for_capability(self, routing_config_path):
        """Test getting provider for a capability."""
        if not routing_config_path.exists():
            pytest.skip(f"Config file not found: {routing_config_path}")

        config = load_routing_config(str(routing_config_path))

        # analyze_visual should route to claude
        provider_id = get_provider_routing("analyze_visual", config)
        assert provider_id == "claude"

        # parse_instruction should route to deepseek
        provider_id = get_provider_routing("parse_instruction", config)
        assert provider_id == "deepseek"

    def test_get_provider_config_from_routing(self, routing_config_path):
        """Test getting provider config from routing."""
        if not routing_config_path.exists():
            pytest.skip(f"Config file not found: {routing_config_path}")

        config = load_routing_config(str(routing_config_path))

        claude_config = get_provider_config("claude", config)

        assert "api_key" in claude_config
        assert "model" in claude_config
        assert "base_url" in claude_config


class TestPromptManagerIntegration:
    """Test prompt manager with actual prompt files."""

    @pytest.fixture
    def prompt_manager(self):
        """Create a PromptManager with real prompt files."""
        prompt_dir = Path(__file__).parent.parent.parent.parent / "ai" / "prompts"
        if not prompt_dir.exists():
            pytest.skip(f"Prompt directory not found: {prompt_dir}")
        return PromptManager(str(prompt_dir))

    def test_load_all_prompts(self, prompt_manager):
        """Test loading all prompt templates."""
        capabilities = prompt_manager.list_capabilities()

        # Should have at least the 5 core capabilities
        expected_capabilities = {
            "analyze_visual",
            "parse_instruction",
            "verify_page_type",
            "decide_next_action",
            "screen_safety",
        }

        for cap in expected_capabilities:
            assert cap in capabilities, f"Missing capability: {cap}"

    def test_get_prompt_with_variables(self, prompt_manager):
        """Test getting and formatting a prompt with variables."""
        template = prompt_manager.get_prompt("analyze_visual")

        assert template.capability == "analyze_visual"
        assert isinstance(template, PromptTemplate)
        assert template.variables is not None

    def test_format_prompt_with_variables(self, prompt_manager):
        """Test formatting a prompt with variable injection."""
        template = prompt_manager.get_prompt("analyze_visual")

        # Format with test data
        formatted = template.format(
            image_description="Vehicle home screen",
            context_info='{"path": "/Home"}',
        )

        assert "Vehicle home screen" in formatted
        assert "Home" in formatted

    def test_validate_all_prompts(self, prompt_manager):
        """Test validating all loaded prompts."""
        capabilities = prompt_manager.list_capabilities()

        for capability in capabilities:
            is_valid = prompt_manager.validate_prompt(capability)
            assert is_valid, f"Prompt validation failed for: {capability}"


class TestTraceIntegration:
    """Test trace integration with AI calls."""

    @pytest.fixture
    def trace_integration(self):
        """Create a TraceIntegration instance."""
        return TraceIntegration(enable_auto=False)

    def test_start_and_finish_span(self, trace_integration):
        """Test complete span lifecycle."""
        span = trace_integration.start_span(
            "test_operation",
            tags={"capability": "test"},
        )

        assert span.span_id.startswith("test_operation_")
        assert span.is_active is True

        trace_integration.finish_span(span, result={"data": "test"})

        assert span.is_active is False

    def test_record_metrics(self, trace_integration):
        """Test recording AI call metrics."""
        trace_integration.record_metrics(
            capability="analyze_visual",
            provider_id="claude",
            mode="vision",
            latency_ms=1500,
            tokens={"input": 500, "output": 300},
            success=True,
        )

        metrics = trace_integration.get_provider_metrics("claude", "vision")
        assert metrics is not None
        assert metrics.total_calls == 1
        assert metrics.successful_calls == 1

    def test_get_active_spans(self, trace_integration):
        """Test getting active spans."""
        span1 = trace_integration.start_span("op1")
        span2 = trace_integration.start_span("op2")

        active = trace_integration.get_active_spans()

        assert len(active) == 2
        assert span1 in active
        assert span2 in active

        # Clean up
        trace_integration.finish_span(span1)
        trace_integration.finish_span(span2)

    def test_health_check(self, trace_integration):
        """Test health check."""
        health = trace_integration.health_check()

        assert health["healthy"] is True
        assert "active_spans" in health
        assert "providers_tracked" in health


class TestUnifiedCapabilityExecution:
    """Test end-to-end capability execution with new architecture."""

    @pytest.fixture
    def mock_provider(self):
        """Create a mock provider for testing."""
        provider = Mock(spec=AIProvider)
        provider.provider_id = "mock"

        # Mock complete_text method
        async def mock_complete_text(prompt, **kwargs):
            return AIResponse(
                content='{"result": "test_response"}',
                provider_id="mock",
                mode="text",
                input_tokens=10,
                output_tokens=20,
                latency_ms=50.0,
            )

        provider.complete_text = mock_complete_text
        return provider

    @pytest.fixture
    def prompt_manager(self):
        """Create a prompt manager."""
        prompt_dir = Path(__file__).parent.parent.parent.parent / "ai" / "prompts"
        if not prompt_dir.exists():
            pytest.skip("Prompt directory not found")
        return PromptManager(str(prompt_dir))

    @pytest.fixture
    def trace_integration(self):
        """Create a trace integration."""
        return TraceIntegration(enable_auto=False)

    @pytest.mark.asyncio
    async def test_execute_parse_capability(
        self, mock_provider, prompt_manager, trace_integration
    ):
        """Test executing parse_instruction capability end-to-end."""
        # Start trace
        span = trace_integration.start_span("parse_instruction")

        # Get prompt
        template = prompt_manager.get_prompt("parse_instruction")
        formatted = template.format(
            instruction="Go to WiFi settings",
            context="Current page: Home",
        )

        # Execute
        response = await mock_provider.complete_text(formatted)

        # Record metrics
        trace_integration.record_metrics(
            capability="parse_instruction",
            provider_id="mock",
            mode="text",
            latency_ms=response.latency_ms,
            tokens={"input": response.input_tokens, "output": response.output_tokens},
            success=True,
        )

        # Finish trace
        trace_integration.finish_span(span)

        assert response.success is True
        assert "test_response" in response.content

    def test_provider_selection_from_config(self):
        """Test provider selection based on configuration."""
        config_path = (
            Path(__file__).parent.parent.parent.parent.parent / "config" / "ai_providers.yaml"
        )
        if not config_path.exists():
            pytest.skip("Config file not found")

        config = load_routing_config(str(config_path))

        # Test routing for different capabilities
        test_cases = [
            ("analyze_visual", "claude"),
            ("parse_instruction", "deepseek"),
            ("verify_page_type", "deepseek"),
        ]

        for capability, expected_provider in test_cases:
            provider_id = get_provider_routing(capability, config)
            assert provider_id == expected_provider, (
                f"Expected {expected_provider} for {capability}, got {provider_id}"
            )


class TestEnvVariableResolution:
    """Test environment variable resolution in config."""

    def test_resolve_env_var_in_config(self):
        """Test resolving ${VAR} syntax."""
        import os

        os.environ["TEST_API_KEY"] = "resolved_key"
        result = resolve_env_var("${TEST_API_KEY}")
        assert result == "resolved_key"

        # Clean up
        del os.environ["TEST_API_KEY"]

    def test_resolve_env_var_with_default(self):
        """Test resolving ${VAR:default} syntax."""
        import os

        os.environ.pop("NONEXISTENT_VAR_12345", None)
        result = resolve_env_var("${NONEXISTENT_VAR_12345:default_value}")
        assert result == "default_value"


class TestConfigValidation:
    """Test configuration validation."""

    def test_validate_valid_config(self):
        """Test validating a valid configuration."""
        config = {
            "providers": {
                "p1": {
                    "class": "Provider1",
                    "config": {
                        "api_key": "key",
                        "model": "model",
                        "base_url": "url",
                    },
                },
            },
            "routing": {"cap1": "p1"},
            "defaults": {},
        }

        assert validate_config(config) is True

    def test_validate_invalid_config_missing_providers(self):
        """Test validation fails with missing providers section."""
        config = {"routing": {}}

        with pytest.raises(ValueError, match="Missing 'providers'"):
            validate_config(config)

    def test_validate_invalid_config_missing_routing(self):
        """Test validation fails with missing routing section."""
        config = {"providers": {}}

        with pytest.raises(ValueError, match="Missing 'routing'"):
            validate_config(config)

    def test_validate_invalid_config_bad_provider_ref(self):
        """Test validation fails with unknown provider reference."""
        config = {
            "providers": {
                "p1": {
                    "class": "Provider1",
                    "config": {"api_key": "k", "model": "m", "base_url": "u"},
                }
            },
            "routing": {"cap1": "unknown_provider"},
            "defaults": {},
        }

        with pytest.raises(ValueError, match="references unknown provider"):
            validate_config(config)
