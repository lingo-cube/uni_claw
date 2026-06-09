"""
Scroll simulation module for V7.0 SimScroll feature.

This module provides mock services for simulating scrollable lists in tests:
- Scroll data models (ScrollSegment, ScrollState, ScrollAction, ScrollPage)
- ScrollableMockVisionService: Mock vision service with scroll simulation
- ScrollableMockActionExecutor: Mock action executor with scroll actions
- ScrollDataStore: Data store for scroll segment management
"""

from .models import ScrollAction, ScrollPage, ScrollSegment, ScrollState
from .scroll_data_store import ScrollDataStore
from .scrollable_mock_vision import ScrollableMockVisionService
from .scrollable_mock_action import ScrollableMockActionExecutor

__all__ = [
    "ScrollSegment",
    "ScrollState",
    "ScrollAction",
    "ScrollPage",
    "ScrollDataStore",
    "ScrollableMockVisionService",
    "ScrollableMockActionExecutor",
]
