"""Provider abstraction layer for AI services.

This module provides the base abstraction for all AI providers,
enabling unified access to different AI services (DeepSeek, Claude, MiMo, etc.).
"""

from abc import ABC, abstractmethod
from typing import Dict, List, Optional, Any
from dataclasses import dataclass
import asyncio


@dataclass
class AIResponse:
    """Unified AI response format across all providers.

    Attributes:
        content: The response content (text)
        provider_id: Identifier of the provider that generated the response
        mode: The mode used (text, vision, multimodal)
        input_tokens: Number of input tokens consumed
        output_tokens: Number of output tokens generated
        latency_ms: Request latency in milliseconds
        model: Model name used for the request
        success: Whether the request was successful
        error_message: Error message if success is False
    """

    content: str
    provider_id: str
    mode: str
    input_tokens: int
    output_tokens: int
    latency_ms: float
    model: str = ""
    success: bool = True
    error_message: Optional[str] = None

    @property
    def total_tokens(self) -> int:
        """Total tokens consumed."""
        return self.input_tokens + self.output_tokens

    @property
    def estimated_cost(self) -> float:
        """Estimated cost in USD (to be implemented by subclasses)."""
        return 0.0

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization."""
        return {
            "content": self.content,
            "provider_id": self.provider_id,
            "mode": self.mode,
            "input_tokens": self.input_tokens,
            "output_tokens": self.output_tokens,
            "total_tokens": self.total_tokens,
            "latency_ms": self.latency_ms,
            "model": self.model,
            "success": self.success,
            "error_message": self.error_message,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "AIResponse":
        """Create from dictionary."""
        return cls(
            content=data["content"],
            provider_id=data["provider_id"],
            mode=data["mode"],
            input_tokens=data["input_tokens"],
            output_tokens=data["output_tokens"],
            latency_ms=data["latency_ms"],
            model=data.get("model", ""),
            success=data.get("success", True),
            error_message=data.get("error_message"),
        )


@dataclass
class AIProviderConfig:
    """Configuration for AI providers.

    Attributes:
        api_key: API key for authentication
        model: Model name/identifier
        base_url: Base URL for API endpoints
        max_concurrent_requests: Maximum concurrent requests
        request_timeout: Request timeout in seconds
        retry_config: Retry configuration
    """

    api_key: str
    model: str
    base_url: str
    max_concurrent_requests: int = 4
    request_timeout: float = 30.0

    def __post_init__(self):
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


class AIProvider(ABC):
    """Abstract base class for AI providers.

    All AI providers must implement this interface to ensure
    consistent behavior across different AI services.
    """

    def __init__(self, config: AIProviderConfig):
        """Initialize the provider.

        Args:
            config: Provider configuration
        """
        self.config = config
        self._semaphore = asyncio.Semaphore(config.max_concurrent_requests)
        self._client = None

    @property
    @abstractmethod
    def provider_id(self) -> str:
        """Unique identifier for this provider.

        Returns:
            str: Provider identifier (e.g., "deepseek", "claude", "mimo")
        """
        pass

    @property
    @abstractmethod
    def supported_modes(self) -> List[str]:
        """List of supported modes.

        Returns:
            List[str]: Supported modes from ["text", "vision", "multimodal"]
        """
        pass

    @abstractmethod
    async def complete_text(
        self,
        prompt: str,
        schema: Optional[Dict] = None,
        max_tokens: int = 2048,
        **kwargs
    ) -> AIResponse:
        """Complete a text prompt.

        Args:
            prompt: Text prompt to complete
            schema: Optional JSON schema for structured output
            max_tokens: Maximum output tokens
            **kwargs: Additional provider-specific parameters

        Returns:
            AIResponse: The completion response

        Raises:
            RuntimeError: If the API call fails
            ValueError: If parameters are invalid
        """
        pass

    @abstractmethod
    async def complete_vision(
        self,
        prompt: str,
        image_data: bytes,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
        **kwargs
    ) -> AIResponse:
        """Complete a vision prompt (image + text).

        Args:
            prompt: Text prompt
            image_data: PNG format image data
            schema: Optional JSON schema for structured output
            max_tokens: Maximum output tokens
            **kwargs: Additional provider-specific parameters

        Returns:
            AIResponse: The completion response

        Raises:
            RuntimeError: If the API call fails
            ValueError: If parameters are invalid or image format is wrong
            NotImplementedError: If provider doesn't support vision
        """
        pass

    @abstractmethod
    async def complete_multimodal(
        self,
        prompt: str,
        image_data: bytes,
        additional_context: Optional[Dict] = None,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
        **kwargs
    ) -> AIResponse:
        """Complete a multimodal prompt (image + text + context).

        Args:
            prompt: Text prompt
            image_data: PNG format image data
            additional_context: Additional context information
            schema: Optional JSON schema for structured output
            max_tokens: Maximum output tokens
            **kwargs: Additional provider-specific parameters

        Returns:
            AIResponse: The completion response

        Raises:
            RuntimeError: If the API call fails
            ValueError: If parameters are invalid
            NotImplementedError: If provider doesn't support multimodal
        """
        pass

    def get_token_estimate(
        self, mode: str, avg_request_tokens: int = 500
    ) -> Dict[str, int]:
        """Estimate token usage for a request.

        Args:
            mode: Call mode (text, vision, multimodal)
            avg_request_tokens: Average request token count

        Returns:
            Dict: Token estimates {"input": int, "output": int, "total": int}
        """
        # Default implementation - subclasses can override for better estimates
        if mode in ("vision", "multimodal"):
            # Vision tasks typically require more input tokens
            return {
                "input": avg_request_tokens * 2,
                "output": avg_request_tokens,
                "total": avg_request_tokens * 3,
            }
        return {
            "input": avg_request_tokens,
            "output": avg_request_tokens // 2,
            "total": avg_request_tokens + avg_request_tokens // 2,
        }

    def get_performance_rating(self, mode: str) -> Dict[str, float]:
        """Get performance rating for this provider.

        Ratings are normalized to 0-1 scale where:
        - latency: 1.0 = fastest, 0.0 = slowest
        - quality: 1.0 = highest quality, 0.0 = lowest
        - efficiency: 1.0 = most token-efficient, 0.0 = least efficient

        Args:
            mode: Call mode

        Returns:
            Dict: Performance ratings {"latency": float, "quality": float, "efficiency": float}
        """
        # Default implementation - subclasses should override with actual ratings
        return {"latency": 0.5, "quality": 0.5, "efficiency": 0.5}

    async def health_check(self) -> bool:
        """Check if the provider is healthy and accessible.

        Returns:
            bool: True if healthy, False otherwise
        """
        try:
            # Simple health check with minimal prompt
            response = await self.complete_text("ping", max_tokens=5)
            return response.success and bool(response.content)
        except Exception:
            return False

    def _check_mode_supported(self, mode: str) -> None:
        """Check if a mode is supported.

        Args:
            mode: Mode to check

        Raises:
            NotImplementedError: If mode is not supported
        """
        if mode not in self.supported_modes:
            raise NotImplementedError(
                f"{self.provider_id} does not support {mode} mode. "
                f"Supported modes: {self.supported_modes}"
            )

    async def _execute_with_semaphore(self, coro):
        """Execute a coroutine with concurrency limiting.

        Args:
            coro: Coroutine to execute

        Returns:
            The coroutine result
        """
        async with self._semaphore:
            return await coro


def create_provider(provider_type: str, config: AIProviderConfig) -> AIProvider:
    """Factory function to create provider instances.

    Args:
        provider_type: Type of provider ("deepseek", "claude", "mimo", "mcp")
        config: Provider configuration

    Returns:
        AIProvider: Provider instance

    Raises:
        ValueError: If provider_type is unknown
    """
    # Import here to avoid circular dependencies
    from .deepseek import DeepSeekProvider
    from .claude import ClaudeProvider
    from .mimo import MiMoProvider
    from .mcp import MCPProvider

    providers = {
        "deepseek": DeepSeekProvider,
        "claude": ClaudeProvider,
        "mimo": MiMoProvider,
        "mcp": MCPProvider,
    }

    provider_class = providers.get(provider_type.lower())
    if not provider_class:
        raise ValueError(
            f"Unknown provider type: {provider_type}. "
            f"Available: {list(providers.keys())}"
        )

    return provider_class(config)
