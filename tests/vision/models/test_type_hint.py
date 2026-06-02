"""Unit tests for TypeHint enum."""

import pytest

from src.models.vision.type_hint import TypeHint


class TestTypeHintEnum:
    """Tests for TypeHint enum values and basic functionality."""

    def test_enum_values(self):
        """Test that all expected enum values exist."""
        expected_values = {
            'clickable_text',
            'switch',
            'slider',
            'button',
            'icon',
            'input_field',
            'text',
            'image',
        }
        assert set(TypeHint.values()) == expected_values

    def test_enum_instances(self):
        """Test that we can access enum instances."""
        assert TypeHint.CLICKABLE_TEXT == 'clickable_text'
        assert TypeHint.SWITCH == 'switch'
        assert TypeHint.TEXT == 'text'


class TestTypeHintFromString:
    """Tests for from_string() method."""

    def test_from_string_exact_match(self):
        """Test exact string matching."""
        assert TypeHint.from_string("clickable_text") == TypeHint.CLICKABLE_TEXT
        assert TypeHint.from_string("switch") == TypeHint.SWITCH
        assert TypeHint.from_string("text") == TypeHint.TEXT

    def test_from_string_case_insensitive(self):
        """Test case-insensitive matching."""
        assert TypeHint.from_string("CLICKABLE_TEXT") == TypeHint.CLICKABLE_TEXT
        assert TypeHint.from_string("Switch") == TypeHint.SWITCH
        assert TypeHint.from_string("  text  ") == TypeHint.TEXT

    def test_from_string_fuzzy_match(self):
        """Test fuzzy matching for common alternatives."""
        assert TypeHint.from_string("toggle") == TypeHint.SWITCH
        assert TypeHint.from_string("checkbox") == TypeHint.SWITCH
        assert TypeHint.from_string("clickable") == TypeHint.CLICKABLE_TEXT
        assert TypeHint.from_string("btn") == TypeHint.BUTTON
        assert TypeHint.from_string("input") == TypeHint.INPUT_FIELD
        assert TypeHint.from_string("img") == TypeHint.IMAGE

    def test_from_string_unknown_defaults_to_text(self):
        """Test that unknown values default to TEXT."""
        assert TypeHint.from_string("unknown_value") == TypeHint.TEXT
        assert TypeHint.from_string("12345") == TypeHint.TEXT


class TestTypeHintValidation:
    """Tests for validation methods."""

    def test_is_valid_true(self):
        """Test is_valid returns True for valid values."""
        assert TypeHint.is_valid("clickable_text")
        assert TypeHint.is_valid("switch")
        assert TypeHint.is_valid("text")

    def test_is_valid_false(self):
        """Test is_valid returns False for invalid values."""
        assert not TypeHint.is_valid("unknown")
        assert not TypeHint.is_valid("invalid_type")


class TestTypeHintHelperMethods:
    """Tests for helper methods."""

    def test_is_interactive(self):
        """Test is_interactive() method."""
        assert TypeHint.CLICKABLE_TEXT.is_interactive()
        assert TypeHint.SWITCH.is_interactive()
        assert TypeHint.BUTTON.is_interactive()
        assert TypeHint.INPUT_FIELD.is_interactive()

    def test_is_interactive_false(self):
        """Test is_interactive() returns False for non-interactive types."""
        assert not TypeHint.TEXT.is_interactive()
        assert not TypeHint.ICON.is_interactive()
        assert not TypeHint.IMAGE.is_interactive()

    def test_is_visual_only(self):
        """Test is_visual_only() method."""
        assert TypeHint.TEXT.is_visual_only()
        assert TypeHint.ICON.is_visual_only()
        assert TypeHint.IMAGE.is_visual_only()

    def test_is_visual_only_false(self):
        """Test is_visual_only() returns False for interactive types."""
        assert not TypeHint.CLICKABLE_TEXT.is_visual_only()
        assert not TypeHint.SWITCH.is_visual_only()
        assert not TypeHint.BUTTON.is_visual_only()
