"""Selection state enumeration for visual elements.

This module defines the SelectionState enum for representing the
selection/activation state of visual elements.
"""

from enum import Enum


class SelectionState(str, Enum):
    """Selection/activation state of a visual element.

    This state is used to identify currently active menu items,
    selected tabs, or disabled controls.

    Values:
        SELECTED: Currently selected/highlighted element
        NORMAL: Normal unselected state
        DISABLED: Disabled state (grayed out, not interactive)
    """

    SELECTED = "selected"
    NORMAL = "normal"
    DISABLED = "disabled"

    @classmethod
    def from_string(cls, value: str) -> 'SelectionState':
        """Create SelectionState from string with fuzzy matching.

        Args:
            value: String value to convert

        Returns:
            SelectionState enum instance

        Examples:
            >>> SelectionState.from_string("selected")
            <SelectionState.SELECTED: 'selected'>
            >>> SelectionState.from_string("active")  # Fuzzy match
            <SelectionState.SELECTED: 'selected'>
            >>> SelectionState.from_string("gray")  # Fuzzy match
            <SelectionState.DISABLED: 'disabled'>
        """
        value_lower = value.lower().strip()

        # Try exact match first
        try:
            return cls(value_lower)
        except ValueError:
            pass

        # Fuzzy mapping for common alternatives
        selected_aliases = {'active', 'highlighted', 'highlight', 'checked'}
        disabled_aliases = {'gray', 'grayed', 'dimmed', 'inactive', 'hidden'}

        if value_lower in selected_aliases:
            return cls.SELECTED
        elif value_lower in disabled_aliases:
            return cls.DISABLED
        else:
            return cls.NORMAL

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings.

        Returns:
            List of valid SelectionState values
        """
        return [e.value for e in cls]

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid SelectionState.

        Args:
            value: String value to validate

        Returns:
            True if value is a valid SelectionState
        """
        return value.lower() in cls.values()

    def is_interactive(self) -> bool:
        """Check if this state represents an interactive element.

        Returns:
            True if the element is interactive (not disabled)
        """
        return self != self.DISABLED

    def is_active(self) -> bool:
        """Check if this state represents an active/selected element.

        Returns:
            True if the element is selected
        """
        return self == self.SELECTED
