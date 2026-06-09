"""
State persistence and legacy models (V6.12 DEPRECATED).

This module contains legacy state management code that has been
superseded by the V6 architecture:

- TraversalRuntimeContext (src/trace/context.py) for runtime state
- Trace recording for persistence
- Graph-based traversal for content tree

CURRENT USAGE:
- Simulation mocks use PageAnalysis, Coordinate, MenuInfo, etc.
- Some legacy tests use TraversalState (old persistence model)

FUTURE:
- Consider migrating PageAnalysis to src.models/ for simulation
- Deprecate StateManager (no longer used in V6)
- Keep data models until simulation is refactored

FIXME: This module is legacy. Do not use for new code.
"""

from .content_tree import (
    ContentNode,
    ContentTree,
    Coordinate,
    Direction,
    MenuInfo,
    MenuItem,
    MenuItemType,
    PageAnalysis,
    PopupInfo,
    TraversalState,  # Legacy: DO NOT USE in V6+
    VisitFingerprint,
)
from .state_manager import StateManager  # Legacy: DO NOT USE in V6+

__all__ = [
    "Coordinate",
    "Direction",
    "MenuInfo",
    "MenuItem",
    "MenuItemType",
    "PageAnalysis",
    "PopupInfo",
    "ContentNode",
    "ContentTree",
    "TraversalState",
    "VisitFingerprint",
    "StateManager",
]
