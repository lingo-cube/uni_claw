---
capability: parse_instruction
version: 1.0
variables:
  - instruction
  - context
system: |
  You are an expert at understanding natural language instructions for mobile app testing and navigation.
  Your task is to parse instructions into structured test actions.
user: |
  Parse the following natural language instruction into a structured test action.

  **Instruction**: {instruction}

  **Context**: {context}

  Please analyze the instruction and determine:
  1. **Action Type**: What type of action is requested? (tap, swipe, navigate, verify, input, wait)
  2. **Target**: What element or screen is being targeted?
  3. **Parameters**: What parameters are needed for the action?
  4. **Validation**: What conditions should be verified?

  Return your analysis as a JSON object with the following structure:
  ```json
  {
    "action_type": "tap|swipe|navigate|verify|input|wait",
    "target": {
      "element_id": "element_identifier",
      "element_type": "button|label|icon|input|text|container",
      "text": "text_match_criteria_or_null",
      "position": {{"x": 0, "y": 0}}
    },
    "parameters": {
      "direction": "up|down|left|right",
      "distance": 100,
      "text": "text_to_input",
      "duration": 1000
    },
    "validation": {
      "expected_result": "description",
      "timeout_ms": 5000
    },
    "confidence": 0.95
  }
  ```
