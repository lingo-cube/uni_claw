"""AI metrics collection and tracking."""

import json
import logging
import time
from collections import defaultdict
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Optional, Any
from enum import Enum
import statistics

logger = logging.getLogger(__name__)


class MetricType(Enum):
    """Types of metrics to collect."""
    CALL_COUNT = "call_count"
    LATENCY = "latency"
    CONFIDENCE = "confidence"
    TOKEN_USAGE = "token_usage"
    ERROR_COUNT = "error_count"


@dataclass
class MetricRecord:
    """A single metric record."""
    capability: str
    metric_type: MetricType
    value: float
    timestamp: datetime
    metadata: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict:
        """Convert to dictionary for serialization."""
        return {
            "capability": self.capability,
            "metric_type": self.metric_type.value,
            "value": self.value,
            "timestamp": self.timestamp.isoformat(),
            "metadata": self.metadata,
        }


class AIMetrics:
    """Metrics collector for AI operations.

    Tracks:
    - Call counts (by capability and success/failure)
    - Latency metrics (P50, P95, P99)
    - Confidence distribution
    - Token usage (if available)
    - Error counts by type
    """

    def __init__(self, max_records: int = 10000):
        """Initialize metrics collector.

        Args:
            max_records: Maximum number of records to keep per metric type
        """
        self._records: Dict[str, List[MetricRecord]] = defaultdict(list)
        self._call_counts: Dict[str, Dict[str, int]] = defaultdict(lambda: {"success": 0, "failure": 0})
        self._error_counts: Dict[str, int] = defaultdict(int)
        self._max_records = max_records

    def record_call(
        self,
        capability: str,
        success: bool,
        latency_ms: float,
        confidence: Optional[float] = None,
        token_count: Optional[int] = None,
    ) -> None:
        """Record a capability call.

        Args:
            capability: Name of the capability
            success: Whether the call succeeded
            latency_ms: Call duration in milliseconds
            confidence: Optional confidence score
            token_count: Optional token usage
        """
        timestamp = datetime.now()

        # Update call counts
        status = "success" if success else "failure"
        self._call_counts[capability][status] += 1

        # Record latency
        self._add_record(MetricRecord(
            capability=capability,
            metric_type=MetricType.LATENCY,
            value=latency_ms,
            timestamp=timestamp,
        ))

        # Record confidence if available
        if confidence is not None:
            self._add_record(MetricRecord(
                capability=capability,
                metric_type=MetricType.CONFIDENCE,
                value=confidence,
                timestamp=timestamp,
            ))

        # Record token usage if available
        if token_count is not None:
            self._add_record(MetricRecord(
                capability=capability,
                metric_type=MetricType.TOKEN_USAGE,
                value=token_count,
                timestamp=timestamp,
            ))

    def record_error(self, capability: str, error_type: str) -> None:
        """Record an error.

        Args:
            capability: Name of the capability
            error_type: Type of error that occurred
        """
        self._error_counts[f"{capability}:{error_type}"] += 1

    def get_call_counts(self, capability: Optional[str] = None) -> Dict[str, Dict[str, int]]:
        """Get call counts.

        Args:
            capability: Optional capability name to filter by

        Returns:
            Dict mapping capability name to {success: count, failure: count}
        """
        if capability:
            return {capability: self._call_counts.get(capability, {"success": 0, "failure": 0})}
        return dict(self._call_counts)

    def get_error_counts(self, capability: Optional[str] = None) -> Dict[str, int]:
        """Get error counts.

        Args:
            capability: Optional capability name to filter by

        Returns:
            Dict mapping error_type to count
        """
        if capability:
            return {k: v for k, v in self._error_counts.items() if k.startswith(f"{capability}:")}
        return dict(self._error_counts)

    def get_latency_stats(self, capability: str) -> Dict[str, float]:
        """Get latency statistics for a capability.

        Args:
            capability: Name of the capability

        Returns:
            Dict with P50, P95, P99, mean, min, max latencies in ms
        """
        records = [r for r in self._records.get(f"{capability}:latency", [])
                   if r.metric_type == MetricType.LATENCY]

        if not records:
            return {}

        values = [r.value for r in records]

        return {
            "p50": statistics.median(values),
            "p95": self._percentile(values, 95),
            "p99": self._percentile(values, 99),
            "mean": statistics.mean(values),
            "min": min(values),
            "max": max(values),
            "count": len(values),
        }

    def get_confidence_distribution(self, capability: str) -> Dict[str, Any]:
        """Get confidence distribution for a capability.

        Args:
            capability: Name of the capability

        Returns:
            Dict with confidence statistics
        """
        records = [r for r in self._records.get(f"{capability}:confidence", [])
                   if r.metric_type == MetricType.CONFIDENCE]

        if not records:
            return {}

        values = [r.value for r in records]

        # Create buckets
        buckets = {
            "0.0-0.5": 0,
            "0.5-0.7": 0,
            "0.7-0.9": 0,
            "0.9-1.0": 0,
        }

        for v in values:
            if v < 0.5:
                buckets["0.0-0.5"] += 1
            elif v < 0.7:
                buckets["0.5-0.7"] += 1
            elif v < 0.9:
                buckets["0.7-0.9"] += 1
            else:
                buckets["0.9-1.0"] += 1

        return {
            "buckets": buckets,
            "mean": statistics.mean(values),
            "min": min(values),
            "max": max(values),
            "count": len(values),
        }

    def get_token_usage(self, capability: Optional[str] = None) -> Dict[str, float]:
        """Get token usage statistics.

        Args:
            capability: Optional capability name to filter by

        Returns:
            Dict with total and mean token usage
        """
        all_records = []
        for key, records in self._records.items():
            if capability and not key.startswith(f"{capability}:"):
                continue
            all_records.extend([r for r in records if r.metric_type == MetricType.TOKEN_USAGE])

        if not all_records:
            return {}

        values = [r.value for r in all_records]

        return {
            "total": sum(values),
            "mean": statistics.mean(values),
            "count": len(values),
        }

    def _add_record(self, record: MetricRecord) -> None:
        """Add a record to the appropriate list.

        Args:
            record: Record to add
        """
        key = f"{record.capability}:{record.metric_type.value}"

        self._records[key].append(record)

        # Prune old records if we exceed max
        if len(self._records[key]) > self._max_records:
            self._records[key] = self._records[key][-self._max_records:]

    def _percentile(self, values: List[float], p: float) -> float:
        """Calculate percentile value.

        Args:
            values: List of values
            p: Percentile (0-100)

        Returns:
            Percentile value
        """
        if not values:
            return 0.0

        sorted_values = sorted(values)
        k = (len(sorted_values) - 1) * (p / 100)
        f = int(k)
        c = k - f

        if f + 1 < len(sorted_values):
            return sorted_values[f] + c * (sorted_values[f + 1] - sorted_values[f])
        return sorted_values[f]

    def get_summary(self) -> Dict[str, Any]:
        """Get a summary of all metrics.

        Returns:
            Dict with aggregated metrics
        """
        return {
            "call_counts": self.get_call_counts(),
            "error_counts": self.get_error_counts(),
            "capabilities": list(self._call_counts.keys()),
        }


class FailureArchiver:
    """Archive failed AI operations for analysis and prompt optimization."""

    def __init__(self, archive_path: Optional[Path] = None, max_records: int = 1000):
        """Initialize failure archiver.

        Args:
            archive_path: Path to JSONL archive file (default: .ai_failures.jsonl)
            max_records: Maximum records to keep in archive
        """
        self._archive_path = archive_path or Path(".ai_failures.jsonl")
        self._max_records = max_records
        self._records: List[Dict] = []
        self._load_existing()

    def _load_existing(self) -> None:
        """Load existing records from archive file."""
        if not self._archive_path.exists():
            return

        try:
            with open(self._archive_path, 'r') as f:
                for line in f:
                    if line.strip():
                        self._records.append(json.loads(line))
        except Exception as e:
            logger.warning(f"Failed to load existing failures: {e}")

    def archive_failure(
        self,
        capability: str,
        input_data: Any,
        error: Exception,
        context: Optional[Dict] = None,
    ) -> None:
        """Archive a failure record.

        Args:
            capability: Name of the capability
            input_data: Input that caused the failure
            error: Exception that occurred
            context: Optional additional context
        """
        record = {
            "capability": capability,
            "input_data": str(input_data),
            "error_type": type(error).__name__,
            "error_message": str(error),
            "timestamp": datetime.now().isoformat(),
            "context": context or {},
        }

        self._records.append(record)

        # Prune if too many
        if len(self._records) > self._max_records:
            self._records = self._records[-self._max_records:]

        # Write to file
        self._write_record(record)

    def _write_record(self, record: Dict) -> None:
        """Write a record to the archive file.

        Args:
            record: Record to write
        """
        try:
            with open(self._archive_path, 'a') as f:
                f.write(json.dumps(record) + '\n')
        except Exception as e:
            logger.error(f"Failed to write failure record: {e}")

    def get_failures(
        self,
        capability: Optional[str] = None,
        limit: int = 100,
    ) -> List[Dict]:
        """Get failure records.

        Args:
            capability: Optional capability name to filter by
            limit: Maximum records to return

        Returns:
            List of failure records
        """
        records = self._records

        if capability:
            records = [r for r in records if r.get("capability") == capability]

        return records[-limit:]

    def get_failure_summary(self) -> Dict[str, Any]:
        """Get summary of failures.

        Returns:
            Dict with failure statistics
        """
        error_counts: Dict[str, int] = defaultdict(int)
        capability_counts: Dict[str, int] = defaultdict(int)

        for record in self._records:
            error_counts[record.get("error_type", "unknown")] += 1
            capability_counts[record.get("capability", "unknown")] += 1

        return {
            "total_failures": len(self._records),
            "error_types": dict(error_counts),
            "capabilities": dict(capability_counts),
        }


__all__ = [
    "AIMetrics",
    "MetricType",
    "MetricRecord",
    "FailureArchiver",
]
