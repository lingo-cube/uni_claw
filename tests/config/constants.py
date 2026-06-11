"""
Test configuration constants.

Defines standard configuration constants for test execution control.
These values are considered design specifications - changes require impact assessment.
"""


class Timeout:
    """Timeout constants for different operation categories."""

    # Short operations (2 seconds)
    SHORT = 2

    # Normal operations (5 seconds)
    NORMAL = 5

    # Long operations (10 seconds)
    LONG = 10

    # Flush/async operations (5.0 seconds - float for compatibility)
    FLUSH = 5.0


class Retry:
    """Retry count constants for different retry scenarios."""

    # Default maximum retries (3)
    MAX_DEFAULT = 3

    # Extended maximum retries (5)
    MAX_EXTENDED = 5

    # Zero retries (no retry)
    COUNT_ZERO = 0

    # Single retry
    COUNT_ONE = 1


class Concurrency:
    """Concurrency limits for parallel operations."""

    # Concurrent requests (20)
    REQUESTS = 20

    # Default maximum children in traversal (10)
    MAX_CHILDREN_DEFAULT = 10

    # Small maximum children for limited scenarios (2)
    MAX_CHILDREN_SMALL = 2


class ScrollThreshold:
    """
    Optional scroll position constants.

    These constants provide semantic names for common scroll positions.
    Tests may continue to use magic numbers (0.33, 0.7, etc.) as needed.
    """

    # Start of scrollable area (0.0)
    START = 0.0

    # Middle of scrollable area (0.5)
    HALF = 0.5

    # End of scrollable area (1.0)
    END = 1.0
