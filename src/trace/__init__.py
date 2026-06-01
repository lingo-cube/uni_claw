"""
Trace System for uni-claw V4.0

This module provides the trace recording and playback system:
- TraversalTrace: Complete trace data structure
- TraceStep: Individual step records
- StateSnapshot: Periodic state snapshots
- TraceRecorder: Recording engine
- ReplayEngine: Playback engine
"""

from .models import (
    TraversalTrace,
    TraceStep,
    StateSnapshot,
    TraceSummary,
)
from .recorder import TraceRecorder, TraceConfig
from .replay import (
    ReplayEngine,
    ReplayMode,
    ReplayResult,
)

__all__ = [
    # Data models
    "TraversalTrace",
    "TraceStep",
    "StateSnapshot",
    "TraceSummary",
    # Recorder
    "TraceRecorder",
    "TraceConfig",
    # Replay
    "ReplayEngine",
    "ReplayMode",
    "ReplayResult",
]
