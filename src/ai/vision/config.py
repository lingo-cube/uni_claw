"""Vision service configuration and factory."""

from dataclasses import dataclass
from typing import Literal

from .service import VisionService
from .claude_service import ClaudeVisionService
from .mock_service import MockVisionService


@dataclass
class VisionConfig:
    """Configuration for vision service."""

    service_type: Literal["claude", "mimo", "mock"] = "claude"
    api_key: str = ""
    model: str = "claude-3-5-sonnet-20241022"
    timeout: float = 30.0
    max_retries: int = 3


def create_vision_service(config: VisionConfig) -> VisionService:
    """Factory function to create a vision service.

    Args:
        config: Vision service configuration

    Returns:
        Configured vision service instance

    Raises:
        ValueError: If service_type is unknown
    """
    if config.service_type == "claude":
        return ClaudeVisionService(
            api_key=config.api_key,
            model=config.model,
        )
    elif config.service_type == "mimo":
        # MiMo service would be implemented when API details are available
        raise NotImplementedError("MiMo service not yet implemented")
    elif config.service_type == "mock":
        return MockVisionService()
    else:
        raise ValueError(f"Unknown service type: {config.service_type}")


__all__ = ["VisionConfig", "create_vision_service"]
