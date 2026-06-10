"""Central mapper for element type conversions.

Provides single source of truth for:
- Android class names → element type strings
- Element type strings → MenuItemType enum
- Element type strings → ExpectedAction enum
- Validation and constants

This module consolidates all implicit element type mappings that were
previously scattered across test files and simulation services.

Motivation:
During the Settings traversal test investigation (V6.11.0), we discovered
element type mappings were duplicated in 3 test files, causing regressions
when one file was updated without updating the others. This centralized
mapper prevents such issues by providing a single source of truth.

Example:
    >>> ElementTypeMapper.from_android_class("android.widget.Switch")
    'switch'
    >>> ElementTypeMapper.to_menu_item_type("switch")
    <MenuItemType.SWITCH: 'switch'>
"""

from enum import Enum
from typing import Dict, List, Optional

from src.models.content_models import MenuItemType, ExpectedAction


class AndroidWidgetClass(str, Enum):
    """Android widget class names that map to element types.

    Common Android widget classes used in UI element classification.
    """

    # Toggles
    SWITCH = "android.widget.Switch"
    CHECK_BOX = "android.widget.CheckBox"
    RADIO_BUTTON = "android.widget.RadioButton"
    TOGGLE_BUTTON = "android.widget.ToggleButton"

    # Buttons
    BUTTON = "android.widget.Button"
    IMAGE_BUTTON = "android.widget.ImageButton"

    # Text
    TEXT_VIEW = "android.widget.TextView"
    EDIT_TEXT = "android.widget.EditText"

    # Layouts
    LINEAR_LAYOUT = "android.widget.LinearLayout"
    RELATIVE_LAYOUT = "android.widget.RelativeLayout"
    FRAME_LAYOUT = "android.widget.FrameLayout"
    CONSTRAINT_LAYOUT = "androidx.constraintlayout.widget.ConstraintLayout"

    # Seekable
    SEEK_BAR = "android.widget.SeekBar"
    RATING_BAR = "android.widget.RatingBar"


class ElementTypeMapper:
    """Central mapper for element type conversions.

    Provides bidirectional mapping between:
    - Android widget class names
    - Element type strings (lowercase)
    - MenuItemType enum values
    - ExpectedAction enum values

    All mappings are centralized here to prevent inconsistencies
    and provide validation.
    """

    # ============================================================================
    # Android Class → Element Type Mapping
    # ============================================================================

    ANDROID_CLASS_MAP: Dict[str, str] = {
        # Toggles/Switches
        "Switch": "switch",
        "CheckBox": "switch",
        "RadioButton": "switch",
        "ToggleButton": "toggle",

        # Buttons
        "Button": "button",
        "ImageButton": "button",

        # Text/Labels (often used as menu items)
        "TextView": "menu_item",
        "EditText": "input",

        # Layouts (often used as container menu items)
        "LinearLayout": "menu_item",
        "RelativeLayout": "menu_item",
        "FrameLayout": "menu_item",
        "ConstraintLayout": "menu_item",

        # Seekable elements
        "SeekBar": "slider",
        "RatingBar": "slider",
    }

    # ============================================================================
    # Element Type → MenuItemType Mapping
    # ============================================================================

    TYPE_TO_MENU_ITEM: Dict[str, MenuItemType] = {
        "menu_item": MenuItemType.MENU_ITEM,
        "switch": MenuItemType.SWITCH,
        "slider": MenuItemType.BUTTON,  # Sliders map to BUTTON (action type)
        "button": MenuItemType.BUTTON,
        "toggle": MenuItemType.TOGGLE,
        "text": MenuItemType.TEXT,
        "readonly": MenuItemType.READONLY,
        "item": MenuItemType.ITEM,
        "input": MenuItemType.TEXT,
        "icon": MenuItemType.ICON,
        "link": MenuItemType.LINK,
        "tab": MenuItemType.TAB,
        "back_button": MenuItemType.BACK_BUTTON,
    }

    # ============================================================================
    # Element Type → ExpectedAction Mapping
    # ============================================================================

    TYPE_TO_EXPECTED_ACTION: Dict[str, ExpectedAction] = {
        # Toggles change state
        "switch": ExpectedAction.TOGGLE,
        "toggle": ExpectedAction.TOGGLE,

        # Sliders adjust values
        "slider": ExpectedAction.ACTION,

        # Buttons trigger actions
        "button": ExpectedAction.ACTION,

        # Menu items navigate
        "menu_item": ExpectedAction.NAVIGATE,
        "tab": ExpectedAction.NAVIGATE,

        # Text is read-only
        "text": ExpectedAction.NONE,
        "readonly": ExpectedAction.NONE,

        # Inputs trigger input
        "input": ExpectedAction.ACTION,

        # Icons can be various actions
        "icon": ExpectedAction.ACTION,
        "link": ExpectedAction.NAVIGATE,

        # Back buttons navigate back
        "back_button": ExpectedAction.NAVIGATE,
    }

    # ============================================================================
    # Class Methods
    # ============================================================================

    @classmethod
    def from_android_class(cls, class_name: str) -> str:
        """Map Android class name to element type string.

        This method performs substring matching, so you can pass either
        the full class name (e.g., "android.widget.Switch") or just the
        class name (e.g., "Switch").

        Args:
            class_name: Android widget class name (full or partial)

        Returns:
            Element type string (lowercase)

        Raises:
            TypeError: If class_name is not a string

        Example:
            >>> ElementTypeMapper.from_android_class("android.widget.Switch")
            'switch'
            >>> ElementTypeMapper.from_android_class("TextView")
            'menu_item'
            >>> ElementTypeMapper.from_android_class("Unknown")
            'button'
        """
        if not isinstance(class_name, str):
            raise TypeError(f"class_name must be a string, got {type(class_name)}")

        # Try exact match first
        if class_name in cls.ANDROID_CLASS_MAP:
            return cls.ANDROID_CLASS_MAP[class_name]

        # Try substring match
        for key, value in cls.ANDROID_CLASS_MAP.items():
            if key in class_name:
                return value

        # Default fallback
        return "button"

    @classmethod
    def to_menu_item_type(cls, type_string: str) -> MenuItemType:
        """Convert element type string to MenuItemType enum.

        Args:
            type_string: Element type string (e.g., "switch", "menu_item")

        Returns:
            MenuItemType enum value

        Raises:
            TypeError: If type_string is not a string

        Example:
            >>> ElementTypeMapper.to_menu_item_type("switch")
            <MenuItemType.SWITCH: 'switch'>
            >>> ElementTypeMapper.to_menu_item_type("unknown")
            <MenuItemType.ITEM: 'item'>
        """
        if not isinstance(type_string, str):
            raise TypeError(f"type_string must be a string, got {type(type_string)}")

        return cls.TYPE_TO_MENU_ITEM.get(type_string, MenuItemType.ITEM)

    @classmethod
    def to_expected_action(cls, type_string: str) -> ExpectedAction:
        """Convert element type string to ExpectedAction enum.

        Args:
            type_string: Element type string (e.g., "switch", "menu_item")

        Returns:
            ExpectedAction enum value

        Raises:
            TypeError: If type_string is not a string

        Example:
            >>> ElementTypeMapper.to_expected_action("switch")
            <ExpectedAction.TOGGLE: 'toggle'>
            >>> ElementTypeMapper.to_expected_action("text")
            <ExpectedAction.NONE: 'none'>
        """
        if not isinstance(type_string, str):
            raise TypeError(f"type_string must be a string, got {type(type_string)}")

        return cls.TYPE_TO_EXPECTED_ACTION.get(type_string, ExpectedAction.NONE)

    @classmethod
    def is_valid_type(cls, type_string: str) -> bool:
        """Check if type string is a valid element type.

        Args:
            type_string: Element type string to validate

        Returns:
            True if valid, False otherwise

        Example:
            >>> ElementTypeMapper.is_valid_type("switch")
            True
            >>> ElementTypeMapper.is_valid_type("invalid_type")
            False
        """
        return type_string in cls.TYPE_TO_MENU_ITEM

    @classmethod
    def is_valid_android_class(cls, class_name: str) -> bool:
        """Check if class name contains a known Android widget class.

        Args:
            class_name: Android widget class name to check

        Returns:
            True if contains known class, False otherwise

        Example:
            >>> ElementTypeMapper.is_valid_android_class("android.widget.Switch")
            True
            >>> ElementTypeMapper.is_valid_android_class("com.unknown.Widget")
            False
        """
        if not isinstance(class_name, str):
            return False

        for key in cls.ANDROID_CLASS_MAP.keys():
            if key in class_name:
                return True
        return False

    @classmethod
    def all_types(cls) -> List[str]:
        """Get all valid element type strings.

        Returns:
            List of valid type strings

        Example:
            >>> ElementTypeMapper.all_types()
            ['menu_item', 'switch', 'slider', 'button', ...]
        """
        return list(cls.TYPE_TO_MENU_ITEM.keys())

    @classmethod
    def all_menu_item_types(cls) -> List[MenuItemType]:
        """Get all MenuItemType enum values.

        Returns:
            List of MenuItemType enum values
        """
        return list(MenuItemType)

    @classmethod
    def validate_and_convert(cls, type_string: str) -> MenuItemType:
        """Validate type string and convert to MenuItemType.

        This is a convenience method that validates and converts in one call.
        Use this when you need both validation and conversion.

        Args:
            type_string: Element type string to validate and convert

        Returns:
            MenuItemType enum value

        Raises:
            ValueError: If type_string is not valid

        Example:
            >>> ElementTypeMapper.validate_and_convert("switch")
            <MenuItemType.SWITCH: 'switch'>
        """
        if not cls.is_valid_type(type_string):
            raise ValueError(
                f"Invalid element type: '{type_string}'. "
                f"Valid types: {cls.all_types()}"
            )
        return cls.to_menu_item_type(type_string)


# ============================================================================
# Convenience Functions
# ============================================================================

def map_android_class(class_name: str) -> str:
    """Convenience function to map Android class to element type.

    Args:
        class_name: Android widget class name

    Returns:
        Element type string
    """
    return ElementTypeMapper.from_android_class(class_name)


def to_menu_item_type(type_string: str) -> MenuItemType:
    """Convenience function to convert type string to MenuItemType.

    Args:
        type_string: Element type string

    Returns:
        MenuItemType enum value
    """
    return ElementTypeMapper.to_menu_item_type(type_string)


def to_expected_action(type_string: str) -> ExpectedAction:
    """Convenience function to convert type string to ExpectedAction.

    Args:
        type_string: Element type string

    Returns:
        ExpectedAction enum value
    """
    return ElementTypeMapper.to_expected_action(type_string)
