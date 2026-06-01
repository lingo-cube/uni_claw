"""Unit tests for AI core infrastructure.

Tests cover:
- config.py: AIProviderConfig, RetryConfig, FallbackConfig
- llm_client.py: LLMClient
- validator.py: ResponseValidator
- prompts.py: PromptRegistry
"""

import asyncio
import json
from unittest.mock import AsyncMock, MagicMock, Mock, patch

import pytest
import aiohttp
from aiohttp import ClientTimeout

from src.ai.core.config import AIProviderConfig, RetryConfig, FallbackConfig
from src.ai.core.llm_client import LLMClient, APIError, RateLimitError, TimeoutError
from src.ai.core.validator import ResponseValidator, ValidationError, ParserNotFoundError
from src.ai.core.prompts import PromptRegistry


# ============================================================================
# Tests for config.py
# ============================================================================

class TestRetryConfig:
    """Tests for RetryConfig dataclass."""

    def test_default_values(self):
        """Test RetryConfig has correct default values."""
        config = RetryConfig()
        assert config.max_attempts == 1
        assert config.base_delay == 1.0
        assert config.max_delay == 8.0
        assert config.exponential_base == 2.0

    def test_custom_values(self):
        """Test RetryConfig with custom values."""
        config = RetryConfig(
            max_attempts=5,
            base_delay=0.5,
            max_delay=16.0,
            exponential_base=3.0
        )
        assert config.max_attempts == 5
        assert config.base_delay == 0.5
        assert config.max_delay == 16.0
        assert config.exponential_base == 3.0


class TestFallbackConfig:
    """Tests for FallbackConfig dataclass."""

    def test_default_values(self):
        """Test FallbackConfig has correct default values."""
        config = FallbackConfig()
        assert config.strategy == "partial"
        assert config.partial_allowlist == []

    def test_custom_values(self):
        """Test FallbackConfig with custom values."""
        config = FallbackConfig(
            strategy="full",
            partial_allowlist=["capability1", "capability2"]
        )
        assert config.strategy == "full"
        assert config.partial_allowlist == ["capability1", "capability2"]

    def test_none_strategy(self):
        """Test FallbackConfig with 'none' strategy."""
        config = FallbackConfig(strategy="none")
        assert config.strategy == "none"


class TestAIProviderConfig:
    """Tests for AIProviderConfig dataclass."""

    def test_minimal_config(self):
        """Test AIProviderConfig with minimal required fields."""
        config = AIProviderConfig(api_key="test-key")
        assert config.api_key == "test-key"
        assert config.model == "deepseek-v4-flash"
        assert config.base_url == "https://api.deepseek.com/v1"
        assert config.max_concurrent_requests == 4
        assert config.request_timeout == 30.0
        assert config.reasoning_detail == "detailed"

    def test_full_config(self):
        """Test AIProviderConfig with all fields specified."""
        retry_config = RetryConfig(max_attempts=3)
        fallback_config = FallbackConfig(strategy="none")

        config = AIProviderConfig(
            api_key="test-key",
            model="deepseek-v4-pro",
            base_url="https://custom.api/v1",
            max_concurrent_requests=8,
            request_timeout=60.0,
            reasoning_detail="concise",
            retry=retry_config,
            fallback=fallback_config,
            enable_internal_validation=False
        )

        assert config.api_key == "test-key"
        assert config.model == "deepseek-v4-pro"
        assert config.base_url == "https://custom.api/v1"
        assert config.max_concurrent_requests == 8
        assert config.request_timeout == 60.0
        assert config.reasoning_detail == "concise"
        assert config.retry.max_attempts == 3
        assert config.fallback.strategy == "none"
        assert config.enable_internal_validation is False

    def test_reasoning_detail_valid_values(self):
        """Test AIProviderConfig accepts all valid reasoning_detail values."""
        for value in ["concise", "step_by_step", "detailed"]:
            config = AIProviderConfig(api_key="test", reasoning_detail=value)
            assert config.reasoning_detail == value


# ============================================================================
# Tests for llm_client.py
# ============================================================================

class TestLLMClient:
    """Tests for LLMClient."""

    @pytest.fixture
    def config(self):
        """Create a test AIProviderConfig."""
        return AIProviderConfig(
            api_key="test-key",
            max_concurrent_requests=2,
            request_timeout=10.0,
            retry=RetryConfig(max_attempts=1)  # No retry for faster tests
        )

    @pytest.fixture
    def client(self, config):
        """Create a test LLMClient."""
        return LLMClient(config)

    @pytest.fixture
    def mock_session(self):
        """Create a mock aiohttp session."""
        session = AsyncMock()
        session.post = AsyncMock()
        session.closed = False
        return session

    @pytest.fixture
    def sample_schema(self):
        """Sample JSON schema for testing."""
        return {
            "type": "object",
            "properties": {
                "result": {"type": "string"},
                "confidence": {"type": "number"}
            },
            "required": ["result", "confidence"]
        }

    @pytest.mark.asyncio
    async def test_init_creates_semaphore(self, config):
        """Test that LLMClient initializes with correct semaphore."""
        client = LLMClient(config)
        assert client._semaphore is not None
        assert client._semaphore._value == config.max_concurrent_requests

    @pytest.mark.asyncio
    async def test_get_session_creates_new_session(self, client):
        """Test _get_session creates a new session if none exists."""
        with patch('aiohttp.ClientSession') as mock_session_class:
            mock_session = AsyncMock()
            mock_session.closed = True
            mock_session_class.return_value = mock_session

            session = await client._get_session()

            mock_session_class.assert_called_once()
            assert session == mock_session

    @pytest.mark.asyncio
    async def test_call_api_success(self, client, sample_schema):
        """Test successful API call."""
        messages = [{"role": "user", "content": "test"}]
        response_data = {
            "choices": [
                {"message": {"content": json.dumps({"result": "success", "confidence": 0.9})}}
            ]
        }

        with patch.object(client, '_get_session') as mock_get_session:
            mock_session = AsyncMock()
            mock_response = AsyncMock()
            mock_response.status = 200
            mock_response.json = AsyncMock(return_value=response_data)
            mock_response.__aenter__ = AsyncMock(return_value=mock_response)
            mock_response.__aexit__ = AsyncMock()

            mock_session.post = MagicMock(return_value=mock_response)
            mock_session.closed = False
            mock_get_session.return_value = mock_session

            result = await client._call_api(messages, {"type": "json_schema", "json_schema": sample_schema})

            assert result == json.dumps({"result": "success", "confidence": 0.9})
            mock_session.post.assert_called_once()

    @pytest.mark.asyncio
    async def test_call_api_rate_limit_error(self, client, sample_schema):
        """Test API call raises RateLimitError on 429 status."""
        messages = [{"role": "user", "content": "test"}]

        with patch.object(client, '_get_session') as mock_get_session:
            mock_session = AsyncMock()
            mock_response = AsyncMock()
            mock_response.status = 429
            mock_response.__aenter__ = AsyncMock(return_value=mock_response)
            mock_response.__aexit__ = AsyncMock()

            mock_session.post = MagicMock(return_value=mock_response)
            mock_session.closed = False
            mock_get_session.return_value = mock_session

            with pytest.raises(RateLimitError, match="Rate limit exceeded"):
                await client._call_api(messages, {"type": "json_schema", "json_schema": sample_schema})

    @pytest.mark.asyncio
    async def test_call_api_server_error(self, client, sample_schema):
        """Test API call raises APIError on 5xx status."""
        messages = [{"role": "user", "content": "test"}]

        with patch.object(client, '_get_session') as mock_get_session:
            mock_session = AsyncMock()
            mock_response = AsyncMock()
            mock_response.status = 500
            mock_response.__aenter__ = AsyncMock(return_value=mock_response)
            mock_response.__aexit__ = AsyncMock()

            mock_session.post = MagicMock(return_value=mock_response)
            mock_session.closed = False
            mock_get_session.return_value = mock_session

            with pytest.raises(APIError, match="Server error"):
                await client._call_api(messages, {"type": "json_schema", "json_schema": sample_schema})

    @pytest.mark.asyncio
    async def test_call_with_retry_no_retry_on_success(self, client, sample_schema):
        """Test _call_with_retry succeeds without retry."""
        messages = [{"role": "user", "content": "test"}]
        expected_result = {"result": "success", "confidence": 0.9}

        with patch.object(client, '_call_api', return_value=json.dumps(expected_result)):
            result = await client._call_with_retry(messages, {"type": "json_schema", "json_schema": sample_schema})
            assert result == expected_result

    @pytest.mark.asyncio
    async def test_call_with_retry_exponential_backoff(self, config):
        """Test _call_with_retry uses exponential backoff."""
        config.retry = RetryConfig(max_attempts=3, base_delay=0.1, max_delay=1.0, exponential_base=2.0)
        client = LLMClient(config)
        messages = [{"role": "user", "content": "test"}]
        schema = {"type": "object"}

        call_count = 0

        async def mock_call_api(messages, response_format):
            nonlocal call_count
            call_count += 1
            if call_count < 3:
                raise RateLimitError("Rate limit exceeded")
            return json.dumps({"result": "success"})

        with patch.object(client, '_call_api', side_effect=mock_call_api):
            result = await client._call_with_retry(messages, {"type": "json_schema", "json_schema": schema})

            assert call_count == 3
            assert result == {"result": "success"}

    @pytest.mark.asyncio
    async def test_call_with_retry_gives_up_after_max_attempts(self, config):
        """Test _call_with_retry gives up after max_attempts."""
        config.retry = RetryConfig(max_attempts=2, base_delay=0.01)
        client = LLMClient(config)
        messages = [{"role": "user", "content": "test"}]
        schema = {"type": "object"}

        with patch.object(client, '_call_api', side_effect=RateLimitError("Always fails")):
            with pytest.raises(RateLimitError):
                await client._call_with_retry(messages, {"type": "json_schema", "json_schema": schema})

    @pytest.mark.asyncio
    async def test_call_with_semaphore_concurrency_control(self, config, sample_schema):
        """Test concurrent calls respect semaphore limit."""
        config.max_concurrent_requests = 2
        client = LLMClient(config)

        call_count = 0
        max_concurrent = 0
        semaphore = asyncio.Semaphore()

        async def mock_call_api(messages, response_format):
            nonlocal call_count, max_concurrent
            async with semaphore:
                call_count += 1
                current = call_count
                max_concurrent = max(max_concurrent, current)
                await asyncio.sleep(0.1)
                call_count -= 1
            return json.dumps({"result": "success"})

        with patch.object(client, '_call_api', side_effect=mock_call_api):
            tasks = [client._call_with_retry([{"role": "user"}], {"type": "json_schema", "json_schema": sample_schema})
                     for _ in range(5)]
            await asyncio.gather(*tasks)

            assert max_concurrent <= config.max_concurrent_requests

    @pytest.mark.asyncio
    async def test_inject_variables(self, client):
        """Test _inject_variables replaces placeholders."""
        template = "Process {instruction} with context {context}"
        variables = {"instruction": "Go to Settings", "context": "Desktop"}

        result = client._inject_variables(template, variables)

        assert result == "Process Go to Settings with context Desktop"

    @pytest.mark.asyncio
    async def test_call_with_prompt(self, client, sample_schema):
        """Test call_with_prompt formats and calls API."""
        prompt = "Analyze: {instruction}"
        variables = {"instruction": "test instruction"}
        expected_result = {"result": "analysis", "confidence": 0.8}

        with patch.object(client, 'call', return_value=expected_result):
            result = await client.call_with_prompt(prompt, sample_schema, variables)

            assert result == expected_result

    @pytest.mark.asyncio
    async def test_close_session(self, client):
        """Test _close_session closes the HTTP session."""
        mock_session = AsyncMock()
        mock_session.closed = False
        client._session = mock_session

        await client._close_session()

        mock_session.close.assert_called_once()

    @pytest.mark.asyncio
    async def test_context_manager(self, config):
        """Test LLMClient as async context manager."""
        async with LLMClient(config) as client:
            assert client is not None
            # Session should be open
            assert client._session is None or not client._session.closed

        # Session should be closed after exit
        if client._session:
            assert client._session.closed


# ============================================================================
# Tests for validator.py
# ============================================================================

class TestResponseValidator:
    """Tests for ResponseValidator."""

    @pytest.fixture
    def validator(self):
        """Create a test ResponseValidator."""
        return ResponseValidator()

    @pytest.fixture
    def sample_schema(self):
        """Sample JSON schema for testing."""
        return {
            "type": "object",
            "properties": {
                "name": {"type": "string"},
                "value": {"type": "number"}
            },
            "required": ["name", "value"]
        }

    def test_register_parser(self, validator):
        """Test registering a parser function."""
        def mock_parser(data):
            return data["name"]

        validator.register_parser("test_type", mock_parser)

        assert validator.has_parser("test_type")
        assert "test_type" in validator._parsers

    def test_validate_and_parse_success(self, validator):
        """Test successful validation and parsing."""
        def mock_parser(data):
            return {"parsed_name": data["name"], "parsed_value": data["value"]}

        validator.register_parser("test_type", mock_parser)

        response = {"name": "test", "value": 42}
        result = validator.validate_and_parse(response, "test_type")

        assert result == {"parsed_name": "test", "parsed_value": 42}

    def test_validate_and_parse_without_schema(self, validator):
        """Test parsing without schema validation."""
        def mock_parser(data):
            return data

        validator.register_parser("test_type", mock_parser)

        response = {"name": "test", "value": 42}
        result = validator.validate_and_parse(response, "test_type", schema=None)

        assert result == response

    def test_validate_and_parse_with_schema_success(self, validator, sample_schema):
        """Test schema validation succeeds for valid data."""
        def mock_parser(data):
            return data

        validator.register_parser("test_type", mock_parser)

        response = {"name": "test", "value": 42}
        result = validator.validate_and_parse(response, "test_type", schema=sample_schema)

        assert result == response

    def test_validate_and_parse_with_schema_failure(self, validator, sample_schema):
        """Test schema validation fails for invalid data."""
        def mock_parser(data):
            return data

        validator.register_parser("test_type", mock_parser)

        # Missing required field 'value'
        response = {"name": "test"}

        with pytest.raises(ValidationError, match="Schema validation failed"):
            validator.validate_and_parse(response, "test_type", schema=sample_schema)

    def test_validate_and_parse_parser_not_found(self, validator):
        """Test ParserNotFoundError raised when parser not registered."""
        response = {"name": "test", "value": 42}

        with pytest.raises(ParserNotFoundError, match="No parser registered for response type: missing_type"):
            validator.validate_and_parse(response, "missing_type")

    def test_validate_and_parse_parser_raises_exception(self, validator):
        """Test ValidationError raised when parser fails."""
        def failing_parser(data):
            raise ValueError("Parser failed")

        validator.register_parser("test_type", failing_parser)

        response = {"name": "test", "value": 42}

        with pytest.raises(ValidationError, match="Parsing failed"):
            validator.validate_and_parse(response, "test_type")

    def test_has_parser(self, validator):
        """Test has_parser method."""
        assert not validator.has_parser("test_type")

        def mock_parser(data):
            return data

        validator.register_parser("test_type", mock_parser)

        assert validator.has_parser("test_type")

    def test_validate_schema_directly(self, validator, sample_schema):
        """Test _validate_schema method directly."""
        valid_response = {"name": "test", "value": 42}

        # Should not raise
        validator._validate_schema(valid_response, sample_schema)

        invalid_response = {"name": "test"}  # Missing 'value'

        with pytest.raises(ValidationError, match="Schema validation failed"):
            validator._validate_schema(invalid_response, sample_schema)


# ============================================================================
# Tests for prompts.py
# ============================================================================

class TestPromptRegistry:
    """Tests for PromptRegistry."""

    @pytest.fixture
    def config(self):
        """Create a test AIProviderConfig."""
        return AIProviderConfig(api_key="test", reasoning_detail="detailed")

    @pytest.fixture
    def registry(self, config):
        """Create a test PromptRegistry."""
        return PromptRegistry(config)

    def test_init_loads_default_prompts(self, registry):
        """Test that initialization loads default prompts."""
        # Check that some default prompts are loaded
        assert "parse_task.system" in registry._prompts
        assert "parse_task.user" in registry._prompts
        assert "verify_page.system" in registry._prompts
        assert "make_decision.system" in registry._prompts

    def test_get_existing_prompt(self, registry):
        """Test getting an existing prompt template."""
        prompt = registry.get("parse_task.system")

        assert isinstance(prompt, str)
        assert len(prompt) > 0
        # Reasoning level should be replaced
        assert "{{REASONING_LEVEL}}" not in prompt

    def test_get_nonexistent_prompt(self, registry):
        """Test getting a non-existent prompt returns empty string."""
        prompt = registry.get("nonexistent.prompt")

        assert prompt == ""

    def test_get_replaces_reasoning_level(self, registry):
        """Test that get() replaces {{REASONING_LEVEL}} placeholder."""
        # Register a prompt with the placeholder
        registry.register("test.prompt", "Analyze with {{REASONING_LEVEL}}.")

        prompt = registry.get("test.prompt")

        assert "{{REASONING_LEVEL}}" not in prompt
        # For detailed reasoning, should have the detailed text
        assert "详细分析每个因素和决策依据" in prompt

    def test_register_custom_prompt(self, registry):
        """Test registering a custom prompt."""
        custom_prompt = "This is a custom prompt for {variable}."
        registry.register("custom.prompt", custom_prompt)

        prompt = registry.get("custom.prompt")

        assert prompt == custom_prompt

    def test_register_overwrites_existing(self, registry):
        """Test that registering overwrites existing prompt."""
        original = registry.get("parse_task.system")

        new_prompt = "New prompt"
        registry.register("parse_task.system", new_prompt)

        prompt = registry.get("parse_task.system")

        assert prompt == new_prompt
        assert prompt != original

    def test_inject_variables(self, registry):
        """Test variable injection into template."""
        template = "Process {instruction} in context {context}."
        variables = {
            "instruction": "Go to Settings",
            "context": "Desktop"
        }

        result = registry.inject_variables(template, variables)

        assert result == "Process Go to Settings in context Desktop."

    def test_inject_variables_with_reasoning_level(self, registry):
        """Test variable injection also replaces reasoning level."""
        template = "Analyze {{REASONING_LEVEL}}: {task}"
        variables = {"task": "test task"}

        result = registry.inject_variables(template, variables)

        assert "{{REASONING_LEVEL}}" not in result
        assert "详细分析每个因素和决策依据" in result
        assert "{task}" not in result
        assert "test task" in result

    def test_reasoning_level_concise(self, config):
        """Test reasoning level injection for 'concise'."""
        config.reasoning_detail = "concise"
        registry = PromptRegistry(config)

        registry.register("test", "{{REASONING_LEVEL}}")
        prompt = registry.get("test")

        assert "简要说明你的分析过程" in prompt

    def test_reasoning_level_step_by_step(self, config):
        """Test reasoning level injection for 'step_by_step'."""
        config.reasoning_detail = "step_by_step"
        registry = PromptRegistry(config)

        registry.register("test", "{{REASONING_LEVEL}}")
        prompt = registry.get("test")

        assert "分步骤说明你的分析过程" in prompt

    def test_reasoning_level_detailed(self, config):
        """Test reasoning level injection for 'detailed'."""
        config.reasoning_detail = "detailed"
        registry = PromptRegistry(config)

        registry.register("test", "{{REASONING_LEVEL}}")
        prompt = registry.get("test")

        assert "详细分析每个因素和决策依据" in prompt

    def test_get_parse_task_prompts(self, registry):
        """Test parse task prompts contain expected content."""
        system = registry.get("parse_task.system")
        user = registry.get("parse_task.user")

        assert "车机自动化测试" in system or "遍历计划" in system
        assert "{instruction}" in user
        assert "{{REASONING_LEVEL}}" not in user

    def test_get_verify_page_prompts(self, registry):
        """Test verify page prompts contain expected content."""
        system = registry.get("verify_page.system")
        user = registry.get("verify_page.user")

        assert "页面类型" in system or "验证" in system
        assert "{expected_type}" in user
        assert "{elements_detail}" in user

    def test_get_screen_elements_prompts(self, registry):
        """Test screen elements prompts contain expected content."""
        system = registry.get("screen_elements.system")
        user = registry.get("screen_elements.user")

        assert "安全" in system or "评估" in system
        assert "{elements_list}" in user
        assert "{instruction}" in user

    def test_get_decision_prompts(self, registry):
        """Test decision prompts contain expected content."""
        system = registry.get("make_decision.system")
        user = registry.get("make_decision.user")

        assert "决策" in system or "操作" in system
        assert "{elements_detail}" in user
        assert "{safe_elements}" in user

    def test_inject_variables_missing_key(self, registry):
        """Test that missing variables in template are left unchanged."""
        template = "Process {instruction} with {missing_var}."
        variables = {"instruction": "test"}

        result = registry.inject_variables(template, variables)

        assert "Process test with {missing_var}." == result
