"""Unit tests for ExceptionHistory."""

import pytest
from datetime import datetime
from src.exception.history import ExceptionHistory
from src.exception.context import ExceptionContext
from src.exception.exceptions import (
    ElementNotFoundException,
    ExceptionSeverity,
    LoadingTimeoutException,
    PopupDetectedException,
    TraversalException,
)
from src.state.content_tree import TraversalState


class TestExceptionHistory:
    """Tests for ExceptionHistory."""

    def test_initial_state(self):
        """Test initial state is empty."""
        history = ExceptionHistory()
        assert len(history) == 0
        assert history.records == []

    def test_record_exception(self):
        """Test recording an exception."""
        history = ExceptionHistory()
        context = _create_context(ElementNotFoundException("Button"))

        history.record(context)

        assert len(history) == 1
        assert history.records[0] is context

    def test_max_records_limit(self):
        """Test old records are removed when max_records is exceeded."""
        history = ExceptionHistory(max_records=3)

        # Add 5 records (Button0..Button4)
        for i in range(5):
            context = _create_context(ElementNotFoundException(f"Button{i}"))
            history.record(context)

        # Should only keep last 3 (Button2, Button3, Button4)
        assert len(history) == 3
        assert "Button2" in str(history.records[0].exception)
        assert "Button4" in str(history.records[-1].exception)

    def test_get_by_type(self):
        """Test querying exceptions by type."""
        history = ExceptionHistory()

        history.record(_create_context(ElementNotFoundException("Button1")))
        history.record(_create_context(PopupDetectedException("Ad")))
        history.record(_create_context(ElementNotFoundException("Button2")))

        element_not_found = history.get_by_type(ElementNotFoundException)
        assert len(element_not_found) == 2

        popup_detected = history.get_by_type(PopupDetectedException)
        assert len(popup_detected) == 1

    def test_get_by_severity(self):
        """Test querying exceptions by severity."""
        history = ExceptionHistory()

        # Add exceptions with different severities
        error_context = _create_context(ElementNotFoundException("Button"))  # ERROR
        warning_context = _create_context(LoadingTimeoutException(5.0))       # WARNING

        history.record(error_context)
        history.record(warning_context)

        error_records = history.get_by_severity(ExceptionSeverity.ERROR)
        assert len(error_records) == 1

        warning_records = history.get_by_severity(ExceptionSeverity.WARNING)
        assert len(warning_records) == 1

    def test_get_statistics(self):
        """Test getting exception statistics."""
        history = ExceptionHistory()

        history.record(_create_context(ElementNotFoundException("Button1")))
        history.record(_create_context(ElementNotFoundException("Button2")))
        history.record(_create_context(PopupDetectedException("Ad")))
        history.record(_create_context(ElementNotFoundException("Button3")))

        stats = history.get_statistics()

        assert stats["total"] == 4
        assert stats["by_type"]["ElementNotFoundException"] == 3
        assert stats["by_type"]["PopupDetectedException"] == 1

    def test_get_statistics_empty(self):
        """Test statistics for empty history."""
        history = ExceptionHistory()
        stats = history.get_statistics()

        assert stats["total"] == 0
        assert stats["by_type"] == {}
        assert stats["by_severity"] == {}

    def test_get_recent(self):
        """Test getting recent exception records."""
        history = ExceptionHistory()

        for i in range(15):
            history.record(_create_context(ElementNotFoundException(f"Button{i}")))

        recent = history.get_recent(count=10)

        assert len(recent) == 10
        assert "Button14" in recent[-1].exception.message

    def test_clear(self):
        """Test clearing exception history."""
        history = ExceptionHistory()
        history.record(_create_context(ElementNotFoundException("Button")))
        history.record(_create_context(PopupDetectedException("Ad")))

        assert len(history) == 2

        history.clear()

        assert len(history) == 0
        assert history.records == []

    def test_contains_operator(self):
        """Test 'in' operator for exception types."""
        history = ExceptionHistory()

        history.record(_create_context(ElementNotFoundException("Button")))

        assert ElementNotFoundException in history
        assert PopupDetectedException not in history

    def test_records_ordered_by_timestamp(self):
        """Test records are maintained in chronological order."""
        history = ExceptionHistory()

        times = []
        for i in range(3):
            context = _create_context(ElementNotFoundException(f"Button{i}"))
            times.append(context.timestamp)
            history.record(context)

        # Check order is preserved
        assert history.records[0].timestamp == times[0]
        assert history.records[1].timestamp == times[1]
        assert history.records[2].timestamp == times[2]

    def test_custom_max_records(self):
        """Test creating history with custom max_records."""
        history = ExceptionHistory(max_records=100)
        assert history.max_records == 100


# Helper functions


def _create_context(exception) -> ExceptionContext:
    """Create a test ExceptionContext."""
    return ExceptionContext(
        exception=exception,
        severity=exception.severity,
        state=TraversalState(),
        node=None,
        operation="test_operation",
        timestamp=datetime.now(),
        retry_count=0,
    )
