# Implicit Mappings Analysis

**Date**: 2026-06-10
**Context**: Retrospective analysis of why previously working code was broken later
**Related Issue**: Element type regression causing dynamic matching failures

## Problem Statement

During the Settings traversal test investigation, we discovered that element type mappings
were duplicated across 3 test files. This duplication led to a situation where one file
could be modified (e.g., changing `menu_item` to `text`) while others remained unchanged,
breaking previously working functionality.

## Identified Implicit Mappings

### 1. Android Class Name → Element Type String

**Location**: Duplicated in 3 test files
- `tests/v6/settings/test_settings_full_traversal.py` (lines 96-103)
- `tests/v6/settings/test_target_search.py` (lines 46-53)
- `tests/v6/settings/test_settings_simulation.py` (lines 98-105)

**Current Implementation** (duplicated):
```python
class_name = elem.get('class', 'button')
if 'Switch' in class_name:
    elem_type = 'switch'
elif 'Button' in class_name:
    elem_type = 'button'
elif 'TextView' in class_name or 'LinearLayout' in class_name:
    elem_type = 'menu_item'
else:
    elem_type = 'button'
```

**Issues**:
- Code duplication means changes can be inconsistent
- No validation of class names
- No central place to update when new UI components are added
- Implicit knowledge of Android widget class names

**Should Be**:
```python
from src.models.element_type_mapper import ElementTypeMapper

elem_type = ElementTypeMapper.from_android_class(class_name)
```

---

### 2. Element Type String → MenuItemType Enum

**Location**: `src/simulation/stateful_mock_vision.py` (line 241-257)
           `src/simulation/scroll/scrollable_mock_vision.py` (line 418-434)

**Current Implementation**:
```python
def _parse_element_type(self, type_string: str) -> MenuItemType:
    type_map = {
        "menu_item": MenuItemType.MENU_ITEM,
        "switch": MenuItemType.SWITCH,
        "slider": MenuItemType.SLIDER,
        "button": MenuItemType.BUTTON,
        "text": MenuItemType.TEXT,
        "readonly": MenuItemType.READONLY,
        "item": MenuItemType.ITEM,
    }
    return type_map.get(type_string, MenuItemType.BUTTON)
```

**Issues**:
- Local mapping in each class
- No shared constants
- Fallback to `BUTTON` might not be appropriate

---

### 3. Match Condition Types

**Location**: `src/graph/template.py` (lines 371-382)
           Used in: DynamicMatcher rules

**Current Implementation**:
```python
"switch_rule": {
    "match_condition": {"type": "switch"},
    "child_template": "switch_leaf",
},
"slider_rule": {
    "match_condition": {"type": "slider"},
    "child_template": "slider_leaf",
},
"menu_rule": {
    "match_condition": {"type": "menu_item"},
    "child_template": "menu_container",
},
```

**Issues**:
- Magic strings for element types
- No validation against MenuItemType enum
- Potential typos could cause silent failures

---

### 4. ExpectedAction Type Inference

**Location**: `src/simulation/scroll/scrollable_mock_vision.py` (line 443-458)

**Current Implementation**:
```python
def _infer_expected_action(self, element: Dict) -> ExpectedAction:
    elem_type = element.get("type", "")

    if elem_type in ("switch", "toggle"):
        return ExpectedAction.TOGGLE

    if element.get("action_target"):
        return ExpectedAction.NAVIGATE

    if elem_type in ("text", "readonly"):
        return ExpectedAction.NONE

    return ExpectedAction.ACTION
```

**Issues**:
- Implicit type → action mapping
- No central definition of which types map to which actions

---

### 5. Action Hint Inference

**Location**: `src/simulation/page_analyzer.py` (line 233-248)

**Current Implementation**:
```python
def _infer_action_hint(self, element: Dict) -> str:
    element_type = element.get("type", "").lower()
    clickable = element.get("clickable", False)
    scrollable = element.get("scrollable", False)

    if clickable and element_type in ["button", "switch", "checkbox", "radio"]:
        return "click"
    elif scrollable:
        return "scroll"
    elif element_type == "slider":
        return "adjust"
    elif element_type == "input":
        return "input"
    elif element_type in ["text", "label"]:
        return "view"
    else:
        return "view"
```

**Issues**:
- Magic strings for action hints
- Type list hardcoded
- No relationship with MenuItemType

---

## Root Cause Analysis

### Process Issues

1. **No Code Review Coverage for Test Code**
   - Test code changes often bypass review
   - Implicit mappings in tests not recognized as important

2. **Lack of Centralization**
   - Each module defines its own mappings
   - No single source of truth

3. **Missing Documentation**
   - Implicit mappings not documented
   - New developers don't know where to look

### Technical Architecture Issues

1. **String-Based Type System**
   - Heavy use of magic strings
   - Enum exists but not consistently used

2. **No Validation**
   - Type strings can be typos
   - Runtime failures only

3. **Fixture Layer Mismatch**
   - Fixtures use lowercase strings
   - Production uses enums
   - Conversion happens in multiple places

---

## Proposed Solution

### 1. Create Unified ElementTypeMapper

**File**: `src/models/element_type_mapper.py`

```python
"""Central mapper for element type conversions.

Provides single source of truth for:
- Android class names → element type strings
- Element type strings → MenuItemType enum
- Element type strings → ExpectedAction enum
- Validation and constants
"""

from enum import Enum
from typing import Dict, Optional

from src.models.content_models import MenuItemType, ExpectedAction


class AndroidWidgetClass(str, Enum):
    """Android widget class names that map to element types."""
    SWITCH = "android.widget.Switch"
    BUTTON = "android.widget.Button"
    TEXT_VIEW = "android.widget.TextView"
    LINEAR_LAYOUT = "android.widget.LinearLayout"
    CHECK_BOX = "android.widget.CheckBox"
    RADIO_BUTTON = "android.widget.RadioButton"
    EDIT_TEXT = "android.widget.EditText"
    SEEKBAR = "android.widget.SeekBar"


class ElementTypeMapper:
    """Central mapper for element type conversions."""

    # Android class → element type mapping
    ANDROID_CLASS_MAP: Dict[str, str] = {
        "Switch": "switch",
        "Button": "button",
        "TextView": "menu_item",
        "LinearLayout": "menu_item",
        "CheckBox": "switch",
        "RadioButton": "switch",
        "EditText": "input",
        "SeekBar": "slider",
    }

    # Element type → MenuItemType mapping
    TYPE_TO_MENU_ITEM: Dict[str, MenuItemType] = {
        "menu_item": MenuItemType.MENU_ITEM,
        "switch": MenuItemType.SWITCH,
        "slider": MenuItemType.SLIDER,
        "button": MenuItemType.BUTTON,
        "toggle": MenuItemType.TOGGLE,
        "text": MenuItemType.TEXT,
        "readonly": MenuItemType.READONLY,
        "item": MenuItemType.ITEM,
        "input": MenuItemType.TEXT,
    }

    # Element type → ExpectedAction mapping
    TYPE_TO_EXPECTED_ACTION: Dict[str, ExpectedAction] = {
        "switch": ExpectedAction.TOGGLE,
        "toggle": ExpectedAction.TOGGLE,
        "slider": ExpectedAction.ACTION,
        "button": ExpectedAction.ACTION,
        "menu_item": ExpectedAction.NAVIGATE,
        "tab": ExpectedAction.NAVIGATE,
        "text": ExpectedAction.NONE,
        "readonly": ExpectedAction.NONE,
        "input": ExpectedAction.ACTION,
    }

    @classmethod
    def from_android_class(cls, class_name: str) -> str:
        """Map Android class name to element type string.

        Args:
            class_name: Android widget class name (full or partial)

        Returns:
            Element type string

        Example:
            >>> ElementTypeMapper.from_android_class("android.widget.Switch")
            "switch"
            >>> ElementTypeMapper.from_android_class("TextView")
            "menu_item"
        """
        for key, value in cls.ANDROID_CLASS_MAP.items():
            if key in class_name:
                return value
        return "button"  # Default fallback

    @classmethod
    def to_menu_item_type(cls, type_string: str) -> MenuItemType:
        """Convert element type string to MenuItemType enum.

        Args:
            type_string: Element type string

        Returns:
            MenuItemType enum value
        """
        return cls.TYPE_TO_MENU_ITEM.get(type_string, MenuItemType.ITEM)

    @classmethod
    def to_expected_action(cls, type_string: str) -> ExpectedAction:
        """Convert element type string to ExpectedAction enum.

        Args:
            type_string: Element type string

        Returns:
            ExpectedAction enum value
        """
        return cls.TYPE_TO_EXPECTED_ACTION.get(type_string, ExpectedAction.NONE)

    @classmethod
    def is_valid_type(cls, type_string: str) -> bool:
        """Check if type string is valid.

        Args:
            type_string: Element type string to validate

        Returns:
            True if valid, False otherwise
        """
        return type_string in cls.TYPE_TO_MENU_ITEM

    @classmethod
    def all_types(cls) -> list[str]:
        """Get all valid element type strings.

        Returns:
            List of valid type strings
        """
        return list(cls.TYPE_TO_MENU_ITEM.keys())
```

### 2. Update Test Files

Replace duplicated mapping code with:

```python
from src.models.element_type_mapper import ElementTypeMapper

# In fixture creation
elem_type = ElementTypeMapper.from_android_class(class_name)
```

### 3. Update Mock Vision Services

Replace `_parse_element_type` with:

```python
from src.models.element_type_mapper import ElementTypeMapper

type_string = element.type
menu_item_type = ElementTypeMapper.to_menu_item_type(type_string)
```

### 4. Add Runtime Validation

```python
# In DynamicMatcher
if not ElementTypeMapper.is_valid_type(item_type):
    raise ValueError(f"Invalid element type: {item_type}. "
                     f"Valid types: {ElementTypeMapper.all_types()}")
```

---

## Implementation Steps

1. ✅ Create `src/models/element_type_mapper.py`
2. ⏳ Update 3 test files to use centralized mapper
3. ⏳ Update `stateful_mock_vision.py` to use mapper
4. ⏳ Update `scrollable_mock_vision.py` to use mapper
5. ⏳ Add validation in `matcher.py`
6. ⏳ Add unit tests for mapper
7. ⏳ Update documentation

---

## Benefits

1. **Single Source of Truth**
   - All type mappings in one place
   - Easy to update and maintain

2. **Type Safety**
   - Enum-based validation
   - Runtime checks

3. **Discoverability**
   - Clear location for type-related code
   - Better IDE autocomplete

4. **Testing**
   - Easy to test all mappings
   - Validation prevents silent failures

5. **Documentation**
   - Self-documenting through enum values
   - Clear relationships between types

---

## Related Files

- `src/models/content_models.py` - MenuItemType, ExpectedAction enums
- `src/graph/template.py` - Template match conditions
- `src/graph/matcher.py` - Dynamic matching logic
- `src/simulation/stateful_mock_vision.py` - Mock vision service
- `src/simulation/page_analyzer.py` - Page analysis logic

---

## References

- Test investigation: `tests/v6/settings/`
- Git history showing regression: search for "menu_item" → "text" change
