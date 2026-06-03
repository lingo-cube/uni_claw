"""Prompt management system for AI capabilities.

This module provides centralized prompt template management with:
- Variable injection
- Version control
- Hot reload capability
"""

from .manager import PromptManager, PromptTemplate

__all__ = [
    "PromptManager",
    "PromptTemplate",
]
