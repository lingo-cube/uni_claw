"""Trace logging utilities for monitoring and debugging.

Provides structured logging with trace ID propagation through the pipeline.
"""

import logging
import time
import uuid
from contextlib import contextmanager
from dataclasses import dataclass, field
from typing import Any, Dict, Optional
from pathlib import Path
from threading import Lock

logger = logging.getLogger(__name__)

# Global trace writer instance
_trace_writer: Optional["TraceFileWriter"] = None
_trace_writer_lock = Lock()


@dataclass
class TraceContext:
    """Trace context for tracking operations through the pipeline."""

    trace_id: str = field(default_factory=lambda: str(uuid.uuid4())[:8])
    parent_id: Optional[str] = None
    span_id: str = field(default_factory=lambda: str(uuid.uuid4())[:8])
    component: str = ""
    operation: str = ""
    start_time: float = field(default_factory=time.time)
    tags: Dict[str, Any] = field(default_factory=dict)
    metadata: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict:
        """Convert to dictionary for logging."""
        return {
            "trace_id": self.trace_id,
            "parent_id": self.parent_id,
            "span_id": self.span_id,
            "component": self.component,
            "operation": self.operation,
            "duration_ms": (time.time() - self.start_time) * 1000,
            "tags": self.tags,
            "metadata": self.metadata,
        }


class TraceLogger:
    """Structured logger with trace context and file writing."""

    def __init__(self, component: str):
        """Initialize trace logger for a component.

        Args:
            component: Component name (e.g., "parse", "vision", "adb")
        """
        self.component = component
        self._context: Optional[TraceContext] = None
        self._writer: Optional["TraceFileWriter"] = self._get_writer()

    def _get_writer(self) -> Optional["TraceFileWriter"]:
        """Get global trace writer instance."""
        global _trace_writer
        if _trace_writer is None:
            with _trace_writer_lock:
                if _trace_writer is None:
                    try:
                        _trace_writer = TraceFileWriter()
                    except Exception:
                        logger.debug("Trace file writer not available")
                        return None
        return _trace_writer

    @property
    def context(self) -> Optional[TraceContext]:
        """Get current trace context."""
        return self._context

    def start_span(
        self,
        operation: str,
        parent_context: Optional[TraceContext] = None,
        **tags
    ) -> TraceContext:
        """Start a new trace span.

        Args:
            operation: Operation name (e.g., "parse_instruction", "analyze_screen")
            parent_context: Parent trace context for chaining
            **tags: Additional tags for the span

        Returns:
            New trace context
        """
        context = TraceContext(
            component=self.component,
            operation=operation,
            tags=tags,
        )

        if parent_context:
            context.trace_id = parent_context.trace_id
            context.parent_id = parent_context.span_id

        self._context = context
        logger.info(
            f"[{context.trace_id}] START {self.component}.{operation}",
            extra={"trace": context.to_dict()}
        )

        # Write to file
        if self._writer:
            self._writer.append_span(context.trace_id, {
                "type": "span_start",
                "timestamp": time.time(),
                **context.to_dict()
            })

        return context

    def finish_span(
        self,
        context: TraceContext,
        result: Optional[Any] = None,
        error: Optional[Exception] = None
    ):
        """Finish a trace span and log the result.

        Args:
            context: Trace context to finish
            result: Operation result (will be sanitized)
            error: Error if operation failed
        """
        context.metadata["success"] = error is None
        if error:
            context.metadata["error_type"] = type(error).__name__
            context.metadata["error_message"] = str(error)

        if result is not None:
            context.metadata["result_type"] = type(result).__name__
            context.metadata["has_result"] = True

        duration = (time.time() - context.start_time) * 1000
        span_data = {
            "type": "span_end",
            "timestamp": time.time(),
            **context.to_dict()
        }

        if error:
            logger.error(
                f"[{context.trace_id}] FAIL {self.component}.{context.operation} ({duration:.0f}ms)",
                extra={"trace": context.to_dict()},
                exc_info=True
            )
            span_data["status"] = "error"
        else:
            logger.info(
                f"[{context.trace_id}] DONE {self.component}.{context.operation} ({duration:.0f}ms)",
                extra={"trace": context.to_dict()}
            )
            span_data["status"] = "success"

        # Write to file
        if self._writer:
            self._writer.append_span(context.trace_id, span_data)

    def log_input(self, context: TraceContext, **data):
        """Log operation input.

        Args:
            context: Trace context
            **data: Input data to log
        """
        logger.debug(
            f"[{context.trace_id}] INPUT {self.component}.{context.operation}",
            extra={"trace": context.to_dict(), "input": data}
        )

        # Write to file
        if self._writer:
            self._writer.append_span(context.trace_id, {
                "type": "input",
                "timestamp": time.time(),
                "trace_id": context.trace_id,
                "span_id": context.span_id,
                "component": self.component,
                "operation": context.operation,
                "data": data
            })

    def log_output(self, context: TraceContext, **data):
        """Log operation output.

        Args:
            context: Trace context
            **data: Output data to log (sanitized)
        """
        sanitized = self._sanitize_output(data)
        logger.debug(
            f"[{context.trace_id}] OUTPUT {self.component}.{context.operation}",
            extra={"trace": context.to_dict(), "output": sanitized}
        )

        # Write to file
        if self._writer:
            self._writer.append_span(context.trace_id, {
                "type": "output",
                "timestamp": time.time(),
                "trace_id": context.trace_id,
                "span_id": context.span_id,
                "component": self.component,
                "operation": context.operation,
                "data": sanitized
            })

    def log_event(self, context: TraceContext, event: str, **data):
        """Log a custom event.

        Args:
            context: Trace context
            event: Event name
            **data: Event data
        """
        logger.info(
            f"[{context.trace_id}] EVENT {self.component}.{event}",
            extra={"trace": context.to_dict(), "event": event, "event_data": data}
        )

        # Write to file
        if self._writer:
            self._writer.append_span(context.trace_id, {
                "type": "event",
                "timestamp": time.time(),
                "trace_id": context.trace_id,
                "span_id": context.span_id,
                "component": self.component,
                "event": event,
                "data": data
            })

    def _sanitize_output(self, data: Any) -> Any:
        """Sanitize output data for logging.

        Args:
            data: Raw output data

        Returns:
            Sanitized data safe for logging
        """
        if isinstance(data, dict):
            return {k: self._sanitize_output(v) for k, v in data.items()}
        elif isinstance(data, list):
            return [self._sanitize_output(item) for item in data[:3]]  # Limit arrays
        elif isinstance(data, str):
            return data[:200]  # Truncate strings
        else:
            return data

    @contextmanager
    def span(self, operation: str, parent_context: Optional[TraceContext] = None, **tags):
        """Context manager for automatic span lifecycle.

        Args:
            operation: Operation name
            parent_context: Parent trace context
            **tags: Additional tags

        Yields:
            TraceContext for the span
        """
        context = self.start_span(operation, parent_context, **tags)
        try:
            yield context
            self.finish_span(context)
        except Exception as e:
            self.finish_span(context, error=e)
            raise


class TraceFileWriter:
    """Write trace logs to file for analysis."""

    def __init__(self, log_dir: Path = Path(".traces")):
        """Initialize trace file writer.

        Args:
            log_dir: Directory to write trace files
        """
        self.log_dir = log_dir
        try:
            self.log_dir.mkdir(exist_ok=True)
        except Exception as e:
            logger.warning(f"Cannot create trace directory: {e}")

    def write_trace(self, trace_id: str, data: Dict):
        """Write trace data to file.

        Args:
            trace_id: Trace identifier
            data: Trace data to write
        """
        try:
            trace_file = self.log_dir / f"{trace_id}.json"
            import json
            with open(trace_file, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=2, ensure_ascii=False)
        except Exception as e:
            logger.warning(f"Failed to write trace file: {e}")

    def append_span(self, trace_id: str, span_data: Dict):
        """Append span data to trace file.

        Args:
            trace_id: Trace identifier
            span_data: Span data to append
        """
        try:
            trace_file = self.log_dir / f"{trace_id}.jsonl"
            import json
            with open(trace_file, "a", encoding="utf-8") as f:
                f.write(json.dumps(span_data, ensure_ascii=False) + "\n")
        except Exception as e:
            logger.warning(f"Failed to append span data: {e}")


def get_trace_writer() -> TraceFileWriter:
    """Get global trace writer instance."""
    global _trace_writer
    if _trace_writer is None:
        with _trace_writer_lock:
            if _trace_writer is None:
                _trace_writer = TraceFileWriter()
    return _trace_writer


def enable_trace_writing(log_dir: Optional[Path] = None):
    """Enable trace file writing.

    Args:
        log_dir: Optional custom log directory
    """
    global _trace_writer
    with _trace_writer_lock:
        _trace_writer = TraceFileWriter(log_dir or Path(".traces"))


__all__ = [
    "TraceContext",
    "TraceLogger",
    "TraceFileWriter",
    "get_trace_writer",
    "enable_trace_writing",
]
