"""
Context models and Session for V6.3 trace integration.

Provides:
- Session: session metadata model with trace_id
- StackFrame: a single entry in the node stack
- TraversalRuntimeContext: mutable runtime context for the engine

Note: The frozen TraversalContext used by AI advisors lives in
src.models.traversal_context and remains unchanged.
"""

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, Set

from .models import generate_id


# ── Session ──────────────────────────────────────────────────────────────────


@dataclass
class Session:
    """Session metadata for a traversal run.

    The session_id doubles as the global trace_id. Stored independently
    at traces/{trace_id}/session.json.
    """

    session_id: str = field(default_factory=generate_id)
    device_id: Optional[str] = None
    device_name: Optional[str] = None
    device_model: str = ""
    os_version: str = ""
    app_version: Optional[str] = None
    app_package: Optional[str] = None
    start_time: float = 0.0
    end_time: Optional[float] = None
    status: str = "running"
    traversal_mode: str = "graph"
    config: Dict[str, Any] = field(default_factory=dict)

    @property
    def trace_id(self) -> str:
        return self.session_id

    def to_dict(self) -> Dict[str, Any]:
        return {
            "session_id": self.session_id,
            "device_id": self.device_id,
            "device_name": self.device_name,
            "device_model": self.device_model,
            "os_version": self.os_version,
            "app_version": self.app_version,
            "app_package": self.app_package,
            "start_time": self.start_time,
            "end_time": self.end_time,
            "status": self.status,
            "traversal_mode": self.traversal_mode,
            "config": self.config,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "Session":
        return cls(
            session_id=data.get("session_id", ""),
            device_id=data.get("device_id"),
            device_name=data.get("device_name"),
            device_model=data.get("device_model", ""),
            os_version=data.get("os_version", ""),
            app_version=data.get("app_version"),
            app_package=data.get("app_package"),
            start_time=data.get("start_time", 0.0),
            end_time=data.get("end_time"),
            status=data.get("status", "running"),
            traversal_mode=data.get("traversal_mode", "graph"),
            config=data.get("config", {}),
        )


# ── Stack frame ──────────────────────────────────────────────────────────────


@dataclass
class StackFrame:
    """A single entry in the node stack."""

    node_id: str
    span_id: str = ""
    node_type: str = ""

    def __eq__(self, other: object) -> bool:
        if isinstance(other, str):
            return self.node_id == other
        if isinstance(other, StackFrame):
            return self.node_id == other.node_id
        return False

    def __hash__(self) -> int:
        return hash(self.node_id)

    def __str__(self) -> str:
        return self.node_id


# ── TraversalRuntimeContext (mutable) ────────────────────────────────────────


@dataclass
class TraversalRuntimeContext:
    """Mutable runtime context used by the traversal engine.

    All fields are mutable. Convert to a frozen TraversalContext
    (from src.models.traversal_context) before passing to AI advisors
    via to_readonly().
    """

    # Trace identity
    trace_id: str = ""

    # Stack
    node_stack: List[StackFrame] = field(default_factory=list)

    # Location
    current_path: List[str] = field(default_factory=list)

    # Page analysis (current screen)
    current_page_analysis: Optional[Any] = None
    current_fingerprint: Optional[str] = None
    cache_valid: bool = False

    # Visited tracking
    visited_pages: Set[str] = field(default_factory=set)
    visited_level1_menus: Set[str] = field(default_factory=set)
    visited_level2_menus: Set[str] = field(default_factory=set)
    visited_nodes: Set[str] = field(default_factory=set)
    visited_children: Dict[str, Set[str]] = field(default_factory=dict)

    # Page tree (discovered pages)
    page_tree: Dict[str, Any] = field(default_factory=dict)

    # Action / error history
    action_history: List[Dict[str, Any]] = field(default_factory=list)
    failed_nodes: Dict[str, Dict[str, Any]] = field(default_factory=dict)
    consecutive_errors: int = 0

    # Limits
    max_depth: int = 100

    # Completion
    completion_policy: Optional[Any] = None

    # Device
    device_experience: Optional[Any] = None

    # Engine-internal (not exposed to AI)
    step_count: int = 0
    retry_count: int = 0
    global_state: Any = None  # Backward compat: GlobalState enum, set by engine
    last_error: Optional[Exception] = None
    exception_chain: Optional[Any] = None
    ai_provider: Optional[Any] = None
    page_cache: Dict[str, Any] = field(default_factory=dict)

    # -- helpers ------------------------------------------------------------

    def get_current_depth(self) -> int:
        return len(self.node_stack)

    def is_at_max_depth(self) -> bool:
        return self.get_current_depth() >= self.max_depth

    def record_action(self, action: str, **kwargs: Any) -> None:
        from datetime import datetime
        self.action_history.append({
            "action": action,
            "timestamp": datetime.now(),
            **kwargs,
        })
        if len(self.action_history) > 5:
            self.action_history = self.action_history[-5:]

    # -- readonly conversion -------------------------------------------------

    def to_readonly(self) -> Any:
        """Convert to a frozen TraversalContext for AI consumption.

        Maps to the existing src.models.traversal_context.TraversalContext
        format for backward compatibility with existing AI advisors.
        """
        from src.models.traversal_context import TraversalContext

        return TraversalContext(
            node_stack=[f.node_id for f in self.node_stack],
            current_path=list(self.current_path),
            visited_pages=self.visited_pages.copy(),
            visited_nodes=self.visited_nodes.copy(),
            max_depth=self.max_depth,
            step_count=self.step_count,
            action_history=list(self.action_history),
            failed_nodes=dict(self.failed_nodes),
        )
