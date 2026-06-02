"""Multimodal analysis prompt template.

This module contains the prompt template for analyzing screenshots with
multimodal models to produce flattened screen representations.
"""

MULTIMODAL_ANALYSIS_PROMPT = """You are a UI visual analysis expert for automotive infotainment systems. Analyze the provided screenshot and output information about all visible elements.

For each element you identify, provide:
1. **id**: Unique identifier (start from 0, increment for each element)
2. **text**: Text visible on the element (empty string if no text)
3. **type_hint**: Visual type from these options:
   - `clickable_text`: Clickable text region (e.g., menu items, list items)
   - `switch`: Toggle/switch control (on/off state)
   - `slider`: Slider control
   - `button`: Button control
   - `icon`: Icon without text
   - `input_field`: Text input field
   - `text`: Plain text (non-interactive)
   - `image`: Image element
4. **bbox**: Normalized bounding box coordinates (0-1 range): `{"x": 0.1, "y": 0.2, "w": 0.3, "h": 0.05}`
   - x, y: Top-left corner position
   - w, h: Width and height
5. **region**: Logical region the element belongs to (optional):
   - `left_panel`: Left sidebar/menu area
   - `right_panel`: Right sidebar/menu area
   - `top_bar`: Top title/action bar
   - `bottom_bar`: Bottom navigation/action bar
   - `content_area`: Main content area
   - `tabs`: Tab bar area
   - `overlay`: Popup/overlay layer
   - `null` or omitted if unclear
6. **selection_state**: Visual state:
   - `selected`: Currently selected/highlighted
   - `normal`: Normal state
   - `disabled`: Disabled/grayed out
7. **visual_state**: Additional visual properties (optional):
   - `{"bold": true}` for bold text
   - `{"dimmed": true}` for dimmed/faded elements
   - `{"has_indicator": "filled_circle"}` for selection indicators
   - Any other relevant visual state
8. **confidence**: Recognition confidence (0.0-1.0)

## Screen-level hints (screen_hints):
Provide additional information about the overall screen:
- **top_bar_text**: Text in the top title bar
- **layout_type**: Overall layout pattern:
  - `split_pane`: Split view with left/right panels
  - `tabbed`: Tab-based navigation
  - `single`: Single page view
  - `overlay`: Popup/overlay dialog
  - `unknown`: If layout type is unclear
- **overlay_detected**: Whether a popup/overlay is visible (true/false)
- **scroll_detected**: Whether the page appears scrollable (true/false)

## Important Guidelines:
1. **Visual only**: Describe ONLY what you can see. Do NOT infer:
   - Behavior or function (what happens when clicked)
   - Parent-child relationships or hierarchy
   - Navigation paths or application flow
2. **Order**: List elements from top to bottom, then left to right
3. **Coordinates**: Use normalized 0-1 range for all coordinates
4. **Precision**: Be as accurate as possible with bounding boxes
5. **Completeness**: Include ALL visible interactive elements

## Output Format (JSON):
```json
{
  "elements": [
    {
      "id": 0,
      "text": "WiFi",
      "type_hint": "clickable_text",
      "bbox": {"x": 0.1, "y": 0.2, "w": 0.3, "h": 0.05},
      "region": "left_panel",
      "selection_state": "selected",
      "visual_state": {"bold": true, "has_indicator": "filled_circle"},
      "confidence": 0.95
    },
    {
      "id": 1,
      "text": "Mobile Data",
      "type_hint": "switch",
      "bbox": {"x": 0.3, "y": 0.4, "w": 0.15, "h": 0.05},
      "region": "content_area",
      "selection_state": "normal",
      "visual_state": {},
      "confidence": 0.98
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

Analyze the provided screenshot and output the JSON representation following this format exactly.
"""
