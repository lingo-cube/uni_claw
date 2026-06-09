"""
Unit tests for ScrollDataStore.

Tests cover:
- Initialization with and without virtual pages
- JSON fixture loading
- Segment retrieval
- Scroll capability check
- Dynamic page registration
"""

import json
import tempfile
from pathlib import Path

import pytest

from src.simulation.scroll.models import ScrollPage, ScrollSegment
from src.simulation.scroll.scroll_data_store import ScrollDataStore


class TestScrollDataStore:
    """Tests for ScrollDataStore class."""

    def test_initialization_empty(self):
        """WHEN ScrollDataStore is created without parameters
        THEN virtual_pages is empty dictionary
        """
        store = ScrollDataStore()

        assert store.get_all_paths() == []
        assert len(store.get_all_paths()) == 0

    def test_initialization_with_pages(self):
        """WHEN ScrollDataStore is created with virtual_pages
        THEN pages are stored correctly
        """
        page = ScrollPage(
            path="test_page",
            has_scroll=True,
            scroll_segments=[
                ScrollSegment(threshold=0.0, elements=[{"id": "item1"}])
            ],
        )
        store = ScrollDataStore(virtual_pages={"test_page": page})

        assert "test_page" in store.get_all_paths()
        assert len(store.get_all_paths()) == 1

    def test_load_from_json(self):
        """WHEN load_from_json is called with valid JSON file
        THEN pages and segments are loaded correctly
        """
        json_data = {
            "pages": [
                {
                    "path": "wifi_list",
                    "has_scroll": True,
                    "scroll_segments": [
                        {"threshold": 0.0, "elements": [{"id": "net1"}]},
                        {"threshold": 0.5, "elements": [{"id": "net2"}]},
                        {"threshold": 1.0, "elements": [{"id": "net3"}]},
                    ],
                }
            ]
        }

        with tempfile.NamedTemporaryFile(
            mode="w", suffix=".json", delete=False
        ) as f:
            json.dump(json_data, f)
            temp_path = f.name

        try:
            store = ScrollDataStore()
            store.load_from_json(temp_path)

            assert "wifi_list" in store.get_all_paths()
            assert store.has_scroll("wifi_list")
            segments = store.get_scroll_segments("wifi_list")
            assert len(segments) == 3
            assert segments[0].threshold == 0.0
            assert segments[1].threshold == 0.5
            assert segments[2].threshold == 1.0
        finally:
            Path(temp_path).unlink()

    def test_load_from_json_multiple_pages(self):
        """WHEN JSON contains multiple pages
        THEN all pages are loaded
        """
        json_data = {
            "pages": [
                {
                    "path": "page1",
                    "has_scroll": True,
                    "scroll_segments": [
                        {"threshold": 0.0, "elements": [{"id": "item1"}]}
                    ],
                },
                {
                    "path": "page2",
                    "has_scroll": True,
                    "scroll_segments": [
                        {"threshold": 0.0, "elements": [{"id": "item2"}]}
                    ],
                },
            ]
        }

        with tempfile.NamedTemporaryFile(
            mode="w", suffix=".json", delete=False
        ) as f:
            json.dump(json_data, f)
            temp_path = f.name

        try:
            store = ScrollDataStore()
            store.load_from_json(temp_path)

            assert len(store.get_all_paths()) == 2
            assert "page1" in store.get_all_paths()
            assert "page2" in store.get_all_paths()
        finally:
            Path(temp_path).unlink()

    def test_get_scroll_segments_existing_page(self):
        """WHEN get_scroll_segments is called for existing page
        THEN returns list of segments
        """
        page = ScrollPage(
            path="test",
            has_scroll=True,
            scroll_segments=[
                ScrollSegment(threshold=0.0, elements=[]),
                ScrollSegment(threshold=0.5, elements=[]),
            ],
        )
        store = ScrollDataStore(virtual_pages={"test": page})

        segments = store.get_scroll_segments("test")

        assert len(segments) == 2
        assert segments[0].threshold == 0.0
        assert segments[1].threshold == 0.5

    def test_get_scroll_segments_nonexistent_page(self):
        """WHEN get_scroll_segments is called for non-existent page
        THEN returns empty list
        """
        store = ScrollDataStore()

        segments = store.get_scroll_segments("nonexistent")

        assert segments == []

    def test_has_scroll_existing_scrollable_page(self):
        """WHEN has_scroll is called for existing scrollable page
        THEN returns True
        """
        page = ScrollPage(
            path="scrollable", has_scroll=True, scroll_segments=[]
        )
        store = ScrollDataStore(virtual_pages={"scrollable": page})

        assert store.has_scroll("scrollable") is True

    def test_has_scroll_existing_non_scrollable_page(self):
        """WHEN has_scroll is called for non-scrollable page
        THEN returns False
        """
        page = ScrollPage(
            path="static", has_scroll=False, scroll_segments=[]
        )
        store = ScrollDataStore(virtual_pages={"static": page})

        assert store.has_scroll("static") is False

    def test_has_scroll_nonexistent_page(self):
        """WHEN has_scroll is called for non-existent page
        THEN returns False
        """
        store = ScrollDataStore()

        assert store.has_scroll("nonexistent") is False

    def test_add_page(self):
        """WHEN add_page is called with ScrollPage
        THEN page is added to virtual pages
        """
        page = ScrollPage(
            path="new_page",
            has_scroll=True,
            scroll_segments=[
                ScrollSegment(threshold=0.0, elements=[{"id": "item1"}])
            ],
        )
        store = ScrollDataStore()
        store.add_page(page)

        assert "new_page" in store.get_all_paths()
        assert store.has_scroll("new_page")
        assert len(store.get_scroll_segments("new_page")) == 1

    def test_add_page_updates_existing(self):
        """WHEN add_page is called for existing page path
        THEN page is updated (replaced)
        """
        old_page = ScrollPage(
            path="test", has_scroll=True, scroll_segments=[]
        )
        store = ScrollDataStore(virtual_pages={"test": old_page})

        new_page = ScrollPage(
            path="test",
            has_scroll=False,
            scroll_segments=[
                ScrollSegment(threshold=0.0, elements=[{"id": "new"}])
            ],
        )
        store.add_page(new_page)

        assert store.has_scroll("test") is False
        segments = store.get_scroll_segments("test")
        assert len(segments) == 1
        assert segments[0].elements[0]["id"] == "new"

    def test_get_page_existing(self):
        """WHEN get_page is called for existing page
        THEN returns ScrollPage object
        """
        page = ScrollPage(
            path="test", has_scroll=True, scroll_segments=[]
        )
        store = ScrollDataStore(virtual_pages={"test": page})

        result = store.get_page("test")

        assert result is not None
        assert result.path == "test"
        assert result.has_scroll is True

    def test_get_page_nonexistent(self):
        """WHEN get_page is called for non-existent page
        THEN returns None
        """
        store = ScrollDataStore()

        result = store.get_page("nonexistent")

        assert result is None

    def test_get_all_paths(self):
        """WHEN get_all_paths is called
        THEN returns list of all page paths
        """
        pages = {
            "page1": ScrollPage(path="page1", has_scroll=True, scroll_segments=[]),
            "page2": ScrollPage(path="page2", has_scroll=False, scroll_segments=[]),
            "page3": ScrollPage(path="page3", has_scroll=True, scroll_segments=[]),
        }
        store = ScrollDataStore(virtual_pages=pages)

        paths = store.get_all_paths()

        assert len(paths) == 3
        assert "page1" in paths
        assert "page2" in paths
        assert "page3" in paths

    def test_load_from_json_empty_pages(self):
        """WHEN JSON has empty pages array
        THEN no pages are loaded
        """
        json_data = {"pages": []}

        with tempfile.NamedTemporaryFile(
            mode="w", suffix=".json", delete=False
        ) as f:
            json.dump(json_data, f)
            temp_path = f.name

        try:
            store = ScrollDataStore()
            store.load_from_json(temp_path)

            assert len(store.get_all_paths()) == 0
        finally:
            Path(temp_path).unlink()

    def test_load_from_json_has_scroll_default(self):
        """WHEN page data doesn't specify has_scroll
        THEN defaults to True
        """
        json_data = {
            "pages": [
                {
                    "path": "default_scroll",
                    "scroll_segments": [
                        {"threshold": 0.0, "elements": [{"id": "item1"}]}
                    ],
                }
            ]
        }

        with tempfile.NamedTemporaryFile(
            mode="w", suffix=".json", delete=False
        ) as f:
            json.dump(json_data, f)
            temp_path = f.name

        try:
            store = ScrollDataStore()
            store.load_from_json(temp_path)

            assert store.has_scroll("default_scroll") is True
        finally:
            Path(temp_path).unlink()
