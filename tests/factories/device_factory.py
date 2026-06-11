"""
Test data factories for devices and coordinates.

Provides factory methods for creating device specifications and coordinates
as test data (not constants).
"""

from dataclasses import dataclass
from typing import Literal


@dataclass
class DeviceSpec:
    """Device specification for test scenarios."""

    width: int
    height: int
    name: str

    def __str__(self) -> str:
        return f"{self.name} ({self.width}x{self.height})"


@dataclass
class Coordinate:
    """Coordinate representation for UI element positions."""

    x: float
    y: float

    def to_dict(self) -> dict[str, float]:
        """Convert to dictionary format for compatibility."""
        return {"x": self.x, "y": self.y}

    def __str__(self) -> str:
        return f"({self.x}, {self.y})"


class DeviceFactory:
    """Factory for creating device specifications."""

    # Default phone device (1440x3168)
    DEFAULT_PHONE = DeviceSpec(
        width=1440,
        height=3168,
        name="default_phone"
    )

    # Small phone device (1080x2340)
    SMALL_PHONE = DeviceSpec(
        width=1080,
        height=2340,
        name="small_phone"
    )

    # Tablet device (2048x2732)
    TABLET = DeviceSpec(
        width=2048,
        height=2732,
        name="tablet"
    )

    @staticmethod
    def create_custom(width: int, height: int, name: str = "custom") -> DeviceSpec:
        """
        Create a custom device specification.

        Args:
            width: Device width in pixels
            height: Device height in pixels
            name: Device identifier name

        Returns:
            A new DeviceSpec instance
        """
        return DeviceSpec(width=width, height=height, name=name)


class CoordinateFactory:
    """Factory for creating coordinate test data."""

    @staticmethod
    def create(x: float, y: float) -> Coordinate:
        """
        Create a coordinate object.

        Args:
            x: X coordinate (0.0 to 1.0, left to right)
            y: Y coordinate (0.0 to 1.0, top to bottom)

        Returns:
            A Coordinate object
        """
        return Coordinate(x=x, y=y)

    @staticmethod
    def create_coordinate(x: float, y: float) -> dict[str, float]:
        """
        Create a coordinate in dictionary format for compatibility.

        Args:
            x: X coordinate (0.0 to 1.0, left to right)
            y: Y coordinate (0.0 to 1.0, top to bottom)

        Returns:
            A dictionary with 'x' and 'y' keys
        """
        return {"x": x, "y": y}

    @staticmethod
    def center() -> Coordinate:
        """Get center position (0.5, 0.5)."""
        return Coordinate(x=0.5, y=0.5)

    @staticmethod
    def top_left() -> Coordinate:
        """Get top-left position (0.0, 0.0)."""
        return Coordinate(x=0.0, y=0.0)

    @staticmethod
    def top_right() -> Coordinate:
        """Get top-right position (1.0, 0.0)."""
        return Coordinate(x=1.0, y=0.0)

    @staticmethod
    def bottom_left() -> Coordinate:
        """Get bottom-left position (0.0, 1.0)."""
        return Coordinate(x=0.0, y=1.0)

    @staticmethod
    def bottom_right() -> Coordinate:
        """Get bottom-right position (1.0, 1.0)."""
        return Coordinate(x=1.0, y=1.0)

    @staticmethod
    def top_menu() -> Coordinate:
        """Get typical top menu position (0.5, 0.1)."""
        return Coordinate(x=0.5, y=0.1)

    @staticmethod
    def bottom_navigation() -> Coordinate:
        """Get typical bottom navigation position (0.5, 0.9)."""
        return Coordinate(x=0.5, y=0.9)
