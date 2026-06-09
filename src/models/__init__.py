"""Core data models for uni-claw framework."""

from .traversal_context import (
    TraversalContext,
    ErrorRecord,
    ActionRecord,
    PageCacheInfo,
    GlobalState,
)

# Content models (V6.13) - migrated from src.state.content_tree
from .content_models import (
    Coordinate,
    Direction,
    MenuInfo,
    MenuItem,
    MenuItemType,
    ExpectedAction,
    PageAnalysis,
    PopupInfo,
    ContentTree,
    ContentNode,
    VisitFingerprint,
    SimulationState,
)

# Backward compatibility alias
# Allows old code to use TraversalState name during migration period
TraversalState = SimulationState

__all__ = [
    # Traversal context
    "TraversalContext",
    "ErrorRecord",
    "ActionRecord",
    "PageCacheInfo",
    "GlobalState",
    # Content models
    "Coordinate",
    "Direction",
    "MenuInfo",
    "MenuItem",
    "MenuItemType",
    "ExpectedAction",
    "PageAnalysis",
    "PopupInfo",
    "ContentTree",
    "ContentNode",
    "VisitFingerprint",
    "SimulationState",
    "TraversalState",  # Backward compatibility alias
]
