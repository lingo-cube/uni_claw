"""Type hint enumeration for visual element classification.

This module defines the TypeHint enum for coarse-grained visual type
classification by multimodal models.
"""

from enum import Enum


class TypeHint(str, Enum):
    """Coarse-grained visual type hint for element classification.

    These types represent visual features observable in screenshots,
    without behavioral inference. They are output by the multimodal
    model and later mapped to precise MenuItemType by the text model.

    Values:
        CLICKABLE_TEXT: Clickable text region (e.g., menu items)
        SWITCH: Switch/toggle control
        SLIDER: Slider control
        BUTTON: Button control
        ICON: Icon element (no text)
        INPUT_FIELD: Text input field
        TEXT: Plain text (non-interactive)
        IMAGE: Image element
    """

    CLICKABLE_TEXT = "clickable_text"
    SWITCH = "switch"
    SLIDER = "slider"
    BUTTON = "button"
    ICON = "icon"
    INPUT_FIELD = "input_field"
    TEXT = "text"
    IMAGE = "image"

    @classmethod
    def from_string(cls, value: str) -> 'TypeHint':
        """Create TypeHint from string with fuzzy matching.

        Args:
            value: String value to convert

        Returns:
            TypeHint enum instance

        Examples:
            >>> TypeHint.from_string("clickable_text")
            <TypeHint.CLICKABLE_TEXT: 'clickable_text'>
            >>> TypeHint.from_string("toggle")  # Fuzzy match
            <TypeHint.SWITCH: 'switch'>
        """
        value_lower = value.lower().strip()

        # Try exact match first
        try:
            return cls(value_lower)
        except ValueError:
            pass

        # Fuzzy mapping for common alternatives
        mapping = {
            'text': cls.TEXT,
            'clickable': cls.CLICKABLE_TEXT,
            'click': cls.CLICKABLE_TEXT,
            'toggle': cls.SWITCH,
            'checkbox': cls.SWITCH,
            'check': cls.SWITCH,
            'btn': cls.BUTTON,
            'input': cls.INPUT_FIELD,
            'field': cls.INPUT_FIELD,
            'img': cls.IMAGE,
            'picture': cls.IMAGE,
        }

        result = mapping.get(value_lower, cls.TEXT)
        return result

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings.

        Returns:
            List of valid TypeHint values
        """
        return [e.value for e in cls]

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid TypeHint.

        Args:
            value: String value to validate

        Returns:
            True if value is a valid TypeHint
        """
        return value.lower() in cls.values()

    def is_interactive(self) -> bool:
        """Check if this type represents an interactive element.

        Returns:
            True if the element type is typically interactive
        """
        interactive_types = {
            self.CLICKABLE_TEXT,
            self.SWITCH,
            self.SLIDER,
            self.BUTTON,
            self.INPUT_FIELD,
        }
        return self in interactive_types

    def is_visual_only(self) -> bool:
        """Check if this type represents a visual-only element.

        Returns:
            True if the element type is visual-only (no interaction)
        """
        return self in {self.ICON, self.IMAGE, self.TEXT}
