"""
Test data factories.

Provides factory methods for creating test data objects including devices and coordinates.
"""

from tests.factories.device_factory import (
    DeviceFactory,
    DeviceSpec,
    CoordinateFactory,
    Coordinate,
)

__all__ = [
    "DeviceFactory",
    "DeviceSpec",
    "CoordinateFactory",
    "Coordinate",
]
