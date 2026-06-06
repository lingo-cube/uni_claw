"""
Trace data models for V6.3 distributed tracing system.

Uses industry-standard terminology: Trace ID, Span ID, Parent Span ID
with ULID identifiers for time-sortable, globally unique IDs.

Three-tier node hierarchy:
- SessionNode: root of the trace, holds session metadata
- StepNode: represents a traversal step (graph node processing)
- SpanNode: represents a fine-grained operation within a step
"""

import os
import time
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional


# ── ULID generation ──────────────────────────────────────────────────────────

# Crockford Base32 encoding alphabet
_ULID_ALPHABET = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"


def _encode_ulid(timestamp_ms: int, randomness: int) -> str:
    """Encode a 48-bit timestamp and 80-bit randomness as a 26-char ULID."""
    chars = []
    # Encode 48-bit timestamp (10 chars)
    for i in range(9, -1, -1):
        chars.append(_ULID_ALPHABET[(timestamp_ms >> (i * 5)) & 0x1F])
    # Encode 80-bit randomness (16 chars)
    for i in range(15, -1, -1):
        chars.append(_ULID_ALPHABET[(randomness >> (i * 5)) & 0x1F])
    return "".join(chars)


def generate_id() -> str:
    """Generate a ULID-based identifier string.

    Returns a 26-character Crockford Base32-encoded, time-sortable, URL-safe string.

    Format: 48-bit timestamp (ms) + 80-bit random = 128-bit = 26 Base32 chars.
    """
    timestamp_ms = int(time.time() * 1000)
    randomness = int.from_bytes(os.urandom(10), "big") & 0xFFFFFFFFFFFFFFFFFFFFFFFFFFFF  # 80 bits
    return _encode_ulid(timestamp_ms, randomness)


# ── Base trace node ──────────────────────────────────────────────────────────


@dataclass
class TraceNode:
    """Base class for all trace nodes.

    Every node in a trace has a globally unique span_id and shares
    a common trace_id. The parent_span_id establishes the call chain.
    """

    trace_id: str = ""
    span_id: str = ""
    parent_span_id: Optional[str] = None
    node_type: str = ""
    timestamp: float = 0.0

    def to_dict(self) -> Dict[str, Any]:
        raise NotImplementedError

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "TraceNode":
        node_type = data.get("node_type", "")
        if node_type == "session":
            return SessionNode.from_dict(data)
        elif node_type == "step":
            return StepNode.from_dict(data)
        elif node_type == "span":
            return SpanNode.from_dict(data)
        raise ValueError(f"Unknown node_type: {node_type}")


# ── Session node ─────────────────────────────────────────────────────────────


@dataclass
class SessionNode(TraceNode):
    """Root node of a trace, holding session-level metadata.

    Corresponds to one traversal run. Its span_id serves as the trace_id
    for all nodes in the trace.
    """

    # Session metadata
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
    children: List["TraceNode"] = field(default_factory=list)

    def __post_init__(self):
        self.node_type = "session"
        if not self.span_id:
            self.span_id = generate_id()
        if not self.trace_id:
            self.trace_id = self.span_id
        if not self.timestamp:
            self.timestamp = self.start_time

    def to_dict(self) -> Dict[str, Any]:
        return {
            "trace_id": self.trace_id,
            "span_id": self.span_id,
            "parent_span_id": self.parent_span_id,
            "node_type": self.node_type,
            "timestamp": self.timestamp,
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
    def from_dict(cls, data: Dict[str, Any]) -> "SessionNode":
        return cls(
            trace_id=data.get("trace_id", ""),
            span_id=data.get("span_id", ""),
            parent_span_id=data.get("parent_span_id"),
            timestamp=data.get("timestamp", 0.0),
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


# ── Step node ────────────────────────────────────────────────────────────────


@dataclass
class StepNode(TraceNode):
    """Represents a traversal step — processing of a single graph node.

    Corresponds to one NODE_SELECT → … → FRAME_COMPLETE cycle.
    """

    node_id: str = ""
    step_type: str = ""  # NODE_SELECT, FRAME_COMPLETE
    page_path: List[str] = field(default_factory=list)
    result: Optional[Dict[str, Any]] = None
    children: List["TraceNode"] = field(default_factory=list)

    def __post_init__(self):
        self.node_type = "step"
        if not self.span_id:
            self.span_id = generate_id()

    def to_dict(self) -> Dict[str, Any]:
        return {
            "trace_id": self.trace_id,
            "span_id": self.span_id,
            "parent_span_id": self.parent_span_id,
            "node_type": self.node_type,
            "timestamp": self.timestamp,
            "node_id": self.node_id,
            "step_type": self.step_type,
            "page_path": self.page_path,
            "result": self.result,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "StepNode":
        return cls(
            trace_id=data.get("trace_id", ""),
            span_id=data.get("span_id", ""),
            parent_span_id=data.get("parent_span_id"),
            timestamp=data.get("timestamp", 0.0),
            node_id=data.get("node_id", ""),
            step_type=data.get("step_type", ""),
            page_path=data.get("page_path", []),
            result=data.get("result"),
        )


# ── Span node ────────────────────────────────────────────────────────────────


@dataclass
class SpanNode(TraceNode):
    """Represents a fine-grained operation within a step.

    Span types and their fields:
    - state_transition: from_state, to_state, state_machine
    - execution: action, status, target, page_before, page_after
    - ai_call: capability, provider_id, success, latency_ms, tokens
    - error: error_type, error_message, severity, stack_trace
    - step_end: step_span_id, result (backfills parent StepNode)
    - session_end: status, end_time (backfills SessionNode)
    """

    span_type: str = ""

    # state_transition fields
    from_state: Optional[str] = None
    to_state: Optional[str] = None
    state_machine: Optional[str] = None

    # execution fields
    action: Optional[str] = None
    status: Optional[str] = None
    target: Optional[str] = None
    page_before: Optional[str] = None
    page_after: Optional[str] = None
    duration_ms: Optional[float] = None

    # ai_call fields
    capability: Optional[str] = None
    provider_id: Optional[str] = None
    success: Optional[bool] = None
    latency_ms: Optional[float] = None
    input_tokens: Optional[int] = None
    output_tokens: Optional[int] = None

    # error fields
    error_type: Optional[str] = None
    error_message: Optional[str] = None
    severity: Optional[str] = None
    stack_trace: Optional[str] = None

    # step_end / session_end fields
    step_span_id: Optional[str] = None

    # screenshot
    screenshot_ref: Optional[str] = None

    # arbtrary metadata
    metadata: Dict[str, Any] = field(default_factory=dict)
    children: List["TraceNode"] = field(default_factory=list)

    def __post_init__(self):
        self.node_type = "span"
        if not self.span_id:
            self.span_id = generate_id()

    def to_dict(self) -> Dict[str, Any]:
        result: Dict[str, Any] = {
            "trace_id": self.trace_id,
            "span_id": self.span_id,
            "parent_span_id": self.parent_span_id,
            "node_type": self.node_type,
            "timestamp": self.timestamp,
            "span_type": self.span_type,
        }

        if self.span_type == "state_transition":
            result.update({
                "from_state": self.from_state,
                "to_state": self.to_state,
                "state_machine": self.state_machine,
            })
        elif self.span_type == "execution":
            result.update({
                "action": self.action,
                "status": self.status,
                "target": self.target,
                "page_before": self.page_before,
                "page_after": self.page_after,
                "duration_ms": self.duration_ms,
            })
            if self.screenshot_ref:
                result["screenshot_ref"] = self.screenshot_ref
        elif self.span_type == "ai_call":
            result.update({
                "capability": self.capability,
                "provider_id": self.provider_id,
                "success": self.success,
                "latency_ms": self.latency_ms,
                "input_tokens": self.input_tokens,
                "output_tokens": self.output_tokens,
            })
        elif self.span_type == "error":
            result.update({
                "error_type": self.error_type,
                "error_message": self.error_message,
                "severity": self.severity,
                "stack_trace": self.stack_trace,
            })
        elif self.span_type == "step_end":
            result.update({
                "step_span_id": self.step_span_id,
                "result": self.metadata.get("result"),
            })
        elif self.span_type == "session_end":
            result.update({
                "status": self.status,
                "end_time": self.timestamp,
            })

        if self.metadata:
            result["metadata"] = self.metadata

        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "SpanNode":
        span_type = data.get("span_type", "")
        return cls(
            trace_id=data.get("trace_id", ""),
            span_id=data.get("span_id", ""),
            parent_span_id=data.get("parent_span_id"),
            timestamp=data.get("timestamp", 0.0),
            span_type=span_type,
            from_state=data.get("from_state"),
            to_state=data.get("to_state"),
            state_machine=data.get("state_machine"),
            action=data.get("action"),
            status=data.get("status"),
            target=data.get("target"),
            page_before=data.get("page_before"),
            page_after=data.get("page_after"),
            duration_ms=data.get("duration_ms"),
            capability=data.get("capability"),
            provider_id=data.get("provider_id"),
            success=data.get("success"),
            latency_ms=data.get("latency_ms"),
            input_tokens=data.get("input_tokens"),
            output_tokens=data.get("output_tokens"),
            error_type=data.get("error_type"),
            error_message=data.get("error_message"),
            severity=data.get("severity"),
            stack_trace=data.get("stack_trace"),
            step_span_id=data.get("step_span_id"),
            screenshot_ref=data.get("screenshot_ref"),
            metadata=data.get("metadata", {}),
        )
