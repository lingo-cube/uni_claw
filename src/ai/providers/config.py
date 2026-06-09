"""Configuration utilities for AI providers.

This module provides configuration loading, environment variable resolution,
and provider routing configuration management.
"""

import os
import re
import logging
from pathlib import Path
from typing import Dict, Any, Optional
import yaml

logger = logging.getLogger(__name__)


def resolve_env_var(value: str) -> str:
    """Resolve environment variables in configuration values.

    Supports ${VAR_NAME} and ${VAR_NAME:default} syntax.

    Args:
        value: String that may contain ${VAR_NAME} references

    Returns:
        str: Value with environment variables resolved
    """
    if not isinstance(value, str):
        return value

    # Pattern: ${VAR_NAME} or ${VAR_NAME:default}
    pattern = r'\$\{([^}:]+)(?::([^}]*))?\}'

    def replace_env(match):
        var_name = match.group(1)
        default = match.group(2) if match.group(2) is not None else ""
        # Use os.environ.get which returns default if key not found
        return os.environ.get(var_name, default)

    return re.sub(pattern, replace_env, value)


def load_routing_config(config_path: str = "config/ai_providers.yaml") -> Dict[str, Any]:
    """Load the provider routing configuration.

    Args:
        config_path: Path to the configuration file

    Returns:
        Dict: Parsed configuration with environment variables resolved

    Raises:
        FileNotFoundError: If config file doesn't exist
        ValueError: If config is invalid
    """
    path = Path(config_path)
    if not path.exists():
        raise FileNotFoundError(f"Configuration file not found: {config_path}")

    with open(path, 'r', encoding='utf-8') as f:
        config = yaml.safe_load(f)

    if not config:
        raise ValueError(f"Empty configuration file: {config_path}")

    # Resolve environment variables in provider configs
    if "providers" in config:
        for provider_id, provider_config in config["providers"].items():
            if "config" in provider_config:
                for key, value in provider_config["config"].items():
                    provider_config["config"][key] = resolve_env_var(value)

    logger.info(f"Loaded routing config from {config_path}")
    return config


def load_routing_config_with_local(
    config_path: str = "config/ai_providers.yaml",
    local_config_path: str = "config/ai_providers.local.yaml"
) -> Dict[str, Any]:
    """Load provider routing config and merge with local overrides.

    This loads the main configuration and merges it with local configuration
    for development/testing. Local config overrides main config values.

    Args:
        config_path: Path to the main configuration file
        local_config_path: Path to the local override file

    Returns:
        Dict: Merged configuration with environment variables resolved

    Raises:
        FileNotFoundError: If main config file doesn't exist
        ValueError: If configuration is invalid
    """
    # Load main config
    config = load_routing_config(config_path)

    # Try to load local config (optional)
    local_path = Path(local_config_path)
    if local_path.exists():
        logger.info(f"Loading local config from {local_config_path}")

        with open(local_path, 'r', encoding='utf-8') as f:
            local_config = yaml.safe_load(f)

        if local_config:
            # Merge providers - local config adds/overrides providers
            if "providers" in local_config:
                if "providers" not in config:
                    config["providers"] = {}
                config["providers"].update(local_config["providers"])

            # Merge routing - local config overrides routing
            if "routing" in local_config:
                if "routing" not in config:
                    config["routing"] = {}
                config["routing"].update(local_config["routing"])

            # Merge other sections
            for key in ["defaults", "mock"]:
                if key in local_config:
                    config[key] = {**config.get(key, {}), **local_config[key]}

            logger.info(f"Merged local config: {list(local_config.keys())}")

    return config


def get_provider_routing(
    capability: str,
    config: Dict[str, Any],
    default_provider: Optional[str] = None,
) -> str:
    """Get the provider ID for a given capability.

    Args:
        capability: The capability name
        config: The routing configuration
        default_provider: Default provider if capability not found

    Returns:
        str: Provider ID for the capability

    Raises:
        ValueError: If no provider found and no default given
    """
    routing = config.get("routing", {})
    provider_id = routing.get(capability)

    if not provider_id:
        defaults = config.get("defaults", {})
        provider_id = defaults.get("default_provider", default_provider)

    if not provider_id:
        raise ValueError(
            f"No provider configured for capability: {capability}. "
            f"Available capabilities: {list(routing.keys())}"
        )

    return provider_id


def get_provider_config(
    provider_id: str,
    config: Dict[str, Any],
) -> Dict[str, Any]:
    """Get the configuration for a specific provider.

    Args:
        provider_id: Provider identifier
        config: The routing configuration

    Returns:
        Dict: Provider configuration (the 'config' sub-dict)

    Raises:
        ValueError: If provider not found
    """
    providers = config.get("providers", {})
    if provider_id not in providers:
        raise ValueError(
            f"Provider not found: {provider_id}. "
            f"Available providers: {list(providers.keys())}"
        )

    provider_entry = providers[provider_id]
    if "config" not in provider_entry:
        raise ValueError(
            f"Provider '{provider_id}' missing 'config' section"
        )

    return provider_entry["config"]


def validate_config(config: Dict[str, Any]) -> bool:
    """Validate the routing configuration.

    Args:
        config: Configuration to validate

    Returns:
        bool: True if valid

    Raises:
        ValueError: If configuration is invalid
    """
    # Check for required sections
    if "providers" not in config:
        raise ValueError("Missing 'providers' section in configuration")

    if "routing" not in config:
        raise ValueError("Missing 'routing' section in configuration")

    # Validate providers
    for provider_id, provider_config in config["providers"].items():
        if "class" not in provider_config:
            raise ValueError(f"Provider '{provider_id}' missing 'class' field")

        if "config" not in provider_config:
            raise ValueError(f"Provider '{provider_id}' missing 'config' field")

        provider_cfg = provider_config["config"]
        required_keys = ["api_key", "model", "base_url"]
        for key in required_keys:
            if key not in provider_cfg:
                raise ValueError(
                    f"Provider '{provider_id}' config missing required key: {key}"
                )

    # Validate routing references exist in providers
    provider_ids = set(config["providers"].keys())
    for capability, provider_id in config["routing"].items():
        if provider_id not in provider_ids:
            raise ValueError(
                f"Routing for '{capability}' references unknown provider: {provider_id}"
            )

    return True


class AIProviderConfig:
    """Configuration for an AI provider.

    Attributes:
        api_key: API key for authentication
        model: Model name/identifier
        base_url: Base URL for API endpoints
        max_concurrent_requests: Maximum concurrent requests
        request_timeout: Request timeout in seconds
    """

    def __init__(
        self,
        api_key: str,
        model: str,
        base_url: str,
        max_concurrent_requests: int = 4,
        request_timeout: float = 30.0,
    ):
        """Initialize the provider configuration.

        Args:
            api_key: API key for authentication
            model: Model name/identifier
            base_url: Base URL for API endpoints
            max_concurrent_requests: Maximum concurrent requests
            request_timeout: Request timeout in seconds
        """
        self.api_key = api_key
        self.model = model
        self.base_url = base_url
        self.max_concurrent_requests = max_concurrent_requests
        self.request_timeout = request_timeout

        self._validate()

    def _validate(self) -> None:
        """Validate configuration."""
        if not self.api_key:
            raise ValueError("api_key is required")
        if not self.model:
            raise ValueError("model is required")
        if not self.base_url:
            raise ValueError("base_url is required")
        if self.max_concurrent_requests <= 0:
            raise ValueError("max_concurrent_requests must be positive")
        if self.request_timeout <= 0:
            raise ValueError("request_timeout must be positive")

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "AIProviderConfig":
        """Create configuration from dictionary.

        Args:
            data: Configuration dictionary

        Returns:
            AIProviderConfig: Configuration instance
        """
        return cls(
            api_key=data["api_key"],
            model=data["model"],
            base_url=data["base_url"],
            max_concurrent_requests=data.get("max_concurrent_requests", 4),
            request_timeout=data.get("request_timeout", 30.0),
        )

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary.

        Returns:
            Dict: Configuration as dictionary
        """
        return {
            "api_key": self.api_key,
            "model": self.model,
            "base_url": self.base_url,
            "max_concurrent_requests": self.max_concurrent_requests,
            "request_timeout": self.request_timeout,
        }
