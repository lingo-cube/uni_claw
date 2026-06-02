"""Page analysis cache for assembled page results.

This module provides caching for PageAnalysis results from the
text-based assembly step.
"""

import hashlib
import json
import logging
import time
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Optional

from src.state.content_tree import PageAnalysis


logger = logging.getLogger(__name__)


@dataclass
class CacheEntry:
    """A cache entry with value and timestamp.

    Attributes:
        value: The cached value
        created_at: Unix timestamp when entry was created
    """

    value: PageAnalysis
    created_at: float


class PageAnalysisCache(ABC):
    """Abstract base class for page analysis cache implementations.

    Provides caching for PageAnalysis results based on a cache key
    that incorporates both flattened screen and context.
    """

    @abstractmethod
    def get(self, cache_key: str) -> Optional[PageAnalysis]:
        """Get cached PageAnalysis for the given cache key.

        Args:
            cache_key: Cache key generated from screen + context

        Returns:
            Cached PageAnalysis if found and not expired, None otherwise
        """

    @abstractmethod
    def set(self, cache_key: str, value: PageAnalysis) -> None:
        """Cache a PageAnalysis for the given cache key.

        Args:
            cache_key: Cache key generated from screen + context
            value: PageAnalysis to cache
        """

    @abstractmethod
    def clear(self) -> None:
        """Clear all cached entries."""


class InMemoryPageAnalysisCache(PageAnalysisCache):
    """In-memory implementation of page analysis cache.

    Uses string cache keys with TTL support and LRU eviction
    when max size is reached.
    """

    def __init__(self, ttl: int = 600, max_size: int = 1000):
        """Initialize the in-memory page analysis cache.

        Args:
            ttl: Time to live for cache entries in seconds (default: 600 = 10 minutes)
            max_size: Maximum number of entries to store (default: 1000)
        """
        self.ttl = ttl
        self.max_size = max_size
        self._cache: dict[str, CacheEntry] = {}

        logger.info(
            f"InMemoryPageAnalysisCache initialized: ttl={ttl}s, max_size={max_size}"
        )

    def get(self, cache_key: str) -> Optional[PageAnalysis]:
        """Get cached PageAnalysis for the given cache key.

        Args:
            cache_key: Cache key generated from screen + context

        Returns:
            Cached PageAnalysis if found and not expired, None otherwise
        """
        entry = self._cache.get(cache_key)

        if entry is None:
            logger.debug(f"Page analysis cache miss: {cache_key[:16]}...")
            return None

        # Check if entry has expired
        age = time.time() - entry.created_at
        if age > self.ttl:
            logger.debug(
                f"Page analysis cache entry expired: {cache_key[:16]}... (age={age:.0f}s)"
            )
            del self._cache[cache_key]
            return None

        logger.debug(
            f"Page analysis cache hit: {cache_key[:16]}... (age={age:.0f}s)"
        )
        return entry.value

    def set(self, cache_key: str, value: PageAnalysis) -> None:
        """Cache a PageAnalysis for the given cache key.

        Implements LRU eviction when max size is reached.

        Args:
            cache_key: Cache key generated from screen + context
            value: PageAnalysis to cache
        """
        # Evict oldest entry if at capacity
        if len(self._cache) >= self.max_size and cache_key not in self._cache:
            oldest_key = min(self._cache.items(), key=lambda x: x[1].created_at)[0]
            del self._cache[oldest_key]
            logger.debug(f"Cache full, evicted oldest entry: {oldest_key[:16]}...")

        self._cache[cache_key] = CacheEntry(value=value, created_at=time.time())
        logger.debug(
            f"Page analysis cache set: {cache_key[:16]}... "
            f"({len(self._cache)}/{self.max_size} entries)"
        )

    def clear(self) -> None:
        """Clear all cached entries."""
        size = len(self._cache)
        self._cache.clear()
        logger.info(f"Page analysis cache cleared: {size} entries removed")

    def size(self) -> int:
        """Get current number of cached entries.

        Returns:
            Number of entries in cache
        """
        return len(self._cache)

    def generate_key(self, flattened_screen_dict: dict, context: dict) -> str:
        """Generate a cache key from flattened screen and context.

        This is a helper method for generating cache keys.

        Args:
            flattened_screen_dict: FlattenedScreen as dictionary
            context: Traversal context dictionary

        Returns:
            Cache key string (hash:hash format)
        """
        # Hash the flattened screen representation
        screen_json = json.dumps(flattened_screen_dict, sort_keys=True)
        screen_hash = hashlib.md5(screen_json.encode()).hexdigest()

        # Hash the context
        context_json = json.dumps(context or {}, sort_keys=True)
        context_hash = hashlib.md5(context_json.encode()).hexdigest()

        return f"{screen_hash}:{context_hash}"

    def hit_rate(self) -> float:
        """Get cache hit rate (placeholder for metrics).

        Returns:
            Hit rate as percentage (0-100)
        """
        # Would need to track hits/misses for real implementation
        return 0.0
