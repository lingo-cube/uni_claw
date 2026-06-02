"""Unit tests for screen cache."""

import time

import pytest

from src.ai.vision.cache.screen_cache import (
    ScreenCache,
    InMemoryScreenCache,
    CacheEntry,
)
from src.models.vision.flattened_screen import FlattenedScreen
from src.models.vision.flattened_element import FlattenedElement
from src.models.vision.bounding_box import BoundingBox
from src.models.vision.type_hint import TypeHint


class TestScreenCache:
    """Tests for ScreenCache interface."""

    def test_is_abstract(self):
        """Test that ScreenCache cannot be instantiated directly."""
        with pytest.raises(TypeError):
            ScreenCache()


class TestCacheEntry:
    """Tests for CacheEntry dataclass."""

    def test_creation(self):
        """Test creating a cache entry."""
        screen = FlattenedScreen(elements=[], screen_hints={})
        entry = CacheEntry(value=screen, created_at=time.time())

        assert entry.value == screen
        assert isinstance(entry.created_at, float)


class TestInMemoryScreenCacheCreation:
    """Tests for InMemoryScreenCache creation."""

    def test_creation_with_defaults(self):
        """Test creating cache with default parameters."""
        cache = InMemoryScreenCache()
        assert cache.ttl == 300
        assert cache.max_size == 1000
        assert cache.size() == 0

    def test_creation_with_custom_ttl(self):
        """Test creating cache with custom TTL."""
        cache = InMemoryScreenCache(ttl=600)
        assert cache.ttl == 600

    def test_creation_with_custom_max_size(self):
        """Test creating cache with custom max size."""
        cache = InMemoryScreenCache(max_size=500)
        assert cache.max_size == 500


class TestInMemoryScreenCacheOperations:
    """Tests for cache operations."""

    def test_cache_miss(self):
        """Test cache miss returns None."""
        cache = InMemoryScreenCache()
        image_data = b"test_image_data"

        result = cache.get(image_data)
        assert result is None

    def test_cache_set_and_get(self):
        """Test setting and getting cached value."""
        cache = InMemoryScreenCache()
        image_data = b"test_image_data"

        screen = FlattenedScreen(
            elements=[
                FlattenedElement(
                    id=0,
                    text="Test",
                    type_hint=TypeHint.TEXT,
                    bbox=BoundingBox(x=0, y=0, w=0.1, h=0.1),
                )
            ],
            screen_hints={},
        )
        cache.set(image_data, screen)

        result = cache.get(image_data)
        assert result is not None
        assert result.element_count() == 1
        assert result.elements[0].text == "Test"

    def test_cache_key_different_for_different_images(self):
        """Test that different images generate different cache keys."""
        cache = InMemoryScreenCache()
        image1 = b"image_data_1"
        image2 = b"image_data_2"

        screen1 = FlattenedScreen(elements=[], screen_hints={'test': 1})
        screen2 = FlattenedScreen(elements=[], screen_hints={'test': 2})

        cache.set(image1, screen1)
        cache.set(image2, screen2)

        result1 = cache.get(image1)
        result2 = cache.get(image2)

        assert result1.screen_hints['test'] == 1
        assert result2.screen_hints['test'] == 2

    def test_cache_overwrites_existing_entry(self):
        """Test that setting same image overwrites existing entry."""
        cache = InMemoryScreenCache()
        image_data = b"test_image_data"

        screen1 = FlattenedScreen(elements=[], screen_hints={'version': 1})
        screen2 = FlattenedScreen(elements=[], screen_hints={'version': 2})

        cache.set(image_data, screen1)
        cache.set(image_data, screen2)

        result = cache.get(image_data)
        assert result.screen_hints['version'] == 2

    def test_clear_cache(self):
        """Test clearing the cache."""
        cache = InMemoryScreenCache()
        image_data = b"test_image_data"

        screen = FlattenedScreen(elements=[], screen_hints={})
        cache.set(image_data, screen)

        assert cache.size() == 1

        cache.clear()

        assert cache.size() == 0
        assert cache.get(image_data) is None


class TestCacheTTL:
    """Tests for cache TTL functionality."""

    def test_entry_expires_after_ttl(self):
        """Test that entries expire after TTL."""
        cache = InMemoryScreenCache(ttl=1)  # 1 second TTL
        image_data = b"test_image_data"

        screen = FlattenedScreen(elements=[], screen_hints={})
        cache.set(image_data, screen)

        # Should be available immediately
        assert cache.get(image_data) is not None

        # Wait for expiration
        time.sleep(1.1)

        # Should be expired
        assert cache.get(image_data) is None
        assert cache.size() == 0

    def test_entry_available_before_ttl(self):
        """Test that entries are available before TTL expires."""
        cache = InMemoryScreenCache(ttl=10)
        image_data = b"test_image_data"

        screen = FlattenedScreen(elements=[], screen_hints={})
        cache.set(image_data, screen)

        time.sleep(1)

        # Should still be available
        assert cache.get(image_data) is not None


class TestCacheLRUEviction:
    """Tests for LRU eviction when cache is full."""

    def test_oldest_entry_evicted_when_full(self):
        """Test that oldest entry is evicted when cache is full."""
        cache = InMemoryScreenCache(max_size=3)

        # Fill cache to capacity
        for i in range(3):
            image_data = f"image_{i}".encode()
            screen = FlattenedScreen(elements=[], screen_hints={'index': i})
            cache.set(image_data, screen)

        assert cache.size() == 3

        # Add one more entry - should evict oldest
        image_data = b"image_3"
        screen = FlattenedScreen(elements=[], screen_hints={'index': 3})
        cache.set(image_data, screen)

        # Size should still be 3
        assert cache.size() == 3

        # First entry should be evicted
        assert cache.get(b"image_0") is None

        # Other entries should still be available
        assert cache.get(b"image_1") is not None
        assert cache.get(b"image_2") is not None
        assert cache.get(b"image_3") is not None

    def test_no_eviction_when_not_full(self):
        """Test that no eviction occurs when cache is not full."""
        cache = InMemoryScreenCache(max_size=10)

        for i in range(5):
            image_data = f"image_{i}".encode()
            screen = FlattenedScreen(elements=[], screen_hints={'index': i})
            cache.set(image_data, screen)

        # All entries should be available
        for i in range(5):
            assert cache.get(f"image_{i}".encode()) is not None


class TestCacheKeyGeneration:
    """Tests for cache key generation."""

    def test_same_image_generates_same_key(self):
        """Test that same image data generates same cache key."""
        cache = InMemoryScreenCache()
        image_data = b"test_image_data"

        screen = FlattenedScreen(elements=[], screen_hints={})
        cache.set(image_data, screen)

        # Get key by setting again and checking cache size doesn't increase
        cache.set(image_data, screen)
        assert cache.size() == 1

    def test_different_images_generate_different_keys(self):
        """Test that different images generate different keys."""
        cache = InMemoryScreenCache()

        screen = FlattenedScreen(elements=[], screen_hints={})
        cache.set(b"image_1", screen)
        cache.set(b"image_2", screen)

        assert cache.size() == 2
