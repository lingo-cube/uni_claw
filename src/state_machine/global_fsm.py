"""
Global state machine for traversal task lifecycle.

This module implements the global state machine that manages the overall
traversal task from initialization through completion.
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Callable, Dict, List, Optional
from datetime import datetime


class GlobalState(str, Enum):
    """States in the global state machine."""

    IDLE = "idle"  # Waiting for task to start
    INITIALIZING = "initializing"  # Loading traversal plan and context
    TRAVERSING = "traversing"  # Active traversal in progress
    PAUSED = "paused"  # Task paused (can be resumed)
    ERROR = "error"  # Error occurred
    RECOVERING = "recovering"  # Attempting recovery from error
    COMPLETED = "completed"  # Task completed successfully
    TERMINATED = "terminated"  # Task terminated (unrecoverable error)


@dataclass
class GlobalStateTransition:
    """Record of a state transition."""

    from_state: GlobalState
    to_state: GlobalState
    timestamp: datetime = field(default_factory=datetime.now)
    reason: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


class GlobalStateMachine:
    """
    Global state machine for traversal lifecycle management.

    Manages high-level task states and transitions, coordinating with
    the traversal state machine and node stack.
    """

    # Valid state transitions
    VALID_TRANSITIONS = {
        GlobalState.IDLE: {GlobalState.INITIALIZING},
        GlobalState.INITIALIZING: {GlobalState.TRAVERSING, GlobalState.ERROR},
        GlobalState.TRAVERSING: {GlobalState.PAUSED, GlobalState.ERROR, GlobalState.COMPLETED},
        GlobalState.PAUSED: {GlobalState.TRAVERSING, GlobalState.TERMINATED},
        GlobalState.ERROR: {GlobalState.RECOVERING, GlobalState.TERMINATED},
        GlobalState.RECOVERING: {GlobalState.INITIALIZING, GlobalState.TERMINATED},
        GlobalState.COMPLETED: set(),  # Terminal state
        GlobalState.TERMINATED: set(),  # Terminal state
    }

    def __init__(self):
        """Initialize the global state machine in IDLE state."""
        self._state = GlobalState.IDLE
        self._transition_history: List[GlobalStateTransition] = []
        self._state_callbacks: Dict[GlobalState, List[Callable]] = {
            state: [] for state in GlobalState
        }
        self._error_context: Optional[Dict[str, Any]] = None

    @property
    def state(self) -> GlobalState:
        """Get current state."""
        return self._state

    @property
    def is_active(self) -> bool:
        """Check if state machine is in an active state (not terminal)."""
        return self._state in {
            GlobalState.IDLE,
            GlobalState.INITIALIZING,
            GlobalState.TRAVERSING,
            GlobalState.PAUSED,
            GlobalState.ERROR,
            GlobalState.RECOVERING,
        }

    @property
    def is_terminal(self) -> bool:
        """Check if state machine is in a terminal state."""
        return self._state in {GlobalState.COMPLETED, GlobalState.TERMINATED}

    @property
    def is_paused(self) -> bool:
        """Check if state machine is paused."""
        return self._state == GlobalState.PAUSED

    @property
    def error_context(self) -> Optional[Dict[str, Any]]:
        """Get error context if in ERROR state."""
        return self._error_context

    def can_transition_to(self, target_state: GlobalState) -> bool:
        """
        Check if transition to target state is valid.

        Args:
            target_state: Desired target state

        Returns:
            True if transition is valid
        """
        return target_state in self.VALID_TRANSITIONS.get(self._state, set())

    def transition_to(
        self, target_state: GlobalState, reason: Optional[str] = None, **metadata
    ) -> bool:
        """
        Transition to target state.

        Args:
            target_state: Desired target state
            reason: Optional reason for transition
            **metadata: Optional metadata to attach to transition

        Returns:
            True if transition succeeded

        Raises:
            ValueError: If transition is invalid
        """
        if not self.can_transition_to(target_state):
            raise ValueError(
                f"Invalid transition from {self._state} to {target_state}. "
                f"Valid transitions: {self.VALID_TRANSITIONS.get(self._state, set())}"
            )

        # Record transition
        transition = GlobalStateTransition(
            from_state=self._state,
            to_state=target_state,
            reason=reason,
            metadata=metadata,
        )
        self._transition_history.append(transition)

        # Update state
        old_state = self._state
        self._state = target_state

        # Trigger callbacks for new state
        self._trigger_state_callbacks(target_state, old_state, transition)

        return True

    def _trigger_state_callbacks(
        self, new_state: GlobalState, old_state: GlobalState, transition: GlobalStateTransition
    ) -> None:
        """Trigger callbacks registered for the new state."""
        for callback in self._state_callbacks.get(new_state, []):
            try:
                callback(new_state, old_state, transition)
            except Exception as e:
                print(f"State callback error: {e}")

    def register_state_callback(self, state: GlobalState, callback: Callable) -> None:
        """
        Register a callback for when entering a specific state.

        Args:
            state: State to watch
            callback: Function to call when entering state
        """
        if state not in self._state_callbacks:
            self._state_callbacks[state] = []
        self._state_callbacks[state].append(callback)

    def get_transition_history(self) -> List[GlobalStateTransition]:
        """Get list of all state transitions."""
        return self._transition_history.copy()

    def get_current_state_duration(self) -> Optional[float]:
        """
        Get duration in current state in seconds.

        Returns:
            Duration since entering current state, or None if IDLE
        """
        if not self._transition_history:
            return None

        last_transition = self._transition_history[-1]
        return (datetime.now() - last_transition.timestamp).total_seconds()

    # State-specific convenience methods

    def start_initialization(self, plan_path: Optional[str] = None) -> bool:
        """Start initialization phase."""
        return self.transition_to(
            GlobalState.INITIALIZING,
            reason="Starting initialization",
            plan_path=plan_path,
        )

    def start_traversing(self) -> bool:
        """Start traversing phase."""
        return self.transition_to(
            GlobalState.TRAVERSING,
            reason="Starting traversal",
        )

    def pause(self, reason: Optional[str] = None) -> bool:
        """Pause traversal."""
        return self.transition_to(GlobalState.PAUSED, reason=reason or "Traversal paused")

    def resume(self) -> bool:
        """Resume from paused state."""
        if self._state != GlobalState.PAUSED:
            raise ValueError(f"Cannot resume from {self._state}")
        return self.transition_to(GlobalState.TRAVERSING, reason="Resuming traversal")

    def report_error(
        self, error: Exception, context: Optional[Dict[str, Any]] = None
    ) -> bool:
        """
        Report an error and transition to ERROR state.

        Args:
            error: The exception that occurred
            context: Optional error context
        """
        self._error_context = {
            "error": str(error),
            "error_type": type(error).__name__,
            "timestamp": datetime.now().isoformat(),
            **(context or {}),
        }
        return self.transition_to(
            GlobalState.ERROR,
            reason=f"Error: {error}",
            error_context=self._error_context,
        )

    def start_recovery(self, recovery_action: str) -> bool:
        """
        Attempt recovery from error.

        Args:
            recovery_action: Description of recovery action
        """
        return self.transition_to(
            GlobalState.RECOVERING,
            reason=f"Attempting recovery: {recovery_action}",
            recovery_action=recovery_action,
        )

    def complete(self) -> bool:
        """Complete traversal successfully."""
        return self.transition_to(GlobalState.COMPLETED, reason="Traversal completed")

    def terminate(self, reason: Optional[str] = None) -> bool:
        """Terminate traversal (unrecoverable error or manual termination)."""
        return self.transition_to(
            GlobalState.TERMINATED,
            reason=reason or "Traversal terminated",
        )

    def reset(self) -> None:
        """Reset state machine to IDLE (for reuse)."""
        self._state = GlobalState.IDLE
        self._transition_history.clear()
        self._error_context = None
