"""Exception handling chain implementation.

This module implements the chain of responsibility pattern for exception handling,
allowing multiple handlers to be tried in priority order.
"""

import logging
from typing import List, Optional

from .context import ExceptionContext, ExceptionHandlingResult
from .handlers import (
    ADBDisconnectedException,
    AppCrashException,
    BacktrackHandler,
    DeviceExceptionHandler,
    FatalExceptionHandler,
    PopupDetectedException,
    RetryHandler,
    UIExceptionHandler,
)

logger = logging.getLogger(__name__)


class ExceptionHandlingChain:
    """Chain of responsibility for exception handling.

    Handlers are tried in priority order. The first handler that returns
    a non-IGNORE result wins, and subsequent handlers are not called.

    Default handler priority (from highest to lowest):
        1. FatalExceptionHandler - fatal exceptions terminate immediately
        2. DeviceExceptionHandler - device issues have specific recovery
        3. UIExceptionHandler - UI issues are handled automatically
        4. RetryHandler - retryable errors get another chance
        5. BacktrackHandler - exhausted retries trigger backtrack
    """

    def __init__(self, handlers: Optional[List] = None):
        """Initialize exception handling chain.

        Args:
            handlers: Optional list of handlers (uses default if not provided)
        """
        self.handlers = handlers or []

    def add_handler(self, handler, priority: Optional[int] = None):
        """Add a handler to the chain.

        Args:
            handler: ExceptionHandler instance to add
            priority: Optional priority (lower = higher priority).
                     If None, adds to end of chain (lowest priority).
        """
        if priority is not None:
            self.handlers.insert(priority, handler)
        else:
            self.handlers.append(handler)

    def set_handlers(self, handlers: List):
        """Set the complete handler list.

        Args:
            handlers: List of handlers in priority order
        """
        self.handlers = handlers

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """Process exception through the handler chain.

        Iterates through handlers in priority order and returns the
        first non-IGNORE result. If all handlers return IGNORE or
        no handler matches, returns IGNORE result.

        Args:
            context: Exception context to process

        Returns:
            ExceptionHandlingResult from the first matching handler,
            or IGNORE if no handler matches
        """
        for handler in self.handlers:
            handler_name = handler.__class__.__name__

            try:
                can_handle = handler.can_handle(context)

                logger.debug(f"Handler {handler_name}.can_handle() = {can_handle}")

                if can_handle:
                    result = handler.handle(context)
                    logger.info(
                        f"Handler {handler_name} returned {result.action.value}: {result.message}"
                    )
                    return result

            except Exception as e:
                # Don't let handler errors break the chain
                logger.error(f"Handler {handler_name} raised exception: {e}")
                continue

        # No handler matched - return IGNORE
        logger.debug("No handler matched, returning IGNORE")
        return ExceptionHandlingResult.ignore(
            message="No handler matched exception, ignoring"
        )

    @classmethod
    def create_default(cls, adb_client=None, max_retries: int = 3) -> "ExceptionHandlingChain":
        """Create a chain with default handlers in priority order.

        Args:
            adb_client: Optional ADB client for device/UI handlers
            max_retries: Maximum retry attempts for retry/backtrack handlers

        Returns:
            ExceptionHandlingChain with default handlers
        """
        chain = cls()

        # Add handlers in priority order (highest to lowest)
        chain.add_handler(FatalExceptionHandler(), priority=0)
        chain.add_handler(DeviceExceptionHandler(adb_client), priority=1)
        chain.add_handler(UIExceptionHandler(adb_client), priority=2)
        chain.add_handler(RetryHandler(max_retries=max_retries), priority=3)
        chain.add_handler(BacktrackHandler(max_retries=max_retries), priority=4)

        return chain
