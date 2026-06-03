"""Unit tests for trace data models."""

import time
import pytest

from src.ai.trace.models import (
    SpanContext,
    AICallTrace,
    ProviderPerformanceMetrics,
    SpanStatus,
)


class TestSpanContext:
    """Test SpanContext dataclass."""

    def test_span_creation(self):
        """Test creating a span context."""
        span = SpanContext(span_id="test_span")

        assert span.span_id == "test_span"
        assert span.parent_span_id is None
        assert span.tags == {}
        assert span.custom_context == {}
        assert span.status == SpanStatus.ACTIVE

    def test_span_with_parent(self):
        """Test creating a span with a parent."""
        span = SpanContext(
            span_id="child_span",
            parent_span_id="parent_span",
        )

        assert span.parent_span_id == "parent_span"

    def test_span_duration(self):
        """Test span duration calculation."""
        span = SpanContext(span_id="test_span")
        time.sleep(0.1)

        duration = span.duration_ms

        assert duration >= 90  # At least 100ms with some tolerance

    def test_span_is_active(self):
        """Test is_active property."""
        span = SpanContext(span_id="test_span")

        assert span.is_active is True

        span.finish()
        assert span.is_active is False

    def test_span_finish(self):
        """Test marking span as finished."""
        span = SpanContext(span_id="test_span")
        span.finish()

        assert span.status == SpanStatus.FINISHED

    def test_span_error(self):
        """Test marking span as errored."""
        span = SpanContext(span_id="test_span")
        span.error()

        assert span.status == SpanStatus.ERROR

    def test_span_to_dict(self):
        """Test serializing span to dict."""
        span = SpanContext(
            span_id="test_span",
            tags={"key": "value"},
        )

        data = span.to_dict()

        assert data["span_id"] == "test_span"
        assert data["tags"] == {"key": "value"}
        assert "duration_ms" in data

    def test_span_from_dict(self):
        """Test creating span from dict."""
        data = {
            "span_id": "test_span",
            "parent_span_id": None,
            "start_time": time.time(),
            "tags": {},
            "custom_context": {},
            "status": "active",
        }

        span = SpanContext.from_dict(data)

        assert span.span_id == "test_span"
        assert span.status == SpanStatus.ACTIVE


class TestAICallTrace:
    """Test AICallTrace dataclass."""

    def test_trace_creation(self):
        """Test creating an AI call trace."""
        trace = AICallTrace(
            trace_id="test_trace",
            capability="analyze_visual",
            provider_id="claude",
            mode="vision",
        )

        assert trace.trace_id == "test_trace"
        assert trace.capability == "analyze_visual"
        assert trace.provider_id == "claude"
        assert trace.mode == "vision"
        assert trace.success is True

    def test_trace_duration_before_finish(self):
        """Test duration before trace is finished."""
        trace = AICallTrace(
            trace_id="test_trace",
            capability="test",
            provider_id="test",
            mode="text",
        )

        assert trace.duration_ms is None

    def test_trace_duration_after_finish(self):
        """Test duration after trace is finished."""
        trace = AICallTrace(
            trace_id="test_trace",
            capability="test",
            provider_id="test",
            mode="text",
        )
        time.sleep(0.1)
        trace.finish()

        assert trace.duration_ms >= 90

    def test_trace_total_tokens(self):
        """Test total tokens calculation."""
        trace = AICallTrace(
            trace_id="test_trace",
            capability="test",
            provider_id="test",
            mode="text",
            input_tokens=100,
            output_tokens=50,
        )

        assert trace.total_tokens == 150

    def test_trace_finish_success(self):
        """Test finishing trace successfully."""
        trace = AICallTrace(
            trace_id="test_trace",
            capability="test",
            provider_id="test",
            mode="text",
        )

        trace.finish(success=True)

        assert trace.success is True
        assert trace.end_time is not None
        assert trace.error_message is None

    def test_trace_finish_error(self):
        """Test finishing trace with error."""
        trace = AICallTrace(
            trace_id="test_trace",
            capability="test",
            provider_id="test",
            mode="text",
        )

        trace.finish(success=False, error_message="API error")

        assert trace.success is False
        assert trace.error_message == "API error"

    def test_trace_add_span(self):
        """Test adding a span to trace."""
        trace = AICallTrace(
            trace_id="test_trace",
            capability="test",
            provider_id="test",
            mode="text",
        )

        span = SpanContext(span_id="test_span")
        trace.add_span(span)

        assert "test_span" in trace.span_contexts
        assert trace.span_contexts["test_span"] == span

    def test_trace_to_dict(self):
        """Test serializing trace to dict."""
        trace = AICallTrace(
            trace_id="test_trace",
            capability="test",
            provider_id="test",
            mode="text",
            input_tokens=100,
        )

        trace.finish()

        data = trace.to_dict()

        assert data["trace_id"] == "test_trace"
        assert data["total_tokens"] == 100
        assert "duration_ms" in data


class TestProviderPerformanceMetrics:
    """Test ProviderPerformanceMetrics dataclass."""

    def test_metrics_creation(self):
        """Test creating metrics."""
        metrics = ProviderPerformanceMetrics(
            provider_id="claude",
            mode="vision",
        )

        assert metrics.provider_id == "claude"
        assert metrics.mode == "vision"
        assert metrics.total_calls == 0
        assert metrics.successful_calls == 0

    def test_success_rate_no_calls(self):
        """Test success rate with no calls."""
        metrics = ProviderPerformanceMetrics(
            provider_id="test",
            mode="test",
        )

        assert metrics.success_rate == 0.0

    def test_success_rate_with_calls(self):
        """Test success rate with calls."""
        metrics = ProviderPerformanceMetrics(
            provider_id="test",
            mode="test",
        )

        metrics.record_call(100, 50, success=True)
        metrics.record_call(100, 50, success=False)

        assert metrics.success_rate == 0.5

    def test_avg_tokens_per_call(self):
        """Test average tokens per call."""
        metrics = ProviderPerformanceMetrics(
            provider_id="test",
            mode="test",
        )

        metrics.record_call(100, 150, success=True)
        metrics.record_call(100, 250, success=True)

        assert metrics.avg_tokens_per_call == 200.0

    def test_record_call_updates_metrics(self):
        """Test that recording a call updates metrics."""
        metrics = ProviderPerformanceMetrics(
            provider_id="test",
            mode="test",
        )

        metrics.record_call(latency_ms=150, tokens=300, success=True)

        assert metrics.total_calls == 1
        assert metrics.successful_calls == 1
        assert metrics.total_tokens == 300
        assert metrics.total_latency_ms == 150.0
        assert metrics.avg_latency_ms == 150.0

    def test_record_failed_call(self):
        """Test recording a failed call."""
        metrics = ProviderPerformanceMetrics(
            provider_id="test",
            mode="test",
        )

        metrics.record_call(latency_ms=100, tokens=0, success=False)

        assert metrics.total_calls == 1
        assert metrics.failed_calls == 1
        assert metrics.successful_calls == 0

    def test_to_dict(self):
        """Test serializing metrics to dict."""
        metrics = ProviderPerformanceMetrics(
            provider_id="test",
            mode="test",
        )
        metrics.record_call(100, 150, success=True)

        data = metrics.to_dict()

        assert data["provider_id"] == "test"
        assert data["total_calls"] == 1
        assert "success_rate" in data
        assert "avg_tokens_per_call" in data

    def test_from_dict(self):
        """Test creating metrics from dict."""
        data = {
            "provider_id": "test",
            "mode": "text",
            "total_calls": 10,
            "successful_calls": 9,
            "failed_calls": 1,
            "total_tokens": 5000,
            "avg_latency_ms": 100.0,
        }

        metrics = ProviderPerformanceMetrics.from_dict(data)

        assert metrics.provider_id == "test"
        assert metrics.total_calls == 10
        assert metrics.successful_calls == 9
