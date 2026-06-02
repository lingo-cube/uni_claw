"""Unit tests for BoundingBox model."""

import pytest

from src.models.vision.bounding_box import BoundingBox


class TestBoundingBoxCreation:
    """Tests for BoundingBox creation and validation."""

    def test_creation(self):
        """Test basic BoundingBox creation."""
        bbox = BoundingBox(x=0.1, y=0.2, w=0.3, h=0.05)
        assert bbox.x == 0.1
        assert bbox.y == 0.2
        assert bbox.w == 0.3
        assert bbox.h == 0.05

    def test_validation_negative_coordinate(self):
        """Test that negative coordinates raise ValueError."""
        with pytest.raises(ValueError, match="x must be in \\[0, 1\\]"):
            BoundingBox(x=-0.1, y=0.2, w=0.3, h=0.05)

    def test_validation_coordinate_above_one(self):
        """Test that coordinates > 1 raise ValueError."""
        with pytest.raises(ValueError, match="y must be in \\[0, 1\\]"):
            BoundingBox(x=0.1, y=1.5, w=0.3, h=0.05)

    def test_validation_negative_width(self):
        """Test that negative width raises ValueError."""
        with pytest.raises(ValueError, match="Width and height must be positive"):
            BoundingBox(x=0.1, y=0.2, w=-0.3, h=0.05)

    def test_validation_zero_height(self):
        """Test that zero height raises ValueError."""
        with pytest.raises(ValueError, match="Width and height must be positive"):
            BoundingBox(x=0.1, y=0.2, w=0.3, h=0.0)

    def test_boundary_values(self):
        """Test that boundary values (0 and 1) are accepted."""
        bbox = BoundingBox(x=0.0, y=0.0, w=1.0, h=1.0)
        assert bbox.x == 0.0
        assert bbox.y == 0.0
        assert bbox.w == 1.0
        assert bbox.h == 1.0


class TestBoundingBoxCenter:
    """Tests for center() method."""

    def test_center_calculation(self):
        """Test center point calculation."""
        bbox = BoundingBox(x=0.0, y=0.0, w=0.5, h=0.5)
        center = bbox.center()
        assert center == (0.25, 0.25)

    def test_center_offset(self):
        """Test center point with offset position."""
        bbox = BoundingBox(x=0.2, y=0.3, w=0.4, h=0.2)
        center = bbox.center()
        assert center == (0.4, 0.4)


class TestBoundingBoxArea:
    """Tests for area() method."""

    def test_area_calculation(self):
        """Test area calculation."""
        bbox = BoundingBox(x=0.0, y=0.0, w=0.5, h=0.5)
        assert bbox.area() == 0.25

    def test_area_small(self):
        """Test area for small bounding box."""
        bbox = BoundingBox(x=0.1, y=0.1, w=0.01, h=0.01)
        assert bbox.area() == 0.0001


class TestBoundingBoxContains:
    """Tests for contains() method."""

    def test_contains_smaller_inside(self):
        """Test that a box contains a smaller box inside it."""
        outer = BoundingBox(x=0.0, y=0.0, w=1.0, h=1.0)
        inner = BoundingBox(x=0.1, y=0.1, w=0.2, h=0.2)
        assert outer.contains(inner)

    def test_contains_larger(self):
        """Test that a box does not contain a larger box."""
        small = BoundingBox(x=0.1, y=0.1, w=0.2, h=0.2)
        large = BoundingBox(x=0.0, y=0.0, w=1.0, h=1.0)
        assert not small.contains(large)

    def test_contains_same(self):
        """Test that a box contains itself."""
        bbox = BoundingBox(x=0.1, y=0.1, w=0.2, h=0.2)
        assert bbox.contains(bbox)

    def test_contains_overlapping(self):
        """Test that overlapping but not containing returns False."""
        bbox1 = BoundingBox(x=0.0, y=0.0, w=0.5, h=0.5)
        bbox2 = BoundingBox(x=0.3, y=0.3, w=0.5, h=0.5)
        assert not bbox1.contains(bbox2)
        assert not bbox2.contains(bbox1)


class TestBoundingBoxOverlaps:
    """Tests for overlaps() method."""

    def test_overlaps_true(self):
        """Test that overlapping boxes return True."""
        bbox1 = BoundingBox(x=0.0, y=0.0, w=0.5, h=0.5)
        bbox2 = BoundingBox(x=0.3, y=0.3, w=0.5, h=0.5)
        assert bbox1.overlaps(bbox2)
        assert bbox2.overlaps(bbox1)

    def test_overlaps_false(self):
        """Test that non-overlapping boxes return False."""
        bbox1 = BoundingBox(x=0.0, y=0.0, w=0.3, h=0.3)
        bbox2 = BoundingBox(x=0.5, y=0.5, w=0.3, h=0.3)
        assert not bbox1.overlaps(bbox2)
        assert not bbox2.overlaps(bbox1)

    def test_overlaps_touching_edge(self):
        """Test that boxes touching at edge are considered overlapping."""
        bbox1 = BoundingBox(x=0.0, y=0.0, w=0.5, h=0.5)
        bbox2 = BoundingBox(x=0.5, y=0.0, w=0.5, h=0.5)
        assert bbox1.overlaps(bbox2)


class TestBoundingBoxSerialization:
    """Tests for serialization methods."""

    def test_to_dict(self):
        """Test conversion to dictionary."""
        bbox = BoundingBox(x=0.1, y=0.2, w=0.3, h=0.4)
        assert bbox.to_dict() == {
            'x': 0.1,
            'y': 0.2,
            'w': 0.3,
            'h': 0.4,
        }

    def test_from_dict(self):
        """Test creation from dictionary."""
        data = {'x': 0.1, 'y': 0.2, 'w': 0.3, 'h': 0.4}
        bbox = BoundingBox.from_dict(data)
        assert bbox.x == 0.1
        assert bbox.y == 0.2
        assert bbox.w == 0.3
        assert bbox.h == 0.4

    def test_from_dict_with_defaults(self):
        """Test creation from dictionary with default values."""
        data = {'x': 0.5, 'y': 0.5}
        bbox = BoundingBox.from_dict(data)
        assert bbox.x == 0.5
        assert bbox.y == 0.5
        # Uses small positive defaults since zero width/height is invalid
        assert bbox.w == 0.001
        assert bbox.h == 0.001
