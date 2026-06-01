"""Trace analyzer for parsing and analyzing trace logs."""

import json
import time
from collections import defaultdict
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional

from ..utils.trace import TraceFileWriter


@dataclass
class SpanEvent:
    """A single span event from trace log."""

    type: str  # span_start, span_end, event, input, output
    timestamp: float
    trace_id: str
    span_id: Optional[str] = None
    parent_id: Optional[str] = None
    component: str = ""
    operation: str = ""
    status: Optional[str] = None  # success, error
    duration_ms: Optional[float] = None
    tags: Dict[str, Any] = field(default_factory=dict)
    metadata: Dict[str, Any] = field(default_factory=dict)
    event: Optional[str] = None
    data: Dict[str, Any] = field(default_factory=dict)

    @classmethod
    def from_dict(cls, data: Dict) -> "SpanEvent":
        """Create SpanEvent from dictionary."""
        return cls(
            type=data.get("type", ""),
            timestamp=data.get("timestamp", 0),
            trace_id=data.get("trace_id", ""),
            span_id=data.get("span_id"),
            parent_id=data.get("parent_id"),
            component=data.get("component", ""),
            operation=data.get("operation", ""),
            status=data.get("status"),
            duration_ms=data.get("duration_ms"),
            tags=data.get("tags", {}),
            metadata=data.get("metadata", {}),
            event=data.get("event"),
            data=data.get("data", {}),
        )


@dataclass
class TraceSession:
    """A complete trace session with all its spans."""

    trace_id: str
    start_time: float
    end_time: float
    spans: List[SpanEvent] = field(default_factory=list)
    events: List[SpanEvent] = field(default_factory=list)

    @property
    def duration_ms(self) -> float:
        """Total session duration in milliseconds."""
        return (self.end_time - self.start_time) * 1000

    @property
    def span_count(self) -> int:
        """Number of spans in this session."""
        return len([s for s in self.spans if s.type == "span_start"])

    def get_spans_by_component(self, component: str) -> List[SpanEvent]:
        """Get all spans for a specific component."""
        return [s for s in self.spans if s.component == component]

    def get_events_by_name(self, event_name: str) -> List[SpanEvent]:
        """Get all events with a specific name."""
        return [e for e in self.events if e.event == event_name]

    def get_span_tree(self) -> Dict[str, Any]:
        """Build a tree structure of spans based on parent-child relationships."""
        span_map = {}
        for span in self.spans:
            if span.type == "span_start":
                span_map[span.span_id] = {
                    "span": span,
                    "children": [],
                }

        # Build tree structure
        root = None
        for span_id, span_data in span_map.items():
            span = span_data["span"]
            if span.parent_id is None:
                root = span_data
            elif span.parent_id in span_map:
                span_map[span.parent_id]["children"].append(span_data)

        return root


class TraceAnalyzer:
    """Analyzer for trace logs."""

    def __init__(self, trace_dir: Path = Path(".traces")):
        """Initialize trace analyzer.

        Args:
            trace_dir: Directory containing trace files
        """
        self.trace_dir = trace_dir
        self.sessions: Dict[str, TraceSession] = {}

    def load_all_traces(self) -> Dict[str, TraceSession]:
        """Load all trace files and build sessions.

        Returns:
            Dictionary mapping trace_id to TraceSession
        """
        if not self.trace_dir.exists():
            return {}

        trace_files = list(self.trace_dir.glob("*.jsonl"))
        all_events = []

        # Load all events from all files
        for trace_file in trace_files:
            try:
                with open(trace_file, "r", encoding="utf-8") as f:
                    for line in f:
                        line = line.strip()
                        if line:
                            try:
                                event_data = json.loads(line)
                                event = SpanEvent.from_dict(event_data)
                                all_events.append(event)
                            except json.JSONDecodeError:
                                continue
            except Exception:
                continue

        # Group by trace_id and build sessions
        sessions_by_trace: Dict[str, List[SpanEvent]] = defaultdict(list)
        for event in all_events:
            sessions_by_trace[event.trace_id].append(event)

        # Build TraceSession objects
        self.sessions = {}
        for trace_id, events in sessions_by_trace.items():
            if not events:
                continue

            # Sort by timestamp
            events.sort(key=lambda e: e.timestamp)

            session = TraceSession(
                trace_id=trace_id,
                start_time=min(e.timestamp for e in events),
                end_time=max(e.timestamp for e in events),
            )

            for event in events:
                if event.type in ("span_start", "span_end"):
                    session.spans.append(event)
                elif event.type == "event":
                    session.events.append(event)

            self.sessions[trace_id] = session

        return self.sessions

    def get_session(self, trace_id: str) -> Optional[TraceSession]:
        """Get a specific trace session.

        Args:
            trace_id: Trace identifier

        Returns:
            TraceSession if found, None otherwise
        """
        if not self.sessions:
            self.load_all_traces()
        return self.sessions.get(trace_id)

    def get_all_sessions(self) -> List[TraceSession]:
        """Get all trace sessions.

        Returns:
            List of all TraceSession objects
        """
        if not self.sessions:
            self.load_all_traces()
        return list(self.sessions.values())

    def analyze_component_performance(self) -> Dict[str, Dict]:
        """Analyze performance metrics by component.

        Returns:
            Dictionary mapping component name to performance metrics
        """
        component_stats: Dict[str, Dict] = defaultdict(lambda: {
            "call_count": 0,
            "total_duration_ms": 0,
            "avg_duration_ms": 0,
            "max_duration_ms": 0,
            "min_duration_ms": float("inf"),
            "error_count": 0,
        })

        for session in self.get_all_sessions():
            for span in session.spans:
                if span.type == "span_end" and span.duration_ms:
                    stats = component_stats[span.component]
                    stats["call_count"] += 1
                    stats["total_duration_ms"] += span.duration_ms
                    stats["max_duration_ms"] = max(stats["max_duration_ms"], span.duration_ms)
                    stats["min_duration_ms"] = min(stats["min_duration_ms"], span.duration_ms)

                    if span.status == "error":
                        stats["error_count"] += 1

        # Calculate averages
        for component, stats in component_stats.items():
            if stats["call_count"] > 0:
                stats["avg_duration_ms"] = stats["total_duration_ms"] / stats["call_count"]
            if stats["min_duration_ms"] == float("inf"):
                stats["min_duration_ms"] = 0

        return dict(component_stats)

    def get_slowest_operations(self, limit: int = 10) -> List[Dict]:
        """Get the slowest operations across all traces.

        Args:
            limit: Maximum number of results

        Returns:
            List of slowest operations with details
        """
        operations = []

        for session in self.get_all_sessions():
            for span in session.spans:
                if span.type == "span_end" and span.duration_ms:
                    operations.append({
                        "component": span.component,
                        "operation": span.operation,
                        "duration_ms": span.duration_ms,
                        "trace_id": span.trace_id,
                        "timestamp": datetime.fromtimestamp(span.timestamp).isoformat(),
                        "status": span.status,
                    })

        # Sort by duration descending
        operations.sort(key=lambda x: x["duration_ms"], reverse=True)
        return operations[:limit]

    def get_trace_timeline(self, trace_id: str) -> List[Dict]:
        """Get timeline of events for a specific trace.

        Args:
            trace_id: Trace identifier

        Returns:
            List of events in chronological order
        """
        session = self.get_session(trace_id)
        if not session:
            return []

        timeline = []
        all_events = session.spans + session.events
        all_events.sort(key=lambda e: e.timestamp)

        for event in all_events:
            timeline.append({
                "type": event.type,
                "timestamp": datetime.fromtimestamp(event.timestamp).isoformat(),
                "component": event.component,
                "operation": event.operation,
                "event": event.event,
                "duration_ms": event.duration_ms,
                "status": event.status,
                "data": event.data,
            })

        return timeline


__all__ = [
    "SpanEvent",
    "TraceSession",
    "TraceAnalyzer",
]
