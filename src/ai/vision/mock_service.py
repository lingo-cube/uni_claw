"""Mock vision service for testing."""

from typing import Dict, List, Optional

from .service import VisionService
from ...state.content_tree import (
    PageAnalysis,
    Direction,
    MenuInfo,
    MenuItem,
    MenuItemType,
    ExpectedAction,
    Coordinate,
)


class MockVisionService(VisionService):
    """Mock vision service for testing.

    This service returns predefined responses, allowing tests
    to verify integration without calling real APIs.
    """

    def __init__(self):
        """Initialize the mock service with empty response queue."""
        self._responses: List[PageAnalysis] = []

    def add_response(self, response: PageAnalysis) -> None:
        """Add a predefined response to the queue.

        Args:
            response: PageAnalysis to return on next call
        """
        self._responses.append(response)

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """Return the next queued response or a default mock response.

        Args:
            image_data: PNG image bytes (ignored)

        Returns:
            PageAnalysis from queue or default mock response
        """
        if self._responses:
            return self._responses.pop(0)
        return self._get_default_response()

    def find_app_entry(self, image_data: bytes, target: str) -> Optional[Dict]:
        """Return a mock app entry response.

        Args:
            image_data: PNG image bytes (ignored)
            target: App name to search for

        Returns:
            Mock response indicating app was found
        """
        return {
            "found": True,
            "name": target,
            "x": 0.5,
            "y": 0.5,
            "confidence": 0.9,
        }

    def _get_default_response(self) -> PageAnalysis:
        """Get a default mock PageAnalysis.

        Returns:
            Default PageAnalysis with safe default values
        """
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
                    type=MenuItemType.MENU_ITEM,
                    expected_action=ExpectedAction.NAVIGATE,
                    coordinate=Coordinate(x=0.45, y=0.35),
                    expects_page_change=True,
                    expects_state_change=False,
                ),
            ],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=Coordinate(x=0.05, y=0.05),
            has_scroll=True,
            is_end_of_list=False,
        )


__all__ = ["MockVisionService"]
