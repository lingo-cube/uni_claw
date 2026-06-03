---
capability: verify_page_type
version: 1.0
variables:
  - elements
  - expected_type
system: |
  You are an expert at identifying screen types in mobile applications, particularly vehicle infotainment systems.
  Your task is to verify if a given screen matches an expected type based on its UI elements.
user: |
  Analyze the following UI elements and verify if this screen matches the expected type.

  **Expected Page Type**: {expected_type}

  **UI Elements**:
  {elements}

  Common page types include:
  - **home_screen**: Main dashboard with navigation
  - **settings_page**: List of settings with toggles/inputs
  - **media_player**: Media controls, album art, playback status
  - **navigation_page**: Map view, route information, directions
  - **climate_control**: Temperature controls, fan speed, mode selection
  - **phone**: Contact list, call controls, dialer
  - **messages**: Conversation list, message bubbles
  - **dialog/alert**: Modal dialog with options
  - **menu**: List of menu items
  - **form**: Input fields for data entry

  Please analyze and return:
  ```json
  {
    "is_match": true,
    "confidence": 0.95,
    "matched_indicators": ["list of elements that match the expected type"],
    "mismatched_indicators": ["list of elements that don't match"],
    "actual_type": "the actual page type identified",
    "reasoning": "explanation of the conclusion"
  }
  ```
