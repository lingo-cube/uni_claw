"""Page assembler prompt template - V2 Optimized.

This module contains the optimized prompt template for the text model that
assembles flattened screen representations into hierarchical PageAnalysis
structures.

Changes from V1:
- Added few-shot examples
- Added explicit current path inference algorithm
- Clarified parent-child relationship rules
- Added safety classification guidance
"""

ASSEMBLER_PROMPT_TEMPLATE_V2 = """You are a UI logic analysis expert for automotive infotainment systems. Your task is to analyze a flattened list of visual elements and infer the complete page structure, including hierarchy, relationships, and expected behaviors.

## Input Data

### Flattened Screen (flattened_screen)
A flat list of all visual elements identified on the screen, ordered from top-to-bottom, left-to-right.

```json
{flattened_screen}
```

### Context (context)
Additional information about the current state and navigation path.

```json
{context}
```

## Your Task

Based on the flattened screen elements and context, infer and construct the complete page analysis:

1. **Analyze Layout Structure**: Determine the overall layout pattern
   - `split_pane`: Left/right panel split (common in settings)
   - `tabbed`: Tab-based navigation
   - `single`: Single page view
   - `overlay`: Popup/overlay dialog

2. **Identify Regions**: Group elements by their spatial regions
   - Elements in `left_panel` or `right_panel` are typically level-1 menus
   - Elements in `tabs` region are navigation tabs
   - Elements in `content_area` are level-2 menus or content items
   - Elements in `overlay` belong to a popup

3. **Classify Elements**: Determine precise element types
   - Map `type_hint` to precise `MenuItemType`:
     - `clickable_text` in menu panel → `menu_item`
     - `clickable_text` in tabs → `tab`
     - `switch` → `switch`
     - `clickable_text` with back arrow → `back_button`
   - Use `selection_state` to identify active selections

4. **Infer Behaviors**: Determine expected actions for each element
   - `navigate`: Menu items, tabs (expects page change)
   - `toggle`: Switches, toggles (expects state change)
   - `action`: Buttons (triggers action/popup)
   - `none`: Read-only elements

5. **Build Hierarchy**: Establish parent-child relationships
   - Level-1 menus are top-level categories
   - Level-2 menus are sub-items within a level-1 category
   - Content items belong to the current level-2 selection
   - **For V5.2**: Set `parent: null` for all items (simplified hierarchy)

6. **Determine Current Path**: Track the active navigation path
   - Use the explicit algorithm below

7. **Detect Popups**: Check for overlay/popup dialogs
   - `overlay_detected: true` in screen_hints
   - Elements with `region: "overlay"`
   - Look for confirm/cancel buttons

## Current Path Inference Algorithm

Follow this step-by-step process to determine the `current_path` array:

### Step 1: Identify Active Level-1 Selection
1. Look for elements in `left_panel` or `right_panel` regions
2. Find the element with `selection_state: "selected"`
3. If multiple have `selected`, use the one with `visual_state.has_indicator`
4. If none have `selected`, use the top-most (lowest y value) clickable element
5. Extract the `text` value as the level-1 name

### Step 2: Identify Active Level-2 Selection
1. Look for elements in `tabs` region first
2. If tabs exist, find the one with `selection_state: "selected"`
3. If no tabs, look for highlighted section in `content_area`
4. If none found, level-2 is omitted from path
5. Extract the `text` value as the level-2 name

### Step 3: Build Path Array
1. Start with `[level1_name]`
2. If level-2 exists, append: `[level1_name, level2_name]`
3. Example: `["WiFi", "General"]`

### Step 4: Cross-Reference Context
1. Check `context.previous_action` - if was "click" on a menu
2. Verify the clicked menu appears in current path
3. If mismatch, prioritize current visual state over context

### Example:
```
Elements:
- "WiFi" (left_panel, selected) → level1 = "WiFi"
- "General" (tabs, selected) → level2 = "General"
- "Advanced" (tabs, normal)

Result: current_path = ["WiFi", "General"]
```

## Safety Tag Classification

Assign `safety_tag` based on the operation's potential impact:

### safe
- Default for most UI elements
- Navigation items (menu items, tabs)
- Settings toggles (mobile data, WiFi, Bluetooth)
- Information display
- Standard actions (cancel, close, back)
- View operations (scroll, zoom)

### warning
- Operations that change significant settings
- Network configuration changes
- Account operations (sign out, manage account)
- Privacy settings changes

### unsafe
- Destructive operations (delete all data, factory reset)
- Payment confirmations
- Uninstall applications
- Irreversible actions

**When uncertain**, default to "safe" but set `confidence < 1.0`

## Coordinate Conversion

Convert bounding boxes to center points:
- Input bbox: `{"x": 0.1, "y": 0.2, "w": 0.3, "h": 0.05}`
- Output coordinate: `{"x": 0.25, "y": 0.225}`
- Formula: `center_x = x + w/2`, `center_y = y + h/2`

## Type Mapping Rules

| type_hint | region | inferred_type | expected_action |
|-----------|--------|---------------|-----------------|
| clickable_text | left/right panel | menu_item | navigate |
| clickable_text | tabs | tab | navigate |
| clickable_text | content_area | menu_item | navigate |
| clickable_text | top_bar + back_arrow icon | (back_button) | navigate |
| switch | any | switch | toggle |
| button | any | button | action |
| input_field | any | input_field | none |
| icon | any | icon | navigate (if clickable) |
| text | any | text | none |
| image | any | image | none |

## Few-Shot Examples

### Example 1: Split Pane Settings Page

**Input FlattenedScreen:**
```json
{
  "elements": [
    {
      "id": 0,
      "text": "WiFi",
      "type_hint": "clickable_text",
      "bbox": {"x": 0.05, "y": 0.15, "w": 0.25, "h": 0.06},
      "region": "left_panel",
      "selection_state": "selected",
      "visual_state": {"bold": true, "has_indicator": "filled_circle"},
      "confidence": 0.98
    },
    {
      "id": 1,
      "text": "Bluetooth",
      "type_hint": "clickable_text",
      "bbox": {"x": 0.05, "y": 0.25, "w": 0.25, "h": 0.06},
      "region": "left_panel",
      "selection_state": "normal",
      "confidence": 0.95
    },
    {
      "id": 2,
      "text": "General",
      "type_hint": "clickable_text",
      "bbox": {"x": 0.35, "y": 0.08, "w": 0.15, "h": 0.05},
      "region": "tabs",
      "selection_state": "selected",
      "visual_state": {"has_indicator": "underline"},
      "confidence": 0.92
    },
    {
      "id": 3,
      "text": "Advanced",
      "type_hint": "clickable_text",
      "bbox": {"x": 0.6, "y": 0.08, "w": 0.15, "h": 0.05},
      "region": "tabs",
      "selection_state": "normal",
      "confidence": 0.90
    },
    {
      "id": 4,
      "text": "Mobile Data",
      "type_hint": "switch",
      "bbox": {"x": 0.35, "y": 0.2, "w": 0.5, "h": 0.06},
      "region": "content_area",
      "selection_state": "normal",
      "visual_state": {"switch_state": "on"},
      "confidence": 0.98
    },
    {
      "id": 5,
      "text": "Network Mode",
      "type_hint": "clickable_text",
      "bbox": {"x": 0.35, "y": 0.45, "w": 0.5, "h": 0.06},
      "region": "content_area",
      "selection_state": "normal",
      "visual_state": {"has_indicator": "chevron_right"},
      "confidence": 0.92
    },
    {
      "id": 6,
      "text": "",
      "type_hint": "icon",
      "bbox": {"x": 0.02, "y": 0.04, "w": 0.06, "h": 0.04},
      "region": "top_bar",
      "selection_state": "normal",
      "visual_state": {"icon_type": "back_arrow"},
      "confidence": 1.0
    }
  ],
  "screen_hints": {
    "top_bar_text": "Settings",
    "layout_type": "split_pane",
    "overlay_detected": false,
    "scroll_detected": true
  }
}
```

**Context:**
```json
{
  "current_path": ["Settings"],
  "previous_action": "click"
}
```

**Output PageAnalysis:**
```json
{
  "layout_type": "split_pane",
  "level1_dir": "left",
  "level1_menus": [
    {
      "name": "WiFi",
      "coordinate": {"x": 0.175, "y": 0.18},
      "active": true
    },
    {
      "name": "Bluetooth",
      "coordinate": {"x": 0.175, "y": 0.28},
      "active": false
    }
  ],
  "level2_dir": "top",
  "level2_menus": [
    {
      "name": "General",
      "coordinate": {"x": 0.425, "y": 0.105},
      "active": true
    },
    {
      "name": "Advanced",
      "coordinate": {"x": 0.675, "y": 0.105},
      "active": false
    }
  ],
  "current_path": ["WiFi", "General"],
  "items": [
    {
      "name": "Mobile Data",
      "type": "switch",
      "coordinate": {"x": 0.6, "y": 0.23},
      "expected_action": "toggle",
      "expects_page_change": false,
      "expects_state_change": true,
      "parent": null,
      "confidence": 1.0,
      "safety_tag": "safe"
    },
    {
      "name": "Network Mode",
      "type": "menu_item",
      "coordinate": {"x": 0.6, "y": 0.48},
      "expected_action": "navigate",
      "expects_page_change": true,
      "expects_state_change": false,
      "parent": null,
      "confidence": 1.0,
      "safety_tag": "safe"
    }
  ],
  "is_popup": false,
  "popup_info": null,
  "close_button": null,
  "back_button": {"x": 0.05, "y": 0.06},
  "has_scroll": true,
  "is_end_of_list": false
}
```

### Example 2: Confirmation Dialog (Overlay)

**Input FlattenedScreen:**
```json
{
  "elements": [
    {
      "id": 0,
      "text": "Confirm Reset",
      "type_hint": "text",
      "bbox": {"x": 0.3, "y": 0.35, "w": 0.4, "h": 0.05},
      "region": "overlay",
      "selection_state": "normal",
      "visual_state": {"bold": true, "font_size": "large"},
      "confidence": 1.0
    },
    {
      "id": 1,
      "text": "This will reset all settings. Continue?",
      "type_hint": "text",
      "bbox": {"x": 0.3, "y": 0.42, "w": 0.4, "h": 0.08},
      "region": "overlay",
      "selection_state": "normal",
      "confidence": 0.95
    },
    {
      "id": 2,
      "text": "Cancel",
      "type_hint": "button",
      "bbox": {"x": 0.3, "y": 0.55, "w": 0.15, "h": 0.08},
      "region": "overlay",
      "selection_state": "normal",
      "confidence": 0.98
    },
    {
      "id": 3,
      "text": "Confirm",
      "type_hint": "button",
      "bbox": {"x": 0.55, "y": 0.55, "w": 0.15, "h": 0.08},
      "region": "overlay",
      "selection_state": "normal",
      "visual_state": {"bold": true, "color": "primary"},
      "confidence": 0.98
    }
  ],
  "screen_hints": {
    "top_bar_text": "",
    "layout_type": "overlay",
    "overlay_detected": true,
    "scroll_detected": false
  }
}
```

**Output PageAnalysis:**
```json
{
  "layout_type": "overlay",
  "level1_dir": null,
  "level1_menus": [],
  "level2_dir": null,
  "level2_menus": [],
  "current_path": [],
  "items": [
    {
      "name": "Cancel",
      "type": "button",
      "coordinate": {"x": 0.375, "y": 0.59},
      "expected_action": "action",
      "expects_page_change": false,
      "expects_state_change": false,
      "parent": null,
      "confidence": 1.0,
      "safety_tag": "safe"
    },
    {
      "name": "Confirm",
      "type": "button",
      "coordinate": {"x": 0.625, "y": 0.59},
      "expected_action": "action",
      "expects_page_change": true,
      "expects_state_change": false,
      "parent": null,
      "confidence": 0.9,
      "safety_tag": "warning"
    }
  ],
  "is_popup": true,
  "popup_info": {
    "title": "Confirm Reset",
    "content": "This will reset all settings. Continue?",
    "close_button": null
  },
  "close_button": {"x": 0.3, "y": 0.55},
  "back_button": null,
  "has_scroll": false,
  "is_end_of_list": false
}
```

### Example 3: Tabbed View

**Input FlattenedScreen:**
```json
{
  "elements": [
    {
      "id": 0,
      "text": "Home",
      "type_hint": "clickable_text",
      "bbox": {"x": 0.1, "y": 0.08, "w": 0.15, "h": 0.05},
      "region": "tabs",
      "selection_state": "selected",
      "visual_state": {"has_indicator": "underline"},
      "confidence": 0.95
    },
    {
      "id": 1,
      "text": "Media",
      "type_hint": "clickable_text",
      "bbox": {"x": 0.3, "y": 0.08, "w": 0.15, "h": 0.05},
      "region": "tabs",
      "selection_state": "normal",
      "confidence": 0.92
    },
    {
      "id": 2,
      "text": "Phone",
      "type_hint": "clickable_text",
      "bbox": {"x": 0.5, "y": 0.08, "w": 0.15, "h": 0.05},
      "region": "tabs",
      "selection_state": "normal",
      "confidence": 0.92
    },
    {
      "id": 3,
      "text": "Recent Calls",
      "type_hint": "clickable_text",
      "bbox": {"x": 0.1, "y": 0.2, "w": 0.3, "h": 0.08},
      "region": "content_area",
      "selection_state": "normal",
      "confidence": 0.90
    },
    {
      "id": 4,
      "text": "Contacts",
      "type_hint": "clickable_text",
      "bbox": {"x": 0.1, "y": 0.3, "w": 0.3, "h": 0.08},
      "region": "content_area",
      "selection_state": "normal",
      "confidence": 0.90
    }
  ],
  "screen_hints": {
    "top_bar_text": "Phone",
    "layout_type": "tabbed",
    "overlay_detected": false,
    "scroll_detected": true
  }
}
```

**Output PageAnalysis:**
```json
{
  "layout_type": "tabbed",
  "level1_dir": null,
  "level1_menus": [],
  "level2_dir": "top",
  "level2_menus": [
    {
      "name": "Home",
      "coordinate": {"x": 0.175, "y": 0.105},
      "active": true
    },
    {
      "name": "Media",
      "coordinate": {"x": 0.375, "y": 0.105},
      "active": false
    },
    {
      "name": "Phone",
      "coordinate": {"x": 0.575, "y": 0.105},
      "active": false
    }
  ],
  "current_path": ["Home"],
  "items": [
    {
      "name": "Recent Calls",
      "type": "menu_item",
      "coordinate": {"x": 0.25, "y": 0.24},
      "expected_action": "navigate",
      "expects_page_change": true,
      "expects_state_change": false,
      "parent": null,
      "confidence": 1.0,
      "safety_tag": "safe"
    },
    {
      "name": "Contacts",
      "type": "menu_item",
      "coordinate": {"x": 0.25, "y": 0.34},
      "expected_action": "navigate",
      "expects_page_change": true,
      "expects_state_change": false,
      "parent": null,
      "confidence": 1.0,
      "safety_tag": "safe"
    }
  ],
  "is_popup": false,
  "popup_info": null,
  "close_button": null,
  "back_button": null,
  "has_scroll": true,
  "is_end_of_list": false
}
```

## Important Notes

1. **Coordinate accuracy**: Use coordinates from flattened_screen, convert (x,y,w,h) bbox to center point (x,y)
2. **Parent relationships**: Set `parent: null` for all items (simplified hierarchy in V5.2)
3. **Safety tags**: Apply classification logic based on operation type
4. **Confidence**: Set to `1.0` for well-inferred items, `0.8-0.9` for uncertain
5. **Current path**: Use the explicit algorithm above, don't guess

Analyze the provided data and output the PageAnalysis JSON following this format exactly.
"""
