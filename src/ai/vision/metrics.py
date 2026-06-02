"""Vision service metrics collection.

This module provides runtime metrics collection for the vision services,
tracking latency, token usage, cache effectiveness, and accuracy.
"""

import logging
from dataclasses import dataclass, field
from datetime import datetime
from typing import List, Dict, Any, Optional


logger = logging.getLogger(__name__)


@dataclass
class VisionMetrics:
    """Metrics for a single vision analysis operation.

    Attributes:
        timestamp: When the analysis was performed
        screenshot_hash: Hash identifier for the screenshot
        mode: Service mode (legacy, flattened, etc.)
        multimodal_latency_ms: Multimodal analysis latency
        text_latency_ms: Text assembly latency
        total_latency_ms: Total latency
        multimodal_output_tokens: Tokens from multimodal model
        text_output_tokens: Tokens from text model
        total_tokens: Total tokens consumed
        multimodal_cached: Whether multimodal result was cached
        assembler_cached: Whether assembler result was cached
        hierarchy_accuracy: Hierarchy inference accuracy (0-1)
        behavior_accuracy: Behavior inference accuracy (0-1)
        popup_detection_accuracy: Popup detection accuracy (0-1)
        error: Error message if analysis failed
    """

    timestamp: datetime
    screenshot_hash: str
    mode: str

    # Latency metrics
    multimodal_latency_ms: float = 0.0
    text_latency_ms: float = 0.0
    total_latency_ms: float = 0.0

    # Token metrics
    multimodal_output_tokens: int = 0
    text_output_tokens: int = 0
    total_tokens: int = 0

    # Cache metrics
    multimodal_cached: bool = False
    assembler_cached: bool = False

    # Accuracy metrics (optional, from ground truth comparison)
    hierarchy_accuracy: Optional[float] = None
    behavior_accuracy: Optional[float] = None
    popup_detection_accuracy: Optional[float] = None

    # Error tracking
    error: Optional[str] = None

    @property
    def output_tokens(self) -> int:
        """Total output tokens."""
        return self.multimodal_output_tokens + self.text_output_tokens

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization."""
        return {
            'timestamp': self.timestamp.isoformat(),
            'screenshot_hash': self.screenshot_hash,
            'mode': self.mode,
            'multimodal_latency_ms': self.multimodal_latency_ms,
            'text_latency_ms': self.text_latency_ms,
            'total_latency_ms': self.total_latency_ms,
            'multimodal_output_tokens': self.multimodal_output_tokens,
            'text_output_tokens': self.text_output_tokens,
            'total_tokens': self.total_tokens,
            'multimodal_cached': self.multimodal_cached,
            'assembler_cached': self.assembler_cached,
            'hierarchy_accuracy': self.hierarchy_accuracy,
            'behavior_accuracy': self.behavior_accuracy,
            'popup_detection_accuracy': self.popup_detection_accuracy,
            'error': self.error,
        }


@dataclass
class MetricsSummary:
    """Summary of collected metrics.

    Attributes:
        total_analyses: Total number of analyses
        successful_analyses: Number of successful analyses
        failed_analyses: Number of failed analyses
        avg_total_latency_ms: Average total latency
        avg_total_tokens: Average total tokens
        cache_hit_rate: Cache hit rate
        avg_hierarchy_accuracy: Average hierarchy accuracy
        avg_behavior_accuracy: Average behavior accuracy
        avg_popup_accuracy: Average popup detection accuracy
        token_reduction_percent: Average token reduction vs baseline
    """

    total_analyses: int = 0
    successful_analyses: int = 0
    failed_analyses: int = 0

    avg_total_latency_ms: float = 0.0
    avg_total_tokens: float = 0.0

    cache_hit_rate: float = 0.0

    avg_hierarchy_accuracy: Optional[float] = None
    avg_behavior_accuracy: Optional[float] = None
    avg_popup_accuracy: Optional[float] = None

    token_reduction_percent: Optional[float] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization."""
        return {
            'total_analyses': self.total_analyses,
            'successful_analyses': self.successful_analyses,
            'failed_analyses': self.failed_analyses,
            'avg_total_latency_ms': self.avg_total_latency_ms,
            'avg_total_tokens': self.avg_total_tokens,
            'cache_hit_rate': self.cache_hit_rate,
            'avg_hierarchy_accuracy': self.avg_hierarchy_accuracy,
            'avg_behavior_accuracy': self.avg_behavior_accuracy,
            'avg_popup_accuracy': self.avg_popup_accuracy,
            'token_reduction_percent': self.token_reduction_percent,
        }


class VisionMetricsCollector:
    """Collector for vision service runtime metrics.

    Collects and aggregates metrics from vision service operations
    to monitor performance, accuracy, and resource usage.
    """

    def __init__(self, max_metrics: int = 10000):
        """Initialize the metrics collector.

        Args:
            max_metrics: Maximum number of metrics to store
        """
        self.max_metrics = max_metrics
        self._metrics: List[VisionMetrics] = []

        logger.info(f"VisionMetricsCollector initialized (max={max_metrics})")

    def record(self, metrics: VisionMetrics) -> None:
        """Record a metrics entry.

        Args:
            metrics: VisionMetrics to record
        """
        self._metrics.append(metrics)

        # Prune if exceeding max size
        if len(self._metrics) > self.max_metrics:
            self._metrics = self._metrics[-self.max_metrics:]

        logger.debug(
            f"Recorded metrics: mode={metrics.mode}, "
            f"latency={metrics.total_latency_ms:.0f}ms, "
            f"tokens={metrics.total_tokens}"
        )

    def get_summary(self, days: int = 7) -> MetricsSummary:
        """Get summary of collected metrics.

        Args:
            days: Number of days to include in summary (default: 7)

        Returns:
            MetricsSummary with aggregated statistics
        """
        if not self._metrics:
            return MetricsSummary()

        # Filter by date range
        cutoff = datetime.now().timestamp() - (days * 86400)
        recent_metrics = [
            m for m in self._metrics
            if m.timestamp.timestamp() >= cutoff
        ]

        if not recent_metrics:
            return MetricsSummary()

        # Calculate summary
        successful = [m for m in recent_metrics if m.error is None]
        failed = [m for m in recent_metrics if m.error is not None]

        summary = MetricsSummary(
            total_analyses=len(recent_metrics),
            successful_analyses=len(successful),
            failed_analyses=len(failed),
        )

        if successful:
            summary.avg_total_latency_ms = sum(m.total_latency_ms for m in successful) / len(successful)
            summary.avg_total_tokens = sum(m.total_tokens for m in successful) / len(successful)

            # Cache hit rate
            cache_hits = sum(
                1 for m in successful
                if m.multimodal_cached or m.assembler_cached
            )
            summary.cache_hit_rate = cache_hits / len(successful)

            # Accuracy metrics (if available)
            hierarchy_accuracies = [m.hierarchy_accuracy for m in successful if m.hierarchy_accuracy is not None]
            if hierarchy_accuracies:
                summary.avg_hierarchy_accuracy = sum(hierarchy_accuracies) / len(hierarchy_accuracies)

            behavior_accuracies = [m.behavior_accuracy for m in successful if m.behavior_accuracy is not None]
            if behavior_accuracies:
                summary.avg_behavior_accuracy = sum(behavior_accuracies) / len(behavior_accuracies)

            popup_accuracies = [m.popup_detection_accuracy for m in successful if m.popup_detection_accuracy is not None]
            if popup_accuracies:
                summary.avg_popup_accuracy = sum(popup_accuracies) / len(popup_accuracies)

        # Token reduction (compare flattened vs legacy)
        legacy_metrics = [m for m in successful if m.mode == 'legacy']
        flattened_metrics = [m for m in successful if m.mode == 'flattened']

        if legacy_metrics and flattened_metrics:
            avg_legacy_tokens = sum(m.total_tokens for m in legacy_metrics) / len(legacy_metrics)
            avg_flattened_tokens = sum(m.total_tokens for m in flattened_metrics) / len(flattened_metrics)

            if avg_legacy_tokens > 0:
                summary.token_reduction_percent = (
                    (avg_legacy_tokens - avg_flattened_tokens) / avg_legacy_tokens * 100
                )

        return summary

    def get_metrics(
        self,
        mode: Optional[str] = None,
        days: int = 7,
        limit: int = 100,
    ) -> List[VisionMetrics]:
        """Get collected metrics with optional filtering.

        Args:
            mode: Filter by mode (optional)
            days: Number of days to include
            limit: Maximum number of metrics to return

        Returns:
            List of VisionMetrics
        """
        cutoff = datetime.now().timestamp() - (days * 86400)

        metrics = [m for m in self._metrics if m.timestamp.timestamp() >= cutoff]

        if mode:
            metrics = [m for m in metrics if m.mode == mode]

        # Return most recent first, limited
        return list(reversed(metrics[-limit:]))

    def clear(self) -> None:
        """Clear all collected metrics."""
        count = len(self._metrics)
        self._metrics.clear()
        logger.info(f"Cleared {count} metrics")

    def get_count(self) -> int:
        """Get current number of stored metrics.

        Returns:
            Number of metrics stored
        """
        return len(self._metrics)


# Global metrics collector instance
_global_collector: Optional[VisionMetricsCollector] = None


def get_global_collector(max_metrics: int = 10000) -> VisionMetricsCollector:
    """Get or create the global metrics collector.

    Args:
        max_metrics: Maximum number of metrics to store

    Returns:
        Global VisionMetricsCollector instance
    """
    global _global_collector

    if _global_collector is None:
        _global_collector = VisionMetricsCollector(max_metrics=max_metrics)

    return _global_collector
