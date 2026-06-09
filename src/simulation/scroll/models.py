"""
Scroll simulation data models for V7.0 SimScroll feature.

Provides dataclasses for scroll segments, state tracking, action history,
and page aggregation. Supports accumulation mode element visibility,
fault injection, and scroll progress tracking.
"""

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional


@dataclass
class ScrollSegment:
    """
    Represents a scroll segment with threshold and visible elements.

    Attributes:
        threshold: Scroll progress threshold (0.0-1.0) at which elements become visible
        elements: List of element dictionaries containing element data (id, name, coordinate, etc.)
    """

    threshold: float
    elements: List[Dict[str, Any]] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        """Convert ScrollSegment to dictionary for serialization."""
        return {
            "threshold": self.threshold,
            "elements": self.elements,
        }


@dataclass
class ScrollState:
    """
    Tracks scroll state for a single page including progress, history, and fault injection flags.

    Attributes:
        current_progress: Current scroll progress from 0.0 (top) to 1.0 (bottom)
        last_scroll_time: Timestamp of last scroll operation (None if no scroll yet)
        scroll_count: Number of scroll operations performed
        scroll_history: List of progress values after each scroll operation
        fail_next_scroll: If True, next scroll operation will not update progress (fault injection)
        simulate_delay_ms: Artificial delay in milliseconds to inject during scroll operations
        # Reserved for V7.x:
        # simulate_jumps: If True, enable jump simulation (V7.x feature)
        # jump_delta_multiplier: Multiplier for jump delta calculation (V7.x feature)
    """

    current_progress: float = 0.0
    last_scroll_time: Optional[float] = None
    scroll_count: int = 0
    scroll_history: List[float] = field(default_factory=list)
    fail_next_scroll: bool = False
    simulate_delay_ms: int = 0
    # Reserved for V7.x - TODO: Implement jump simulation in V7.x
    # simulate_jumps: bool = False
    # jump_delta_multiplier: float = 1.5

    def to_dict(self) -> Dict[str, Any]:
        """Convert ScrollState to dictionary for serialization."""
        return {
            "current_progress": self.current_progress,
            "last_scroll_time": self.last_scroll_time,
            "scroll_count": self.scroll_count,
            "scroll_history": self.scroll_history,
            "fail_next_scroll": self.fail_next_scroll,
            "simulate_delay_ms": self.simulate_delay_ms,
        }


@dataclass
class ScrollAction:
    """
    Records metadata for a single scroll operation.

    Attributes:
        action: Action type ("DOWN" or "UP")
        path: Page path where scroll was performed
        step_percent: Scroll step size as percentage (0.0-1.0)
        before_progress: Scroll progress before the operation
        after_progress: Scroll progress after the operation
        timestamp: Timestamp when the scroll was performed
    """

    action: str
    path: str
    step_percent: float
    before_progress: float
    after_progress: float
    timestamp: float

    def to_dict(self) -> Dict[str, Any]:
        """Convert ScrollAction to dictionary for serialization."""
        return {
            "action": self.action,
            "path": self.path,
            "step_percent": self.step_percent,
            "before_progress": self.before_progress,
            "after_progress": self.after_progress,
            "timestamp": self.timestamp,
        }


@dataclass
class ScrollPage:
    """
    Aggregates scroll segments for a single page.

    Attributes:
        path: Page identifier/path key
        has_scroll: Whether this page has scrollable content
        scroll_segments: List of ScrollSegment objects defining element visibility at different thresholds
    """

    path: str
    has_scroll: bool
    scroll_segments: List[ScrollSegment] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        """Convert ScrollPage to dictionary for serialization."""
        return {
            "path": self.path,
            "has_scroll": self.has_scroll,
            "scroll_segments": [seg.to_dict() for seg in self.scroll_segments],
        }
