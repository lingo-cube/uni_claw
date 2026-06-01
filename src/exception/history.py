"""Exception history tracking and analysis.

This module provides functionality for recording, querying, and analyzing
exceptions that occur during traversal.
"""

import logging
from collections import Counter
from datetime import datetime
from typing import List, Optional, Type

from .context import ExceptionContext
from .exceptions import ExceptionSeverity, TraversalException

logger = logging.getLogger(__name__)


class ExceptionHistory:
    """Records and queries exception history during traversal.

    Maintains a rolling buffer of exception contexts with configurable
    maximum size. Provides query methods for filtering by type and severity.
    """

    def __init__(self, max_records: int = 1000):
        """Initialize exception history.

        Args:
            max_records: Maximum number of records to keep (default 1000).
                        Oldest records are removed when limit is exceeded.
        """
        self.max_records = max_records
        self.records: List[ExceptionContext] = []

    def record(self, context: ExceptionContext) -> None:
        """Record an exception context in history.

        Args:
            context: Exception context to record
        """
        self.records.append(context)

        # Remove oldest records if we exceed the limit
        while len(self.records) > self.max_records:
            self.records.pop(0)

        logger.debug(f"Recorded exception: {context.exception.__class__.__name__}")

    def get_by_type(self, exc_type: Type[TraversalException]) -> List[ExceptionContext]:
        """Query exceptions by type.

        Args:
            exc_type: Exception class to filter by (e.g., ElementNotFoundException)

        Returns:
            List of exception contexts matching the type, ordered by timestamp
        """
        return [
            r for r in self.records
            if isinstance(r.exception, exc_type)
        ]

    def get_by_severity(self, severity: ExceptionSeverity) -> List[ExceptionContext]:
        """Query exceptions by severity level.

        Args:
            severity: Severity level to filter by

        Returns:
            List of exception contexts with the given severity, ordered by timestamp
        """
        return [
            r for r in self.records
            if r.severity == severity
        ]

    def get_statistics(self) -> dict:
        """Get statistics about recorded exceptions.

        Returns:
            Dictionary with:
                - total: Total number of exceptions
                - by_type: Counter of exception types
                - by_severity: Counter of severity levels
        """
        if not self.records:
            return {
                "total": 0,
                "by_type": {},
                "by_severity": {},
            }

        by_type = Counter(type(r.exception).__name__ for r in self.records)
        by_severity = Counter(
            r.severity.value if isinstance(r.severity, ExceptionSeverity) else r.severity
            for r in self.records
        )

        return {
            "total": len(self.records),
            "by_type": dict(by_type.most_common()),
            "by_severity": dict(by_severity.most_common()),
        }

    def clear(self) -> None:
        """Clear all exception history."""
        self.records.clear()
        logger.debug("Exception history cleared")

    def get_recent(self, count: int = 10) -> List[ExceptionContext]:
        """Get the most recent exception records.

        Args:
            count: Number of recent records to return (default 10)

        Returns:
            List of the most recent exception contexts
        """
        return self.records[-count:] if self.records else []

    def __len__(self) -> int:
        """Get current number of records."""
        return len(self.records)

    def __contains__(self, exc_type: Type[TraversalException]) -> bool:
        """Check if any exception of given type exists in history."""
        return any(isinstance(r.exception, exc_type) for r in self.records)
