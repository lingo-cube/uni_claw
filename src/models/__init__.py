"""Core data models for uni-claw framework."""

from .traversal_context import (
    TraversalContext,
    ErrorRecord,
    ActionRecord,
    PageCacheInfo,
    GlobalState,
)

__all__ = [
    "TraversalContext",
    "ErrorRecord",
    "ActionRecord",
    "PageCacheInfo",
    "GlobalState",
]
