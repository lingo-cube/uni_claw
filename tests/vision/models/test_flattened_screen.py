"""Unit tests for FlattenedScreen model."""

import pytest

from src.models.vision.flattened_screen import FlattenedScreen
from src.models.vision.flattened_element import FlattenedElement
from src.models.vision.bounding_box import BoundingBox
from src.models.vision.type_hint import TypeHint
from src.models.vision.selection_state import SelectionState
from src.models.vision.screen_hints import ScreenHints


class TestFlattenedScreenCreation:
    """Tests for FlattenedScreen creation and initialization."""

    def test_creation_empty(self):
        """Test creating empty FlattenedScreen."""
        screen = FlattenedScreen()
        assert screen.element_count() == 0
        assert screen.elements == []
        assert screen.screen_hints == {}

    def test_creation_with_elements(self):
        """Test creating FlattenedScreen with elements."""
        elem1 = FlattenedElement(id=0, text="First")
        elem2 = FlattenedElement(id=1, text="Second")
        screen = FlattenedScreen(elements=[elem1, elem2])
        assert screen.element_count() == 2

    def test_auto_sort_by_position(self):
        """Test that elements are automatically sorted by position."""
        # Create elements in random order
        elem1 = FlattenedElement(
            id=0,
            bbox=BoundingBox(x=0.1, y=0.5, w=0.1, h=0.1)
        )
        elem2 = FlattenedElement(
            id=1,
            bbox=BoundingBox(x=0.1, y=0.1, w=0.1, h=0.1)
        )
        elem3 = FlattenedElement(
            id=2,
            bbox=BoundingBox(x=0.5, y=0.1, w=0.1, h=0.1)
        )

        # Add in random order
        screen = FlattenedScreen(elements=[elem1, elem2, elem3])

        # Should be sorted: y first, then x
        assert screen.elements[0].id == 1  # (0.1, 0.1) - lowest y
        assert screen.elements[1].id == 2  # (0.5, 0.1) - same y, higher x
        assert screen.elements[2].id == 0  # (0.1, 0.5) - highest y


class TestFlattenedScreenQueries:
    """Tests for query methods."""

    def test_element_count(self):
        """Test element_count() method."""
        elem1 = FlattenedElement(id=0)
        elem2 = FlattenedElement(id=1)
        screen = FlattenedScreen(elements=[elem1, elem2])
        assert screen.element_count() == 2

    def test_get_elements_in_region(self):
        """Test get_elements_in_region() method."""
        elem1 = FlattenedElement(id=0, region="left_panel")
        elem2 = FlattenedElement(id=1, region="content_area")
        elem3 = FlattenedElement(id=2, region="left_panel")

        screen = FlattenedScreen(elements=[elem1, elem2, elem3])

        left_panel_elems = screen.get_elements_in_region("left_panel")
        assert len(left_panel_elems) == 2
        assert {e.id for e in left_panel_elems} == {0, 2}

    def test_get_selected_elements(self):
        """Test get_selected_elements() method."""
        elem1 = FlattenedElement(
            id=0,
            selection_state=SelectionState.SELECTED
        )
        elem2 = FlattenedElement(
            id=1,
            selection_state=SelectionState.NORMAL
        )
        elem3 = FlattenedElement(
            id=2,
            selection_state=SelectionState.SELECTED
        )

        screen = FlattenedScreen(elements=[elem1, elem2, elem3])

        selected = screen.get_selected_elements()
        assert len(selected) == 2
        assert {e.id for e in selected} == {0, 2}

    def test_get_elements_by_type(self):
        """Test get_elements_by_type() method."""
        elem1 = FlattenedElement(
            id=0,
            type_hint=TypeHint.CLICKABLE_TEXT
        )
        elem2 = FlattenedElement(
            id=1,
            type_hint=TypeHint.TEXT
        )
        elem3 = FlattenedElement(
            id=2,
            type_hint=TypeHint.CLICKABLE_TEXT
        )

        screen = FlattenedScreen(elements=[elem1, elem2, elem3])

        clickable = screen.get_elements_by_type("clickable_text")
        assert len(clickable) == 2
        assert {e.id for e in clickable} == {0, 2}

    def test_get_interactive_elements(self):
        """Test get_interactive_elements() method."""
        elem1 = FlattenedElement(
            id=0,
            type_hint=TypeHint.CLICKABLE_TEXT,
            selection_state=SelectionState.NORMAL,
        )
        elem2 = FlattenedElement(
            id=1,
            type_hint=TypeHint.TEXT,
            selection_state=SelectionState.NORMAL,
        )
        elem3 = FlattenedElement(
            id=2,
            type_hint=TypeHint.BUTTON,
            selection_state=SelectionState.DISABLED,
        )

        screen = FlattenedScreen(elements=[elem1, elem2, elem3])

        interactive = screen.get_interactive_elements()
        assert len(interactive) == 1
        assert interactive[0].id == 0


class TestFlattenedScreenSerialization:
    """Tests for serialization methods."""

    def test_to_dict(self):
        """Test conversion to dictionary."""
        elem = FlattenedElement(
            id=0,
            text="Test",
            bbox=BoundingBox(x=0.1, y=0.2, w=0.3, h=0.4),
        )
        screen = FlattenedScreen(
            elements=[elem],
            screen_hints={'layout_type': 'split_pane'},
        )

        result = screen.to_dict()
        assert len(result['elements']) == 1
        assert result['elements'][0]['text'] == 'Test'
        assert result['screen_hints']['layout_type'] == 'split_pane'

    def test_from_dict(self):
        """Test creation from dictionary."""
        data = {
            'elements': [
                {
                    'id': 0,
                    'text': 'Test',
                    'type_hint': 'text',
                    'bbox': {'x': 0.1, 'y': 0.2, 'w': 0.3, 'h': 0.4},
                    'selection_state': 'normal',
                    'visual_state': {},
                    'confidence': 1.0,
                }
            ],
            'screen_hints': {'layout_type': 'split_pane'},
        }

        screen = FlattenedScreen.from_dict(data)
        assert screen.element_count() == 1
        assert screen.elements[0].text == 'Test'
        assert screen.screen_hints['layout_type'] == 'split_pane'


class TestFlattenedScreenHints:
    """Tests for screen hints methods."""

    def test_get_screen_hints(self):
        """Test get_screen_hints() method."""
        screen = FlattenedScreen(
            screen_hints={
                'top_bar_text': 'Settings',
                'layout_type': 'split_pane',
                'overlay_detected': False,
            }
        )

        hints = screen.get_screen_hints()
        assert isinstance(hints, ScreenHints)
        assert hints.top_bar_text == 'Settings'
        assert hints.layout_type == 'split_pane'

    def test_set_screen_hints(self):
        """Test set_screen_hints() method."""
        hints = ScreenHints(
            top_bar_text='Settings',
            layout_type='split_pane',
        )

        screen = FlattenedScreen()
        screen.set_screen_hints(hints)

        assert screen.screen_hints['top_bar_text'] == 'Settings'
        assert screen.screen_hints['layout_type'] == 'split_pane'
