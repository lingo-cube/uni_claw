"""Claude Vision service implementation."""

import logging
from typing import Dict, Optional

try:
    from anthropic import Anthropic
except ImportError:
    Anthropic = None

from .base_service import BaseVisionService, VisionError
from ...state.content_tree import PageAnalysis

logger = logging.getLogger(__name__)


class ClaudeVisionService(BaseVisionService):
    """Vision service using Claude API.

    This service uses Anthropic's Claude API with vision capabilities
    to analyze screenshots and extract page structure.
    """

    def __init__(self, api_key: str, model: str = "claude-3-5-sonnet-20241022"):
        """Initialize the Claude Vision service.

        Args:
            api_key: Anthropic API key
            model: Claude model to use (default: claude-3-5-sonnet-20241022)

        Raises:
            ImportError: If anthropic package is not installed
        """
        if Anthropic is None:
            raise ImportError("anthropic package is required for ClaudeVisionService")

        self.client = Anthropic(api_key=api_key)
        self.model = model

    def _call_vision(self, prompt: str, image_data: bytes) -> str:
        """Call Claude Vision API.

        Args:
            prompt: Prompt text
            image_data: PNG image bytes

        Returns:
            Response text from Claude

        Raises:
            VisionError: On API errors
        """
        image_base64 = self._encode_image_base64(image_data)

        try:
            message = self.client.messages.create(
                model=self.model,
                max_tokens=4096,
                messages=[{
                    "role": "user",
                    "content": [
                        {"type": "text", "text": prompt},
                        {
                            "type": "image",
                            "source": {
                                "type": "base64",
                                "media_type": "image/png",
                                "data": image_base64.split(",")[1],
                            },
                        },
                    ],
                }],
            )
            return message.content[0].text

        except Exception as e:
            raise VisionError(f"Claude API error: {e}")

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """Analyze screenshot and extract page structure.

        Args:
            image_data: PNG image bytes

        Returns:
            PageAnalysis with detected elements

        Raises:
            VisionError: On analysis failure
        """
        response_text = self._call_vision(PROMPT_STRUCTURE, image_data)
        response_dict = self._extract_json(response_text)

        try:
            return PageAnalysis(**response_dict)
        except Exception as e:
            raise VisionError(f"Failed to parse PageAnalysis from response: {e}")

    def find_app_entry(self, image_data: bytes, target: str) -> Optional[Dict]:
        """Find an app icon on the home screen.

        Args:
            image_data: PNG image bytes
            target: App name to search for

        Returns:
            Dict with found info or None if not found
        """
        prompt = PROMPT_FIND_ENTRY.format(target=target)
        image_base64 = self._encode_image_base64(image_data)

        try:
            message = self.client.messages.create(
                model=self.model,
                max_tokens=1024,
                messages=[{
                    "role": "user",
                    "content": [
                        {"type": "text", "text": prompt},
                        {
                            "type": "image",
                            "source": {
                                "type": "base64",
                                "media_type": "image/png",
                                "data": image_base64.split(",")[1],
                            },
                        },
                    ],
                }],
            )

            response_text = message.content[0].text
            response_dict = self._extract_json(response_text)

            if response_dict.get("found", False):
                return {
                    "found": True,
                    "name": response_dict.get("name", target),
                    "x": response_dict.get("x", 0.0),
                    "y": response_dict.get("y", 0.0),
                    "confidence": response_dict.get("confidence", 0.0),
                }
            return None

        except Exception as e:
            logger.warning(f"Failed to find app entry: {e}")
            return None


# Prompt templates
PROMPT_STRUCTURE = """You are analyzing a mobile app screen for UI traversal.

Analyze this screenshot and provide:
1. Menu structure (level 1 and level 2 menus with their positions and active state)
2. Current path (which menus are currently active/highlighted)
3. All clickable items in the content area with BUTTON TYPE CLASSIFICATION
4. Any popups, dialogs, or special UI elements

BUTTON TYPE CLASSIFICATION:
For each item, determine its type and expected behavior:

Types:
- menu_item: List items that navigate to sub-pages (e.g., settings entries)
- tab: Tab buttons that switch between top-level views
- back_button: Back/return navigation buttons
- switch: On/off toggle switches (typically with sliding animation)
- toggle: Buttons that toggle between states (e.g., favorite buttons)
- button: Generic action buttons (triggers operations, dialogs, etc.)
- link: Navigation links or hypertext
- icon: Icon-only buttons without text labels
- text: Non-interactive text elements
- readonly: Elements that display but don't respond to clicks

Expected Actions:
- navigate: Button will change the current page/view (menu_item, tab, back_button)
- toggle: Button will change UI state without page change (switch, toggle)
- action: Button triggers an operation (button, link) - may show popup or jump
- none: No expected response (readonly, text)

Field Guidelines:
- expects_page_change: true for navigate/action, false for toggle/none
- expects_state_change: true for toggle, false for navigate/action/none

Return JSON with this exact structure:
{
  "level1_dir": "left|right|top|bottom",
  "level1_menus": [{"name": "menu_name", "x": 0.0-1.0, "y": 0.0-1.0, "active": true|false}],
  "level2_dir": "left|right|top|bottom",
  "level2_menus": [{"name": "tab_name", "x": 0.0-1.0, "y": 0.0-1.0, "active": true|false}],
  "current_path": ["level1_name", "level2_name"],
  "items": [
    {
      "name": "item_name",
      "type": "menu_item|tab|back_button|switch|toggle|button|link|icon|text|readonly",
      "expected_action": "navigate|toggle|action|none",
      "expects_page_change": true|false,
      "expects_state_change": true|false,
      "x": 0.0-1.0,
      "y": 0.0-1.0,
      "parent": "parent_name_or_null"
    }
  ],
  "is_popup": false,
  "popup_info": {"title": "...", "content": "...", "close_button": {"x": 0.0, "y": 0.0}} or null,
  "close_button": {"x": 0.0, "y": 0.0} or null,
  "back_button": {"x": 0.0, "y": 0.0} or null,
  "has_scroll": false,
  "is_end_of_list": false
}

Important:
- All coordinates must be normalized 0-1 (relative to screen size)
- Mark parent-child relationships using the "parent" field
- Use current_path to indicate which menus are currently active
- For icons without text, name them like "[icon] description"
- Include all interactive elements, not just text
- Default to expected_action="action" if uncertain
- Use expects_page_change=true for navigate/action types
- Use expects_state_change=true only for toggle types
"""

PROMPT_FIND_ENTRY = """You are helping to navigate to a specific app on a mobile device.

Target app: "{target}"

Analyze this screenshot and find the app icon. Return JSON:
{{
  "found": true|false,
  "name": "exact_app_name",
  "x": 0.0-1.0,
  "y": 0.0-1.0,
  "confidence": 0.0-1.0
}}

If not found, set found=false and return null coordinates."""


__all__ = ["ClaudeVisionService"]
