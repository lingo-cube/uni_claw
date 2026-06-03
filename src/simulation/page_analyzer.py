"""
PageAnalyzer component for V6 simulation testing.

Intelligent page analysis that structures raw page data into proper PageAnalysis format.
Acts as simulation equivalent to real vision analysis pipeline.
"""

import time
from typing import Any, Dict, List, Optional


class PageNotFoundError(Exception):
    """Exception raised when a page is not found in virtual pages."""
    pass


class PageAnalyzer:
    """
    Analyze and structure page data for simulation testing.

    Acts as simulation equivalent to real vision analysis pipeline,
    converting raw page data into correct PageAnalysis format.
    """

    def __init__(self, virtual_pages: Dict[str, Dict[str, Any]]):
        """
        Initialize analyzer with virtual page data.

        Args:
            virtual_pages: Mapping of path strings to raw page data
        """
        self._virtual_pages = virtual_pages
        self._cache: Dict[str, Dict[str, Any]] = {}

    def analyze_page(self, path: str) -> Dict[str, Any]:
        """
        Analyze page and return structured PageAnalysis.

        Args:
            path: Page path to analyze

        Returns:
            Structured PageAnalysis dictionary

        Raises:
            PageNotFoundError: If path not found in virtual pages
        """
        # Check cache first
        if path in self._cache:
            return self._cache[path]

        # Get raw page data
        raw_data = self._get_raw_page_data(path)

        # Structure into PageAnalysis format
        page_analysis = self._structure_page_analysis(path, raw_data)

        # Cache result
        self._cache[path] = page_analysis
        return page_analysis

    def _get_raw_page_data(self, path: str) -> Dict[str, Any]:
        """
        Get raw page data from virtual pages.

        Args:
            path: Page path (can be page name or path-like string)

        Returns:
            Raw page data dictionary

        Raises:
            PageNotFoundError: If path not found
        """
        # First try direct lookup
        if path in self._virtual_pages:
            return self._virtual_pages[path]

        # Try to match by current_path field (for new PageAnalysis format)
        normalized_path = self._normalize_path(path)
        for page_name, page_data in self._virtual_pages.items():
            page_current_path = page_data.get("current_path", [])
            if self._paths_match(normalized_path, page_current_path):
                return page_data

        # If not found, try common page name mappings
        path_mappings = {
            "root": "HomeScreen",
            "home": "HomeScreen",
            "settings": "SettingsPage",
            "display": "DisplaySettings",
            "sound": "SoundSettings"
        }

        mapped_name = path_mappings.get(path.lower())
        if mapped_name and mapped_name in self._virtual_pages:
            return self._virtual_pages[mapped_name]

        raise PageNotFoundError(f"Page not found: {path}")

    def _normalize_path(self, path: str) -> List[str]:
        """
        Normalize path string to path components.

        Args:
            path: Path string (e.g., "root", "Settings", "Settings/Display")

        Returns:
            List of path components
        """
        if not path or path == "root":
            return []

        # Handle different path separators
        if "/" in path:
            return path.split("/")
        if "\\" in path:
            return path.split("\\")

        return [path]

    def _paths_match(self, path1: List[str], path2: List[str]) -> bool:
        """
        Check if two paths match.

        Args:
            path1: First path
            path2: Second path

        Returns:
            True if paths match
        """
        return str(path1) == str(path2)

    def _structure_page_analysis(
        self, path: str, raw_data: Dict[str, Any]
    ) -> Dict[str, Any]:
        """
        Convert raw page data into correct PageAnalysis structure.

        Args:
            path: Page path
            raw_data: Raw page data dictionary

        Returns:
            Structured PageAnalysis dictionary
        """
        # Support both old "elements" and new "items" field
        raw_elements = raw_data.get("items", []) or raw_data.get("elements", [])

        return {
            "page_type": self._infer_page_type(raw_data),
            "page_path": path,
            "elements": self._process_elements(raw_elements),
            "metadata": {
                "timestamp": time.time(),
                "source": "simulation",
                "page_name": raw_data.get("page_name", raw_data.get("name", "unknown")),
                "has_scroll": raw_data.get("has_scroll", False),
                "is_popup": raw_data.get("is_popup", False),
                "current_path": raw_data.get("current_path", []),
            }
        }

    def _process_elements(self, elements: List[Dict]) -> List[Dict[str, Any]]:
        """
        Process UI elements, adding types and metadata.

        Args:
            elements: List of raw element dictionaries

        Returns:
            List of processed element dictionaries
        """
        processed = []
        for element in elements:
            # Support both "id" and "name" for element identification
            element_id = element.get("id") or element.get("name", f"element_{len(processed)}")

            processed_element = {
                "element_id": element_id,
                "element_type": element.get("type", "unknown"),
                "text": element.get("name", element.get("text", "")),
                "bounds": element.get("bounds", element.get("coordinate", {})),
                "action_hint": element.get("expected_action", self._infer_action_hint(element)),
                "metadata": {
                    "clickable": element.get("clickable", element.get("expected_action") in ["navigate", "click"]),
                    "scrollable": element.get("scrollable", element.get("type") == "slider"),
                    "enabled": element.get("enabled", True),
                    "expected_action": element.get("expected_action", "unknown"),
                }
            }
            processed.append(processed_element)
        return processed

    def _infer_page_type(self, page_data: Dict) -> str:
        """
        Infer page type from content using heuristic rules.

        Args:
            page_data: Page data dictionary

        Returns:
            Inferred page type string
        """
        page_name = page_data.get("page_name", "").lower()
        elements = page_data.get("elements", [])

        # Infer based on page name and element features
        if "settings" in page_name or "设置" in page_name:
            return "settings"
        elif any(e.get("type") == "list" for e in elements):
            return "list"
        elif any(e.get("type") == "webview" for e in elements):
            return "web"
        elif any(e.get("type") == "dialog" for e in elements):
            return "dialog"
        elif any(e.get("type") == "popup" for e in elements):
            return "popup"
        else:
            return "unknown"

    def _infer_action_hint(self, element: Dict) -> str:
        """
        Infer suggested action for an element.

        Args:
            element: Element dictionary

        Returns:
            Action hint string (click, scroll, adjust, view)
        """
        element_type = element.get("type", "").lower()
        clickable = element.get("clickable", False)
        scrollable = element.get("scrollable", False)

        if clickable and element_type in ["button", "switch", "checkbox", "radio"]:
            return "click"
        elif scrollable:
            return "scroll"
        elif element_type == "slider":
            return "adjust"
        elif element_type == "input":
            return "input"
        elif element_type in ["text", "label"]:
            return "view"
        else:
            return "view"

    def clear_cache(self) -> None:
        """Clear analysis cache."""
        self._cache.clear()

    def get_cache_size(self) -> int:
        """Get number of cached analyses."""
        return len(self._cache)

    def get_cached_paths(self) -> List[str]:
        """Get list of cached page paths."""
        return list(self._cache.keys())