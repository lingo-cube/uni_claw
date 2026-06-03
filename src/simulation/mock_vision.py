"""
Mock vision service for V6 simulation.

Provides virtual screen analysis without requiring real devices.
Enhanced with PageAnalyzer integration for intelligent page analysis.
"""

import time
from typing import Any, Dict, List, Optional

from .page_analyzer import PageAnalyzer, PageNotFoundError


class MockVisionService:
    """
    Mock vision service for simulation testing.

    Returns pre-configured virtual page analyses based on current traversal path.
    """

    def __init__(self, virtual_pages: Dict[str, Dict[str, Any]]):
        """
        Initialize mock vision service with PageAnalyzer integration.

        Args:
            virtual_pages: Mapping of path strings to PageAnalysis data
        """
        self.virtual_pages = virtual_pages
        self._analyzer = PageAnalyzer(virtual_pages)
        self._path_mapping = self._build_path_mapping(virtual_pages)
        self._call_count = 0
        self._current_context: Optional[Any] = None
        self._injected_path: Optional[str] = None
        self._path_getter = Optional[any]

    def _build_path_mapping(self, virtual_pages: Dict) -> Dict[str, str]:
        """
        Build mapping of page names to paths.

        Args:
            virtual_pages: Dictionary of virtual pages

        Returns:
            Dictionary mapping page names to paths
        """
        mapping = {}
        for path, data in virtual_pages.items():
            page_name = data.get("page_name", path)
            mapping[page_name] = path
        return mapping

    def set_context(self, context: Any) -> None:
        """
        Set traversal context for path resolution with multiple context support.

        Args:
            context: TraversalContext, InMemoryTracer, or other context object
        """
        self._current_context = context

        # Support multiple context types
        if hasattr(context, 'current_path'):
            # TraversalContext support
            self._path_getter = lambda: "/".join(context.current_path)
        elif hasattr(context, 'visited_tree'):
            # InMemoryTracer support
            self._path_getter = lambda: self._infer_path_from_tracer(context)
        else:
            # Default path
            self._path_getter = lambda: "root"

    def _infer_path_from_tracer(self, tracer: Any) -> str:
        """
        Infer current path from tracer object.

        Args:
            tracer: InMemoryTracer or similar tracer object

        Returns:
            Current path string
        """
        if hasattr(tracer, 'steps') and not tracer.steps:
            return "root"
        if hasattr(tracer, 'steps'):
            last_step = tracer.steps[-1]
            return getattr(last_step, 'current_path', 'root')
        return "root"

    def analyze_screenshot(self, screenshot_path: Optional[str] = None) -> Dict[str, Any]:
        """
        Analyze the current screenshot using PageAnalyzer.

        Args:
            screenshot_path: Optional path parameter (ignored in simulation)

        Returns:
            PageAnalysis dictionary for current path, or empty analysis if path not found
        """
        self._call_count += 1
        current_path = self._get_current_path()

        try:
            # Use PageAnalyzer for intelligent analysis
            return self._analyzer.analyze_page(current_path)
        except PageNotFoundError:
            # Return empty page analysis if page not found
            return self._empty_page_analysis()

    def _get_current_path(self) -> str:
        """Get current traversal path using path getter."""
        # Try injected path first (highest priority)
        if self._injected_path:
            return self._injected_path

        # Try path getter from context
        if self._path_getter:
            try:
                path = self._path_getter()
                if path:
                    return path
            except Exception:
                pass  # Fall through to other methods

        # Try to get from context directly
        if self._current_context:
            # Try TraversalContext style
            if hasattr(self._current_context, "current_path"):
                current_path = self._current_context.current_path
                if isinstance(current_path, list):
                    # Convert list to path string
                    return "/".join(current_path) if current_path else "root"
                return str(current_path)

            # Try InMemoryTracer style
            if hasattr(self._current_context, "visited_tree"):
                return self._infer_path_from_tracer(self._current_context)

        return "root"

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
        return self._call_count

    def reset(self) -> None:
        """Reset service state for reuse in tests."""
        self._call_count = 0
        self._current_context = None
        self._path_getter = None
        self._injected_path = None
        # Clear PageAnalyzer cache
        self._analyzer.clear_cache()

    def get_cache_stats(self) -> Dict[str, Any]:
        """Get PageAnalyzer cache statistics."""
        return {
            "cache_size": self._analyzer.get_cache_size(),
            "cached_paths": self._analyzer.get_cached_paths()
        }


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
