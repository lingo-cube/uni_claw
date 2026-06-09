"""Vision service for screen analysis using AI."""

import logging
from abc import ABC, abstractmethod
from typing import Optional

from anthropic import Anthropic

from ..models.content_models import PageAnalysis

logger = logging.getLogger(__name__)


class VisionError(Exception):
    """Vision service error."""

    pass


# Prompt templates for AI analysis
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
  "level1_dir": "one of: left, right, top, bottom (choose exactly one)",
  "level1_menus": [{"name": "menu_name", "coordinate": {"x": 0.0-1.0, "y": 0.0-1.0}, "active": true|false}],
  "level2_dir": "one of: left, right, top, bottom (choose exactly one)",
  "level2_menus": [{"name": "tab_name", "coordinate": {"x": 0.0-1.0, "y": 0.0-1.0}, "active": true|false}],
  "current_path": ["level1_name", "level2_name"],
  "items": [
    {
      "name": "item_name",
      "type": "menu_item|tab|back_button|switch|toggle|button|link|icon|text|readonly",
      "expected_action": "navigate|toggle|action|none",
      "expects_page_change": true|false,
      "expects_state_change": true|false,
      "coordinate": {"x": 0.0-1.0, "y": 0.0-1.0},
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

EXAMPLES:
{
  "name": "互联",
  "type": "tab",
  "expected_action": "navigate",
  "expects_page_change": true,
  "expects_state_change": false,
  "coordinate": {"x": 0.28, "y": 0.06}
},
{
  "name": "移动数据",
  "type": "menu_item",
  "expected_action": "navigate",
  "expects_page_change": true,
  "expects_state_change": false,
  "coordinate": {"x": 0.45, "y": 0.35}
},
{
  "name": "[开关]移动数据开关",
  "type": "switch",
  "expected_action": "toggle",
  "expects_page_change": false,
  "expects_state_change": true,
  "coordinate": {"x": 0.85, "y": 0.35}
},
{
  "name": "设置",
  "type": "button",
  "expected_action": "action",
  "expects_page_change": true,
  "expects_state_change": false,
  "coordinate": {"x": 0.9, "y": 0.9}
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
- level1_dir and level2_dir MUST be a single value from: left, right, top, bottom (NEVER use pipe-separated values like "top|bottom" - choose ONE direction)
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

If not found, set found=false and return null coordinates.
"""


class VisionService(ABC):
    """Abstract base for vision/analysis services."""

    @abstractmethod
    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """Analyze a screenshot and return page structure.

        Args:
            image_data: PNG image bytes

        Returns:
            PageAnalysis with detected elements
        """
        pass

    @abstractmethod
    def find_app_entry(self, image_data: bytes, target: str) -> Optional[dict]:
        """Find target app icon on home screen.

        Args:
            image_data: PNG image bytes
            target: App name to search for

        Returns:
            Dict with x, y coordinates if found, None otherwise
        """
        pass

    @property
    def last_call_metrics(self) -> Optional[dict]:
        """Return metrics from the most recent analyze_screenshot call.

        Subclasses may override to expose provider-specific data
        (provider_id, input_tokens, output_tokens). Default returns None.

        Must be updated synchronously by each analyze_screenshot call.
        """
        return None


class ClaudeVisionService(VisionService):
    """Vision service using official Claude API."""

    def __init__(self, api_key: str, model: str = "claude-3-5-sonnet-20241022"):
        """Initialize Claude vision service.

        Args:
            api_key: Anthropic API key
            model: Model to use for vision
        """
        from .base_vision import BaseVisionService

        self.client = Anthropic(api_key=api_key)
        self.model = model
        # Use base class utilities
        self._encode_base64 = BaseVisionService._encode_image_base64
        self._extract_json = BaseVisionService._extract_json
        self._parse_page_analysis = BaseVisionService._parse_page_analysis
        self._parse_find_entry = BaseVisionService._parse_find_entry

    def _call_vision(self, prompt: str, image_data: bytes) -> str:
        """Make vision API call to Claude."""
        image_base64 = self._encode_base64(self, image_data)

        try:
            message = self.client.messages.create(
                model=self.model,
                max_tokens=4096,
                messages=[
                    {
                        "role": "user",
                        "content": [
                            {"type": "text", "text": prompt},
                            {
                                "type": "image",
                                "source": {
                                    "type": "base64",
                                    "media_type": "image/png",
                                    "data": image_base64,
                                },
                            },
                        ],
                    }
                ],
            )

            content = message.content[0].text
            logger.debug(f"AI response: {content[:200]}...")

            return content

        except Exception as e:
            raise VisionError(f"Vision API call failed: {e}") from e

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """Analyze screenshot using Claude."""
        response = self._call_vision(PROMPT_STRUCTURE, image_data)
        content = self._extract_json(self, response)
        return self._parse_page_analysis(self, content)

    def find_app_entry(self, image_data: bytes, target: str) -> Optional[dict]:
        """Find app entry point on home screen."""
        prompt = PROMPT_FIND_ENTRY.format(target=target)
        response = self._call_vision(prompt, image_data)
        content = self._extract_json(self, response)
        return self._parse_find_entry(self, content)


class MockVisionService(VisionService):
    """Mock vision service for testing."""

    def __init__(self):
        """Initialize mock with predefined responses."""
        self._call_count = 0
        self._responses: list[PageAnalysis] = []

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """Return mock analysis."""
        self._call_count += 1

        if self._responses:
            return self._responses.pop(0)

        # Default mock response
        from ..state.content_tree import (
            Coordinate,
            Direction,
            MenuInfo,
            MenuItem,
            PageAnalysis,
        )

        return PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[
                MenuInfo(name="DiLink", coordinate=Coordinate(x=0.08, y=0.12), active=True),
                MenuInfo(name="DiPilot", coordinate=Coordinate(x=0.08, y=0.20), active=False),
            ],
            level2_dir=Direction.TOP,
            level2_menus=[
                MenuInfo(name="互联", coordinate=Coordinate(x=0.28, y=0.06), active=True),
                MenuInfo(name="音响", coordinate=Coordinate(x=0.45, y=0.06), active=False),
            ],
            current_path=["DiLink", "互联"],
            items=[
                MenuItem(
                    name="移动数据",
                    type="item",
                    coordinate=Coordinate(x=0.45, y=0.35),
                ),
                MenuItem(
                    name="[图标]移动数据开关",
                    type="switch",
                    coordinate=Coordinate(x=0.85, y=0.35),
                    parent="移动数据",
                ),
            ],
        )

    def find_app_entry(self, image_data: bytes, target: str) -> Optional[dict]:
        """Mock find entry - always finds the target."""
        return {"x": 0.5, "y": 0.5, "name": target}

    def add_response(self, response: PageAnalysis) -> None:
        """Add a predefined response."""
        self._responses.append(response)

    @property
    def call_count(self) -> int:
        """Get number of calls made."""
        return self._call_count
