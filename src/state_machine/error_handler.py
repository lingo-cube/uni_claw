"""
Error handling system for V6.1 traversal state machine.

This module provides comprehensive error classification, strategy selection,
and recovery execution for traversal operations.
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Dict, List, Optional, Callable
import logging
import time

logger = logging.getLogger(__name__)


class ErrorType(str, Enum):
    """Categories of errors that can occur during traversal."""

    NETWORK = "network_error"
    UI_ELEMENT = "ui_element_error"
    TIMEOUT = "timeout_error"
    PERMISSION = "permission_error"
    APP_CRASH = "app_crash_error"
    UNKNOWN = "unknown_error"


class ErrorStrategy(str, Enum):
    """Error handling strategies."""

    SKIP = "skip"  # Skip current node and continue
    RETRY = "retry"  # Retry operation with backoff
    BACKTRACK = "backtrack"  # Return to parent container
    CONTINUE = "continue"  # Continue with next operation
    ABORT = "abort"  # Abort traversal with error


@dataclass
class ErrorContext:
    """Complete error handling context."""

    error: Exception
    error_type: ErrorType
    error_strategy: ErrorStrategy
    retry_count: int = 0
    max_retries: int = 3
    can_skip: bool = True
    can_backtrack: bool = True
    fallback_action: Optional[str] = None
    recovery_attempts: List[str] = field(default_factory=list)
    context_data: Dict[str, Any] = field(default_factory=dict)


@dataclass
class ErrorRecoveryResult:
    """Error recovery execution result."""

    success: bool
    recovery_action: str
    restored_state: Dict[str, Any]
    execution_continued: bool
    recovery_time_ms: float
    remaining_retries: int
    error_message: Optional[str] = None


class ErrorClassifier:
    """Classify exceptions into specific error types."""

    # Error type mapping based on exception types
    ERROR_MAPPING = {
        # Network errors (will be checked by pattern matching)
        "network": ErrorType.NETWORK,
        "timeout": ErrorType.TIMEOUT,
        "element": ErrorType.UI_ELEMENT,
        "permission": ErrorType.PERMISSION,
        "crash": ErrorType.APP_CRASH,
    }

    # Network error patterns (timeout removed to avoid conflict with timeout errors)
    NETWORK_PATTERNS = [
        "network",
        "connection",
        "unreachable",
        "dns",
        "socket",
        "internet",
    ]

    # Element not found patterns
    ELEMENT_PATTERNS = [
        "element",
        "not found",
        "no such",
        "unable to locate",
        "xpath",
        "selector",
    ]

    # Timeout error patterns
    TIMEOUT_PATTERNS = [
        "timeout",
        "timed out",
        "time out",
    ]

    # Permission error patterns
    PERMISSION_PATTERNS = [
        "permission",
        "denied",
        "unauthorized",
        "access",
        "forbidden",
    ]

    # App crash patterns
    CRASH_PATTERNS = [
        "crash",
        "fatal",
        "application not responding",
        "anr",
        "force close",
    ]

    def classify(self, error: Exception) -> ErrorType:
        """
        Classify error into specific type.

        Args:
            error: Exception to classify

        Returns:
            ErrorType enum value
        """
        error_message = str(error).lower()
        error_type_name = type(error).__name__.lower()

        # Check crash patterns first (most critical)
        if self._matches_patterns(error_message, self.CRASH_PATTERNS):
            return ErrorType.APP_CRASH

        # Check permission patterns
        if self._matches_patterns(error_message, self.PERMISSION_PATTERNS):
            return ErrorType.PERMISSION

        # Check timeout patterns (before network, since network can have timeouts)
        if self._matches_patterns(error_message, self.TIMEOUT_PATTERNS):
            return ErrorType.TIMEOUT

        # Check network patterns
        if self._matches_patterns(error_message, self.NETWORK_PATTERNS):
            return ErrorType.NETWORK

        # Check element patterns
        if self._matches_patterns(error_message, self.ELEMENT_PATTERNS):
            return ErrorType.UI_ELEMENT

        # Check by exception type name
        for pattern, error_type in self.ERROR_MAPPING.items():
            if pattern in error_type_name:
                return error_type

        # Default to unknown
        return ErrorType.UNKNOWN

    def _matches_patterns(self, text: str, patterns: List[str]) -> bool:
        """
        Check if text matches any of the given patterns.

        Args:
            text: Text to search
            patterns: List of patterns to match

        Returns:
            True if any pattern matches
        """
        return any(pattern in text for pattern in patterns)


class ErrorStrategySelector:
    """Select appropriate error handling strategy."""

    # Strategy rules by error type (in priority order)
    STRATEGY_RULES = {
        ErrorType.NETWORK: [
            ErrorStrategy.RETRY,
            ErrorStrategy.BACKTRACK,
            ErrorStrategy.ABORT,
        ],
        ErrorType.UI_ELEMENT: [
            ErrorStrategy.SKIP,
            ErrorStrategy.RETRY,
            ErrorStrategy.BACKTRACK,
        ],
        ErrorType.TIMEOUT: [
            ErrorStrategy.RETRY,
            ErrorStrategy.CONTINUE,
            ErrorStrategy.BACKTRACK,
        ],
        ErrorType.PERMISSION: [
            ErrorStrategy.ABORT,
            ErrorStrategy.BACKTRACK,
        ],
        ErrorType.APP_CRASH: [
            ErrorStrategy.ABORT,
        ],
        ErrorType.UNKNOWN: [
            ErrorStrategy.CONTINUE,
            ErrorStrategy.SKIP,
            ErrorStrategy.ABORT,
        ],
    }

    def __init__(self):
        """Initialize error strategy selector."""
        self._strategy_cache: Dict[str, ErrorStrategy] = {}

    def select_strategy(
        self,
        error_type: ErrorType,
        context: Dict[str, Any]
    ) -> ErrorStrategy:
        """
        Select strategy based on error type and context.

        Args:
            error_type: Type of error
            context: Current traversal context

        Returns:
            Selected ErrorStrategy
        """
        # Create cache key
        cache_key = f"{error_type.value}_{context.get('retry_count', 0)}_{context.get('can_backtrack', True)}"

        # Check cache
        if cache_key in self._strategy_cache:
            return self._strategy_cache[cache_key]

        # Get available strategies for this error type
        available_strategies = self.STRATEGY_RULES.get(
            error_type,
            [ErrorStrategy.ABORT]
        )

        # Select first applicable strategy
        for strategy in available_strategies:
            if self._is_strategy_applicable(strategy, context):
                self._strategy_cache[cache_key] = strategy
                return strategy

        # Default to abort
        self._strategy_cache[cache_key] = ErrorStrategy.ABORT
        return ErrorStrategy.ABORT

    def _is_strategy_applicable(self, strategy: ErrorStrategy, context: Dict[str, Any]) -> bool:
        """
        Check if strategy can be applied in current context.

        Args:
            strategy: Strategy to check
            context: Current traversal context

        Returns:
            True if strategy is applicable
        """
        retry_count = context.get('retry_count', 0)
        max_retries = context.get('max_retries', 3)
        can_backtrack = context.get('can_backtrack', True)
        can_skip = context.get('can_skip', True)
        node_stack_length = context.get('node_stack_length', 0)

        if strategy == ErrorStrategy.RETRY:
            return retry_count < max_retries
        elif strategy == ErrorStrategy.BACKTRACK:
            return can_backtrack and node_stack_length > 1
        elif strategy == ErrorStrategy.SKIP:
            return can_skip
        elif strategy == ErrorStrategy.CONTINUE:
            return True  # Always can continue
        elif strategy == ErrorStrategy.ABORT:
            return True  # Always can abort

        return False


class RecoveryExecutor:
    """Execute error recovery strategies."""

    def __init__(self):
        """Initialize recovery executor."""
        self._recovery_hooks: Dict[ErrorStrategy, Callable] = {
            ErrorStrategy.SKIP: self._skip_current_node,
            ErrorStrategy.RETRY: self._retry_operation,
            ErrorStrategy.BACKTRACK: self._backtrack_to_parent,
            ErrorStrategy.CONTINUE: self._continue_execution,
            ErrorStrategy.ABORT: self._abort_traversal,
        }

    def execute(
        self,
        strategy: ErrorStrategy,
        context: Dict[str, Any],
        error: Exception
    ) -> ErrorRecoveryResult:
        """
        Execute selected recovery strategy.

        Args:
            strategy: Strategy to execute
            context: Current traversal context
            error: The error that occurred

        Returns:
            ErrorRecoveryResult with execution details
        """
        start_time = time.time()
        success = False
        action_taken = "none"
        restored_state = {}
        continued = False
        error_message = None

        try:
            recovery_hook = self._recovery_hooks.get(strategy)
            if recovery_hook:
                result = recovery_hook(context, error)
                success = result.get('success', False)
                action_taken = result.get('action', strategy.value)
                restored_state = result.get('restored_state', {})
                continued = result.get('continued', False)
            else:
                error_message = f"No recovery hook for strategy: {strategy}"
                logger.error(error_message)

        except Exception as recovery_error:
            error_message = f"Recovery failed: {recovery_error}"
            logger.error(error_message)
            # Fall back to abort
            result = self._abort_traversal(context, recovery_error)
            success = result.get('success', False)
            action_taken = "abort_fallback"
            restored_state = result.get('restored_state', {})

        recovery_time_ms = (time.time() - start_time) * 1000
        remaining_retries = context.get('max_retries', 3) - context.get('retry_count', 0)

        return ErrorRecoveryResult(
            success=success,
            recovery_action=action_taken,
            restored_state=restored_state,
            execution_continued=continued,
            recovery_time_ms=recovery_time_ms,
            remaining_retries=remaining_retries,
            error_message=error_message,
        )

    def _skip_current_node(self, context: Dict[str, Any], error: Exception) -> Dict[str, Any]:
        """Skip current node and continue to next."""
        return {
            'success': True,
            'action': 'skip',
            'restored_state': {},
            'continued': True,
        }

    def _retry_operation(self, context: Dict[str, Any], error: Exception) -> Dict[str, Any]:
        """Retry operation with backoff."""
        retry_count = context.get('retry_count', 0)
        # Simple exponential backoff
        backoff_time = min(2 ** retry_count, 10)  # Max 10 seconds

        return {
            'success': True,
            'action': f'retry_with_backoff_{backoff_time}s',
            'restored_state': {'retry_count': retry_count + 1},
            'continued': False,  # Will retry, not continue
        }

    def _backtrack_to_parent(self, context: Dict[str, Any], error: Exception) -> Dict[str, Any]:
        """Backtrack to parent container."""
        node_stack = context.get('node_stack', [])
        if len(node_stack) > 1:
            # Pop current container and return to parent
            return {
                'success': True,
                'action': 'backtrack_to_parent',
                'restored_state': {
                    'previous_container': node_stack[-2] if len(node_stack) >= 2 else None
                },
                'continued': True,
            }
        else:
            return {
                'success': False,
                'action': 'backtrack_failed',
                'restored_state': {},
                'continued': False,
            }

    def _continue_execution(self, context: Dict[str, Any], error: Exception) -> Dict[str, Any]:
        """Continue with next operation despite error."""
        return {
            'success': True,
            'action': 'continue_despite_error',
            'restored_state': {},
            'continued': True,
        }

    def _abort_traversal(self, context: Dict[str, Any], error: Exception) -> Dict[str, Any]:
        """Abort traversal with error."""
        return {
            'success': False,
            'action': 'abort',
            'restored_state': {},
            'continued': False,
        }


class ErrorHandler:
    """Complete error handling system for V6.1."""

    def __init__(self):
        """Initialize error handler."""
        self.classifier = ErrorClassifier()
        self.strategy_selector = ErrorStrategySelector()
        self.recovery_executor = RecoveryExecutor()

        # Statistics
        self.total_errors = 0
        self.recovered_count = 0
        self.error_statistics: Dict[str, int] = {}

    def handle_error(
        self,
        error: Exception,
        context: Dict[str, Any]
    ) -> ErrorRecoveryResult:
        """
        Main error handling entry point.

        Args:
            error: Exception that occurred
            context: Current traversal context

        Returns:
            ErrorRecoveryResult with recovery details
        """
        self.total_errors += 1

        # Classify error
        error_type = self.classifier.classify(error)
        logger.info(f"Classified error as: {error_type.value}")

        # Select strategy
        strategy = self.strategy_selector.select_strategy(error_type, context)
        logger.info(f"Selected strategy: {strategy.value}")

        # Execute recovery
        recovery_result = self.recovery_executor.execute(strategy, context, error)

        # Update statistics
        error_type_str = error_type.value
        self.error_statistics[error_type_str] = self.error_statistics.get(error_type_str, 0) + 1

        if recovery_result.success:
            self.recovered_count += 1

        return recovery_result

    @property
    def recovery_rate(self) -> float:
        """Calculate error recovery rate."""
        if self.total_errors == 0:
            return 0.0
        return self.recovered_count / self.total_errors

    def get_error_summary(self) -> Dict[str, Any]:
        """Get comprehensive error handling summary."""
        return {
            "total_errors": self.total_errors,
            "recovered_errors": self.recovered_count,
            "recovery_rate": self.recovery_rate,
            "error_statistics": self.error_statistics.copy(),
        }