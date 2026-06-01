"""AI Provider configuration."""

from dataclasses import dataclass, field
from typing import List, Literal


@dataclass
class RetryConfig:
    """Retry configuration for API calls."""

    max_attempts: int = 1  # Maximum attempts (1 = no retry)
    base_delay: float = 1.0  # Base delay in seconds
    max_delay: float = 8.0  # Maximum delay in seconds
    exponential_base: float = 2.0  # Exponential backoff base


@dataclass
class FallbackConfig:
    """Fallback configuration for capability failures."""

    strategy: Literal["none", "partial", "full"] = "partial"
    partial_allowlist: List[str] = field(default_factory=list)


@dataclass
class AIProviderConfig:
    """Configuration for AI Provider.

    This config controls:
    - API access (key, model, base URL)
    - Concurrency and timeout settings
    - Output preferences (reasoning detail)
    - Retry and fallback behavior
    - Validation settings
    """

    # API configuration
    api_key: str
    model: str = "deepseek-v4-flash"
    base_url: str = "https://api.deepseek.com/v1"

    # Concurrency control
    max_concurrent_requests: int = 4
    request_timeout: float = 30.0

    # Output configuration
    reasoning_detail: Literal["concise", "step_by_step", "detailed"] = "detailed"

    # Retry and fallback
    retry: RetryConfig = field(default_factory=RetryConfig)
    fallback: FallbackConfig = field(default_factory=FallbackConfig)

    # Validation
    enable_internal_validation: bool = True


__all__ = ["AIProviderConfig", "RetryConfig", "FallbackConfig"]
