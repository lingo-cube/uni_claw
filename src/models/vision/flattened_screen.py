"""Flattened screen data model.

This module defines the FlattenedScreen class for representing
the complete visual analysis output from multimodal models.
"""

from dataclasses import dataclass, field
from typing import Any, Dict, List

from .flattened_element import FlattenedElement
from .screen_hints import ScreenHints
from .selection_state import SelectionState


@dataclass
class FlattenedScreen:
    """Flattened screen representation from multimodal analysis.

    Contains a flat list of all visual elements identified on screen,
    along with screen-level hints. Elements are automatically sorted
    by position (top-to-bottom, left-to-right) for consistent processing.

    Attributes:
        elements: Flat list of identified visual elements
        screen_hints: Screen-level metadata and hints
    """

    elements: List[FlattenedElement] = field(default_factory=list)
    screen_hints: Dict[str, Any] = field(default_factory=dict)

    def __post_init__(self):
        """Initialize and sort elements by position."""
        # Sort elements: top to bottom, then left to right
        self.elements.sort(key=lambda e: (e.bbox.y, e.bbox.x))

    def element_count(self) -> int:
        """Return the total number of elements.

        Returns:
            Number of elements in the screen
        """
        return len(self.elements)

    def get_elements_in_region(self, region_id: str) -> List[FlattenedElement]:
        """Get all elements belonging to a specific region.

        Args:
            region_id: The ID of the region to filter by

        Returns:
            List of elements in the specified region
        """
        return [e for e in self.elements if e.region == region_id]

    def get_selected_elements(self) -> List[FlattenedElement]:
        """Get all currently selected/highlighted elements.

        Returns:
            List of elements with SELECTED state
        """
        return [
            e for e in self.elements
            if e.selection_state == SelectionState.SELECTED
        ]

    def get_elements_by_type(self, type_hint: str) -> List[FlattenedElement]:
        """Get all elements of a specific type.

        Args:
            type_hint: The type hint to filter by

        Returns:
            List of elements with the specified type
        """
        from .type_hint import TypeHint
        target_type = TypeHint.from_string(type_hint)
        return [e for e in self.elements if e.type_hint == target_type]

    def get_interactive_elements(self) -> List[FlattenedElement]:
        """Get all interactive elements.

        Returns:
            List of elements that appear interactive
        """
        return [e for e in self.elements if e.is_interactive()]

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary representation.

        Returns:
            Dictionary representation of the screen
        """
        return {
            'elements': [e.to_dict() for e in self.elements],
            'screen_hints': self.screen_hints,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'FlattenedScreen':
        """Create FlattenedScreen from dictionary.

        Args:
            data: Dictionary with screen data

        Returns:
            FlattenedScreen instance
        """
        # Parse elements
        elements = []
        for elem_data in data.get('elements', []):
            elements.append(FlattenedElement.from_dict(elem_data))

        return cls(
            elements=elements,
            screen_hints=data.get('screen_hints', {}),
        )

    def get_screen_hints(self) -> ScreenHints:
        """Get typed screen hints from the raw dictionary.

        Returns:
            ScreenHints object with typed fields
        """
        return ScreenHints.from_dict(self.screen_hints)

    def set_screen_hints(self, hints: ScreenHints) -> None:
        """Set screen hints from a ScreenHints object.

        Args:
            hints: ScreenHints object to set
        """
        self.screen_hints = hints.to_dict()
