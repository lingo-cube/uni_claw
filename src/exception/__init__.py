"""Exception handling system for uni-claw traversal.

This module provides a comprehensive exception handling framework with:
- Exception hierarchy with severity levels
- Handler interface and built-in handlers
- Chain-of-responsibility processing
- Recovery actions
- Exception history tracking
"""

from .chain import ExceptionHandlingChain
from .context import ExceptionAction, ExceptionContext, ExceptionHandlingResult, RecoveryAction
from .exceptions import (
    ADBDisconnectedException,
    AIAnalysisFailedException,
    AIException,
    AIResponseInvalidException,
    AppCrashException,
    ClickFailedException,
    CoordinateExpiredException,
    DeviceException,
    DeviceOfflineException,
    ElementNotFoundException,
    ExceptionSeverity,
    InputFailedException,
    LocationException,
    OperationException,
    PageRedirectException,
    PathMismatchException,
    PopupDetectedException,
    TraversalException,
    UIException,
    LoadingTimeoutException,
)
from .handlers import (
    BacktrackHandler,
    DeviceExceptionHandler,
    ExceptionHandler,
    FatalExceptionHandler,
    RetryHandler,
    UIExceptionHandler,
)
from .history import ExceptionHistory

__all__ = [
    # Exception classes
    "TraversalException",
    "ExceptionSeverity",
    "LocationException",
    "ElementException",
    "PathMismatchException",
    "CoordinateExpiredException",
    "OperationException",
    "ClickFailedException",
    "InputFailedException",
    "DeviceException",
    "ADBDisconnectedException",
    "AppCrashException",
    "DeviceOfflineException",
    "UIException",
    "PopupDetectedException",
    "PageRedirectException",
    "LoadingTimeoutException",
    "AIException",
    "AIAnalysisFailedException",
    "AIResponseInvalidException",
    # Context and result classes
    "ExceptionContext",
    "ExceptionHandlingResult",
    "ExceptionAction",
    "RecoveryAction",
    # Handler classes
    "ExceptionHandler",
    "FatalExceptionHandler",
    "DeviceExceptionHandler",
    "UIExceptionHandler",
    "RetryHandler",
    "BacktrackHandler",
    # Chain and history
    "ExceptionHandlingChain",
    "ExceptionHistory",
]
