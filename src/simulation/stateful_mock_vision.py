"""Stateful mock vision service for simulation testing.

Implements VisionService ABC with state management and page transition
simulation using StateFixture definitions.

Key Features:
- State management with current page tracking
- Page transition simulation based on fixture rules
- Correct PageAnalysis field mapping for GraphEngine compatibility
- Navigation history tracking with configurable depth
"""

from typing import Any, Dict, List, Optional

from src.models.content_models import (
    Coordinate,
    Direction,
    ExpectedAction,
    MenuInfo,
    MenuItem,
    MenuItemType,
    PageAnalysis,
    PopupInfo,
)
from src.ai.vision_service import VisionService

from .state_fixture import StateFixture


class StatefulMockVisionService(VisionService):
    """Mock vision service with state management and page transition simulation.

    Uses a StateFixture to define page states and transition rules.
    Supports runtime page navigation and action simulation.

    Attributes:
        fixture: The StateFixture defining pages and transitions
        current_page_id: Current active page ID
        navigation_history: Stack of visited page IDs
    """

    def __init__(self, fixture: StateFixture):
        """Initialize the stateful mock vision service.

        Args:
            fixture: StateFixture defining pages and transitions
        """
        self._fixture = fixture
        self._current_page_id = fixture.initial_page_id
        self._navigation_history: List[str] = []
        self._call_count = 0

    # -- VisionService ABC implementation ------------------------------------

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """Analyze screenshot by returning current page state.

        The image_data parameter is ignored — page context is determined
        by the current page state managed by this service.

        Args:
            image_data: Ignored (for VisionService ABC compatibility)

        Returns:
            PageAnalysis pydantic model for the current page.
        """
        self._call_count += 1
        return self._build_current_page_analysis()

    def find_app_entry(self, image_data: bytes, target: str) -> Optional[dict]:
        """Find target app icon — simulation returns center coordinate."""
        return {"x": 0.5, "y": 0.5}

    def get_current_page(self) -> Optional[dict]:
        """Get current page info for wait condition verification.

        Returns:
            Dict with current page path information
        """
        if self._current_page_id:
            page = self._fixture.get_page(self._current_page_id)
            if page:
                return {
                    "page_id": page.id,
                    "page_name": page.page_name,
                    "path": self._navigation_history + [self._current_page_id],
                    "is_complete": page.is_complete,
                }
        return None

    # -- State management and action simulation -------------------------------

    def simulate_action(self, element_id: str, action: str = "click") -> bool:
        """Simulate an element action and update page state if valid.

        Args:
            element_id: ID of the element to act upon
            action: Action type (e.g., "click", "swipe")

        Returns:
            True if action succeeded and page changed, False otherwise
        """
        if not self._current_page_id:
            return False

        # Find matching transition
        transition = self._fixture.get_transition(
            trigger_element_id=element_id,
            from_page_id=self._current_page_id,
            action=action,
        )

        if transition:
            # Save current page to history
            self._navigation_history.append(self._current_page_id)

            # Limit history depth
            if len(self._navigation_history) > self._fixture.history_depth:
                self._navigation_history.pop(0)

            # Update current page
            self._current_page_id = transition.to_page
            return True

        return False

    def navigate_back(self) -> bool:
        """Navigate back to the previous page.

        Returns:
            True if navigation succeeded, False if at root (no history)
        """
        if not self._navigation_history:
            return False

        # Pop the previous page from history
        previous_page = self._navigation_history.pop()
        self._current_page_id = previous_page
        return True

    def reset_to_initial(self) -> None:
        """Reset to the initial page state."""
        self._current_page_id = self._fixture.initial_page_id
        self._navigation_history.clear()
        self._call_count = 0

    # -- Properties -----------------------------------------------------------

    @property
    def current_page_id(self) -> Optional[str]:
        """Get the current page ID."""
        return self._current_page_id

    @property
    def navigation_history(self) -> List[str]:
        """Get the navigation history stack."""
        return list(self._navigation_history)

    @property
    def call_count(self) -> int:
        """Get the number of analyze_screenshot calls."""
        return self._call_count

    # -- Internal helpers -----------------------------------------------------

    def _build_current_page_analysis(self) -> PageAnalysis:
        """Build a PageAnalysis for the current page.

        Returns:
            PageAnalysis with correctly mapped fields for GraphEngine compatibility

        Key mappings:
            fixture.text → MenuItem.name (NOT MenuItem.text)
            fixture.type → MenuItemType enum
            PageAnalysis.items (NOT PageAnalysis.menu_items)
        """
        page = self._fixture.get_page(self._current_page_id)
        if not page:
            # Return empty analysis if page not found
            return PageAnalysis(
                level1_dir=Direction.RIGHT,
                level1_menus=[],
                level2_dir=Direction.BOTTOM,
                level2_menus=[],
                current_path=[],
                items=[],
                is_end_of_list=False,
            )

        # Build menu items from page elements
        items: List[MenuItem] = []
        for element in page.elements:
            menu_item = MenuItem(
                # CRITICAL: Map fixture.text → MenuItem.name (not MenuItem.text)
                name=element.text,
                # Convert type string to MenuItemType enum
                type=self._parse_element_type(element.type),
                # Coordinate object
                coordinate=Coordinate(
                    x=element.coordinate.get("x", 0.5),
                    y=element.coordinate.get("y", 0.5),
                ),
                # Infer expected action from element properties
                expected_action=self._infer_expected_action(element),
                # Set page change expectation based on action target
                expects_page_change=element.action_target is not None,
                # State change based on element type
                expects_state_change=element.type in ("switch", "toggle"),
            )
            items.append(menu_item)

        # Build current path from navigation history + current page
        # V6.9.3: Use page names instead of IDs for precondition matching
        # Convert page IDs to page names
        path_names = []
        for page_id in self._navigation_history:
            p = self._fixture.get_page(page_id)
            if p:
                path_names.append(p.page_name or page_id)
        if self._current_page_id:
            p = self._fixture.get_page(self._current_page_id)
            path_names.append(p.page_name if p and p.page_name else self._current_page_id)
        current_path = path_names

        return PageAnalysis(
            level1_dir=Direction.RIGHT,
            level1_menus=[],
            level2_dir=Direction.BOTTOM,
            level2_menus=[],
            current_path=current_path,
            # CRITICAL: Use 'items' (not 'menu_items') for GraphEngine compatibility
            items=items,
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=page.is_complete,
        )

    def _parse_element_type(self, type_string: str) -> MenuItemType:
        """Parse a fixture type string to MenuItemType enum.

        Args:
            type_string: Type string from fixture (e.g., "button", "switch")

        Returns:
            MenuItemType enum value

        Raises:
            ValueError: If type_string is not a valid MenuItemType
        """
        # Try direct conversion
        try:
            return MenuItemType.from_value(type_string)
        except ValueError:
            # Fallback to BUTTON for unknown types
            return MenuItemType.BUTTON

    def _infer_expected_action(self, element) -> ExpectedAction:
        """Infer ExpectedAction from element properties.

        Args:
            element: PageElement from fixture

        Returns:
            ExpectedAction enum value
        """
        # Switch/toggle elements expect state change
        if element.type in ("switch", "toggle"):
            return ExpectedAction.TOGGLE

        # Elements with action_target expect navigation
        if element.action_target:
            return ExpectedAction.NAVIGATE

        # Read-only elements
        if element.type in ("text", "readonly"):
            return ExpectedAction.NONE

        # Default to action for buttons
        return ExpectedAction.ACTION

    # -- Path context (for compatibility with existing test patterns) -------

    def set_path_context(self, path: List[str]) -> None:
        """Update current page based on path context.

        For compatibility with existing test patterns that use
        set_path_context(). This maps path segments to page IDs.

        Args:
            path: List of path segments
        """
        if path and path[-1]:
            # Try to find a matching page
            last_segment = path[-1]
            for page_id in self._fixture.pages.keys():
                if last_segment in page_id or page_id in last_segment:
                    self._current_page_id = page_id
                    return
            # Default to last segment as page ID
            self._current_page_id = last_segment
