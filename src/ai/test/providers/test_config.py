"""Unit tests for AI provider configuration utilities."""

import os
import pytest
from pathlib import Path
import tempfile
from src.ai.providers.config import (
    resolve_env_var,
    load_routing_config,
    get_provider_routing,
    get_provider_config,
    validate_config,
    AIProviderConfig,
)


class TestResolveEnvVar:
    """Test environment variable resolution."""

    def test_resolve_existing_env_var(self):
        """Test resolving an existing environment variable."""
        os.environ["TEST_VAR"] = "test_value"
        result = resolve_env_var("${TEST_VAR}")
        assert result == "test_value"

    def test_resolve_env_var_with_default(self):
        """Test resolving with default value when var doesn't exist."""
        # Make sure the variable doesn't exist
        os.environ.pop("NONEXISTENT_VAR_12345", None)
        result = resolve_env_var("${NONEXISTENT_VAR_12345:default_value}")
        # When var doesn't exist, use default
        assert result == "default_value"

    def test_resolve_env_var_no_default_missing(self):
        """Test resolving missing var without default."""
        os.environ.pop("REALLY_MISSING_VAR_12345", None)
        result = resolve_env_var("${REALLY_MISSING_VAR_12345}")
        assert result == ""

    def test_resolve_multiple_env_vars(self):
        """Test resolving multiple variables in one string."""
        os.environ["VAR1"] = "value1"
        os.environ["VAR2"] = "value2"
        result = resolve_env_var("${VAR1}/${VAR2}")
        assert result == "value1/value2"

    def test_resolve_no_env_vars(self):
        """Test string without env vars."""
        result = resolve_env_var("plain_string")
        assert result == "plain_string"


class TestAIProviderConfig:
    """Test AIProviderConfig class."""

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

    def test_config_from_dict(self):
        """Test creating config from dict."""
        data = {
            "api_key": "test-key",
            "model": "test-model",
            "base_url": "https://api.example.com",
            "max_concurrent_requests": 8,
        }

        config = AIProviderConfig.from_dict(data)

        assert config.api_key == "test-key"
        assert config.max_concurrent_requests == 8

    def test_config_to_dict(self):
        """Test converting config to dict."""
        config = AIProviderConfig(
            api_key="test-key",
            model="test-model",
            base_url="https://api.example.com",
        )

        data = config.to_dict()

        assert data["api_key"] == "test-key"
        assert data["model"] == "test-model"
        assert data["base_url"] == "https://api.example.com"


class TestLoadRoutingConfig:
    """Test routing configuration loading."""

    @pytest.fixture
    def sample_config_file(self):
        """Create a sample config file."""
        config_content = """
version: 1.0

providers:
  test_provider:
    class: "TestProvider"
    config:
      api_key: "${TEST_API_KEY}"
      model: "test-model"
      base_url: "https://api.test.com"

routing:
  test_capability: test_provider

defaults:
  default_provider: test_provider
"""
        with tempfile.NamedTemporaryFile(mode='w', suffix='.yaml', delete=False) as f:
            f.write(config_content)
            temp_path = f.name

        yield temp_path

        # Cleanup
        os.unlink(temp_path)

    def test_load_routing_config(self, sample_config_file):
        """Test loading routing configuration."""
        os.environ["TEST_API_KEY"] = "resolved_key"
        config = load_routing_config(sample_config_file)

        assert "providers" in config
        assert "routing" in config
        assert config["providers"]["test_provider"]["config"]["api_key"] == "resolved_key"

    def test_load_nonexistent_file(self):
        """Test loading non-existent file."""
        with pytest.raises(FileNotFoundError):
            load_routing_config("nonexistent_file.yaml")


class TestGetProviderRouting:
    """Test provider routing lookup."""

    @pytest.fixture
    def sample_config(self):
        """Sample routing configuration."""
        return {
            "providers": {
                "provider1": {"class": "P1", "config": {}},
                "provider2": {"class": "P2", "config": {}},
            },
            "routing": {
                "capability1": "provider1",
                "capability2": "provider2",
            },
            "defaults": {
                "default_provider": "provider1",
            },
        }

    def test_get_provider_routing_found(self, sample_config):
        """Test getting routing for existing capability."""
        provider_id = get_provider_routing("capability1", sample_config)
        assert provider_id == "provider1"

    def test_get_provider_routing_with_default(self, sample_config):
        """Test getting routing with default provider."""
        provider_id = get_provider_routing("nonexistent_capability", sample_config)
        assert provider_id == "provider1"

    def test_get_provider_routing_no_default(self):
        """Test getting routing without default."""
        config = {
            "providers": {"p1": {}},
            "routing": {},
            "defaults": {},
        }

        with pytest.raises(ValueError, match="No provider configured"):
            get_provider_routing("test_capability", config)


class TestGetProviderConfig:
    """Test provider config lookup."""

    @pytest.fixture
    def sample_config(self):
        """Sample configuration."""
        return {
            "providers": {
                "provider1": {
                    "class": "P1",
                    "config": {"api_key": "key1", "model": "m1", "base_url": "url1"},
                },
            },
            "routing": {},
            "defaults": {},
        }

    def test_get_provider_config_found(self, sample_config):
        """Test getting config for existing provider."""
        config = get_provider_config("provider1", sample_config)
        assert config["api_key"] == "key1"

    def test_get_provider_config_not_found(self, sample_config):
        """Test getting config for nonexistent provider."""
        with pytest.raises(ValueError, match="Provider not found"):
            get_provider_config("nonexistent", sample_config)


class TestValidateConfig:
    """Test configuration validation."""

    def test_validate_valid_config(self):
        """Test validating a valid configuration."""
        config = {
            "providers": {
                "p1": {
                    "class": "P1",
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

    def test_validate_missing_providers(self):
        """Test validation with missing providers section."""
        config = {"routing": {}}

        with pytest.raises(ValueError, match="Missing 'providers'"):
            validate_config(config)

    def test_validate_missing_routing(self):
        """Test validation with missing routing section."""
        config = {"providers": {}}

        with pytest.raises(ValueError, match="Missing 'routing'"):
            validate_config(config)

    def test_validate_provider_missing_class(self):
        """Test provider with missing class field."""
        config = {
            "providers": {"p1": {"config": {}}},
            "routing": {"cap1": "p1"},
            "defaults": {},
        }

        with pytest.raises(ValueError, match="missing 'class'"):
            validate_config(config)

    def test_validate_provider_missing_config_key(self):
        """Test provider config missing required key."""
        config = {
            "providers": {
                "p1": {
                    "class": "P1",
                    "config": {"api_key": "key"},  # Missing model, base_url
                },
            },
            "routing": {"cap1": "p1"},
            "defaults": {},
        }

        with pytest.raises(ValueError, match="missing required key"):
            validate_config(config)

    def test_validate_routing_unknown_provider(self):
        """Test routing referencing unknown provider."""
        config = {
            "providers": {"p1": {"class": "P1", "config": {"api_key": "k", "model": "m", "base_url": "u"}}},
            "routing": {"cap1": "unknown_provider"},
            "defaults": {},
        }

        with pytest.raises(ValueError, match="references unknown provider"):
            validate_config(config)
