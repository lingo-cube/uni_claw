"""State persistence and management."""

import json
import logging
from pathlib import Path
from typing import Optional, Union

from .content_tree import TraversalState

logger = logging.getLogger(__name__)


class StateManager:
    """Manages traversal state persistence and recovery."""

    def __init__(self, state_file: Union[str, Path]):
        """Initialize state manager.

        Args:
            state_file: Path to state persistence file
        """
        self.state_file = Path(state_file)
        self._state: Optional[TraversalState] = None

        # Trace logging
        self._trace = None
        try:
            from ..utils.trace import TraceLogger
            self._trace = TraceLogger("state")
        except ImportError:
            pass

    @property
    def state(self) -> TraversalState:
        """Get current state, loading from file if needed."""
        if self._state is None:
            self.load()
        return self._state

    def load(self) -> TraversalState:
        """Load state from file or create new state."""
        trace_context = None
        if self._trace:
            trace_context = self._trace.start_span(
                operation="load_state",
                tags={"file": str(self.state_file)}
            )

        if self.state_file.exists():
            try:
                with open(self.state_file, "r") as f:
                    data = json.load(f)
                # Convert visited list back to set
                if "visited" in data and isinstance(data["visited"], list):
                    data["visited"] = set(data["visited"])
                self._state = TraversalState(**data)

                logger.info(f"[STATE] Loaded state from {self.state_file}")
                logger.info(f"[STATE] Path: {self._state.current_path}, "
                           f"Visited: {len(self._state.visited)}, "
                           f"Step: {self._state.step_count}")

                if self._trace and trace_context:
                    self._trace.log_output(trace_context,
                        current_path=self._state.current_path,
                        visited_count=len(self._state.visited),
                        step_count=self._state.step_count,
                        loaded_from_file=True
                    )
                    self._trace.finish_span(trace_context)
            except Exception as e:
                logger.warning(f"Failed to load state: {e}. Creating new state.")
                self._state = TraversalState()

                if self._trace and trace_context:
                    self._trace.finish_span(trace_context,
                        error=Exception(f"Load failed: {e}"))
        else:
            self._state = TraversalState()
            logger.info("[STATE] Created new traversal state")

            if self._trace and trace_context:
                self._trace.log_output(trace_context, new_state=True)
                self._trace.finish_span(trace_context)

        return self._state

    def save(self) -> None:
        """Persist current state to file."""
        trace_context = None
        if self._trace:
            trace_context = self._trace.start_span(
                operation="save_state",
                tags={"file": str(self.state_file)}
            )

        if self._state is None:
            logger.warning("[STATE] No state to save")
            if self._trace and trace_context:
                self._trace.finish_span(trace_context,
                    error=Exception("No state to save"))
            return

        try:
            self.state_file.parent.mkdir(parents=True, exist_ok=True)
            with open(self.state_file, "w") as f:
                # Convert sets to lists for JSON serialization
                state_dict = self._state.model_dump()
                state_dict["visited"] = list(state_dict.get("visited", set()))
                json.dump(state_dict, f, indent=2)

            logger.debug(f"[STATE] Saved state to {self.state_file}")
            logger.info(f"[STATE] Path: {self._state.current_path}, "
                       f"Visited: {len(self._state.visited)}, "
                       f"Step: {self._state.step_count}")

            if self._trace and trace_context:
                self._trace.log_output(trace_context,
                    current_path=self._state.current_path,
                    visited_count=len(self._state.visited),
                    step_count=self._state.step_count
                )
                self._trace.finish_span(trace_context)

        except Exception as e:
            logger.error(f"[STATE] Failed to save state: {e}")
            if self._trace and trace_context:
                self._trace.finish_span(trace_context, error=e)

    def reset(self) -> None:
        """Reset to fresh state."""
        self._state = TraversalState()
        self.save()
        logger.info("Reset traversal state")

    def update(self, **kwargs) -> None:
        """Update state fields and save."""
        for key, value in kwargs.items():
            if hasattr(self._state, key):
                setattr(self._state, key, value)
        self.save()
