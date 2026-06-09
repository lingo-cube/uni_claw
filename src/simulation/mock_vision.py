"""
Mock vision service for V6.4 simulation.

Implements VisionService ABC for compatibility with GraphTraversalEngine.
Returns pre-configured virtual page analyses based on current traversal path.
"""

import time
from typing import Any, Dict, List, Optional

from src.models.content_models import (
    Coordinate,
    Direction,
    MenuInfo,
    MenuItem,
    PageAnalysis,
    PopupInfo,
)
from src.ai.vision_service import VisionService

from .page_analyzer import PageAnalyzer, PageNotFoundError


class MockVisionService(VisionService):
    """Mock vision service implementing VisionService ABC.

    Returns pre-configured virtual page analyses based on current path.
    Compatible with GraphTraversalEngine injection.
    """

    def __init__(self, virtual_pages: Dict[str, Dict[str, Any]]):
        self._virtual_pages = virtual_pages
        self._analyzer = PageAnalyzer(virtual_pages)
        self._current_path: List[str] = []
        self._injected_path: Optional[str] = None
        self._call_count = 0

    # -- VisionService ABC implementation ------------------------------------

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """Analyze screenshot by looking up pre-configured virtual page data.

        The image_data parameter is ignored in simulation — page context
        is determined by the current path set via set_path_context().

        Returns:
            PageAnalysis pydantic model for the current page.
        """
        self._call_count += 1
        path = self._resolve_path()
        try:
            raw = self._analyzer.analyze_page(path)
        except PageNotFoundError:
            raw = {"app_name": "", "page_name": path, "items": []}
        return self._build_page_analysis(path, raw)

    def find_app_entry(self, image_data: bytes, target: str) -> Optional[dict]:
        """Find target app icon — simulation always returns screen center."""
        return {"x": 0.5, "y": 0.5}

    def get_current_page(self) -> Optional[dict]:
        """Get current page info for wait condition verification."""
        path = self._resolve_path()
        return {"path": path.split("/") if path else []}

    # -- Path context management ---------------------------------------------

    def set_path_context(self, path: List[str]) -> None:
        """Update the current path used for page lookup.

        Called by the engine before each step so that analyze_screenshot
        can return the correct virtual page data.
        """
        self._current_path = list(path)

    def inject_path(self, path: str) -> None:
        """Inject a specific path for testing (overrides context)."""
        self._injected_path = path

    def clear_injected_path(self) -> None:
        self._injected_path = None

    # -- State management ----------------------------------------------------

    @property
    def call_count(self) -> int:
        return self._call_count

    def reset(self) -> None:
        self._call_count = 0
        self._current_path = []
        self._injected_path = None
        self._analyzer.clear_cache()

    # -- Internal ------------------------------------------------------------

    def _resolve_path(self) -> str:
        if self._injected_path:
            return self._injected_path
        if self._current_path:
            return "/".join(self._current_path)
        return "home"

    def _build_page_analysis(self, path: str, data: Dict[str, Any]) -> PageAnalysis:
        """Build a PageAnalysis pydantic model from virtual page data."""
        items_raw = data.get("elements", [])
        items: List[MenuItem] = []
        for item in items_raw:
            coord = item.get("coordinate", {})
            items.append(MenuItem(
                name=item.get("text", item.get("name", "")),
                type=item.get("type", "item"),
                coordinate=Coordinate(
                    x=coord.get("x", 0.5),
                    y=coord.get("y", 0.5),
                ),
                description=item.get("description"),
            ))

        # Parse path into segments for current_path
        path_segments = [s for s in path.split("/") if s] if path else []

        return PageAnalysis(
            level1_dir=data.get("level1_dir", Direction.RIGHT),
            level1_menus=data.get("level1_menus", []),
            level2_dir=data.get("level2_dir", Direction.BOTTOM),
            level2_menus=data.get("level2_menus", []),
            current_path=path_segments,
            items=items,
            is_popup=data.get("is_popup", False),
            popup_info=PopupInfo(**data["popup_info"]) if data.get("popup_info") else None,
            close_button=Coordinate(**data["close_button"]) if data.get("close_button") else None,
            back_button=Coordinate(**data["back_button"]) if data.get("back_button") else None,
            has_scroll=data.get("has_scroll", False),
            is_end_of_list=data.get("is_end_of_list", False),
        )


# -- Backward-compatible builder (used by existing tests) --------------------


class PageAnalysisBuilder:
    """Builder for creating PageAnalysis-like dictionaries (legacy)."""

    @staticmethod
    def create(
        app_name: str = "",
        page_name: str = "",
        items: Optional[List[Dict[str, Any]]] = None,
    ) -> Dict[str, Any]:
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
        return {
            "type": "menu_item",
            "text": text,
            "x": x,
            "y": y,
        }
