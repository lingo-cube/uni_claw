"""
Scroll data store for managing scroll segment data from JSON fixtures.

Provides ScrollDataStore class for loading scroll segment data from JSON files,
retrieving segments for pages, checking scroll capability, and dynamic page registration.
"""

import json
from pathlib import Path
from typing import Any, Dict, List, Optional

from .models import ScrollPage, ScrollSegment


class ScrollDataStore:
    """
    Data store for scroll segment data.

    Manages virtual pages with scroll segments loaded from JSON fixtures or registered dynamically.
    Provides methods for retrieving segments, checking scroll capability, and adding pages.
    """

    def __init__(self, virtual_pages: Optional[Dict[str, ScrollPage]] = None):
        """
        Initialize ScrollDataStore with optional virtual pages.

        Args:
            virtual_pages: Optional dictionary mapping page paths to ScrollPage objects
        """
        self._virtual_pages: Dict[str, ScrollPage] = virtual_pages or {}

    def load_from_json(self, json_path: str) -> None:
        """
        Load scroll segment data from a JSON fixture file.

        JSON format should be:
        {
            "pages": [
                {
                    "path": "wifi_list",
                    "has_scroll": true,
                    "scroll_segments": [
                        {"threshold": 0.0, "elements": [...]},
                        {"threshold": 0.5, "elements": [...]},
                        {"threshold": 1.0, "elements": [...]}
                    ]
                }
            ]
        }

        Args:
            json_path: Path to the JSON file containing scroll data
        """
        with open(json_path, "r") as f:
            data = json.load(f)

        for page_data in data.get("pages", []):
            scroll_segments = []
            for seg_data in page_data.get("scroll_segments", []):
                segment = ScrollSegment(
                    threshold=seg_data["threshold"],
                    elements=seg_data["elements"],
                )
                scroll_segments.append(segment)

            page = ScrollPage(
                path=page_data["path"],
                has_scroll=page_data.get("has_scroll", True),
                scroll_segments=scroll_segments,
            )
            self._virtual_pages[page.path] = page

    def get_scroll_segments(self, path: str) -> List[ScrollSegment]:
        """
        Get scroll segments for a given page path.

        Args:
            path: Page path identifier

        Returns:
            List of ScrollSegment objects for the page, empty list if page not found
        """
        page = self._virtual_pages.get(path)
        if page is None:
            return []
        return page.scroll_segments

    def has_scroll(self, path: str) -> bool:
        """
        Check if a page has scrollable content.

        Args:
            path: Page path identifier

        Returns:
            True if page exists and has_scroll=True, False otherwise
        """
        page = self._virtual_pages.get(path)
        if page is None:
            return False
        return page.has_scroll

    def add_page(self, page: ScrollPage) -> None:
        """
        Add or update a page in the virtual pages store.

        Args:
            page: ScrollPage object to add
        """
        self._virtual_pages[page.path] = page

    def get_page(self, path: str) -> Optional[ScrollPage]:
        """
        Get a page by path.

        Args:
            path: Page path identifier

        Returns:
            ScrollPage object if found, None otherwise
        """
        return self._virtual_pages.get(path)

    def get_all_paths(self) -> List[str]:
        """
        Get all registered page paths.

        Returns:
            List of all page path identifiers
        """
        return list(self._virtual_pages.keys())
