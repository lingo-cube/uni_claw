"""Screen region data model.

This module defines the Region class for representing screen areas
and their functional roles in layout analysis.
"""

from dataclasses import dataclass
from typing import Literal

from .bounding_box import BoundingBox


# Valid region roles for type hinting
RegionRole = Literal["menu", "content", "tabs", "overlay", "unknown"]


@dataclass(frozen=True)
class Region:
    """Screen region with spatial bounds and functional role.

    A region represents a logical area of the screen with a specific
    purpose (menu, content, tabs, etc.).

    Attributes:
        id: Unique identifier for the region (e.g., "left_panel", "top_bar")
        bounds: Spatial boundaries of the region
        role: Functional role of the region

    Example:
        >>> region = Region(
        ...     id="left_panel",
        ...     bounds=BoundingBox(x=0.0, y=0.0, w=0.3, h=1.0),
        ...     role="menu"
        ... )
    """

    id: str
    bounds: BoundingBox
    role: RegionRole

    def __post_init__(self):
        """Validate region fields."""
        if not self.id:
            raise ValueError("Region id cannot be empty")
        if not isinstance(self.bounds, BoundingBox):
            raise TypeError("bounds must be a BoundingBox instance")

    def to_dict(self) -> dict:
        """Convert to dictionary representation.

        Returns:
            Dictionary with id, bounds, and role
        """
        return {
            'id': self.id,
            'bounds': self.bounds.to_dict(),
            'role': self.role,
        }

    @classmethod
    def from_dict(cls, data: dict) -> 'Region':
        """Create Region from dictionary.

        Args:
            data: Dictionary with id, bounds, and role keys

        Returns:
            Region instance
        """
        return cls(
            id=data.get('id', ''),
            bounds=BoundingBox.from_dict(data.get('bounds', {})),
            role=data.get('role', 'unknown'),
        )

    def contains_point(self, x: float, y: float) -> bool:
        """Check if a point is within this region.

        Args:
            x: Normalized x coordinate (0-1)
            y: Normalized y coordinate (0-1)

        Returns:
            True if the point is within the region bounds
        """
        return (
            self.bounds.x <= x <= self.bounds.x + self.bounds.w
            and self.bounds.y <= y <= self.bounds.y + self.bounds.h
        )
