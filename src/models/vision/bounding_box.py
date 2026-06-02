"""Normalized bounding box data model.

This module defines the BoundingBox class for representing element positions
and sizes in normalized coordinates (0-1 range).
"""

from dataclasses import dataclass
from typing import Tuple


@dataclass(frozen=True)
class BoundingBox:
    """Normalized bounding box describing element position and size on screen.

    All coordinates are normalized to [0, 1] range:
    - x, y: Top-left corner coordinates
    - w, h: Width and height

    Example:
        >>> bbox = BoundingBox(x=0.1, y=0.2, w=0.3, h=0.05)
        >>> center = bbox.center()  # (0.25, 0.225)
    """

    x: float  # Top-left x coordinate, normalized 0~1
    y: float  # Top-left y coordinate, normalized 0~1
    w: float  # Width, normalized 0~1
    h: float  # Height, normalized 0~1

    def __post_init__(self):
        """Validate coordinate ranges."""
        # First check for negative width/height (specific error message)
        if self.w < 0 or self.h < 0:
            raise ValueError(
                f"Width and height must be positive, got w={self.w}, h={self.h}"
            )

        # Then check all coordinates are in [0, 1] range
        for name, value in [
            ('x', self.x),
            ('y', self.y),
            ('w', self.w),
            ('h', self.h),
        ]:
            if value < 0 or value > 1:
                raise ValueError(
                    f"{name} must be in [0, 1], got {value}"
                )

        # Finally, check for zero width/height
        if self.w == 0 or self.h == 0:
            raise ValueError(
                f"Width and height must be positive, got w={self.w}, h={self.h}"
            )

    def center(self) -> Tuple[float, float]:
        """Return the center point coordinates.

        Returns:
            Tuple of (x, y) center coordinates
        """
        return (self.x + self.w / 2, self.y + self.h / 2)

    def area(self) -> float:
        """Return the area of the bounding box.

        Returns:
            Area as a float in range [0, 1]
        """
        return self.w * self.h

    def contains(self, other: 'BoundingBox') -> bool:
        """Check if this bounding box contains another.

        Args:
            other: Another BoundingBox to check

        Returns:
            True if this box completely contains the other
        """
        return (
            self.x <= other.x
            and self.y <= other.y
            and self.x + self.w >= other.x + other.w
            and self.y + self.h >= other.y + other.h
        )

    def overlaps(self, other: 'BoundingBox') -> bool:
        """Check if this bounding box overlaps with another.

        Args:
            other: Another BoundingBox to check

        Returns:
            True if the boxes overlap (including edge touching)
        """
        return not (
            self.x + self.w < other.x
            or other.x + other.w < self.x
            or self.y + self.h < other.y
            or other.y + other.h < self.y
        )

    def to_dict(self) -> dict:
        """Convert to dictionary representation.

        Returns:
            Dictionary with x, y, w, h keys
        """
        return {
            'x': self.x,
            'y': self.y,
            'w': self.w,
            'h': self.h,
        }

    @classmethod
    def from_dict(cls, data: dict) -> 'BoundingBox':
        """Create BoundingBox from dictionary.

        Args:
            data: Dictionary with x, y, w, h keys

        Returns:
            BoundingBox instance

        Note:
            Uses small positive defaults (0.001) for w/h if not provided,
            since zero width/height is not valid for actual elements.
        """
        return cls(
            x=data.get('x', 0.0),
            y=data.get('y', 0.0),
            w=data.get('w', 0.001),  # Small positive default
            h=data.get('h', 0.001),  # Small positive default
        )
