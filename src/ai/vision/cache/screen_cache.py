"""Screen cache for flattened screen analysis results.

This module provides caching for FlattenedScreen results from multimodal
analysis to avoid repeated API calls for identical screenshots.
"""

import hashlib
import logging
import time
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Optional

from src.models.vision.flattened_screen import FlattenedScreen


logger = logging.getLogger(__name__)


@dataclass
class CacheEntry:
    """A cache entry with value and timestamp.

    Attributes:
        value: The cached value
        created_at: Unix timestamp when entry was created
    """

    value: FlattenedScreen
    created_at: float


class ScreenCache(ABC):
    """Abstract base class for screen cache implementations.

    Provides caching for FlattenedScreen results based on image data hash.
    """

    @abstractmethod
    def get(self, image_data: bytes) -> Optional[FlattenedScreen]:
        """Get cached FlattenedScreen for the given image data.

        Args:
            image_data: PNG format screenshot data

        Returns:
            Cached FlattenedScreen if found and not expired, None otherwise
        """

    @abstractmethod
    def set(self, image_data: bytes, value: FlattenedScreen) -> None:
        """Cache a FlattenedScreen for the given image data.

        Args:
            image_data: PNG format screenshot data
            value: FlattenedScreen to cache
        """

    @abstractmethod
    def clear(self) -> None:
        """Clear all cached entries."""


class InMemoryScreenCache(ScreenCache):
    """In-memory implementation of screen cache.

    Uses MD5 hash of image data as cache key with TTL support
    and LRU eviction when max size is reached.
    """

    def __init__(self, ttl: int = 300, max_size: int = 1000):
        """Initialize the in-memory screen cache.

        Args:
            ttl: Time to live for cache entries in seconds (default: 300 = 5 minutes)
            max_size: Maximum number of entries to store (default: 1000)
        """
        self.ttl = ttl
        self.max_size = max_size
        self._cache: dict[str, CacheEntry] = {}

        logger.info(
            f"InMemoryScreenCache initialized: ttl={ttl}s, max_size={max_size}"
        )

    def _generate_key(self, image_data: bytes) -> str:
        """Generate cache key from image data.

        Uses MD5 hash of the raw image bytes.

        Args:
            image_data: PNG format screenshot data

        Returns:
            Hexadecimal MD5 hash string
        """
        return hashlib.md5(image_data).hexdigest()

    def get(self, image_data: bytes) -> Optional[FlattenedScreen]:
        """Get cached FlattenedScreen for the given image data.

        Args:
            image_data: PNG format screenshot data

        Returns:
            Cached FlattenedScreen if found and not expired, None otherwise
        """
        key = self._generate_key(image_data)
        entry = self._cache.get(key)

        if entry is None:
            logger.debug(f"Screen cache miss: {key[:8]}...")
            return None

        # Check if entry has expired
        age = time.time() - entry.created_at
        if age > self.ttl:
            logger.debug(f"Screen cache entry expired: {key[:8]}... (age={age:.0f}s)")
            del self._cache[key]
            return None

        logger.debug(
            f"Screen cache hit: {key[:8]}... (age={age:.0f}s, "
            f"{entry.value.element_count()} elements)"
        )
        return entry.value

    def set(self, image_data: bytes, value: FlattenedScreen) -> None:
        """Cache a FlattenedScreen for the given image data.

        Implements LRU eviction when max size is reached.

        Args:
            image_data: PNG format screenshot data
            value: FlattenedScreen to cache
        """
        key = self._generate_key(image_data)

        # Evict oldest entry if at capacity
        if len(self._cache) >= self.max_size and key not in self._cache:
            oldest_key = min(self._cache.items(), key=lambda x: x[1].created_at)[0]
            del self._cache[oldest_key]
            logger.debug(f"Cache full, evicted oldest entry: {oldest_key[:8]}...")

        self._cache[key] = CacheEntry(value=value, created_at=time.time())
        logger.debug(
            f"Screen cache set: {key[:8]}... ({value.element_count()} elements, "
            f"{len(self._cache)}/{self.max_size} entries)"
        )

    def clear(self) -> None:
        """Clear all cached entries."""
        size = len(self._cache)
        self._cache.clear()
        logger.info(f"Screen cache cleared: {size} entries removed")

    def size(self) -> int:
        """Get current number of cached entries.

        Returns:
            Number of entries in cache
        """
        return len(self._cache)

    def hit_rate(self) -> float:
        """Get cache hit rate (placeholder for metrics).

        Returns:
            Hit rate as percentage (0-100)
        """
        # Would need to track hits/misses for real implementation
        return 0.0
