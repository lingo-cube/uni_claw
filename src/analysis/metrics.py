"""Metrics collector for AI service calls and traversal operations."""

import json
import time
from collections import defaultdict
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional

from .trace_analyzer import TraceAnalyzer


@dataclass
class MetricPoint:
    """A single metric data point."""

    name: str
    value: float
    timestamp: float
    tags: Dict[str, str] = field(default_factory=dict)
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class AI_CALL_Metrics:
    """Metrics for AI service calls."""

    total_calls: int = 0
    successful_calls: int = 0
    failed_calls: int = 0
    total_duration_ms: float = 0
    avg_duration_ms: float = 0
    max_duration_ms: float = 0
    min_duration_ms: float = float("inf")
    last_call_time: Optional[float] = None
    confidence_scores: List[float] = field(default_factory=list)

    def add_call(self, duration_ms: float, success: bool, confidence: Optional[float] = None):
        """Add a call to the metrics.

        Args:
            duration_ms: Call duration in milliseconds
            success: Whether the call succeeded
            confidence: Optional confidence score
        """
        self.total_calls += 1
        self.total_duration_ms += duration_ms
        self.max_duration_ms = max(self.max_duration_ms, duration_ms)
        self.min_duration_ms = min(self.min_duration_ms, duration_ms)
        self.last_call_time = time.time()

        if success:
            self.successful_calls += 1
        else:
            self.failed_calls += 1

        if confidence is not None:
            self.confidence_scores.append(confidence)

        if self.total_calls > 0:
            self.avg_duration_ms = self.total_duration_ms / self.total_calls

    def get_success_rate(self) -> float:
        """Get success rate as percentage."""
        if self.total_calls == 0:
            return 0
        return (self.successful_calls / self.total_calls) * 100

    def get_avg_confidence(self) -> Optional[float]:
        """Get average confidence score."""
        if not self.confidence_scores:
            return None
        return sum(self.confidence_scores) / len(self.confidence_scores)


@dataclass
class TraversalMetrics:
    """Metrics for traversal operations."""

    total_steps: int = 0
    visited_items: int = 0
    skipped_items: int = 0
    screens_analyzed: int = 0
    total_duration_ms: float = 0
    avg_step_duration_ms: float = 0
    screens_per_step: float = 0

    def add_step(self, screens_count: int, duration_ms: float):
        """Add a step to the metrics.

        Args:
            screens_count: Number of screens analyzed in this step
            duration_ms: Step duration in milliseconds
        """
        self.total_steps += 1
        self.screens_analyzed += screens_count
        self.total_duration_ms += duration_ms

        if self.total_steps > 0:
            self.avg_step_duration_ms = self.total_duration_ms / self.total_steps
            self.screens_per_step = self.screens_analyzed / self.total_steps


class MetricsCollector:
    """Collector for system metrics."""

    def __init__(self):
        """Initialize metrics collector."""
        self.ai_metrics: Dict[str, AI_CALL_Metrics] = defaultdict(AI_CALL_Metrics)
        self.traversal_metrics: Dict[str, TraversalMetrics] = defaultdict(TraversalMetrics)
        self.metric_points: List[MetricPoint] = []

    def record_ai_call(
        self,
        service: str,
        operation: str,
        duration_ms: float,
        success: bool,
        confidence: Optional[float] = None,
        trace_id: Optional[str] = None
    ):
        """Record an AI service call.

        Args:
            service: Service name (e.g., "TraversalPlan", "vision")
            operation: Operation name (e.g., "execute", "analyze_screenshot")
            duration_ms: Call duration in milliseconds
            success: Whether the call succeeded
            confidence: Optional confidence score
            trace_id: Optional trace ID for correlation
        """
        key = f"{service}.{operation}"
        self.ai_metrics[key].add_call(duration_ms, success, confidence)

        # Also record as a metric point
        self.metric_points.append(MetricPoint(
            name=f"ai_call_duration_ms",
            value=duration_ms,
            timestamp=time.time(),
            tags={
                "service": service,
                "operation": operation,
                "success": str(success),
                "trace_id": trace_id or "",
            }
        ))

    def record_traversal_step(
        self,
        session_id: str,
        screens_count: int,
        duration_ms: float,
        visited_count: int = 0,
        skipped_count: int = 0
    ):
        """Record a traversal step.

        Args:
            session_id: Traversal session ID
            screens_count: Number of screens analyzed
            duration_ms: Step duration in milliseconds
            visited_count: Number of items visited
            skipped_count: Number of items skipped
        """
        self.traversal_metrics[session_id].add_step(screens_count, duration_ms)
        self.traversal_metrics[session_id].visited_items += visited_count
        self.traversal_metrics[session_id].skipped_items += skipped_count

    def get_ai_metrics_summary(self) -> Dict[str, Dict]:
        """Get summary of all AI metrics.

        Returns:
            Dictionary mapping operation name to metrics summary
        """
        summary = {}
        for key, metrics in self.ai_metrics.items():
            summary[key] = {
                "total_calls": metrics.total_calls,
                "success_rate": metrics.get_success_rate(),
                "avg_duration_ms": metrics.avg_duration_ms,
                "max_duration_ms": metrics.max_duration_ms,
                "min_duration_ms": metrics.min_duration_ms if metrics.min_duration_ms != float("inf") else 0,
                "avg_confidence": metrics.get_avg_confidence(),
                "last_call": datetime.fromtimestamp(metrics.last_call_time).isoformat() if metrics.last_call_time else None,
            }
        return summary

    def get_traversal_metrics_summary(self) -> Dict[str, Dict]:
        """Get summary of all traversal metrics.

        Returns:
            Dictionary mapping session ID to metrics summary
        """
        summary = {}
        for session_id, metrics in self.traversal_metrics.items():
            summary[session_id] = {
                "total_steps": metrics.total_steps,
                "visited_items": metrics.visited_items,
                "skipped_items": metrics.skipped_items,
                "screens_analyzed": metrics.screens_analyzed,
                "total_duration_ms": metrics.total_duration_ms,
                "avg_step_duration_ms": metrics.avg_step_duration_ms,
                "screens_per_step": metrics.screens_per_step,
            }
        return summary

    def get_metrics_timeline(self, start_time: Optional[float] = None, end_time: Optional[float] = None) -> List[Dict]:
        """Get metric points within a time range.

        Args:
            start_time: Optional start timestamp
            end_time: Optional end timestamp

        Returns:
            List of metric points in chronological order
        """
        filtered = self.metric_points

        if start_time:
            filtered = [m for m in filtered if m.timestamp >= start_time]
        if end_time:
            filtered = [m for m in filtered if m.timestamp <= end_time]

        return [
            {
                "name": m.name,
                "value": m.value,
                "timestamp": datetime.fromtimestamp(m.timestamp).isoformat(),
                "tags": m.tags,
            }
            for m in sorted(filtered, key=lambda x: x.timestamp)
        ]

    def export_to_prometheus_format(self) -> str:
        """Export metrics in Prometheus text format.

        Returns:
            Metrics in Prometheus format
        """
        lines = []

        for key, metrics in self.ai_metrics.items():
            safe_key = key.replace(".", "_")
            lines.append(f"ai_call_total{{operation=\"{safe_key}\"}} {metrics.total_calls}")
            lines.append(f"ai_call_success_rate{{operation=\"{safe_key}\"}} {metrics.get_success_rate()}")
            lines.append(f"ai_call_duration_ms{{operation=\"{safe_key}\"}} {metrics.avg_duration_ms}")
            lines.append(f"ai_call_max_duration_ms{{operation=\"{safe_key}\"}} {metrics.max_duration_ms}")

        for session_id, metrics in self.traversal_metrics.items():
            lines.append(f"traversal_steps_total{{session=\"{session_id}\"}} {metrics.total_steps}")
            lines.append(f"traversal_items_visited{{session=\"{session_id}\"}} {metrics.visited_items}")
            lines.append(f"traversal_items_skipped{{session=\"{session_id}\"}} {metrics.skipped_items}")

        return "\n".join(lines)


# Global metrics collector instance
_metrics_collector: Optional[MetricsCollector] = None


def get_metrics_collector() -> MetricsCollector:
    """Get global metrics collector instance."""
    global _metrics_collector
    if _metrics_collector is None:
        _metrics_collector = MetricsCollector()
    return _metrics_collector


__all__ = [
    "MetricPoint",
    "AI_CALL_Metrics",
    "TraversalMetrics",
    "MetricsCollector",
    "get_metrics_collector",
]
