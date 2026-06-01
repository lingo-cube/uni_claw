"""Configuration loader for AI provider from environment variables."""

import logging
import os
from typing import Optional

from .core.config import AIProviderConfig, RetryConfig, FallbackConfig
from .vision.config import VisionConfig

logger = logging.getLogger(__name__)


def load_ai_config() -> AIProviderConfig:
    """Load AI provider configuration from environment variables.

    Environment Variables:
        DEEPSEEK_API_KEY: API key for DeepSeek (required)
        DEEPSEEK_MODEL: Model name (default: deepseek-v4-flash)
        DEEPSEEK_BASE_URL: API base URL (default: https://api.deepseek.com/v1)
        AI_PROVIDER_MAX_CONCURRENT: Max concurrent requests (default: 4)
        AI_PROVIDER_TIMEOUT: Request timeout in seconds (default: 30.0)
        AI_PROVIDER_REASONING_LEVEL: Reasoning detail (default: detailed)
        AI_RETRY_MAX_ATTEMPTS: Max retry attempts (default: 1)
        AI_RETRY_BASE_DELAY: Base retry delay in seconds (default: 1.0)
        AI_RETRY_MAX_DELAY: Max retry delay in seconds (default: 8.0)
        AI_FALLBACK_STRATEGY: Fallback strategy (default: partial)
        AI_ENABLE_VALIDATION: Enable internal validation (default: true)

    Returns:
        AIProviderConfig with values from environment

    Raises:
        ValueError: If required configuration is missing
    """
    api_key = os.getenv("DEEPSEEK_API_KEY")
    if not api_key:
        raise ValueError("DEEPSEEK_API_KEY environment variable is required")

    return AIProviderConfig(
        api_key=api_key,
        model=os.getenv("DEEPSEEK_MODEL", "deepseek-v4-flash"),
        base_url=os.getenv("DEEPSEEK_BASE_URL", "https://api.deepseek.com/v1"),
        max_concurrent_requests=int(os.getenv("AI_PROVIDER_MAX_CONCURRENT", "4")),
        request_timeout=float(os.getenv("AI_PROVIDER_TIMEOUT", "30.0")),
        reasoning_detail=os.getenv("AI_PROVIDER_REASONING_LEVEL", "detailed"),
        retry=RetryConfig(
            max_attempts=int(os.getenv("AI_RETRY_MAX_ATTEMPTS", "1")),
            base_delay=float(os.getenv("AI_RETRY_BASE_DELAY", "1.0")),
            max_delay=float(os.getenv("AI_RETRY_MAX_DELAY", "8.0")),
            exponential_base=float(os.getenv("AI_RETRY_EXPONENTIAL_BASE", "2.0")),
        ),
        fallback=FallbackConfig(
            strategy=os.getenv("AI_FALLBACK_STRATEGY", "partial"),
            partial_allowlist=os.getenv("AI_FALLBACK_ALLOWLIST", "").split(",") if os.getenv("AI_FALLBACK_ALLOWLIST") else [],
        ),
        enable_internal_validation=os.getenv("AI_ENABLE_VALIDATION", "true").lower() == "true",
    )


def load_vision_config() -> VisionConfig:
    """Load vision service configuration from environment variables.

    Environment Variables:
        VISION_SERVICE_TYPE: Service type (claude/mimo/mock, default: mock)
        VISION_API_KEY: API key for vision service
        VISION_MODEL: Model name for Claude (default: claude-3-5-sonnet-20241022)
        VISION_TIMEOUT: Request timeout in seconds (default: 30.0)
        VISION_MAX_RETRIES: Max retry attempts (default: 3)

    Returns:
        VisionConfig with values from environment
    """
    return VisionConfig(
        service_type=os.getenv("VISION_SERVICE_TYPE", "mock"),
        api_key=os.getenv("VISION_API_KEY", ""),
        model=os.getenv("VISION_MODEL", "claude-3-5-sonnet-20241022"),
        timeout=float(os.getenv("VISION_TIMEOUT", "30.0")),
        max_retries=int(os.getenv("VISION_MAX_RETRIES", "3")),
    )


def validate_config(config: AIProviderConfig) -> bool:
    """Validate AI provider configuration.

    Args:
        config: Configuration to validate

    Returns:
        True if valid

    Raises:
        ValueError: If configuration is invalid
    """
    if not config.api_key:
        raise ValueError("API key is required")

    if config.max_concurrent_requests < 1:
        raise ValueError("max_concurrent_requests must be at least 1")

    if config.request_timeout < 0:
        raise ValueError("request_timeout must be positive")

    if config.reasoning_detail not in ("concise", "step_by_step", "detailed"):
        raise ValueError(f"Invalid reasoning_detail: {config.reasoning_detail}")

    if config.retry.max_attempts < 1:
        raise ValueError("max_attempts must be at least 1")

    if config.retry.base_delay < 0 or config.retry.max_delay < config.retry.base_delay:
        raise ValueError("Invalid retry delay configuration")

    if config.fallback.strategy not in ("none", "partial", "full"):
        raise ValueError(f"Invalid fallback strategy: {config.fallback.strategy}")

    return True


__all__ = ["load_ai_config", "load_vision_config", "validate_config"]
