"""
Trace System for uni-claw V6.3

Distributed tracing with standard terminology (Trace ID, Span ID, Parent Span ID),
pluggable storage backends, and comprehensive trace analysis.

Modules:
- models: TraceNode, SessionNode, StepNode, SpanNode
- storage: TraceStorage, FileStorage, MemoryStorage
- recorder: TraceRecorder, StepTracker
- analyzer: TraceAnalyzer, build_tree
- context: Session, TraversalRuntimeContext
- recovery: ContextRebuilder, RecoveryStrategy
"""

from .models import (
    TraceNode,
    SessionNode,
    StepNode,
    SpanNode,
    generate_id,
)
from .storage import (
    TraceStorage,
    FileStorage,
    MemoryStorage,
)
from .recorder import (
    TraceRecorder,
    StepTracker,
)
from .analyzer import (
    TraceAnalyzer,
    build_tree,
)
from .context import (
    Session,
    StackFrame,
    TraversalRuntimeContext,
)
from .recovery import (
    ContextRebuilder,
    RecoveryStrategy,
)
from .metrics import (
    AICallMetrics,
    ExecutionMetrics,
    ErrorMetrics,
)

__all__ = [
    # Models
    "TraceNode",
    "SessionNode",
    "StepNode",
    "SpanNode",
    "generate_id",
    # Storage
    "TraceStorage",
    "FileStorage",
    "MemoryStorage",
    # Recorder
    "TraceRecorder",
    "StepTracker",
    # Analyzer
    "TraceAnalyzer",
    "build_tree",
    # Context
    "Session",
    "StackFrame",
    "TraversalRuntimeContext",
    # Recovery
    "ContextRebuilder",
    "RecoveryStrategy",
    # Metrics
    "AICallMetrics",
    "ExecutionMetrics",
    "ErrorMetrics",
]
