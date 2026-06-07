"""Initialization exceptions for V6.8 engine initialization.

This module defines exception types specifically for the engine initialization
phase, distinguishing between recoverable and non-recoverable errors.
"""

from typing import List, Optional


class InitializationError(Exception):
    """Base exception for initialization-phase errors.

    Provides a recoverable attribute to distinguish between errors that
    can be recovered from (retry with fallback strategy) vs those that
    require user intervention or plan correction.

    Attributes:
        recoverable: Whether this error can be automatically recovered
    """

    def __init__(self, message: str, recoverable: bool = False):
        """Initialize initialization error.

        Args:
            message: Human-readable error description
            recoverable: Whether error can be automatically recovered
        """
        super().__init__(message)
        self.recoverable = recoverable
        self._message = message

    def __str__(self) -> str:
        """String representation with recoverable status."""
        status = "Recoverable" if self.recoverable else "Non-recoverable"
        return f"[{status}] {self._message}"


class ConfigurationError(InitializationError):
    """Raised for non-recoverable plan validation failures.

    These errors indicate problems with the TraversalPlan configuration
    that cannot be fixed by automatic retry or fallback strategies.
    """

    def __init__(self, message: str):
        """Initialize configuration error.

        Args:
            message: Human-readable error description
        """
        super().__init__(message, recoverable=False)


class EntryPolicyError(InitializationError):
    """Raised when all entry strategies in the fallback chain fail.

    This is a recoverable error - the user may retry later or adjust the
    device state, but the error itself indicates all automatic strategies
    were exhausted.

    Attributes:
        failed_strategies: List of strategy names that failed
        last_error: The final error that caused failure
    """

    def __init__(
        self,
        message: str,
        failed_strategies: Optional[List[str]] = None,
        last_error: Optional[Exception] = None,
    ):
        """Initialize entry policy error.

        Args:
            message: Human-readable error description
            failed_strategies: List of strategy names that were attempted
            last_error: The final error from the last failed strategy
        """
        super().__init__(message, recoverable=True)
        self.failed_strategies = failed_strategies or []
        self.last_error = last_error

    def __str__(self) -> str:
        """String representation with strategy details."""
        base = super().__str__()
        if self.failed_strategies:
            strategies = ", ".join(self.failed_strategies)
            base += f" | Failed strategies: {strategies}"
        if self.last_error:
            base += f" | Last error: {type(self.last_error).__name__}: {self.last_error}"
        return base


class WaitConditionError(InitializationError):
    """Raised when entry condition verification fails or times out.

    This is a recoverable error - the condition may be satisfied later
    or with different timing parameters.

    Attributes:
        condition: The wait condition that failed
        timeout_seconds: Timeout that was exceeded
    """

    def __init__(
        self,
        message: str,
        condition: Optional[dict] = None,
        timeout_seconds: Optional[float] = None,
    ):
        """Initialize wait condition error.

        Args:
            message: Human-readable error description
            condition: The wait condition dict that failed
            timeout_seconds: Timeout that was exceeded
        """
        super().__init__(message, recoverable=True)
        self.condition = condition or {}
        self.timeout_seconds = timeout_seconds

    def __str__(self) -> str:
        """String representation with condition details."""
        base = super().__str__()
        if self.timeout_seconds:
            base += f" | Timeout: {self.timeout_seconds}s"
        return base


class EntryError(InitializationError):
    """Raised when a single entry strategy execution fails.

    This is an internal error used by the entry policy framework.
    It triggers fallback to the next strategy in the chain.

    Attributes:
        strategy: The strategy that failed
        reason: Human-readable reason for failure
    """

    def __init__(self, strategy: str, reason: str):
        """Initialize entry error.

        Args:
            strategy: Name of the strategy that failed
            reason: Human-readable reason for failure
        """
        message = f"Entry strategy '{strategy}' failed: {reason}"
        super().__init__(message, recoverable=True)
        self.strategy = strategy
        self.reason = reason

    def __str__(self) -> str:
        """String representation."""
        return f"[EntryError] strategy={self.strategy}, reason={self.reason}"
