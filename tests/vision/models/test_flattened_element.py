"""Unit tests for FlattenedElement model."""

import pytest

from src.models.vision.flattened_element import FlattenedElement
from src.models.vision.bounding_box import BoundingBox
from src.models.vision.type_hint import TypeHint
from src.models.vision.selection_state import SelectionState


class TestFlattenedElementCreation:
    """Tests for FlattenedElement creation and validation."""

    def test_creation_minimal(self):
        """Test minimal FlattenedElement creation."""
        elem = FlattenedElement(id=0)
        assert elem.id == 0
        assert elem.text == ""
        assert elem.type_hint == TypeHint.TEXT
        assert elem.bbox.x == 0
        assert elem.bbox.y == 0
        assert elem.confidence == 1.0

    def test_creation_full(self):
        """Test full FlattenedElement creation."""
        bbox = BoundingBox(x=0.1, y=0.2, w=0.3, h=0.05)
        elem = FlattenedElement(
            id=1,
            text="WiFi",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=bbox,
            region="left_panel",
            selection_state=SelectionState.SELECTED,
            visual_state={"bold": True},
            confidence=0.95,
        )
        assert elem.id == 1
        assert elem.text == "WiFi"
        assert elem.type_hint == TypeHint.CLICKABLE_TEXT
        assert elem.region == "left_panel"
        assert elem.selection_state == SelectionState.SELECTED
        assert elem.visual_state == {"bold": True}
        assert elem.confidence == 0.95

    def test_validation_confidence_out_of_range(self):
        """Test that invalid confidence raises ValueError."""
        with pytest.raises(ValueError, match="confidence must be in \\[0, 1\\]"):
            FlattenedElement(id=0, confidence=1.5)

        with pytest.raises(ValueError, match="confidence must be in \\[0, 1\\]"):
            FlattenedElement(id=0, confidence=-0.1)


class TestFlattenedElementHelperMethods:
    """Tests for helper methods."""

    def test_is_interactive_true(self):
        """Test is_interactive() returns True for interactive elements."""
        elem = FlattenedElement(
            id=0,
            type_hint=TypeHint.CLICKABLE_TEXT,
            selection_state=SelectionState.NORMAL,
        )
        assert elem.is_interactive()

    def test_is_interactive_disabled(self):
        """Test is_interactive() returns False for disabled elements."""
        elem = FlattenedElement(
            id=0,
            type_hint=TypeHint.BUTTON,
            selection_state=SelectionState.DISABLED,
        )
        assert not elem.is_interactive()

    def test_is_interactive_non_interactive_type(self):
        """Test is_interactive() returns False for non-interactive types."""
        elem = FlattenedElement(
            id=0,
            type_hint=TypeHint.TEXT,
            selection_state=SelectionState.NORMAL,
        )
        assert not elem.is_interactive()

    def test_center(self):
        """Test center() method."""
        bbox = BoundingBox(x=0.0, y=0.0, w=0.5, h=0.5)
        elem = FlattenedElement(id=0, bbox=bbox)
        assert elem.center() == (0.25, 0.25)


class TestFlattenedElementSerialization:
    """Tests for serialization methods."""

    def test_to_dict(self):
        """Test conversion to dictionary."""
        bbox = BoundingBox(x=0.1, y=0.2, w=0.3, h=0.05)
        elem = FlattenedElement(
            id=1,
            text="WiFi",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=bbox,
            region="left_panel",
            selection_state=SelectionState.SELECTED,
            visual_state={"bold": True},
            confidence=0.95,
        )

        expected = {
            'id': 1,
            'text': 'WiFi',
            'type_hint': 'clickable_text',
            'bbox': {'x': 0.1, 'y': 0.2, 'w': 0.3, 'h': 0.05},
            'region': 'left_panel',
            'selection_state': 'selected',
            'visual_state': {'bold': True},
            'confidence': 0.95,
        }
        assert elem.to_dict() == expected

    def test_from_dict(self):
        """Test creation from dictionary."""
        data = {
            'id': 1,
            'text': 'WiFi',
            'type_hint': 'clickable_text',
            'bbox': {'x': 0.1, 'y': 0.2, 'w': 0.3, 'h': 0.05},
            'region': 'left_panel',
            'selection_state': 'selected',
            'visual_state': {'bold': True},
            'confidence': 0.95,
        }
        elem = FlattenedElement.from_dict(data)

        assert elem.id == 1
        assert elem.text == 'WiFi'
        assert elem.type_hint == TypeHint.CLICKABLE_TEXT
        assert elem.bbox.x == 0.1
        assert elem.region == 'left_panel'
        assert elem.selection_state == SelectionState.SELECTED
        assert elem.visual_state == {'bold': True}
        assert elem.confidence == 0.95

    def test_from_dict_with_defaults(self):
        """Test creation from dictionary with default values."""
        data = {'id': 5}
        elem = FlattenedElement.from_dict(data)

        assert elem.id == 5
        assert elem.text == ''
        assert elem.type_hint == TypeHint.TEXT
        assert elem.bbox is not None
        assert elem.selection_state == SelectionState.NORMAL
        assert elem.confidence == 1.0

    def test_from_dict_fuzzy_type_hint(self):
        """Test from_dict handles fuzzy type hint matching."""
        data = {
            'id': 1,
            'type_hint': 'toggle',  # Should map to SWITCH
        }
        elem = FlattenedElement.from_dict(data)
        assert elem.type_hint == TypeHint.SWITCH

    def test_from_dict_fuzzy_selection_state(self):
        """Test from_dict handles fuzzy selection state matching."""
        data = {
            'id': 1,
            'selection_state': 'active',  # Should map to SELECTED
        }
        elem = FlattenedElement.from_dict(data)
        assert elem.selection_state == SelectionState.SELECTED
