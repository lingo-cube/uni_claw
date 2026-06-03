"""Unit tests for TraceIntegration class."""

import pytest
from unittest.mock import Mock, patch

from src.ai.trace.integration import TraceIntegration
from src.ai.trace.models import SpanContext, SpanStatus


class TestTraceIntegration:
    """Test TraceIntegration class."""

    @pytest.fixture
    def integration(self):
        """Create a TraceIntegration instance."""
        return TraceIntegration(enable_auto=False)

    def test_initialization(self, integration):
        """Test integration initializes correctly."""
        assert integration.enable_auto is False
        assert len(integration._active_spans) == 0
        assert len(integration._completed_traces) == 0

    def test_start_span(self, integration):
        """Test starting a new span."""
        span = integration.start_span("test_operation", tags={"key": "value"})

        assert span.span_id.startswith("test_operation_")
        assert span.tags == {"key": "value"}
        assert span.status == SpanStatus.ACTIVE
        assert span.span_id in integration._active_spans

    def test_start_span_with_parent(self, integration):
        """Test starting a span with a parent."""
        parent = integration.start_span("parent_operation")
        child = integration.start_span(
            "child_operation", parent_context=parent
        )

        assert child.parent_span_id == parent.span_id

    def test_inject_context(self, integration):
        """Test injecting context into a span."""
        span = integration.start_span("test_operation")
        custom_context = {"user_id": "123", "session_id": "abc"}

        integration.inject_context(span, custom_context)

        assert span.custom_context == custom_context

    def test_finish_span_success(self, integration):
        """Test finishing a span successfully."""
        span = integration.start_span("test_operation")

        integration.finish_span(span, result={"key": "value"})

        assert span.status == SpanStatus.FINISHED
        assert span.span_id not in integration._active_spans

    def test_finish_span_error(self, integration):
        """Test finishing a span with error."""
        span = integration.start_span("test_operation")
        error = Exception("Test error")

        integration.finish_span(span, error=error)

        assert span.status == SpanStatus.ERROR

    def test_finish_span_not_found(self, integration):
        """Test finishing a span that doesn't exist."""
        span = SpanContext(span_id="nonexistent")

        # Should not raise, just log warning
        integration.finish_span(span)

        assert span.status == SpanStatus.ACTIVE  # Unchanged

    def test_record_metrics(self, integration):
        """Test recording metrics."""
        integration.record_metrics(
            capability="analyze_visual",
            provider_id="claude",
            mode="vision",
            latency_ms=150.0,
            tokens={"input": 100, "output": 50},
            success=True,
        )

        metrics = integration.get_provider_metrics("claude", "vision")

        assert metrics is not None
        assert metrics.total_calls == 1
        assert metrics.successful_calls == 1
        assert metrics.total_tokens == 150

    def test_record_metrics_failure(self, integration):
        """Test recording metrics for failed call."""
        integration.record_metrics(
            capability="test",
            provider_id="test",
            mode="text",
            latency_ms=100.0,
            tokens={"input": 50, "output": 0},
            success=False,
        )

        metrics = integration.get_provider_metrics("test", "text")

        assert metrics.total_calls == 1
        assert metrics.failed_calls == 1
        assert metrics.successful_calls == 0

    def test_get_active_spans(self, integration):
        """Test getting active spans."""
        span1 = integration.start_span("operation1")
        span2 = integration.start_span("operation2")

        active = integration.get_active_spans()

        assert len(active) == 2
        assert span1 in active
        assert span2 in active

    def test_get_active_spans_empty(self, integration):
        """Test getting active spans when none exist."""
        active = integration.get_active_spans()

        assert active == []

    def test_get_provider_metrics(self, integration):
        """Test getting provider metrics."""
        integration.record_metrics(
            capability="test",
            provider_id="claude",
            mode="vision",
            latency_ms=100,
            tokens={"input": 50, "output": 50},
        )

        # Get specific mode
        metrics = integration.get_provider_metrics("claude", "vision")
        assert metrics is not None
        assert metrics.total_calls == 1

        # Get all modes
        all_metrics = integration.get_provider_metrics("claude")
        assert "vision" in all_metrics

    def test_get_provider_metrics_nonexistent(self, integration):
        """Test getting metrics for nonexistent provider."""
        metrics = integration.get_provider_metrics("nonexistent")

        assert metrics is None

    def test_get_all_metrics(self, integration):
        """Test getting all metrics."""
        integration.record_metrics("test1", "provider1", "text", 100, {"input": 50, "output": 50})
        integration.record_metrics("test2", "provider2", "vision", 100, {"input": 50, "output": 50})

        all = integration.get_all_metrics()

        assert "provider1" in all
        assert "provider2" in all

    def test_clear_metrics(self, integration):
        """Test clearing all metrics."""
        integration.record_metrics("test", "provider", "text", 100, {"input": 50, "output": 50})

        integration.clear_metrics()

        assert len(integration.get_all_metrics()) == 0

    def test_create_trace(self, integration):
        """Test creating a new trace."""
        trace = integration.create_trace(
            capability="analyze_visual",
            provider_id="claude",
            mode="vision",
        )

        assert trace.trace_id.startswith("trace_")
        assert trace.capability == "analyze_visual"
        assert trace.provider_id == "claude"
        assert trace.mode == "vision"
        assert trace in integration._completed_traces

    def test_health_check(self, integration):
        """Test health check."""
        health = integration.health_check()

        assert health["healthy"] is True
        assert "active_spans" in health
        assert "providers_tracked" in health

    def test_health_check_with_activity(self, integration):
        """Test health check with active spans."""
        integration.start_span("test")
        integration.record_metrics("test", "provider", "text", 100, {"input": 50, "output": 50})

        health = integration.health_check()

        assert health["active_spans"] == 1
        assert health["providers_tracked"] == 1


class TestTraceIntegrationWithMock:
    """Test TraceIntegration with mocked TraceLogger."""

    @pytest.fixture
    def mock_trace_logger(self):
        """Create a mock TraceLogger."""
        mock_logger = Mock()
        mock_logger.log_event = Mock()
        mock_logger.log_metric = Mock()
        return mock_logger

    @pytest.fixture
    def integration_with_logger(self, mock_trace_logger):
        """Create integration with mock logger."""
        return TraceIntegration(trace_logger=mock_trace_logger, enable_auto=True)

    def test_start_span_logs_to_logger(self, integration_with_logger, mock_trace_logger):
        """Test that starting span logs to TraceLogger."""
        span = integration_with_logger.start_span("test_op", tags={"key": "value"})

        assert mock_trace_logger.log_event.called
        call_args = mock_trace_logger.log_event.call_args
        assert call_args[0][0] == "span_start"
        assert "span_id" in call_args[0][1]

    def test_finish_span_logs_to_logger(self, integration_with_logger, mock_trace_logger):
        """Test that finishing span logs to TraceLogger."""
        span = integration_with_logger.start_span("test_op")
        integration_with_logger.finish_span(span, result={"data": "test"})

        assert mock_trace_logger.log_event.call_count >= 2  # start + finish
        finish_call = mock_trace_logger.log_event.call_args_list[-1]
        assert finish_call[0][0] == "span_finish"

    def test_record_metrics_logs_to_logger(self, integration_with_logger, mock_trace_logger):
        """Test that recording metrics logs to TraceLogger."""
        integration_with_logger.record_metrics(
            capability="test",
            provider_id="provider",
            mode="text",
            latency_ms=100,
            tokens={"input": 50, "output": 50},
        )

        assert mock_trace_logger.log_metric.called
        call_args = mock_trace_logger.log_metric.call_args
        assert call_args[0][0] == "ai_call_metrics"

    def test_trace_logger_connection_status(self, integration_with_logger):
        """Test that TraceLogger connection status is reflected."""
        health = integration_with_logger.health_check()

        assert health["trace_logger_connected"] is True
        assert health["auto_enabled"] is True
