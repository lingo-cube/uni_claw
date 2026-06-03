"""Data models for AI call tracing.

This module provides data structures for tracking AI calls, performance metrics,
and span contexts for distributed tracing.
"""

import time
from dataclasses import dataclass, field
from datetime import datetime
from typing import Dict, Any, Optional
from enum import Enum


class SpanStatus(Enum):
    """Status of a trace span."""

    ACTIVE = "active"
    FINISHED = "finished"
    ERROR = "error"


@dataclass
class SpanContext:
    """Context for a distributed trace span.

    Attributes:
        span_id: Unique identifier for this span
        parent_span_id: ID of parent span if nested
        start_time: Timestamp when span started
        tags: Key-value tags for categorization
        custom_context: Additional context data
        status: Current status of the span
    """

    span_id: str
    parent_span_id: Optional[str] = None
    start_time: float = field(default_factory=time.time)
    tags: Dict[str, Any] = field(default_factory=dict)
    custom_context: Dict[str, Any] = field(default_factory=dict)
    status: SpanStatus = SpanStatus.ACTIVE

    @property
    def duration_ms(self) -> float:
        """Duration of the span in milliseconds."""
        return (time.time() - self.start_time) * 1000

    @property
    def is_active(self) -> bool:
        """Whether the span is still active."""
        return self.status == SpanStatus.ACTIVE

    def finish(self) -> None:
        """Mark the span as finished."""
        self.status = SpanStatus.FINISHED

    def error(self) -> None:
        """Mark the span as errored."""
        self.status = SpanStatus.ERROR

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization."""
        return {
            "span_id": self.span_id,
            "parent_span_id": self.parent_span_id,
            "start_time": self.start_time,
            "duration_ms": self.duration_ms,
            "tags": self.tags,
            "custom_context": self.custom_context,
            "status": self.status.value,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "SpanContext":
        """Create from dictionary."""
        return cls(
            span_id=data["span_id"],
            parent_span_id=data.get("parent_span_id"),
            start_time=data["start_time"],
            tags=data.get("tags", {}),
            custom_context=data.get("custom_context", {}),
            status=SpanStatus(data.get("status", "active")),
        )


@dataclass
class AICallTrace:
    """Complete trace of an AI provider call.

    Attributes:
        trace_id: Unique identifier for the trace
        capability: The capability being called
        provider_id: Which provider was used
        mode: The mode (text, vision, multimodal)
        start_time: When the call started
        end_time: When the call ended
        input_tokens: Number of input tokens
        output_tokens: Number of output tokens
        success: Whether the call succeeded
        error_message: Error message if failed
        span_contexts: All spans involved in the trace
    """

    trace_id: str
    capability: str
    provider_id: str
    mode: str
    start_time: float = field(default_factory=time.time)
    end_time: Optional[float] = None
    input_tokens: int = 0
    output_tokens: int = 0
    success: bool = True
    error_message: Optional[str] = None
    span_contexts: Dict[str, SpanContext] = field(default_factory=dict)

    @property
    def duration_ms(self) -> Optional[float]:
        """Total duration of the trace in milliseconds."""
        if self.end_time is None:
            return None
        return (self.end_time - self.start_time) * 1000

    @property
    def total_tokens(self) -> int:
        """Total tokens consumed."""
        return self.input_tokens + self.output_tokens

    def finish(self, success: bool = True, error_message: Optional[str] = None) -> None:
        """Mark the trace as finished."""
        self.end_time = time.time()
        self.success = success
        self.error_message = error_message

    def add_span(self, span: SpanContext) -> None:
        """Add a span to this trace."""
        self.span_contexts[span.span_id] = span

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization."""
        return {
            "trace_id": self.trace_id,
            "capability": self.capability,
            "provider_id": self.provider_id,
            "mode": self.mode,
            "start_time": self.start_time,
            "end_time": self.end_time,
            "duration_ms": self.duration_ms,
            "input_tokens": self.input_tokens,
            "output_tokens": self.output_tokens,
            "total_tokens": self.total_tokens,
            "success": self.success,
            "error_message": self.error_message,
            "span_contexts": {
                k: v.to_dict() for k, v in self.span_contexts.items()
            },
        }


@dataclass
class ProviderPerformanceMetrics:
    """Performance metrics for an AI provider.

    Attributes:
        provider_id: Which provider these metrics are for
        mode: The mode being measured
        total_calls: Total number of calls
        successful_calls: Number of successful calls
        failed_calls: Number of failed calls
        total_tokens: Total tokens consumed
        total_latency_ms: Total latency across all calls
        avg_latency_ms: Average latency per call
        p50_latency_ms: 50th percentile latency
        p95_latency_ms: 95th percentile latency
        p99_latency_ms: 99th percentile latency
        last_updated: When these metrics were last updated
    """

    provider_id: str
    mode: str
    total_calls: int = 0
    successful_calls: int = 0
    failed_calls: int = 0
    total_tokens: int = 0
    total_latency_ms: float = 0.0
    avg_latency_ms: float = 0.0
    p50_latency_ms: Optional[float] = None
    p95_latency_ms: Optional[float] = None
    p99_latency_ms: Optional[float] = None
    last_updated: float = field(default_factory=time.time)

    @property
    def success_rate(self) -> float:
        """Success rate (0-1)."""
        if self.total_calls == 0:
            return 0.0
        return self.successful_calls / self.total_calls

    @property
    def avg_tokens_per_call(self) -> float:
        """Average tokens per call."""
        if self.total_calls == 0:
            return 0.0
        return self.total_tokens / self.total_calls

    def record_call(
        self,
        latency_ms: float,
        tokens: int,
        success: bool = True,
    ) -> None:
        """Record a call and update metrics."""
        self.total_calls += 1
        self.total_latency_ms += latency_ms
        self.total_tokens += tokens
        self.avg_latency_ms = self.total_latency_ms / self.total_calls
        self.last_updated = time.time()

        if success:
            self.successful_calls += 1
        else:
            self.failed_calls += 1

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization."""
        return {
            "provider_id": self.provider_id,
            "mode": self.mode,
            "total_calls": self.total_calls,
            "successful_calls": self.successful_calls,
            "failed_calls": self.failed_calls,
            "success_rate": self.success_rate,
            "total_tokens": self.total_tokens,
            "avg_tokens_per_call": self.avg_tokens_per_call,
            "avg_latency_ms": self.avg_latency_ms,
            "p50_latency_ms": self.p50_latency_ms,
            "p95_latency_ms": self.p95_latency_ms,
            "p99_latency_ms": self.p99_latency_ms,
            "last_updated": self.last_updated,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "ProviderPerformanceMetrics":
        """Create from dictionary."""
        return cls(
            provider_id=data["provider_id"],
            mode=data["mode"],
            total_calls=data["total_calls"],
            successful_calls=data["successful_calls"],
            failed_calls=data["failed_calls"],
            total_tokens=data["total_tokens"],
            total_latency_ms=data.get("total_latency_ms", 0.0),
            avg_latency_ms=data.get("avg_latency_ms", 0.0),
            p50_latency_ms=data.get("p50_latency_ms"),
            p95_latency_ms=data.get("p95_latency_ms"),
            p99_latency_ms=data.get("p99_latency_ms"),
            last_updated=data.get("last_updated", time.time()),
        )
