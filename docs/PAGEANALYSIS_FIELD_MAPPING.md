# PageAnalysis Field Mapping Guide

Clarifies the field mapping between StateFixture YAML and the PageAnalysis/MenuItem models used in GraphTraversalEngine.

## Overview

The V6.9.2 implementation uses specific field mappings that are critical for compatibility between StatefulMockVisionService and GraphTraversalEngine. Incorrect mapping causes integration failures.

## Critical Mapping Rules

### Rule 1: `items` vs `menu_items`

**PageAnalysis uses `items`, NOT `menu_items`**

```python
# PageAnalysis model (src/state/content_tree.py)
class PageAnalysis(BaseModel):
    name: str
    items: List[MenuItem]  # NOT menu_items!
    # ...
```

### Rule 2: `name` vs `text`

**MenuItem stores display text in `name`, NOT `text`**

```python
# MenuItem model (src/state/content_tree.py)
class MenuItem(BaseModel):
    name: str  # Display text (from fixture `text` field)
    item_type: MenuItemType  # Element type (from fixture `type` field)
    # ...
```

## Complete Field Mapping

### StateFixture Element → MenuItem

| StateFixture Field | MenuItem Field | Notes |
|--------------------|----------------|-------|
| `id` | (not stored) | Used internally only |
| `text` | `name` | Display text |
| `type` | `item_type` | Converted to MenuItemType enum |
| `coordinate.x` | `coordinate.x` | X position (0-1) |
| `coordinate.y` | `coordinate.y` | Y position (0-1) |
| `action_target` | (not stored) | Used for transitions |

### StateFixture Page → PageAnalysis

| StateFixture Field | PageAnalysis Field | Notes |
|--------------------|--------------------|-------|
| `page_name` | `name` | Page name |
| `elements` | `items` | List of MenuItem objects |
| `is_complete` | (not stored) | Used for completion detection |

## Implementation Details

### StatefulMockVisionService._build_page_analysis()

The critical method that performs the mapping:

```python
def _build_page_analysis(self) -> PageAnalysis:
    """Build PageAnalysis for the current page state."""
    page = self._fixture.get_page(self._current_page_id)

    items = []
    for element in page.elements:
        menu_item = MenuItem(
            name=element.text,  # text → name
            item_type=self._parse_element_type(element.type),  # type → item_type
            coordinate=Coordinate(
                x=element.coordinate.x,
                y=element.coordinate.y,
            ) if element.coordinate else None,
        )
        items.append(menu_item)

    return PageAnalysis(
        name=page.page_name,
        items=items,  # elements → items
    )
```

### Type Conversion: String → MenuItemType

```python
def _parse_element_type(self, type_str: str) -> MenuItemType:
    """Convert type string to MenuItemType enum."""
    type_mapping = {
        "button": MenuItemType.BUTTON,
        "back_button": MenuItemType.BACK,
        "text_input": MenuItemType.TEXT_INPUT,
        "switch": MenuItemType.SWITCH,
        "slider": MenuItemType.SLIDER,
        "text": MenuItemType.TEXT,
        "image": MenuItemType.IMAGE,
        "list_item": MenuItemType.LIST_ITEM,
        "checkbox": MenuItemType.CHECKBOX,
        "radio": MenuItemType.RADIO,
    }
    return type_mapping.get(type_str, MenuItemType.UNKNOWN)
```

## Why This Matters

### DynamicMatcher Integration

DynamicMatcher expects MenuItem objects with specific fields:

```python
# DynamicMatcher looks for:
menu_item.name  # Display text for matching
menu_item.item_type  # Element type
menu_item.coordinate  # Position
```

If mapping is incorrect:
- DynamicMatcher won't find elements
- Tests will fail with "element not found" errors
- Traversal won't progress

### Common Errors

**Error 1: Using `menu_items` instead of `items`**

```python
# WRONG
page_analysis = PageAnalysis(
    name="Page",
    menu_items=[...]  # AttributeError: PageAnalysis has no field 'menu_items'
)

# CORRECT
page_analysis = PageAnalysis(
    name="Page",
    items=[...]  # Correct field name
)
```

**Error 2: Using `text` instead of `name`**

```python
# WRONG
menu_item = MenuItem(
    text="Submit",  # Wrong field
    item_type=MenuItemType.BUTTON
)

# CORRECT
menu_item = MenuItem(
    name="Submit",  # Correct field
    item_type=MenuItemType.BUTTON
)
```

**Error 3: Wrong field in fixture → model mapping**

```python
# WRONG - Maps fixture.text to MenuItem.text (doesn't exist)
MenuItem(text=element.text, ...)

# CORRECT - Maps fixture.text to MenuItem.name
MenuItem(name=element.text, ...)
```

## Verification

### Unit Test

Ensure you have this test to verify mapping:

```python
def test_menu_item_compatible_with_dynamic_matcher():
    """Verify MenuItem from StatefulMockVisionService works with DynamicMatcher."""
    fixture = StateFixture.from_yaml("tests/v6/fixtures/simple_two_page.yaml")
    vision = StatefulMockVisionService(fixture)

    # Get PageAnalysis
    page_analysis = vision.analyze_screenshot(b"fake_image")

    # Verify items field exists
    assert hasattr(page_analysis, "items")
    assert len(page_analysis.items) > 0

    # Verify MenuItem has correct fields
    menu_item = page_analysis.items[0]
    assert hasattr(menu_item, "name")
    assert hasattr(menu_item, "item_type")
    assert hasattr(menu_item, "coordinate")

    # Verify it works with DynamicMatcher (if applicable)
    # matcher = DynamicMatcher(...)
    # matches = matcher.find_matches(menu_item.name)
```

### Manual Verification

```python
# Load fixture
fixture = StateFixture.from_yaml("fixture.yaml")
vision = StatefulMockVisionService(fixture)

# Get analysis
analysis = vision.analyze_screenshot(b"")

# Check structure
print(f"Page name: {analysis.name}")  # Should be page_name from fixture
print(f"Items count: {len(analysis.items)}")  # Should be len(elements)

for item in analysis.items:
    print(f"  - {item.name} ({item.item_type})")  # Should be element.text, element.type
```

## Diagram

```
┌─────────────────────────┐
│  StateFixture YAML      │
│                         │
│  pages:                 │
│    home:                │
│      page_name: "Home"  │
│      elements:           │
│        - text: "Submit" │  ───┐
│          type: "button" │    │
└─────────────────────────┘    │
                              │
                              │ maps to
                              ↓
┌─────────────────────────┐   │
│  PageAnalysis           │   │
│                         │   │
│  name: "Home"           │ ←──┘ page_name
│  items: [               │
│    MenuItem {           │
│      name: "Submit"     │ ←─ element.text
│      item_type: BUTTON  │ ←─ element.type (enum)
│      coordinate: {...}  │
│    }                    │
│  ]                      │
└─────────────────────────┘
```

## Quick Reference

| YAML Field | Model | Field |
|------------|-------|-------|
| `pages[].page_name` | PageAnalysis | `name` |
| `pages[].elements[]` | PageAnalysis | `items` |
| `elements[].text` | MenuItem | `name` |
| `elements[].type` | MenuItem | `item_type` |
| `elements[].coordinate` | MenuItem | `coordinate` |

## Summary

**Key Takeaways:**

1. **`items` not `menu_items`** - PageAnalysis uses `items`
2. **`name` not `text`** - MenuItem stores display text in `name`
3. **Test the mapping** - Verify with unit tests
4. **Check DynamicMatcher** - Ensure compatibility with matching logic

**If tests fail:**

1. Check PageAnalysis has `items` field
2. Check MenuItem has `name` field (not `text`)
3. Verify _build_page_analysis() mapping
4. Run test_menu_item_compatible_with_dynamic_matcher()

---

**Last Updated:** 2026-06-07
**Version:** V6.9.2
