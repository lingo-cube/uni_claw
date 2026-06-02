"""Unit tests for SelectionState enum."""

import pytest

from src.models.vision.selection_state import SelectionState


class TestSelectionStateEnum:
    """Tests for SelectionState enum values and basic functionality."""

    def test_enum_values(self):
        """Test that all expected enum values exist."""
        expected_values = {'selected', 'normal', 'disabled'}
        assert set(SelectionState.values()) == expected_values

    def test_enum_instances(self):
        """Test that we can access enum instances."""
        assert SelectionState.SELECTED == 'selected'
        assert SelectionState.NORMAL == 'normal'
        assert SelectionState.DISABLED == 'disabled'


class TestSelectionStateFromString:
    """Tests for from_string() method."""

    def test_from_string_exact_match(self):
        """Test exact string matching."""
        assert SelectionState.from_string("selected") == SelectionState.SELECTED
        assert SelectionState.from_string("normal") == SelectionState.NORMAL
        assert SelectionState.from_string("disabled") == SelectionState.DISABLED

    def test_from_string_case_insensitive(self):
        """Test case-insensitive matching."""
        assert SelectionState.from_string("SELECTED") == SelectionState.SELECTED
        assert SelectionState.from_string("Normal") == SelectionState.NORMAL
        assert SelectionState.from_string("  disabled  ") == SelectionState.DISABLED

    def test_from_string_fuzzy_selected(self):
        """Test fuzzy matching for selected/active states."""
        assert SelectionState.from_string("active") == SelectionState.SELECTED
        assert SelectionState.from_string("highlighted") == SelectionState.SELECTED
        assert SelectionState.from_string("highlight") == SelectionState.SELECTED
        assert SelectionState.from_string("checked") == SelectionState.SELECTED

    def test_from_string_fuzzy_disabled(self):
        """Test fuzzy matching for disabled states."""
        assert SelectionState.from_string("gray") == SelectionState.DISABLED
        assert SelectionState.from_string("grayed") == SelectionState.DISABLED
        assert SelectionState.from_string("dimmed") == SelectionState.DISABLED
        assert SelectionState.from_string("inactive") == SelectionState.DISABLED
        assert SelectionState.from_string("hidden") == SelectionState.DISABLED

    def test_from_string_unknown_defaults_to_normal(self):
        """Test that unknown values default to NORMAL."""
        assert SelectionState.from_string("unknown_value") == SelectionState.NORMAL
        assert SelectionState.from_string("random") == SelectionState.NORMAL


class TestSelectionStateValidation:
    """Tests for validation methods."""

    def test_is_valid_true(self):
        """Test is_valid returns True for valid values."""
        assert SelectionState.is_valid("selected")
        assert SelectionState.is_valid("normal")
        assert SelectionState.is_valid("disabled")

    def test_is_valid_false(self):
        """Test is_valid returns False for invalid values."""
        assert not SelectionState.is_valid("unknown")
        assert not SelectionState.is_valid("active")


class TestSelectionStateHelperMethods:
    """Tests for helper methods."""

    def test_is_interactive(self):
        """Test is_interactive() method."""
        assert SelectionState.SELECTED.is_interactive()
        assert SelectionState.NORMAL.is_interactive()
        assert not SelectionState.DISABLED.is_interactive()

    def test_is_active(self):
        """Test is_active() method."""
        assert SelectionState.SELECTED.is_active()
        assert not SelectionState.NORMAL.is_active()
        assert not SelectionState.DISABLED.is_active()
