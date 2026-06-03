"""Uni-Claw AI Module - Refactored Architecture.

This module provides the AI capabilities for the uni-claw framework using:
- Provider abstraction layer (providers/)
- Prompt management (prompts/)
- Trace integration (trace/)
- Declarative configuration (config/ai_providers.yaml)
"""

# Core interfaces
from .advisor import AIStrategyAdvisor
from .noop_advisor import NoOpAIAdvisor
from .mock_advisor import MockAIAdvisor
from .ai_types import DecisionResult, ContainerInference

# UniBrain - Main AI provider
from .provider import UniBrain, UniBrainConfig

# New architecture components
try:
    from .providers import (
        AIProvider,
        AIResponse,
        AIProviderConfig,
        create_provider,
        DeepSeekProvider,
        ClaudeProvider,
        MiMoProvider,
    )
    _PROVIDERS_AVAILABLE = True
except ImportError:
    _PROVIDERS_AVAILABLE = False

try:
    from .prompts import PromptManager, PromptTemplate
    _PROMPTS_AVAILABLE = True
except ImportError:
    _PROMPTS_AVAILABLE = False

try:
    from .trace import TraceIntegration, SpanContext
    _TRACE_AVAILABLE = True
except ImportError:
    _TRACE_AVAILABLE = False

__all__ = [
    # Interfaces
    "AIStrategyAdvisor",
    "NoOpAIAdvisor",
    "MockAIAdvisor",
    "DecisionResult",
    "ContainerInference",
    # UniBrain
    "UniBrain",
    "UniBrainConfig",
    # Providers (new architecture)
    "AIProvider",
    "AIResponse",
    "AIProviderConfig",
    "create_provider",
    "DeepSeekProvider",
    "ClaudeProvider",
    "MiMoProvider",
    # Prompts (new architecture)
    "PromptManager",
    "PromptTemplate",
    # Trace (new architecture)
    "TraceIntegration",
    "SpanContext",
]
