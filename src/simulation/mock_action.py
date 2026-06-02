"""
Mock action executor for V6 simulation.

Provides virtual device control without requiring real devices.
"""

import time
from typing import Any, Dict, List, Optional, Tuple


class MockActionExecutor:
    """
    Mock action executor for simulation testing.

    Records all actions without actually controlling a device.
    """

    def __init__(self, simulate_delay: float = 0.0):
        """
        Initialize mock action executor.

        Args:
            simulate_delay: Optional delay in seconds to simulate device latency
        """
        self.action_history: List[Dict[str, Any]] = []
        self.simulate_delay = simulate_delay

    def tap(self, x: float, y: float) -> bool:
        """
        Execute a tap action.

        Args:
            x: X coordinate (normalized 0-1)
            y: Y coordinate (normalized 0-1)

        Returns:
            True (always succeeds in mock)
        """
        if self.simulate_delay > 0:
            time.sleep(self.simulate_delay)

        self.action_history.append({
            "action": "tap",
            "x": x,
            "y": y,
            "timestamp": time.time(),
        })
        return True

    def swipe(
        self,
        start: Tuple[float, float],
        end: Tuple[float, float],
        duration: float = 0.3,
    ) -> bool:
        """
        Execute a swipe action.

        Args:
            start: Starting (x, y) coordinates
            end: Ending (x, y) coordinates
            duration: Swipe duration in seconds

        Returns:
            True (always succeeds in mock)
        """
        if self.simulate_delay > 0:
            time.sleep(self.simulate_delay)

        self.action_history.append({
            "action": "swipe",
            "start": list(start),
            "end": list(end),
            "duration": duration,
            "timestamp": time.time(),
        })
        return True

    def press_back(self) -> bool:
        """
        Execute a back button press.

        Returns:
            True (always succeeds in mock)
        """
        if self.simulate_delay > 0:
            time.sleep(self.simulate_delay)

        self.action_history.append({
            "action": "back",
            "timestamp": time.time(),
        })
        return True

    def press_home(self) -> bool:
        """
        Execute a home button press.

        Returns:
            True (always succeeds in mock)
        """
        if self.simulate_delay > 0:
            time.sleep(self.simulate_delay)

        self.action_history.append({
            "action": "home",
            "timestamp": time.time(),
        })
        return True

    def input_text(self, text: str) -> bool:
        """
        Input text into the focused field.

        Args:
            text: Text to input

        Returns:
            True (always succeeds in mock)
        """
        if self.simulate_delay > 0:
            time.sleep(self.simulate_delay)

        self.action_history.append({
            "action": "input_text",
            "text": text,
            "timestamp": time.time(),
        })
        return True

    def get_history(self) -> List[Dict[str, Any]]:
        """
        Get a copy of the action history.

        Returns:
            Copy of action history list
        """
        return self.action_history.copy()

    def clear_history(self) -> None:
        """Clear the action history."""
        self.action_history = []

    def get_action_count(self) -> int:
        """Get total number of actions executed."""
        return len(self.action_history)

    def get_tap_count(self) -> int:
        """Get number of tap actions."""
        return sum(1 for a in self.action_history if a["action"] == "tap")

    def get_back_count(self) -> int:
        """Get number of back actions."""
        return sum(1 for a in self.action_history if a["action"] == "back")

    def get_swipe_count(self) -> int:
        """Get number of swipe actions."""
        return sum(1 for a in self.action_history if a["action"] == "swipe")

    def has_action(self, action_type: str) -> bool:
        """Check if history contains an action of given type."""
        return any(a["action"] == action_type for a in self.action_history)

    def get_last_action(self) -> Optional[Dict[str, Any]]:
        """Get the last action executed."""
        if self.action_history:
            return self.action_history[-1]
        return None
