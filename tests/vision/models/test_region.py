"""Unit tests for Region model."""

import pytest

from src.models.vision.region import Region
from src.models.vision.bounding_box import BoundingBox


class TestRegionCreation:
    """Tests for Region creation and validation."""

    def test_creation(self):
        """Test basic Region creation."""
        bbox = BoundingBox(x=0.0, y=0.0, w=0.3, h=1.0)
        region = Region(id="left_panel", bounds=bbox, role="menu")
        assert region.id == "left_panel"
        assert region.bounds == bbox
        assert region.role == "menu"

    def test_validation_empty_id(self):
        """Test that empty id raises ValueError."""
        bbox = BoundingBox(x=0.0, y=0.0, w=0.3, h=1.0)
        with pytest.raises(ValueError, match="id cannot be empty"):
            Region(id="", bounds=bbox, role="menu")

    def test_validation_invalid_bounds_type(self):
        """Test that non-BoundingBox bounds raises TypeError."""
        with pytest.raises(TypeError, match="bounds must be a BoundingBox"):
            Region(id="test", bounds={'x': 0, 'y': 0}, role="content")

    def test_all_valid_roles(self):
        """Test all valid region roles."""
        bbox = BoundingBox(x=0.0, y=0.0, w=1.0, h=1.0)
        valid_roles = ["menu", "content", "tabs", "overlay", "unknown"]

        for role in valid_roles:
            region = Region(id=f"region_{role}", bounds=bbox, role=role)
            assert region.role == role


class TestRegionSerialization:
    """Tests for serialization methods."""

    def test_to_dict(self):
        """Test conversion to dictionary."""
        bbox = BoundingBox(x=0.1, y=0.2, w=0.3, h=0.4)
        region = Region(id="test_region", bounds=bbox, role="content")

        expected = {
            'id': 'test_region',
            'bounds': {'x': 0.1, 'y': 0.2, 'w': 0.3, 'h': 0.4},
            'role': 'content',
        }
        assert region.to_dict() == expected

    def test_from_dict(self):
        """Test creation from dictionary."""
        data = {
            'id': 'test_region',
            'bounds': {'x': 0.1, 'y': 0.2, 'w': 0.3, 'h': 0.4},
            'role': 'content',
        }
        region = Region.from_dict(data)

        assert region.id == 'test_region'
        assert region.bounds.x == 0.1
        assert region.bounds.y == 0.2
        assert region.role == 'content'

    def test_from_dict_with_defaults(self):
        """Test creation from dictionary with default values."""
        data = {'id': 'test'}
        region = Region.from_dict(data)

        assert region.id == 'test'
        assert region.bounds.x == 0.0
        assert region.bounds.y == 0.0
        # Uses small positive defaults since zero width/height is invalid
        assert region.bounds.w == 0.001
        assert region.bounds.h == 0.001
        assert region.role == 'unknown'


class TestRegionContainsPoint:
    """Tests for contains_point() method."""

    def test_contains_point_inside(self):
        """Test point inside region."""
        bbox = BoundingBox(x=0.1, y=0.1, w=0.3, h=0.3)
        region = Region(id="test", bounds=bbox, role="content")

        assert region.contains_point(0.2, 0.2)  # Inside
        assert region.contains_point(0.1, 0.1)  # Top-left corner
        assert region.contains_point(0.4, 0.4)  # Bottom-right corner

    def test_contains_point_outside(self):
        """Test point outside region."""
        bbox = BoundingBox(x=0.1, y=0.1, w=0.3, h=0.3)
        region = Region(id="test", bounds=bbox, role="content")

        assert not region.contains_point(0.0, 0.0)  # Before region
        assert not region.contains_point(0.5, 0.5)  # After region
        assert not region.contains_point(1.0, 1.0)  # Far away

    def test_contains_point_on_boundary(self):
        """Test point on region boundary."""
        bbox = BoundingBox(x=0.1, y=0.1, w=0.3, h=0.3)
        region = Region(id="test", bounds=bbox, role="content")

        # Points on boundary are included
        assert region.contains_point(0.1, 0.2)  # Left edge
        assert region.contains_point(0.4, 0.2)  # Right edge
        assert region.contains_point(0.2, 0.1)  # Top edge
        assert region.contains_point(0.2, 0.4)  # Bottom edge
