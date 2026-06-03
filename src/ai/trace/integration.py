"""Trace integration for AI provider calls.

This module provides distributed tracing integration for monitoring
AI provider calls, collecting metrics, and managing span contexts.
"""

import logging
import time
import uuid
from typing import Dict, Any, Optional, List
from datetime import datetime

from .models import (
    SpanContext,
    AICallTrace,
    ProviderPerformanceMetrics,
    SpanStatus,
)

logger = logging.getLogger(__name__)


class TraceIntegration:
    """Integration layer for distributed tracing of AI calls.

    This class provides:
    - Span lifecycle management
    - Context injection for custom metadata
    - Automatic metrics collection
    - Integration with existing tracing infrastructure
    """

    def __init__(self, trace_logger=None, enable_auto: bool = True):
        """Initialize the trace integration.

        Args:
            trace_logger: Optional existing trace logger to integrate with
            enable_auto: Whether to enable automatic tracing
        """
        self.trace_logger = trace_logger
        self.enable_auto = enable_auto
        self._active_spans: Dict[str, SpanContext] = {}
        self._completed_traces: List[AICallTrace] = []
        self._provider_metrics: Dict[str, Dict[str, ProviderPerformanceMetrics]] = {}

        # Try to use existing TraceLogger if available
        if self.trace_logger is None and enable_auto:
            self._initialize_trace_logger()

        logger.info(
            f"TraceIntegration initialized (auto={'enabled' if enable_auto else 'disabled'})"
        )

    def _initialize_trace_logger(self) -> None:
        """Try to initialize the existing TraceLogger."""
        try:
            from src.utils.trace import TraceLogger
            self.trace_logger = TraceLogger("unibrain")
            logger.info("Connected to existing TraceLogger")
        except (ImportError, Exception) as e:
            logger.debug(f"Could not connect to TraceLogger: {e}")
            self.enable_auto = False

    def start_span(
        self,
        operation: str,
        tags: Optional[Dict[str, Any]] = None,
        parent_context: Optional[SpanContext] = None,
    ) -> SpanContext:
        """Start a new trace span.

        Args:
            operation: Name of the operation being traced
            tags: Optional tags for categorization
            parent_context: Optional parent span for nesting

        Returns:
            SpanContext: The created span context
        """
        span_id = f"{operation}_{uuid.uuid4().hex[:8]}"

        span_context = SpanContext(
            span_id=span_id,
            parent_span_id=parent_context.span_id if parent_context else None,
            tags=tags or {},
            status=SpanStatus.ACTIVE,
        )

        self._active_spans[span_id] = span_context

        # Log to TraceLogger if available
        if self.trace_logger:
            try:
                self.trace_logger.log_event(
                    "span_start",
                    {
                        "span_id": span_id,
                        "operation": operation,
                        "tags": tags,
                        "parent_id": parent_context.span_id if parent_context else None,
                    },
                )
            except Exception as e:
                logger.debug(f"Failed to log to TraceLogger: {e}")

        logger.debug(f"Started span: {span_id} for operation: {operation}")
        return span_context

    def inject_context(
        self, span_context: SpanContext, custom_context: Dict[str, Any]
    ) -> None:
        """Inject custom context into a span.

        Args:
            span_context: The span to inject context into
            custom_context: Key-value context data
        """
        span_context.custom_context.update(custom_context)
        logger.debug(f"Injected custom context into span: {span_context.span_id}")

    def finish_span(
        self,
        span_context: SpanContext,
        result: Any = None,
        error: Optional[Exception] = None,
    ) -> None:
        """Finish a span and record its result.

        Args:
            span_context: The span to finish
            result: Optional result of the operation
            error: Optional error if operation failed
        """
        if span_context.span_id not in self._active_spans:
            logger.warning(f"Span not found: {span_context.span_id}")
            return

        duration_ms = span_context.duration_ms

        if error:
            span_context.error()
            logger.error(
                f"Span {span_context.span_id} failed after {duration_ms:.0f}ms: {error}"
            )
        else:
            span_context.finish()
            logger.debug(
                f"Span {span_context.span_id} completed in {duration_ms:.0f}ms"
            )

        # Log to TraceLogger if available
        if self.trace_logger:
            try:
                self.trace_logger.log_event(
                    "span_finish",
                    {
                        "span_id": span_context.span_id,
                        "duration_ms": duration_ms,
                        "success": error is None,
                        "error": str(error) if error else None,
                    },
                )
            except Exception as e:
                logger.debug(f"Failed to log to TraceLogger: {e}")

        # Move from active to completed
        del self._active_spans[span_context.span_id]

    def record_metrics(
        self,
        capability: str,
        provider_id: str,
        mode: str,
        latency_ms: float,
        tokens: Dict[str, int],
        success: bool = True,
    ) -> None:
        """Record metrics for an AI call.

        Args:
            capability: The capability that was called
            provider_id: Which provider was used
            mode: The mode (text, vision, multimodal)
            latency_ms: Request latency in milliseconds
            tokens: Token counts {"input": int, "output": int}
            success: Whether the call succeeded
        """
        total_tokens = tokens.get("input", 0) + tokens.get("output", 0)

        # Update provider metrics
        if provider_id not in self._provider_metrics:
            self._provider_metrics[provider_id] = {}

        if mode not in self._provider_metrics[provider_id]:
            self._provider_metrics[provider_id][mode] = ProviderPerformanceMetrics(
                provider_id=provider_id, mode=mode
            )

        metrics = self._provider_metrics[provider_id][mode]
        metrics.record_call(latency_ms, total_tokens, success)

        logger.info(
            f"[Metrics] {capability} via {provider_id} ({mode}): "
            f"{latency_ms:.0f}ms, {tokens.get('input', 0)}+{tokens.get('output', 0)} tokens, "
            f"success={success}"
        )

        # Log to TraceLogger if available
        if self.trace_logger:
            try:
                self.trace_logger.log_metric(
                    "ai_call_metrics",
                    {
                        "capability": capability,
                        "provider_id": provider_id,
                        "mode": mode,
                        "latency_ms": latency_ms,
                        "input_tokens": tokens.get("input", 0),
                        "output_tokens": tokens.get("output", 0),
                        "total_tokens": total_tokens,
                        "success": success,
                        "timestamp": datetime.now().isoformat(),
                    },
                )
            except Exception as e:
                logger.debug(f"Failed to log to TraceLogger: {e}")

    def get_active_spans(self) -> List[SpanContext]:
        """Get all currently active spans.

        Returns:
            List of active span contexts
        """
        return list(self._active_spans.values())

    def get_provider_metrics(
        self, provider_id: str, mode: Optional[str] = None
    ) -> Optional[ProviderPerformanceMetrics | Dict[str, ProviderPerformanceMetrics]]:
        """Get metrics for a provider.

        Args:
            provider_id: Provider to get metrics for
            mode: Optional mode to filter by

        Returns:
            Metrics object or dict of metrics by mode
        """
        if provider_id not in self._provider_metrics:
            return None

        if mode:
            return self._provider_metrics[provider_id].get(mode)
        return self._provider_metrics[provider_id]

    def get_all_metrics(self) -> Dict[str, Dict[str, ProviderPerformanceMetrics]]:
        """Get all provider metrics.

        Returns:
            Dict of provider_id -> mode -> metrics
        """
        return self._provider_metrics.copy()

    def clear_metrics(self) -> None:
        """Clear all collected metrics."""
        self._provider_metrics.clear()
        logger.info("Cleared all metrics")

    def create_trace(
        self, capability: str, provider_id: str, mode: str
    ) -> AICallTrace:
        """Create a new AI call trace.

        Args:
            capability: The capability being called
            provider_id: Which provider is being used
            mode: The mode (text, vision, multimodal)

        Returns:
            AICallTrace: The created trace
        """
        trace_id = f"trace_{uuid.uuid4().hex[:12]}"
        trace = AICallTrace(
            trace_id=trace_id,
            capability=capability,
            provider_id=provider_id,
            mode=mode,
        )
        self._completed_traces.append(trace)
        return trace

    def health_check(self) -> Dict[str, Any]:
        """Check the health of the trace integration.

        Returns:
            Dict with health status information
        """
        return {
            "healthy": True,
            "active_spans": len(self._active_spans),
            "completed_traces": len(self._completed_traces),
            "providers_tracked": len(self._provider_metrics),
            "trace_logger_connected": self.trace_logger is not None,
            "auto_enabled": self.enable_auto,
        }
