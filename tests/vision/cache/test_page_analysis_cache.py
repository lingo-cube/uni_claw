"""Unit tests for page analysis cache."""

import time

import pytest

from src.ai.vision.cache.page_analysis_cache import (
    PageAnalysisCache,
    InMemoryPageAnalysisCache,
    CacheEntry,
)
from src.state.content_tree import (
    PageAnalysis,
    Coordinate,
    Direction,
    MenuInfo,
)


class TestPageAnalysisCache:
    """Tests for PageAnalysisCache interface."""

    def test_is_abstract(self):
        """Test that PageAnalysisCache cannot be instantiated directly."""
        with pytest.raises(TypeError):
            PageAnalysisCache()


class TestInMemoryPageAnalysisCacheCreation:
    """Tests for InMemoryPageAnalysisCache creation."""

    def test_creation_with_defaults(self):
        """Test creating cache with default parameters."""
        cache = InMemoryPageAnalysisCache()
        assert cache.ttl == 600
        assert cache.max_size == 1000
        assert cache.size() == 0

    def test_creation_with_custom_ttl(self):
        """Test creating cache with custom TTL."""
        cache = InMemoryPageAnalysisCache(ttl=300)
        assert cache.ttl == 300

    def test_creation_with_custom_max_size(self):
        """Test creating cache with custom max size."""
        cache = InMemoryPageAnalysisCache(max_size=500)
        assert cache.max_size == 500


class TestInMemoryPageAnalysisCacheOperations:
    """Tests for cache operations."""

    def test_cache_miss(self):
        """Test cache miss returns None."""
        cache = InMemoryPageAnalysisCache()
        cache_key = "test_key"

        result = cache.get(cache_key)
        assert result is None

    def test_cache_set_and_get(self):
        """Test setting and getting cached value."""
        cache = InMemoryPageAnalysisCache()
        cache_key = "test_key"

        page_analysis = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[
                MenuInfo(name="WiFi", coordinate=Coordinate(x=0.1, y=0.2), active=True)
            ],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["WiFi"],
            items=[],
        )
        cache.set(cache_key, page_analysis)

        result = cache.get(cache_key)
        assert result is not None
        assert result.current_path == ["WiFi"]
        assert len(result.level1_menus) == 1

    def test_cache_different_keys(self):
        """Test that different keys store different values."""
        cache = InMemoryPageAnalysisCache()

        page1 = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[
                MenuInfo(name="WiFi", coordinate=Coordinate(x=0.1, y=0.2), active=True)
            ],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["WiFi"],
            items=[],
        )

        page2 = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[
                MenuInfo(name="Bluetooth", coordinate=Coordinate(x=0.1, y=0.3), active=True)
            ],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Bluetooth"],
            items=[],
        )

        cache.set("key1", page1)
        cache.set("key2", page2)

        result1 = cache.get("key1")
        result2 = cache.get("key2")

        assert result1.current_path == ["WiFi"]
        assert result2.current_path == ["Bluetooth"]

    def test_cache_overwrites_existing_entry(self):
        """Test that setting same key overwrites existing entry."""
        cache = InMemoryPageAnalysisCache()
        cache_key = "test_key"

        page1 = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[
                MenuInfo(name="WiFi", coordinate=Coordinate(x=0.1, y=0.2), active=True)
            ],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["WiFi"],
            items=[],
        )

        page2 = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[
                MenuInfo(name="Bluetooth", coordinate=Coordinate(x=0.1, y=0.3), active=True)
            ],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Bluetooth"],
            items=[],
        )

        cache.set(cache_key, page1)
        cache.set(cache_key, page2)

        result = cache.get(cache_key)
        assert result.current_path == ["Bluetooth"]

    def test_clear_cache(self):
        """Test clearing the cache."""
        cache = InMemoryPageAnalysisCache()
        cache_key = "test_key"

        page = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
        )
        cache.set(cache_key, page)

        assert cache.size() == 1

        cache.clear()

        assert cache.size() == 0
        assert cache.get(cache_key) is None


class TestCacheTTL:
    """Tests for cache TTL functionality."""

    def test_entry_expires_after_ttl(self):
        """Test that entries expire after TTL."""
        cache = InMemoryPageAnalysisCache(ttl=1)  # 1 second TTL
        cache_key = "test_key"

        page = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
        )
        cache.set(cache_key, page)

        # Should be available immediately
        assert cache.get(cache_key) is not None

        # Wait for expiration
        time.sleep(1.1)

        # Should be expired
        assert cache.get(cache_key) is None
        assert cache.size() == 0

    def test_entry_available_before_ttl(self):
        """Test that entries are available before TTL expires."""
        cache = InMemoryPageAnalysisCache(ttl=10)
        cache_key = "test_key"

        page = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
        )
        cache.set(cache_key, page)

        time.sleep(1)

        # Should still be available
        assert cache.get(cache_key) is not None


class TestCacheLRUEviction:
    """Tests for LRU eviction when cache is full."""

    def test_oldest_entry_evicted_when_full(self):
        """Test that oldest entry is evicted when cache is full."""
        cache = InMemoryPageAnalysisCache(max_size=3)

        page = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
        )

        # Fill cache to capacity
        for i in range(3):
            cache.set(f"key_{i}", page)

        assert cache.size() == 3

        # Add one more entry - should evict oldest
        cache.set("key_3", page)

        # Size should still be 3
        assert cache.size() == 3

        # First entry should be evicted
        assert cache.get("key_0") is None

        # Other entries should still be available
        assert cache.get("key_1") is not None
        assert cache.get("key_2") is not None
        assert cache.get("key_3") is not None

    def test_no_eviction_when_not_full(self):
        """Test that no eviction occurs when cache is not full."""
        cache = InMemoryPageAnalysisCache(max_size=10)

        page = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
        )

        for i in range(5):
            cache.set(f"key_{i}", page)

        # All entries should be available
        for i in range(5):
            assert cache.get(f"key_{i}") is not None


class TestCacheKeyGeneration:
    """Tests for cache key generation helper."""

    def test_generate_key_from_screen_and_context(self):
        """Test generating cache key from screen and context."""
        cache = InMemoryPageAnalysisCache()

        screen_dict = {
            'elements': [{'id': 0, 'text': 'Test'}],
            'screen_hints': {'layout': 'split'},
        }
        context = {'current_path': ['Settings']}

        key1 = cache.generate_key(screen_dict, context)
        key2 = cache.generate_key(screen_dict, context)

        # Same inputs should generate same key
        assert key1 == key2

    def test_generate_key_different_for_different_inputs(self):
        """Test that different inputs generate different keys."""
        cache = InMemoryPageAnalysisCache()

        screen_dict1 = {'elements': [{'id': 0}], 'screen_hints': {}}
        screen_dict2 = {'elements': [{'id': 1}], 'screen_hints': {}}
        context = {}

        key1 = cache.generate_key(screen_dict1, context)
        key2 = cache.generate_key(screen_dict2, context)

        # Different inputs should generate different keys
        assert key1 != key2

    def test_generate_key_includes_context(self):
        """Test that context is included in key generation."""
        cache = InMemoryPageAnalysisCache()

        screen_dict = {'elements': [], 'screen_hints': {}}
        context1 = {'path': 'A'}
        context2 = {'path': 'B'}

        key1 = cache.generate_key(screen_dict, context1)
        key2 = cache.generate_key(screen_dict, context2)

        # Different context should generate different keys
        assert key1 != key2
