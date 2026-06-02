"""
Mock vision service for V6 simulation.

Provides virtual screen analysis without requiring real devices.
"""

import time
from typing import Any, Dict, List, Optional


class MockVisionService:
    """
    Mock vision service for simulation testing.

    Returns pre-configured virtual page analyses based on current traversal path.
    """

    def __init__(self, virtual_pages: Dict[str, Dict[str, Any]]):
        """
        Initialize mock vision service.

        Args:
            virtual_pages: Mapping of path strings to PageAnalysis data
        """
        self.virtual_pages = virtual_pages
        self.call_count = 0
        self._current_context: Optional[Any] = None
        self._injected_path: Optional[str] = None

    def analyze_screenshot(self) -> Dict[str, Any]:
        """
        Analyze the current screenshot.

        Returns virtual page analysis based on current path.

        Returns:
            PageAnalysis dictionary for current path, or empty analysis if path not found
        """
        self.call_count += 1

        # Get current path
        current_path = self._get_current_path()

        # Return matching page or empty analysis
        if current_path in self.virtual_pages:
            return self.virtual_pages[current_path].copy()

        # Return empty page analysis
        return self._empty_page_analysis()

    def _get_current_path(self) -> str:
        """Get current traversal path."""
        # Try to get from injected path first
        if self._injected_path:
            return self._injected_path

        # Try to get from context
        if self._current_context and hasattr(self._current_context, "current_path"):
            return "/".join(self._current_context.current_path)

        return ""

    def _empty_page_analysis(self) -> Dict[str, Any]:
        """Return empty page analysis."""
        return {
            "app_name": "",
            "page_name": "",
            "items": [],
            "timestamp": time.time(),
        }

    def set_context(self, context: Any) -> None:
        """
        Set the traversal context for path resolution.

        Args:
            context: TraversalContext instance
        """
        self._current_context = context

    def inject_path(self, path: str) -> None:
        """
        Inject a specific path for testing.

        Args:
            path: Path string to use
        """
        self._injected_path = path

    def clear_injected_path(self) -> None:
        """Clear injected path."""
        self._injected_path = None

    def get_call_count(self) -> int:
        """Get total number of analyze_screenshot calls."""
        return self.call_count

    def reset_call_count(self) -> None:
        """Reset call count to zero."""
        self.call_count = 0


class PageAnalysisBuilder:
    """Builder for creating PageAnalysis objects."""

    @staticmethod
    def create(
        app_name: str = "",
        page_name: str = "",
        items: Optional[List[Dict[str, Any]]] = None,
    ) -> Dict[str, Any]:
        """
        Create a PageAnalysis dictionary.

        Args:
            app_name: Application name
            page_name: Page/screen name
            items: List of UI elements on the page

        Returns:
            PageAnalysis dictionary
        """
        return {
            "app_name": app_name,
            "page_name": page_name,
            "items": items or [],
            "timestamp": time.time(),
        }

    @staticmethod
    def create_button(
        text: str,
        x: float = 0.5,
        y: float = 0.5,
        bounds: Optional[List[float]] = None,
    ) -> Dict[str, Any]:
        """Create a button element."""
        return {
            "type": "button",
            "text": text,
            "x": x,
            "y": y,
            "bounds": bounds or [x - 0.1, y - 0.05, x + 0.1, y + 0.05],
        }

    @staticmethod
    def create_menu_item(
        text: str,
        x: float = 0.5,
        y: float = 0.5,
    ) -> Dict[str, Any]:
        """Create a menu item element."""
        return {
            "type": "menu_item",
            "text": text,
            "x": x,
            "y": y,
        }
