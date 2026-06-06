"""
Lightweight metrics callbacks for component integration.

Components report raw metrics via optional callbacks.
The engine assembles complete Span nodes from these metrics.

All callbacks default to no-op so collection never blocks.
"""

from dataclasses import dataclass, field
from typing import Any, Callable, Dict, Optional


@dataclass
class AICallMetrics:
    """Raw metrics from an AI call."""
    capability: str = ""
    provider_id: Optional[str] = None
    success: bool = True
    latency_ms: float = 0.0
    input_tokens: Optional[int] = None
    output_tokens: Optional[int] = None


@dataclass
class ExecutionMetrics:
    """Raw metrics from an action execution."""
    action: str = ""
    status: str = "success"
    target: Optional[str] = None
    page_before: Optional[str] = None
    page_after: Optional[str] = None
    duration_ms: float = 0.0


@dataclass
class ErrorMetrics:
    """Raw metrics from an error."""
    error_type: str = ""
    error_message: str = ""
    severity: str = "error"
    stack_trace: Optional[str] = None
    context: Dict[str, Any] = field(default_factory=dict)


# Callback type aliases
AICallCallback = Callable[[AICallMetrics], None]
ExecutionCallback = Callable[[ExecutionMetrics], None]
ErrorCallback = Callable[[ErrorMetrics], None]
StateTransitionCallback = Callable[[str, str], None]
