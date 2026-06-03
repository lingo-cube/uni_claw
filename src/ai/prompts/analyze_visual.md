---
capability: analyze_visual
version: 1.0
variables:
  - image_description
  - context_info
system: |
  You are an expert at analyzing mobile application screenshots, particularly vehicle infotainment systems.
  Your task is to identify UI elements, their types, positions, and relationships.
user: |
  Analyze the following vehicle infotainment system screenshot.

  **Image Description**: {image_description}

  **Context Information**:
  {context_info}

  Please provide a detailed analysis including:
  1. **UI Elements**: List all visible UI elements with their types (button, label, icon, slider, etc.)
  2. **Positions**: For each element, provide its approximate position (top-left coordinates, dimensions)
  3. **Hierarchy**: Describe the parent-child relationships between elements
  4. **Interactive Elements**: Identify which elements are interactive (tappable, scrollable, etc.)
  5. **Text Content**: Extract all visible text content
  6. **State**: Note any visible state indicators (selected, disabled, active, etc.)

  Return your analysis as a JSON object with the following structure:
  ```json
  {
    "elements": [
      {
        "id": "element_id",
        "type": "button|label|icon|slider|switch|text|container",
        "text": "visible text content",
        "position": {"x": 0, "y": 0, "width": 100, "height": 50},
        "parent_id": "parent_element_id_or_null",
        "attributes": {"enabled": true, "selected": false},
        "confidence": 0.95
      }
    ],
    "layout": {
      "type": "vertical_list|horizontal_list|grid|form|dialog",
      "scrollable": true,
      "scroll_direction": "vertical"
    },
    "summary": "Brief description of the screen and its purpose"
  }
  ```
