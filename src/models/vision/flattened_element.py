"""Flattened element data model.

This module defines the FlattenedElement class for representing
individual visual elements identified by the multimodal model.
"""

from dataclasses import dataclass, field
from typing import Any, Dict, Optional

from .bounding_box import BoundingBox
from .type_hint import TypeHint
from .selection_state import SelectionState


@dataclass
class FlattenedElement:
    """A flattened visual element from screenshot analysis.

    Represents a single UI element identified by the multimodal model,
    containing visual features without behavioral inference.

    Attributes:
        id: Unique element identifier (within this analysis)
        text: Visible text content on the element
        type_hint: Coarse visual type classification
        bbox: Normalized bounding box coordinates
        region: ID of the containing region (optional)
        selection_state: Visual selection/activation state
        visual_state: Additional visual state descriptors
        confidence: Recognition confidence (0.0 - 1.0)
    """

    id: int
    text: str = ""
    type_hint: TypeHint = TypeHint.TEXT
    bbox: Optional[BoundingBox] = None
    region: Optional[str] = None
    selection_state: SelectionState = SelectionState.NORMAL
    visual_state: Dict[str, Any] = field(default_factory=dict)
    confidence: float = 1.0

    def __post_init__(self):
        """Validate and initialize fields."""
        # Validate confidence range first
        if not 0 <= self.confidence <= 1:
            raise ValueError(
                f"confidence must be in [0, 1], got {self.confidence}"
            )

        # Set default bbox if None (using object.__setattr__ for frozen BoundingBox)
        # Use small positive values since zero width/height is not valid
        if self.bbox is None:
            object.__setattr__(self, 'bbox', BoundingBox(x=0, y=0, w=0.001, h=0.001))

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary representation.

        Returns:
            Dictionary representation of the element
        """
        return {
            'id': self.id,
            'text': self.text,
            'type_hint': self.type_hint.value,
            'bbox': self.bbox.to_dict() if self.bbox else None,
            'region': self.region,
            'selection_state': self.selection_state.value,
            'visual_state': self.visual_state,
            'confidence': self.confidence,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'FlattenedElement':
        """Create FlattenedElement from dictionary.

        Args:
            data: Dictionary with element data

        Returns:
            FlattenedElement instance
        """
        # Parse bbox
        bbox = None
        if data.get('bbox'):
            bbox = BoundingBox.from_dict(data['bbox'])

        return cls(
            id=data.get('id', 0),
            text=data.get('text', ''),
            type_hint=TypeHint.from_string(data.get('type_hint', 'text')),
            bbox=bbox,
            region=data.get('region'),
            selection_state=SelectionState.from_string(
                data.get('selection_state', 'normal')
            ),
            visual_state=data.get('visual_state', {}),
            confidence=data.get('confidence', 1.0),
        )

    def is_interactive(self) -> bool:
        """Check if this element appears interactive.

        Returns:
            True if the element is likely interactive based on visual cues
        """
        return (
            self.type_hint.is_interactive()
            and self.selection_state.is_interactive()
        )

    def center(self) -> tuple[float, float]:
        """Get the center point of the element.

        Returns:
            Tuple of (x, y) center coordinates
        """
        return self.bbox.center()
