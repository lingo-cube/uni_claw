"""Screen hints data model.

This module defines the ScreenHints class for screen-level metadata
and visual analysis hints.
"""

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

from .region import Region


@dataclass
class ScreenHints:
    """Screen-level hints and metadata.

    Contains high-level information about the screen layout, state,
    and visual features that aid in subsequent analysis.

    Attributes:
        top_bar_text: Text from the top title bar
        layout_type: Overall layout type classification
        regions: List of identified screen regions
        overlay_detected: Whether a popup/overlay is detected
        scroll_detected: Whether the page appears scrollable
        extra: Additional metadata for extensibility
    """

    top_bar_text: str = ""
    layout_type: str = "unknown"
    regions: List[Region] = field(default_factory=list)
    overlay_detected: bool = False
    scroll_detected: bool = False
    extra: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary representation.

        Returns:
            Dictionary representation of the hints
        """
        return {
            'top_bar_text': self.top_bar_text,
            'layout_type': self.layout_type,
            'regions': [r.to_dict() for r in self.regions],
            'overlay_detected': self.overlay_detected,
            'scroll_detected': self.scroll_detected,
            'extra': self.extra,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'ScreenHints':
        """Create ScreenHints from dictionary.

        Args:
            data: Dictionary with screen hints data

        Returns:
            ScreenHints instance
        """
        # Parse regions
        regions = []
        for region_data in data.get('regions', []):
            regions.append(Region.from_dict(region_data))

        return cls(
            top_bar_text=data.get('top_bar_text', ''),
            layout_type=data.get('layout_type', 'unknown'),
            regions=regions,
            overlay_detected=data.get('overlay_detected', False),
            scroll_detected=data.get('scroll_detected', False),
            extra=data.get('extra', {}),
        )
