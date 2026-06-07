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
            # Dispatch to appropriate span type subclass
            span_type = data.get("span_type", "")
            if span_type == "page_transition":
                return PageTransitionSpan.from_dict(data)
            elif span_type == "dynamic_lifecycle":
                return DynamicNodeLifecycleSpan.from_dict(data)
            elif span_type == "state_decision":
                return StateDecisionSpan.from_dict(data)
            else:
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

    # ai_call context fields
    page_id: Optional[str] = None
    element_count: Optional[int] = None

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
            if self.page_id is not None:
                result["page_id"] = self.page_id
            if self.element_count is not None:
                result["element_count"] = self.element_count
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
            page_id=data.get("page_id"),
            element_count=data.get("element_count"),
            metadata=data.get("metadata", {}),
        )


# ── Enhanced span types for V6.9.2 ────────────────────────────────────────────────


@dataclass
class PageTransitionSpan(SpanNode):
    """Records page transition events.

    Captured when an action causes navigation from one page to another.
    Includes trigger element and action type for analysis.
    """

    span_type: str = "page_transition"
    from_page: Optional[str] = None
    to_page: Optional[str] = None
    trigger_element: Optional[str] = None
    trigger_action: Optional[str] = None

    def __post_init__(self):
        # Validate span type
        if self.span_type != "page_transition":
            raise ValueError(f"PageTransitionSpan must have span_type='page_transition', got '{self.span_type}'")
        self.node_type = "span"
        if not self.span_id:
            self.span_id = generate_id()

    def to_dict(self) -> Dict[str, Any]:
        result = super().to_dict()
        result.update({
            "span_type": self.span_type,
            "from_page": self.from_page,
            "to_page": self.to_page,
            "trigger_element": self.trigger_element,
            "trigger_action": self.trigger_action,
        })
        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "PageTransitionSpan":
        return cls(
            trace_id=data.get("trace_id", ""),
            span_id=data.get("span_id", ""),
            parent_span_id=data.get("parent_span_id"),
            timestamp=data.get("timestamp", 0.0),
            from_page=data.get("from_page"),
            to_page=data.get("to_page"),
            trigger_element=data.get("trigger_element"),
            trigger_action=data.get("trigger_action"),
        )


@dataclass
class DynamicNodeLifecycleSpan(SpanNode):
    """Records lifecycle events for dynamically generated nodes.

    Events include: created, matched, pushed, executed, popped.
    Tracks the relationship with parent nodes and match rules.
    """

    span_type: str = "dynamic_lifecycle"
    event: Optional[str] = None  # created, matched, pushed, executed, popped
    node_id: Optional[str] = None
    parent_id: Optional[str] = None
    match_rule_id: Optional[str] = None
    element_id: Optional[str] = None

    def __post_init__(self):
        # Validate span type
        if self.span_type != "dynamic_lifecycle":
            raise ValueError(f"DynamicNodeLifecycleSpan must have span_type='dynamic_lifecycle', got '{self.span_type}'")
        # Validate event type
        valid_events = {"created", "matched", "pushed", "executed", "popped"}
        if self.event and self.event not in valid_events:
            raise ValueError(f"Invalid event '{self.event}'. Must be one of: {valid_events}")
        self.node_type = "span"
        if not self.span_id:
            self.span_id = generate_id()

    def to_dict(self) -> Dict[str, Any]:
        result = super().to_dict()
        result.update({
            "span_type": self.span_type,
            "event": self.event,
            "node_id": self.node_id,
            "parent_id": self.parent_id,
            "match_rule_id": self.match_rule_id,
            "element_id": self.element_id,
        })
        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "DynamicNodeLifecycleSpan":
        return cls(
            trace_id=data.get("trace_id", ""),
            span_id=data.get("span_id", ""),
            parent_span_id=data.get("parent_span_id"),
            timestamp=data.get("timestamp", 0.0),
            event=data.get("event"),
            node_id=data.get("node_id"),
            parent_id=data.get("parent_id"),
            match_rule_id=data.get("match_rule_id"),
            element_id=data.get("element_id"),
        )


@dataclass
class StateDecisionSpan(SpanNode):
    """Records state machine decision points.

    Captures why a particular state transition or decision was made.
    Includes context information for analysis and debugging.
    """

    span_type: str = "state_decision"
    current_state: Optional[str] = None
    decision: Optional[str] = None  # AUTO_ESCAPE, COMPLETE, etc.
    reason: Optional[str] = None
    context: Dict[str, Any] = field(default_factory=dict)

    def __post_init__(self):
        # Validate span type
        if self.span_type != "state_decision":
            raise ValueError(f"StateDecisionSpan must have span_type='state_decision', got '{self.span_type}'")
        self.node_type = "span"
        if not self.span_id:
            self.span_id = generate_id()

    def to_dict(self) -> Dict[str, Any]:
        result = super().to_dict()
        result.update({
            "span_type": self.span_type,
            "current_state": self.current_state,
            "decision": self.decision,
            "reason": self.reason,
            "context": self.context,
        })
        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "StateDecisionSpan":
        return cls(
            trace_id=data.get("trace_id", ""),
            span_id=data.get("span_id", ""),
            parent_span_id=data.get("parent_span_id"),
            timestamp=data.get("timestamp", 0.0),
            current_state=data.get("current_state"),
            decision=data.get("decision"),
            reason=data.get("reason"),
            context=data.get("context", {}),
        )
