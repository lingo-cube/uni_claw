"""Configuration management using Pydantic settings."""

import os
from functools import lru_cache
from pathlib import Path
from typing import Literal

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class VisionServiceConfig(BaseSettings):
    """Vision service configuration for the two-step pipeline."""

    model_config = SettingsConfigDict(
        env_prefix="VISION",
        case_sensitive=False,
        extra="ignore",
    )

    # Service mode
    mode: Literal["legacy", "flattened", "dual"] = Field(
        default="flattened",
        description="Vision service mode: legacy, flattened, or dual",
    )

    # Multimodal model configuration
    multimodal_model: str = Field(
        default="claude-3-5-sonnet-20241022",
        description="Multimodal model for visual perception",
    )
    multimodal_max_tokens: int = Field(
        default=4096,
        description="Max tokens for multimodal model output",
    )

    # Text model configuration
    text_model: str = Field(
        default="deepseek-v4-flash",
        description="Text model for logical assembly",
    )
    text_max_tokens: int = Field(
        default=2048,
        description="Max tokens for text model output",
    )

    # Cache configuration
    enable_cache: bool = Field(
        default=True,
        description="Enable caching for vision analysis results",
    )
    screen_cache_ttl: int = Field(
        default=300,
        description="Screen cache TTL in seconds",
    )
    page_analysis_cache_ttl: int = Field(
        default=600,
        description="Page analysis cache TTL in seconds",
    )
    cache_max_size: int = Field(
        default=1000,
        description="Maximum cache size",
    )

    # Fallback configuration
    enable_fallback: bool = Field(
        default=True,
        description="Enable fallback to legacy service on error",
    )
    fallback_on_error: bool = Field(
        default=True,
        description="Fallback to legacy service on any error",
    )
    fallback_timeout_ms: float = Field(
        default=5000,
        description="Timeout before triggering fallback (ms)",
    )

    # Performance monitoring
    enable_metrics: bool = Field(
        default=True,
        description="Enable metrics collection",
    )
    metrics_sample_rate: float = Field(
        default=0.1,
        description="Metrics sampling rate (0.0-1.0)",
    )


class Settings(BaseSettings):
    """Application settings with environment variable support."""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
        extra="ignore",
    )

    # API Keys
    anthropic_api_key: str = Field(
        default="",
        description="Anthropic API key for Claude vision service",
    )
    deepseek_api_key: str = Field(
        default="",
        description="DeepSeek API key for LLM capabilities",
    )
    mimo_api_key: str = Field(
        default="",
        description="XiaoMi MiMo API key for vision service",
    )

    # Vision Model Configuration
    vision_provider: Literal["anthropic", "mimo", "mimo-cc", "mock"] = Field(
        default="mimo-cc",
        description="Vision service provider to use",
    )
    vision_model: str = Field(
        default="claude-3-5-sonnet-20241022",
        description="Vision model to use for screen analysis",
    )
    # MiMo Configuration (OpenAI v1 protocol)
    mimo_model: str = Field(
        default="mimo-v2.5",
        description="MiMo model version",
    )
    mimo_base_url: str = Field(
        default="https://api.xiaomimimo.com/v1",
        description="MiMo API base URL (OpenAI v1 protocol)",
    )
    # MiMo CC Configuration (Anthropic protocol)
    mimo_cc_model: str = Field(
        default="mimo-v2.5",
        description="MiMo CC model version",
    )
    mimo_cc_base_url: str = Field(
        default="https://token-plan-cn.xiaomimimo.com/anthropic",
        description="MiMo CC API base URL (Anthropic protocol)",
    )

    # Two-step pipeline configuration (PRD V5.2)
    vision: VisionServiceConfig = Field(
        default_factory=VisionServiceConfig,
        description="Two-step pipeline vision service configuration",
    )

    # ADB Configuration
    adb_device_id: str = Field(
        default="",
        description="ADB device ID (empty for auto-detect)",
    )
    adb_path: str = Field(
        default="adb",
        description="Path to adb executable",
    )

    # Traversal Settings
    max_steps: int = Field(
        default=200,
        description="Maximum traversal steps",
    )
    wait_time: float = Field(
        default=0.5,
        description="Wait time after click in seconds",
    )
    max_retries: int = Field(
        default=2,
        description="Maximum retries for failed operations",
    )
    timeout: int = Field(
        default=30,
        description="Timeout for operations in seconds",
    )

    # State Persistence
    state_file: str = Field(
        default=".traversal_state.json",
        description="Path to state persistence file",
    )

    # Logging
    log_level: Literal["DEBUG", "INFO", "WARNING", "ERROR"] = Field(
        default="INFO",
        description="Logging level",
    )

    # Project Paths
    @property
    def project_root(self) -> Path:
        """Get project root directory."""
        # Use __file__ to get the location of this settings file
        # and navigate to project root (src/../)
        return Path(__file__).parent.parent.parent

    @property
    def screenshots_dir(self) -> Path:
        """Get screenshots directory."""
        return self.project_root / "screenshots"


@lru_cache
def get_settings() -> Settings:
    """Get cached settings instance."""
    return Settings()
