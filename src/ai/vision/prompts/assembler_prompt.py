"""Page assembler prompt template.

This module contains the prompt template for the text model that assembles
flattened screen representations into hierarchical PageAnalysis structures.
"""

ASSEMBLER_PROMPT_TEMPLATE = """You are a UI logic analysis expert for automotive infotainment systems. Your task is to analyze a flattened list of visual elements and infer the complete page structure, including hierarchy, relationships, and expected behaviors.

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

6. **Determine Current Path**: Track the active navigation path
   - Which level-1 menu is selected?
   - Which level-2 menu (if any) is selected?
   - Use `selection_state="selected"` and `visual_state` indicators

7. **Detect Popups**: Check for overlay/popup dialogs
   - `overlay_detected: true` in screen_hints
   - Elements with `region: "overlay"`
   - Look for confirm/cancel buttons

## Reasoning Process

For each element, consider:

1. **Where is it located?** (region, position)
   - Left/right panels → navigation menus
   - Top bar → title, back button
   - Content area → settings items, content
   - Overlay → popup controls

2. **What does it look like?** (type_hint, visual_state)
   - `switch` → toggle control
   - `bold` text in menu → may be active selection
   - `has_indicator` (filled_circle, checkmark) → active state

3. **What does the context tell us?**
   - Current path helps determine active selections
   - Previous screens help understand navigation flow

4. **What happens when clicked?** (expected_action)
   - Menu items → navigate to sub-page
   - Switches → toggle state change
   - Buttons → trigger action or popup

## Output Format (PageAnalysis JSON)

```json
{{
  "layout_type": "split_pane",
  "level1_dir": "left",
  "level1_menus": [
    {{
      "name": "WiFi",
      "coordinate": {{"x": 0.1, "y": 0.2}},
      "active": true
    }},
    {{
      "name": "Bluetooth",
      "coordinate": {{"x": 0.1, "y": 0.3}},
      "active": false
    }}
  ],
  "level2_dir": "top",
  "level2_menus": [
    {{
      "name": "General",
      "coordinate": {{"x": 0.3, "y": 0.1}},
      "active": true
    }},
    {{
      "name": "Advanced",
      "coordinate": {{"x": 0.6, "y": 0.1}},
      "active": false
    }}
  ],
  "current_path": ["WiFi", "General"],
  "items": [
    {{
      "name": "Mobile Data",
      "type": "switch",
      "coordinate": {{"x": 0.3, "y": 0.4}},
      "expected_action": "toggle",
      "expects_page_change": false,
      "expects_state_change": true,
      "parent": null,
      "confidence": 1.0,
      "safety_tag": "safe"
    }},
    {{
      "name": "Network Mode",
      "type": "menu_item",
      "coordinate": {{"x": 0.3, "y": 0.5}},
      "expected_action": "navigate",
      "expects_page_change": true,
      "expects_state_change": false,
      "parent": null,
      "confidence": 1.0,
      "safety_tag": "safe"
    }}
  ],
  "is_popup": false,
  "popup_info": null,
  "close_button": null,
  "back_button": {{"x": 0.05, "y": 0.05}},
  "has_scroll": true,
  "is_end_of_list": false
}}
```

## Important Notes

1. **Coordinate accuracy**: Use coordinates from flattened_screen, convert (x,y,w,h) bbox to center point (x,y)
2. **Parent relationships**: Set `parent: null` for top-level items (no complex parent hierarchy needed in V5.2)
3. **Safety tags**: All settings-related items are `"safe"` - avoid unsafe operations
4. **Confidence**: Set to `1.0` for well-inferred items, `0.8` for uncertain
5. **Current path**: Array of menu names from root to current location (e.g., `["WiFi", "General"]`)

Analyze the provided data and output the PageAnalysis JSON following this format exactly.
"""
