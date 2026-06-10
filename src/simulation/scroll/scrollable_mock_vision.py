"""
Scrollable mock vision service for simulating scrollable lists in tests.

Extends StatefulMockVisionService with scroll simulation capabilities:
- Scroll state tracking per page (progress, count, history)
- Accumulation mode element visibility (elements appear when threshold <= progress)
- Fault injection (delay, unresponsiveness)
- Element ID-based deduplication
"""

import hashlib
import time
from typing import Any, Dict, List, Optional

from src.models.content_models import (
    Coordinate,
    Direction,
    ExpectedAction,
    MenuItem,
    MenuItemType,
    PageAnalysis,
)
from src.models.element_type_mapper import ElementTypeMapper
from src.simulation.state_fixture import StateFixture
from src.simulation.stateful_mock_vision import StatefulMockVisionService

from .models import ScrollAction, ScrollSegment, ScrollState
from .scroll_data_store import ScrollDataStore


class ScrollableMockVisionService(StatefulMockVisionService):
    """
    Mock vision service with scrollable list simulation support.

    Extends StatefulMockVisionService to support scrollable content with:
    - Per-page scroll state tracking (progress, count, history)
    - Progressive element visibility based on scroll threshold
    - Fault injection for testing edge cases
    - Element deduplication across scroll segments

    Attributes:
        data_store: ScrollDataStore containing scroll segment data
        _scroll_states: Dict mapping page keys to ScrollState instances
        _screen_width: Screen width for coordinate normalization (default: 1080)
        _screen_height: Screen height for coordinate normalization (default: 1920)
    """

    def __init__(
        self,
        fixture: Optional[StateFixture] = None,
        data_store: Optional[ScrollDataStore] = None,
        screen_width: int = 1080,
        screen_height: int = 1920,
    ):
        """
        Initialize the scrollable mock vision service.

        Args:
            fixture: Optional StateFixture for page state definitions
            data_store: Optional ScrollDataStore for scroll segment data
            screen_width: Screen width for coordinate normalization (default: 1080)
            screen_height: Screen height for coordinate normalization (default: 1920)
        """
        # Initialize base class - if no fixture provided, create empty one
        if fixture is None:
            from src.simulation.state_fixture import PageState
            # Create minimal fixture with initial page
            empty_pages = {"initial": PageState(id="initial", page_name="Initial", elements=[], is_complete=True)}
            fixture = StateFixture(pages=empty_pages, initial_page_id="initial")

        super().__init__(fixture)

        self.data_store = data_store or ScrollDataStore()
        self._scroll_states: Dict[str, ScrollState] = {}
        self._screen_width = screen_width
        self._screen_height = screen_height

    # -- Scroll state management -----------------------------------------------

    def _resolve_path_key(self) -> str:
        """
        Resolve the current page key for scroll state lookup.

        Uses _current_page_id from base class to maintain scroll state per page.

        Returns:
            Page key string for scroll state lookup
        """
        return self._current_page_id or "initial"

    def _get_scroll_state(self, page_key: str) -> ScrollState:
        """
        Get or create scroll state for a page.

        Args:
            page_key: Page identifier

        Returns:
            ScrollState instance for the page
        """
        if page_key not in self._scroll_states:
            self._scroll_states[page_key] = ScrollState()
        return self._scroll_states[page_key]

    # -- VisionService overrides -----------------------------------------------

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """
        Analyze screenshot returning progressive elements based on scroll progress.

        Overrides base class to support scroll simulation. Returns elements
        from scroll segments whose threshold <= current progress (accumulation mode).

        Args:
            image_data: Ignored (page context from current state)

        Returns:
            PageAnalysis with MenuItem items based on scroll progress
        """
        self._call_count += 1
        page_key = self._resolve_path_key()
        scroll_state = self._get_scroll_state(page_key)

        # Get scroll segments for current page
        segments = self.data_store.get_scroll_segments(page_key)

        # Collect visible elements using accumulation mode
        visible_elements = self._collect_visible_elements(
            segments, scroll_state.current_progress
        )

        # Build PageAnalysis with visible elements
        return self._build_page_analysis(visible_elements, scroll_state)

    # -- Scroll simulation -----------------------------------------------------

    def simulate_scroll(self, delta: float) -> bool:
        """
        Simulate a scroll operation by updating progress.

        Args:
            delta: Scroll delta (positive for down, negative for up)

        Returns:
            True if scroll succeeded, False if scroll failed (due to fault injection)
        """
        page_key = self._resolve_path_key()
        scroll_state = self._get_scroll_state(page_key)

        # Check for scroll failure (fault injection)
        if scroll_state.fail_next_scroll:
            scroll_state.fail_next_scroll = False  # Reset for one-time fault
            return False

        # Apply delay if configured (fault injection)
        if scroll_state.simulate_delay_ms > 0:
            time.sleep(scroll_state.simulate_delay_ms / 1000.0)

        # Calculate new progress with clamping
        old_progress = scroll_state.current_progress
        new_progress = max(0.0, min(1.0, old_progress + delta))

        # Update scroll state
        scroll_state.current_progress = new_progress
        scroll_state.scroll_count += 1
        scroll_state.last_scroll_time = time.time()
        scroll_state.scroll_history.append(new_progress)

        return True

    def get_scroll_progress(self) -> float:
        """
        Get current scroll progress for the active page.

        Returns:
            Current scroll progress (0.0-1.0)
        """
        page_key = self._resolve_path_key()
        scroll_state = self._get_scroll_state(page_key)
        return scroll_state.current_progress

    def reset_scroll_state(self, page_key: Optional[str] = None) -> None:
        """
        Reset scroll state for a page or current page.

        Args:
            page_key: Optional page key. If None, resets current page.
        """
        if page_key is None:
            page_key = self._resolve_path_key()

        if page_key in self._scroll_states:
            self._scroll_states[page_key] = ScrollState()

    # -- Fault injection --------------------------------------------------------

    def set_scroll_delay(self, page_key: str, delay_ms: int) -> None:
        """
        Inject artificial delay during scroll operations.

        Args:
            page_key: Page identifier
            delay_ms: Delay in milliseconds
        """
        scroll_state = self._get_scroll_state(page_key)
        scroll_state.simulate_delay_ms = delay_ms

    def enable_scroll_failure(
        self, page_key: str, fail_once: bool = True
    ) -> None:
        """
        Enable scroll failure simulation.

        Args:
            page_key: Page identifier
            fail_once: If True, only next scroll fails. If False, all scrolls fail until disabled.
        """
        scroll_state = self._get_scroll_state(page_key)
        scroll_state.fail_next_scroll = True

    # -- Internal helpers -----------------------------------------------------

    def _collect_visible_elements(
        self, segments: List[ScrollSegment], progress: float
    ) -> List[Dict[str, Any]]:
        """
        Collect visible elements using accumulation mode.

        In accumulation mode, all elements from segments with threshold <= progress
        are visible. Elements are deduplicated by ID.

        Args:
            segments: List of ScrollSegment objects
            progress: Current scroll progress (0.0-1.0)

        Returns:
            Deduplicated list of visible element dictionaries
        """
        # Accumulate elements from segments whose threshold <= progress
        accumulated = []
        for segment in segments:
            if segment.threshold <= progress:
                accumulated.extend(segment.elements)

        # Deduplicate by element ID
        seen_ids = set()
        deduplicated = []
        for idx, element in enumerate(accumulated):
            elem_id = element.get("id")
            # For elements without IDs, generate a temporary ID for deduplication
            if not elem_id:
                content = element.get("text", "element")
                elem_id = self._generate_element_id(content, progress, 0, idx)

            if elem_id not in seen_ids:
                seen_ids.add(elem_id)
                # Add the original element (without modifying its ID)
                deduplicated.append(element)

        return deduplicated

    def _generate_element_id(
        self, content: str, progress: float, segment_index: int, element_index: int
    ) -> str:
        """
        Generate unique element ID using four parameters.

        Format: content_hash + progress + segment_index + element_index
        Ensures uniqueness even with identical content at different positions.

        Args:
            content: Element content/text
            progress: Scroll progress
            segment_index: Index of scroll segment
            element_index: Index within segment

        Returns:
            32-character hex string
        """
        # Hash the content
        content_hash = hashlib.md5(content.encode()).hexdigest()[:16]

        # Create position suffix
        progress_str = f"{progress:.2f}"
        segment_str = f"{segment_index:03d}"
        element_str = f"{element_index:03d}"
        position_str = f"{progress_str}{segment_str}{element_str}"

        # Hash the position string
        position_hash = hashlib.md5(position_str.encode()).hexdigest()[:16]

        # Combine for 32-char ID
        return content_hash + position_hash

    def _extract_coordinate(self, element: Dict[str, Any]) -> Dict[str, float]:
        """
        Extract coordinate from element supporting both formats.

        Supports:
        - coordinate: {x: float, y: float}
        - bounds: [x, y, w, h]

        Args:
            element: Element dictionary

        Returns:
            Dictionary with x, y normalized coordinates (0.0-1.0)
        """
        # Try coordinate format first
        if "coordinate" in element:
            coord = element["coordinate"]
            return {"x": coord.get("x", 0.5), "y": coord.get("y", 0.5)}

        # Try bounds format
        if "bounds" in element:
            bounds = element["bounds"]
            if len(bounds) >= 2:
                x = bounds[0]
                y = bounds[1]
                # Normalize to screen coordinates
                return {
                    "x": x / self._screen_width,
                    "y": y / self._screen_height,
                }

        # Default to center
        return {"x": 0.5, "y": 0.5}

    def _build_page_analysis(
        self, visible_elements: List[Dict[str, Any]], scroll_state: ScrollState
    ) -> PageAnalysis:
        """
        Build PageAnalysis adapting to MenuItem model.

        Creates PageAnalysis with MenuItem items compatible with V6.11 models.
        Supports both coordinate and bounds element formats.

        Args:
            visible_elements: List of visible element dictionaries
            scroll_state: Current scroll state

        Returns:
            PageAnalysis with MenuItem items
        """
        # Build menu items from visible elements
        items: List[MenuItem] = []
        for idx, element in enumerate(visible_elements):
            # Extract coordinate
            coord_dict = self._extract_coordinate(element)

            # Generate unique ID if not present
            elem_id = element.get("id")
            if not elem_id:
                elem_id = self._generate_element_id(
                    element.get("text", "element"),
                    scroll_state.current_progress,
                    0,  # segment_index - simplified
                    idx,
                )

            # Create MenuItem
            menu_item = MenuItem(
                # CRITICAL: Map element.text → MenuItem.name (not MenuItem.text)
                name=element.get("text", elem_id),
                # Convert type string to MenuItemType enum
                type=self._parse_element_type(element.get("type", "button")),
                # Coordinate object
                coordinate=Coordinate(x=coord_dict["x"], y=coord_dict["y"]),
                # Infer expected action from element properties
                expected_action=self._infer_expected_action(element),
                # No page change expectation for scroll list items
                expects_page_change=False,
                # State change based on element type
                expects_state_change=element.get("type") in ("switch", "toggle"),
            )
            items.append(menu_item)

        # Build current path from navigation history + current page
        path_names = []
        for page_id in self._navigation_history:
            p = self._fixture.get_page(page_id)
            if p:
                path_names.append(p.page_name or page_id)
        if self._current_page_id:
            p = self._fixture.get_page(self._current_page_id)
            path_names.append(p.page_name if p and p.page_name else self._current_page_id)
        current_path = path_names

        # Determine if at end of list and if scrolling is still possible
        page_key = self._resolve_path_key()
        segments = self.data_store.get_scroll_segments(page_key)

        # Check if there are segments beyond current progress
        has_more_content = False
        for segment in segments:
            if segment.threshold > scroll_state.current_progress:
                has_more_content = True
                break

        # has_scroll is True if there's more content to reveal by scrolling
        is_end_of_list = not has_more_content

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
            has_scroll=has_more_content,
            is_end_of_list=is_end_of_list,
        )

    def _parse_element_type(self, type_string: str) -> MenuItemType:
        """Parse a fixture type string to MenuItemType enum.

        Uses centralized ElementTypeMapper for consistency and validation.

        Args:
            type_string: Type string from element (e.g., "button", "switch")

        Returns:
            MenuItemType enum value
        """
        return ElementTypeMapper.to_menu_item_type(type_string)

    def _infer_expected_action(self, element: Dict[str, Any]) -> ExpectedAction:
        """Infer ExpectedAction from element properties.

        Uses centralized ElementTypeMapper for consistency.

        Args:
            element: Element dictionary

        Returns:
            ExpectedAction enum value
        """
        # Elements with action_target expect navigation (override type-based inference)
        if element.get("action_target"):
            return ExpectedAction.NAVIGATE

        # Use centralized mapper for type-based inference
        elem_type = element.get("type", "")
        return ElementTypeMapper.to_expected_action(elem_type)

    # -- Properties -----------------------------------------------------------

    @property
    def scroll_states(self) -> Dict[str, ScrollState]:
        """Get all scroll states."""
        return dict(self._scroll_states)
