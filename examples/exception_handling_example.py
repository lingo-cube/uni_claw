"""Example demonstrating the exception handling system.

This example shows how to:
1. Create custom exception handlers
2. Use the exception handling chain
3. Query exception history
"""

from src.exception import (
    ADBDisconnectedException,
    ExceptionAction,
    ExceptionContext,
    ExceptionHandlingChain,
    ExceptionHandlingResult,
    ExceptionSeverity,
    RecoveryAction,
    TraversalException,
)
from src.exception.handlers import (
    BacktrackHandler,
    DeviceExceptionHandler,
    ExceptionHandler,
    FatalExceptionHandler,
    RetryHandler,
    UIExceptionHandler,
)
from src.exception.history import ExceptionHistory


def example_basic_exception_handling():
    """Basic exception handling example."""
    print("=== Basic Exception Handling ===\n")

    # Create exception handling chain with default handlers
    chain = ExceptionHandlingChain.create_default()

    # Simulate an exception
    exc = ElementNotFoundException(
        element="SubmitButton",
        context="LoginPage"
    )

    # Create exception context
    from datetime import datetime
    from src.state.content_tree import TraversalState

    state = TraversalState()
    context = ExceptionContext(
        exception=exc,
        severity=exc.severity,
        state=state,
        node=None,
        operation="tap_and_wait",
        timestamp=datetime.now(),
        retry_count=0,
    )

    # Handle the exception
    result = chain.handle(context)

    print(f"Exception: {exc.message}")
    print(f"Severity: {exc.severity.value}")
    print(f"Action: {result.action.value}")
    print(f"Message: {result.message}\n")


def example_custom_handler():
    """Custom exception handler example."""
    print("=== Custom Handler Example ===\n")

    class TimeoutHandler(ExceptionHandler):
        """Custom handler for timeout exceptions."""

        def can_handle(self, context: ExceptionContext) -> bool:
            # Check if exception message contains "timeout"
            return "timeout" in context.exception.message.lower()

        def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
            # Handle with longer wait
            return ExceptionHandlingResult.recover(
                recovery=RecoveryAction.WAIT_AND_RETRY,
                message="Timeout detected, waiting longer before retry"
            )

    # Create chain and add custom handler
    chain = ExceptionHandlingChain()
    chain.add_handler(TimeoutHandler(), priority=0)  # High priority
    chain.add_handler(RetryHandler(max_retries=3), priority=1)

    # Create a timeout-like exception
    exc = TraversalException("Loading timeout waiting for element")
    exc._severity = ExceptionSeverity.ERROR

    from datetime import datetime
    from src.state.content_tree import TraversalState

    state = TraversalState()
    context = ExceptionContext(
        exception=exc,
        severity=exc.severity,
        state=state,
        node=None,
        operation="analyze_page",
        timestamp=datetime.now(),
        retry_count=1,
    )

    result = chain.handle(context)
    print(f"Action: {result.action.value}")
    print(f"Message: {result.message}\n")


def example_exception_history():
    """Exception history example."""
    print("=== Exception History Example ===\n")

    # Create exception history
    history = ExceptionHistory(max_records=100)

    # Simulate some exceptions
    from datetime import datetime
    from src.state.content_tree import TraversalState

    state = TraversalState()

    exceptions_to_record = [
        ElementNotFoundException("Button1"),
        PopupDetectedException("AdPopup"),
        ElementNotFoundException("Button2"),
        ADBDisconnectedException(),
    ]

    for i, exc in enumerate(exceptions_to_record):
        context = ExceptionContext(
            exception=exc,
            severity=exc.severity,
            state=state,
            node=None,
            operation=f"operation_{i}",
            timestamp=datetime.now(),
            retry_count=i,
        )
        history.record(context)

    # Query statistics
    stats = history.get_statistics()
    print(f"Total exceptions: {stats['total']}")
    print(f"By type: {stats['by_type']}")
    print(f"By severity: {stats['by_severity']}\n")

    # Query by type
    element_not_found = history.get_by_type(ElementNotFoundException)
    print(f"ElementNotFoundException count: {len(element_not_found)}")

    # Query by severity
    errors = history.get_by_severity(ExceptionSeverity.ERROR)
    print(f"ERROR severity count: {len(errors)}\n")


def example_handler_priority():
    """Example showing handler priority order."""
    print("=== Handler Priority Example ===\n")

    # Create default chain
    chain = ExceptionHandlingChain.create_default()

    print("Handler priority order:")
    for i, handler in enumerate(chain.handlers):
        print(f"{i + 1}. {handler.__class__.__name__}")
    print()

    # Test FATAL exception - should be handled by FatalExceptionHandler
    from datetime import datetime
    from src.state.content_tree import TraversalState

    state = TraversalState()

    fatal_exc = DeviceOfflineException()
    context = ExceptionContext(
        exception=fatal_exc,
        severity=fatal_exc.severity,
        state=state,
        node=None,
        operation="connect_device",
        timestamp=datetime.now(),
        retry_count=0,
    )

    result = chain.handle(context)
    print(f"FATAL exception -> {result.action.value}")
    print(f"Handled by: FatalExceptionHandler (priority 1)\n")


if __name__ == "__main__":
    # Import all exceptions at the top level
    from src.exception.exceptions import (
        ElementNotFoundException,
        PopupDetectedException,
    )

    example_basic_exception_handling()
    example_custom_handler()
    example_exception_history()
    example_handler_priority()

    print("=== Example Complete ===")
    print("\nKey Takeaways:")
    print("- Exception handling chain processes handlers in priority order")
    print("- First matching handler wins (no subsequent handlers called)")
    print("- Custom handlers can be added at any priority level")
    print("- Exception history provides queryable statistics")
    print("- All exceptions have default severity levels")
